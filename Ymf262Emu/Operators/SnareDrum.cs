﻿using System;

namespace Ymf262Emu.Operators
{
    /// <summary>
    /// Emulates the snare drum OPL operator.
    /// </summary>
    internal sealed class SnareDrum : Operator
    {
        private readonly Random random = new Random();

        /// <summary>
        /// Initializes a new instance of the SnareDrum operator.
        /// </summary>
        /// <param name="opl">FmSynthesizer instance which owns the operator.</param>
        public SnareDrum(FmSynthesizer opl)
            : base(0x14, opl)
        {
        }

        /// <summary>
        /// Returns the current output value of the operator.
        /// </summary>
        /// <param name="modulator">Modulation factor to apply to the output.</param>
        /// <returns>Current output value of the operator.</returns>
        public override double GetOperatorOutput(double modulator)
        {
            if (this.envelopeGenerator.State == AdsrState.Off)
                return 0;

            var envelopeInDB = this.envelopeGenerator.GetEnvelope(this.egt, this.am);
            this.envelope = Math.Pow(10, envelopeInDB / 10.0);

            // If it is in OPL2 mode, use first four waveforms only:
            int waveIndex = this.ws & ((this.opl._new << 2) + 3);

            this.phase = this.opl.highHatOperator.phase * 2;
            var operatorOutput = this.GetOutput(modulator, this.phase, waveIndex);
            var noise = this.random.NextDouble() * this.envelope;

            if (operatorOutput / this.envelope != 1 && operatorOutput / this.envelope != -1)
            {
                if (operatorOutput > 0)
                    operatorOutput = noise;
                else if (operatorOutput < 0)
                    operatorOutput = -noise;
                else operatorOutput = 0;
            }

            return operatorOutput * 2;
        }
    }
}
