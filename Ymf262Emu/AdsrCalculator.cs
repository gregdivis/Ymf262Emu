﻿using System;
using System.Runtime.CompilerServices;

namespace Ymf262Emu
{
    /// <summary>
    /// Generates an ADSR envelope for waveform data.
    /// </summary>
    internal sealed class AdsrCalculator
    {
        private double xAttackIncrement, xMinimumInAttack;
        private double dBdecayIncrement;
        private double dBreleaseIncrement;
        private double attenuation, totalLevel, sustainLevel;
        private double x, envelope;
        private readonly FmSynthesizer opl;

        /// <summary>
        /// Initializes a new instance of the AdsrCalculator class.
        /// </summary>
        /// <param name="opl">FmSynthesizer instance which owns the AdsrCalculator.</param>
        public AdsrCalculator(FmSynthesizer opl)
        {
            this.x = DecibelsToX(-96);
            this.envelope = -96;
            this.opl = opl;
        }

        /// <summary>
        /// Gets or sets the current ADSR state.
        /// </summary>
        public AdsrState State { get; set; }
        /// <summary>
        /// Gets or sets the current sustain level.
        /// </summary>
        public int SustainLevel
        {
            get
            {
                if (this.sustainLevel == -93)
                    return 0x0F;
                else
                    return (int)this.sustainLevel / -3;
            }
            set
            {
                if (value == 0x0F)
                    this.sustainLevel = -93;
                else
                    this.sustainLevel = -3 * value;
            }
        }
        /// <summary>
        /// Gets or sets the total output level.
        /// </summary>
        public int TotalLevel
        {
            get => (int)(this.totalLevel / -0.75);
            set => this.totalLevel = value * -0.75;
        }

        public void SetAtennuation(int frequencyNumber, int block, int ksl)
        {
            int hi4bits = (frequencyNumber >> 6) & 0x0F;
            int index = hi4bits * 8 + block;
            switch (ksl)
            {
                case 0:
                    this.attenuation = 0;
                    break;
                case 1:
                    // ~3 dB/Octave
                    this.attenuation = ksl3dBtable[index];
                    break;
                case 2:
                    // ~1.5 dB/Octave
                    this.attenuation = ksl3dBtable[index] / 2;
                    break;
                case 3:
                    // ~6 dB/Octave
                    this.attenuation = ksl3dBtable[index] * 2;
                    break;
            }
        }
        public void SetActualAttackRate(int attackRate, int ksr, int keyScaleNumber)
        {
            // According to the YMF278B manual's OPL3 section, the attack curve is exponential,
            // with a dynamic range from -96 dB to 0 dB and a resolution of 0.1875 dB 
            // per level.
            //
            // This method sets an attack increment and attack minimum value 
            // that creates a exponential dB curve with 'period0to100' seconds in length
            // and 'period10to90' seconds between 10% and 90% of the curve total level.
            int actualAttackRate = this.CalculateActualRate(attackRate, ksr, keyScaleNumber);
            var attackTimeValues = GetAttackTimeValues(actualAttackRate);

            int period0to100inSamples = (int)(attackTimeValues[0] * this.opl.SampleRate);
            int period10to90inSamples = (int)(attackTimeValues[1] * this.opl.SampleRate);

            // The x increment is dictated by the period between 10% and 90%:
            this.xAttackIncrement = this.opl.CalculateIncrement(PercentageToX_10, PercentageToX_90, attackTimeValues[1]);
            // Discover how many samples are still from the top.
            // It cannot reach 0 dB, since x is a logarithmic parameter and would be
            // negative infinity. So we will use -0.1875 dB as the resolution
            // maximum.
            //
            // percentageToX(0.9) + samplesToTheTop*xAttackIncrement = dBToX(-0.1875); ->
            // samplesToTheTop = (dBtoX(-0.1875) - percentageToX(0.9)) / xAttackIncrement); ->
            // period10to100InSamples = period10to90InSamples + samplesToTheTop; ->
            int period10to100inSamples = (int)(period10to90inSamples + ((DecibelsToX_n0_1875 - PercentageToX_90) / this.xAttackIncrement));
            // Discover the minimum x that, through the attackIncrement value, keeps 
            // the 10%-90% period, and reaches 0 dB at the total period:
            this.xMinimumInAttack = PercentageToX_10 - ((period0to100inSamples - period10to100inSamples) * this.xAttackIncrement);
        }

