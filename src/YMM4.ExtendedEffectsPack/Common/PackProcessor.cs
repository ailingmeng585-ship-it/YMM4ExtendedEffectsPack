using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4.ExtendedEffectsPack.Common;

internal sealed class PackProcessor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly PackEffect item;
    private readonly PackShader shader;
    private readonly ID2D1Image output;
    private ID2D1Image? input;
    private bool disposed;
    public ID2D1Image Output => output;

    public PackProcessor(IGraphicsDevicesAndContext devices, PackEffect item)
    {
        this.devices = devices;
        this.item = item;
        shader = new PackShader(devices);
        if (!shader.IsEnabled)
        {
            shader.Dispose();
            throw new NotSupportedException("Extended Effects requires Direct3D feature level 11 / Shader Model 5.");
        }
        output = shader.Output;
    }
    public void SetInput(ID2D1Image? image)
    {
        input = image;
        shader.SetInput(0, image, true);
        shader.SetInput(1, image, true);
    }
    public void ClearInput()
    {
        shader.SetInput(0, null, true);
        shader.SetInput(1, null, true);
        input = null;
    }
    public DrawDescription Update(EffectDescription desc)
    {
        if (input is null) return desc.DrawDescription;
        var bounds = devices.DeviceContext.GetImageLocalBounds(input);
        Vector4 rect = new(bounds.Left, bounds.Top, Math.Max(1, bounds.Right - bounds.Left), Math.Max(1, bounds.Bottom - bounds.Top));
        // Guard unbounded effects and pathological dimensions without allocating infinite surfaces.
        if (!float.IsFinite(rect.X + rect.Y + rect.Z + rect.W) || rect.Z > 16384 || rect.W > 16384)
            throw new NotSupportedException("Crop unbounded inputs to a finite image (maximum 16384 px per side).");
        var c = item.Constants(desc.ItemPosition.Frame, desc.ItemDuration.Frame, desc.FPS, rect);
        shader.Configure(c);
        return desc.DrawDescription;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ClearInput();
        output.Dispose();
        shader.Dispose();
    }
}
