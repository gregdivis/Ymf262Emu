using System;

namespace Ymf262Emu.Operators;

/// <summary>
/// Emulates the highhat OPL operator.
/// </summary>
/// <param name="opl">FmSynthesizer instance which owns the operator.</param>
internal sealed class HighHat(FmSynthesizer opl) : TopCymbal(0x11, opl)
{
    /// <summary>
    /// Returns the current output value of the operator.
    /// </summary>
    /// <param name="modulator">Modulation factor to apply to the output.</param>
    /// <returns>Current output value of the operator.</returns>
    public override double GetOperatorOutput(double modulator)
    {
        var topCymbalOperatorPhase = this.opl.topCymbalOperator.phase * PhaseMultiplierTable[this.opl.topCymbalOperator.mult];
        var operatorOutput = this.GetOperatorOutput(modulator, topCymbalOperatorPhase);
        if (operatorOutput == 0)
            operatorOutput = Random.Shared.NextDouble() * this.envelope;

        return operatorOutput;
    }
}
