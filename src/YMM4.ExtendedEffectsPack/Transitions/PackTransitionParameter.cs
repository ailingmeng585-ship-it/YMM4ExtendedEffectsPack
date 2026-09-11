using System.ComponentModel.DataAnnotations;
using System.Numerics;
using Newtonsoft.Json;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Transition;
using YMM4.ExtendedEffectsPack.Common;

namespace YMM4.ExtendedEffectsPack.Transitions;

public abstract class PackTransitionParameter : TransitionParameterBase
{
    private EasingType easingType = EasingType.Cubic;
    private EasingMode easingMode = EasingMode.InOut;
    [Display(Name = "緩急", Order = 90)] [EnumComboBox]
    public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
    [Display(Name = "加減速", Order = 91)] [EnumComboBox]
    public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
    [JsonIgnore] public abstract int EffectId { get; }
    internal abstract float[] ReadValues(double frame, double length, int fps);
    internal virtual Vector4 ReadTint() => Vector4.One;
    protected static float Value(Animation a, double f, double l, int fps) => (float)a.GetValue((long)f, (long)l, fps);

    public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
        => new PackTransitionSource(devices, before, after, this);

    internal ShaderConstants Constants(double frame, double length, int fps, Vector4 rect)
    {
        // A duration of N frames has visible samples 0..N-1. Both endpoints must be exact.
        double raw = length <= 1 ? 1 : Math.Clamp(frame / (length - 1), 0, 1);
        float progress = (float)Math.Clamp(Easing.GetValue(EasingType, EasingMode, raw), 0, 1);
        if (raw <= 0) progress = 0;
        if (raw >= 1) progress = 1;
        var v = ReadValues(frame, length, fps);
        Array.Resize(ref v, 12);
        return new ShaderConstants
        {
            Rect = rect, Meta = new Vector4(EffectId, (float)(frame / Math.Max(1, fps)), progress, 1),
            A = new Vector4(v[0], v[1], v[2], v[3]), B = new Vector4(v[4], v[5], v[6], v[7]),
            C = new Vector4(v[8], v[9], v[10], v[11]), Tint = ReadTint()
        };
    }
}

internal sealed class PackTransitionSource : ITransitionSource
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly ID2D1Image before, after;
    private readonly PackTransitionParameter item;
    private readonly PackShader shader;
    private readonly ID2D1Image output;
    private bool disposed;
    public ID2D1Image Output => output;
    public PackTransitionSource(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after, PackTransitionParameter item)
    {
        this.devices = devices; this.before = before; this.after = after; this.item = item;
        shader = new PackShader(devices);
        if (!shader.IsEnabled)
        {
            shader.Dispose();
            throw new NotSupportedException("Extended Effects transitions require Shader Model 5.");
        }
        output = shader.Output;
        shader.SetInput(0, before, true);
        shader.SetInput(1, after, true);
    }
    public void Update(TimelineItemSourceDescription desc)
    {
        var a = devices.DeviceContext.GetImageLocalBounds(before);
        var b = devices.DeviceContext.GetImageLocalBounds(after);
        float left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
        float right = Math.Max(a.Right, b.Right), bottom = Math.Max(a.Bottom, b.Bottom);
        var rect = new Vector4(left, top, Math.Max(1, right-left), Math.Max(1, bottom-top));
        if (!float.IsFinite(rect.X + rect.Y + rect.Z + rect.W) || rect.Z > 16384 || rect.W > 16384)
            throw new NotSupportedException("Transition inputs must have finite bounds; crop generators first.");
        shader.Configure(item.Constants(desc.ItemPosition.Frame, desc.ItemDuration.Frame, desc.FPS, rect));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        shader.SetInput(0, null, true); shader.SetInput(1, null, true);
        output.Dispose(); shader.Dispose();
        // before/after belong to the host; never dispose them here.
    }
}
