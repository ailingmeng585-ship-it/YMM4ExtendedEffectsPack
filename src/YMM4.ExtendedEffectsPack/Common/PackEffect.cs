using System.Numerics;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YMM4.ExtendedEffectsPack.Common;

public abstract class PackEffect : VideoEffectBase
{
    [JsonIgnore] public abstract int EffectId { get; }
    internal abstract float[] ReadValues(double frame, double length, int fps);
    internal virtual Vector4 ReadTint() => Vector4.One;
    internal virtual double ReadClock(double frame, double length, int fps) => frame / Math.Max(1, fps);
    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => EffectId == 12 ? new MotionGhostProcessor(devices, this) : new PackProcessor(devices, this);
    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription desc) => [];

    internal ShaderConstants Constants(double frame, double length, int fps, Vector4 rect,
        float progress = 0, bool transition = false)
    {
        var v = ReadValues(frame, length, fps);
        Array.Resize(ref v, 12);
        return new ShaderConstants
        {
            Rect = rect,
            Meta = new Vector4(EffectId, (float)ReadClock(frame, length, fps), progress, transition ? 1 : 0),
            A = new Vector4(v[0], v[1], v[2], v[3]),
            B = new Vector4(v[4], v[5], v[6], v[7]),
            C = new Vector4(v[8], v[9], v[10], v[11]),
            Tint = ReadTint()
        };
    }
    protected static float Value(Animation a, double f, double l, int fps) => (float)a.GetValue((long)f, (long)l, fps);

    private static double SampleBpm(Animation bpm, double frame, long length, int fps)
    {
        long lo = (long)Math.Floor(frame);
        double fraction = frame - lo;
        return bpm.GetValue(lo, length, fps) * (1 - fraction)
            + bpm.GetValue(lo + 1, length, fps) * fraction;
    }

    // Deterministic midpoint quadrature: animated BPM does not restart phase when its value changes.
    protected static double IntegratedBeats(Animation bpm, double frame, double length, int fps)
    {
        if (frame <= 0) return 0;
        int count = (int)Math.Clamp(Math.Ceiling(frame), 1, 256);
        double sum = 0;
        for (int i = 0; i < count; i++) sum += SampleBpm(bpm, frame * (i + .5) / count, (long)length, fps);
        return sum * frame / count / Math.Max(1, fps) / 60;
    }
}