        private static readonly double DecibelsToX_n0_1875 = DecibelsToX(-0.1875);
        private static readonly double PercentageToX_10 = PercentageToX(0.1);
        private static readonly double PercentageToX_90 = PercentageToX(0.9);

        public void SetActualDecayRate(int decayRate, int ksr, int keyScaleNumber)
        {
            int actualDecayRate = this.CalculateActualRate(decayRate, ksr, keyScaleNumber);
            double period10to90inSeconds = decayAndReleaseTimeValuesTable[actualDecayRate];
            // Differently from the attack curve, the decay/release curve is linear.        
            // The dB increment is dictated by the period between 10% and 90%:
            this.dBdecayIncrement = this.opl.CalculateIncrement(PercentageToDB(0.1), PercentageToDB(0.9), period10to90inSeconds);
        }
        public void SetActualReleaseRate(int releaseRate, int ksr, int keyScaleNumber)
        {
            int actualReleaseRate = this.CalculateActualRate(releaseRate, ksr, keyScaleNumber);
            var period10to90inSeconds = decayAndReleaseTimeValuesTable[actualReleaseRate];
            this.dBreleaseIncrement = this.opl.CalculateIncrement(PercentageToDB(0.1), PercentageToDB(0.9), period10to90inSeconds);
        }
        public double GetEnvelope(int egt, int am)
        {
            // The datasheets attenuation values
            // must be halved to match the real OPL3 output.
            var envelopeSustainLevel = this.sustainLevel / 2;
            var envelopeTremolo = this.opl.GetTremoloValue(this.opl.dam, this.opl.tremoloIndex) / 2;
            var envelopeAttenuation = this.attenuation / 2;
            var envelopeTotalLevel = this.totalLevel / 2;

            double envelopeMinimum = -96;
            double envelopeResolution = 0.1875;

            double outputEnvelope;
            //
            // Envelope Generation
            //
            switch (this.State)
            {
                case AdsrState.Attack:
                    // Since the attack is exponential, it will never reach 0 dB, so
                    // we´ll work with the next to maximum in the envelope resolution.
                    if (this.envelope < -envelopeResolution && this.xAttackIncrement != -double.PositiveInfinity)
                    {
                        // The attack is exponential.
                        this.envelope = -Math.Pow(2, this.x);
                        this.x += this.xAttackIncrement;
                        break;
                    }
                    else
                    {
                        // It is needed here to explicitly set envelope = 0, since
                        // only the attack can have a period of
                        // 0 seconds and produce a infinity envelope increment.
                        this.envelope = 0;
                        this.State = AdsrState.Decay;
                    }
                    goto case AdsrState.Decay;

                case AdsrState.Decay:
                    // The decay and release are linear.                
                    if (this.envelope > envelopeSustainLevel)
                    {
                        this.envelope -= this.dBdecayIncrement;
                        break;
                    }
                    else
                    {
                        this.State = AdsrState.Sustain;
                    }
                    goto case AdsrState.Sustain;

                case AdsrState.Sustain:
                    // The Sustain stage is mantained all the time of the Key ON,
                    // even if we are in non-sustaining mode.
                    // This is necessary because, if the key is still pressed, we can
                    // change back and forth the state of EGT, and it will release and
                    // hold again accordingly.
                    if (egt == 1)
                    {
                        break;
                    }
                    else
                    {
                        if (this.envelope > envelopeMinimum)
                            this.envelope -= this.dBreleaseIncrement;
                        else
                            this.State = AdsrState.Off;
                    }
                    break;
                case AdsrState.Release:
                    // If we have Key OFF, only here we are in the Release stage.
                    // Now, we can turn EGT back and forth and it will have no effect,i.e.,
                    // it will release inexorably to the Off stage.
                    if (this.envelope > envelopeMinimum)
                        this.envelope -= dBreleaseIncrement;
                    else
                        this.State = AdsrState.Off;
                    break;
            }

            // Ongoing original envelope
            outputEnvelope = this.envelope;

            //Tremolo
            if (am == 1)
                outputEnvelope += envelopeTremolo;

            //Attenuation
            outputEnvelope += envelopeAttenuation;

            //Total Level
            outputEnvelope += envelopeTotalLevel;

            // The envelope has a resolution of 0.1875 dB:
            outputEnvelope = ((int)(outputEnvelope / envelopeResolution)) * envelopeResolution;

            return outputEnvelope;
        }
        public void KeyOn()
        {
            var xCurrent = Intrinsics.Log2(-this.envelope);
            this.x = Math.Min(xCurrent, this.xMinimumInAttack);
            this.State = AdsrState.Attack;
        }
        public void KeyOff()
        {
            if (this.State != AdsrState.Off)
                this.State = AdsrState.Release;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateActualRate(int rate, int ksr, int keyScaleNumber)
        {
            int rof = (int)((uint)keyScaleNumber >> (ksr << 1));

            int actualRate = (rate * 4) + rof;

            // If, as an example at the maximum, rate is 15 and the rate offset is 15, 
            // the value would
            // be 75, but the maximum allowed is 63:
            if (actualRate > 63)
                actualRate = 63;

            return actualRate;
        }

        private static double DecibelsToX(double dB) => Intrinsics.Log2(-dB);
        private static double PercentageToDB(double percentage) => Math.Log10(percentage) * 10.0;
        private static double PercentageToX(double percentage) => DecibelsToX(PercentageToDB(percentage));

        #region Lookup Tables

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<double> GetAttackTimeValues(int i) => new ReadOnlySpan<double>(attackTimeValuesTable, i * 2, 2);

        // These attack periods in miliseconds were taken from the YMF278B manual. 
        // The attack actual rates range from 0 to 63, with different data for 
        // 0%-100% and for 10%-90%: 
        private static readonly double[] attackTimeValuesTable =
        {
            double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity,
            double.PositiveInfinity, double.PositiveInfinity,
            2.82624, 1.48275,
            2.2528, 1.15507,
            1.88416, 0.99123,
            1.59744, 0.86835,
            1.41312, 0.74138,
            1.1264, 0.57754,
            0.94208, 0.49562,
            0.79872, 0.43418,
            0.70656, 0.37069,
            0.5632, 0.28877,
            0.47104, 0.24781,
            0.39936, 0.21709,

            0.35328, 0.18534,
            0.2816, 0.14438,
            0.23552, 0.1239,
            0.19968, 0.10854,
            0.17676, 0.09267,
            0.1408, 0.07219,
            0.11776, 0.06195,
            0.09984, 0.05427,
            0.08832, 0.04634,
            0.0704, 0.0361,
            0.05888, 0.03098,
            0.04992, 0.02714,
            0.04416, 0.02317,
            0.0352, 0.01805,
            0.02944, 0.01549,
            0.02496, 0.01357,

            0.02208, 0.01158,
            0.0176, 0.00902,
            0.01472, 0.00774,
            0.01248, 0.00678,
            0.01104, 0.00579,
            0.0088, 0.00451,
            0.00736, 0.00387,
            0.00624, 0.00339,
            0.00552, 0.0029,
            0.0044, 0.00226,
            0.00368, 0.00194,
            0.00312, 0.0017,
            0.00276, 0.00145,
            0.0022, 0.00113,
            0.00184, 0.00097,
            0.00156, 0.00085,

            0.0014, 0.00073,
            0.00112, 0.00061,
            0.00092, 0.00049,
            0.0008, 0.00043,
            0.0007, 0.00037,
            0.00056, 0.00031,
            0.00046, 0.00026,
            0.00042, 0.00022,
            0.00038, 0.00019,
            0.0003, 0.00014,
            0.00024, 0.00011,
            0.0002, 0.00011,
            0.00, 0.00,
            0.00, 0.00,
            0.00, 0.00,
            0.00, 0.00
        };

        // These decay and release periods in miliseconds were taken from the YMF278B manual. 
        // The rate index range from 0 to 63, with different data for 
        // 0%-100% and for 10%-90%: 
        private static readonly double[] decayAndReleaseTimeValuesTable =
        {
            double.PositiveInfinity,
            double.PositiveInfinity,
            double.PositiveInfinity,
            double.PositiveInfinity,
            8.21248,
            6.57408,
            5.50912,
            4.73088,
            4.10624,
            3.28704,
            2.75456,
            2.36544,
            2.05312,
            1.64352,
            1.37728,
            1.18272,

            1.02656,
            0.82176,
            0.68864,
            0.59136,
            0.51328,
            0.41088,
            0.34434,
            0.29568,
            0.25664,
            0.20544,
            0.17216,
            0.14784,
            0.12832,
            0.10272,
            0.08608,
            0.07392,

            0.06416,
            0.05136,
            0.04304,
            0.03696,
            0.03208,
            0.02568,
            0.02152,
            0.01848,
            0.01604,
            0.01284,
            0.01076,
            0.00924,
            0.00802,
            0.00642,
            0.00538,
            0.00462,

            0.00402,
            0.00322,
            0.00268,
            0.00232,
            0.00202,
            0.00162,
            0.00135,
            0.00115,
            0.00101,
            0.00081,
            0.00069,
            0.00058,
            0.00051,
            0.00051,
            0.00051,
            0.00051
        };

        private static readonly double[] ksl3dBtable =
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0 , 0 , 0, -3, -6, -9,
            0, 0, 0, 0, -3, -6, -9, -12,
            0, 0, 0, -1.875, -4.875, -7.875, -10.875, -13.875,

            0, 0, 0, -3, -6, -9, -12, -15,
            0, 0, -1.125, -4.125, -7.125, -10.125, -13.125, -16.125,
            0, 0, -1.875, -4.875, -7.875, -10.875, -13.875, -16.875,
            0, 0, -2.625, -5.625, -8.625, -11.625, -14.625, -17.625,

            0, 0, -3, -6, -9, -12, -15, -18,
            0, -0.750, -3.750, -6.750, -9.750, -12.750, -15.750, -18.750,
            0, -1.125, -4.125, -7.125, -10.125, -13.125, -16.125, -19.125,
            0, -1.500, -4.500, -7.500, -10.500, -13.500, -16.500, -19.500,

            0, -1.875, -4.875, -7.875, -10.875, -13.875, -16.875, -19.875,
            0, -2.250, -5.250, -8.250, -11.250, -14.250, -17.250, -20.250,
            0, -2.625, -5.625, -8.625, -11.625, -14.625, -17.625, -20.625,
            0, -3, -6, -9, -12, -15, -18, -21
        };
        #endregion
    }

    /// <summary>
    /// Specifies the current state of an ADSR envelope.
    /// </summary>
    internal enum AdsrState
    {
        /// <summary>
        /// The channel is off.
        /// </summary>
        Off,
        /// <summary>
        /// The envelope is in the attack phase.
        /// </summary>
        Attack,
        /// <summary>
        /// The envelope is in the decay phase.
        /// </summary>
        Decay,
        /// <summary>
        /// The envelope is in the sustain phase.
        /// </summary>
        Sustain,
        /// <summary>
        /// The envelope is in the release phase.
        /// </summary>
        Release
    }
}
