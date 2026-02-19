using System;
using Ymf262Emu.Operators;

namespace Ymf262Emu.Channels;

/// <summary>
/// Emulates a 2-operator rhythm OPL channel.
/// </summary>
/// <param name="baseAddress">Base address of the channel's registers.</param>
/// <param name="o1">First operator in the channel.</param>
/// <param name="o2">Second operator in the channel.</param>
/// <param name="opl">FmSynthesizer instance which owns the channel.</param>
internal sealed class RhythmChannel(int baseAddress, Operator o1, Operator o2, FmSynthesizer opl) : Channel2(baseAddress, o1, o2, opl)
{
    /// <summary>
    /// Returns an array containing the channel's output values.
    /// </summary>
    /// <returns>Array containing the channel's output values.</returns>
    public override void GetChannelOutput(Span<double> output)
    {
        var op1Output = this.op1.GetOperatorOutput(Operator.NoModulator);
        var op2Output = this.op2.GetOperatorOutput(Operator.NoModulator);
        var channelOutput = (op1Output + op2Output) / 2;

        this.GetFourChannelOutput(channelOutput, output);
    }
    /// <summary>
    /// Activates channel output.
    /// </summary>
    public override void KeyOn()
    {
    }
    /// <summary>
    /// Disables channel output.
    /// </summary>
    public override void KeyOff()
    {
    }
}
