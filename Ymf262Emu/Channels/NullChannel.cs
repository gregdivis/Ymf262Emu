using System;

namespace Ymf262Emu.Channels;

/// <summary>
/// Placeholder OPL channel that generates no output.
/// </summary>
/// <param name="opl">FmSynthesizer instance which owns the channel.</param>
internal sealed class NullChannel(FmSynthesizer opl) : Channel(0, opl)
{
    /// <summary>
    /// Returns an array containing the channel's output values.
    /// </summary>
    /// <returns>Array containing the channel's output values.</returns>
    public override void GetChannelOutput(Span<double> output) => output.Clear();
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
    /// <summary>
    /// Updates the state of all of the operators in the channel.
    /// </summary>
    public override void UpdateOperators()
    {
    }
}
