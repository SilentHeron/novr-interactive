using System;
namespace NOVR.MotionControls;

// Original synthesized cues; generated once, with no third-party audio assets.
internal enum InteractionCue { Click, Grab, Release, Detent, Guard, Helmet, Eject, Switch, Touch }
internal static class InteractionSoundWave
{
    internal static float[] Create(InteractionCue cue, int rate = 24000)
    {
        if (cue == InteractionCue.Switch) return Switch(rate);
        var length=cue is InteractionCue.Guard or InteractionCue.Helmet or InteractionCue.Eject ? .12 : .065;
        var data=new float[(int)(rate*length)];uint noise=1234567+(uint)cue;
        var frequency=cue==InteractionCue.Grab?420:cue==InteractionCue.Release?650:cue==InteractionCue.Detent?950:cue==InteractionCue.Helmet?720:cue==InteractionCue.Eject?230:cue==InteractionCue.Touch?280:900;
        for(var i=0;i<data.Length;i++)
        {
            var t=i/(double)rate;noise=1664525*noise+1013904223;
            var n=((noise>>8)/(double)0xffffff)*2-1;
            var envelope=Math.Min(1,t/.001)*Math.Exp(-t/(cue==InteractionCue.Grab?.016:.01));
            var tone=Math.Sin(2*Math.PI*frequency*t)*.32+n*(cue==InteractionCue.Grab?.6:.36);
            if((cue is InteractionCue.Detent or InteractionCue.Guard) && t>.025) tone+=Math.Sin(2*Math.PI*1800*t)*Math.Exp(-(t-.025)/.009)*.2;
            data[i]=(float)(tone*envelope*Math.Min(1,(length-t)/.008)*(cue == InteractionCue.Touch ? .22 : 1));
        }
        data[0]=data[data.Length-1]=0;return data;
    }
    private static float[] Switch(int rate)
    {
        var data = new float[(int)(rate * .10)]; uint noise = 74321;
        for (var i = 0; i < data.Length; i++)
        {
            var t = i / (double)rate; noise = 1664525 * noise + 1013904223;
            var n = (noise >> 8) / (double)0xffffff * 2 - 1;
            // Initial metal click followed by a lower spring/stop impact.
            var first = Math.Min(1,t/.0008) * Math.Exp(-t/.007);
            var u = Math.Max(0,t-.019);
            var stop = t > .019 ? Math.Min(1,u/.001) * Math.Exp(-u/.016) : 0;
            data[i] = (float)((n*.48 + Math.Sin(2*Math.PI*1700*t)*.22)*first +
                (n*.24 + Math.Sin(2*Math.PI*330*u)*.48)*stop);
        }
        data[0] = data[data.Length-1] = 0; return data;
    }

}
