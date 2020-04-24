﻿using System;
using Ymf262Emu.Operators;

namespace Ymf262Emu.Channels
{
    internal sealed class BassDrum : Channel2
    {
        public BassDrum(FmSynthesizer opl)
            : base(6, new Operator(0x10, opl), new Operator(0x13, opl), opl)
        {
        }

        public override void GetChannelOutput(Span<double> output)
        {
            // Bass Drum ignores first operator, when it is in series.
            if (this.cnt == 1)
                this.op1.ar = 0;

            base.GetChannelOutput(output);
        }
        // Key ON and OFF are unused in rhythm channels.
        public override void KeyOn()
        {
        }
        public override void KeyOff()
        {
        }
    }
}
