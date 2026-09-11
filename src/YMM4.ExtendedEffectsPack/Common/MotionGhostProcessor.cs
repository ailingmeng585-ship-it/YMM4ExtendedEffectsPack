using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4.ExtendedEffectsPack.Common;

/// <summary>Bakes the current item transform before capturing history, so position-keyframed text also trails.</summary>
internal sealed class MotionGhostProcessor : IVideoEffectProcessor
{
    private readonly PackEffect item;
    private readonly AffineTransform2D scale, center, uncenter;
    private readonly Transform3D project;
    private readonly Crop crop;
    private readonly ColorMatrix opacity;
    private readonly PackShader shader;
    private readonly FrameHistory history;
    private readonly ID2D1Image scene, output;
    private readonly DisposeCollector owned = new();
    private ID2D1Image? input;
    private bool disposed;
    public ID2D1Image Output => output;

    public MotionGhostProcessor(IGraphicsDevicesAndContext devices, PackEffect item)
    {
        this.item = item;
        try
        {
            var dc = devices.DeviceContext;
            scale = new AffineTransform2D(dc); owned.Collect(scale);
            project = new Transform3D(dc); owned.Collect(project);
            center = new AffineTransform2D(dc); owned.Collect(center);
            crop = new Crop(dc); owned.Collect(crop);
            opacity = new ColorMatrix(dc); owned.Collect(opacity);
            shader = new PackShader(devices); owned.Collect(shader);
            if (!shader.IsEnabled) throw new NotSupportedException("Motion Ghost requires Shader Model 5.");
            uncenter = new AffineTransform2D(dc); owned.Collect(uncenter);
            history = new FrameHistory(devices); owned.Collect(history);
            Connect(scale, project); Connect(project, center); Connect(center, crop);
            Connect(crop, opacity); Connect(opacity, shader); Connect(shader, uncenter);
            scene = opacity.Output; owned.Collect(scene);
            output = uncenter.Output; owned.Collect(output);
        }
        catch { owned.Dispose(); throw; }
    }
    private static void Connect(ID2D1Effect from, ID2D1Effect to)
    {
        using var image = from.Output;
        to.SetInput(0, image, true);
    }
    public void SetInput(ID2D1Image? image)
    {
        input = image;
        scale.SetInput(0, image, true);
    }
    public void ClearInput()
    {
        input = null;
        scale.SetInput(0, null, true);
        shader.SetInput(1, null, true);
        history.Clear();
    }
    public DrawDescription Update(EffectDescription desc)
    {
        if (input is null) return desc.DrawDescription;
        var dd = desc.DrawDescription;
        int width = desc.ScreenSize.Width, height = desc.ScreenSize.Height;
        if (width < 1 || height < 1 || width > 16384 || height > 16384)
            throw new NotSupportedException("Motion Ghost requires a finite screen up to 16384 px.");
        scale.TransformMatrix = Matrix3x2.CreateScale(dd.Zoom);
        float rad = MathF.PI / 180;
        project.TransformMatrix =
            (dd.Invert ? Matrix4x4.CreateScale(-1, 1, 1, new Vector3(dd.CenterPoint, 0)) : Matrix4x4.Identity)
            * Matrix4x4.CreateRotationZ(dd.Rotation.Z * rad)
            * Matrix4x4.CreateRotationY(-dd.Rotation.Y * rad)
            * Matrix4x4.CreateRotationX(-dd.Rotation.X * rad)
            * Matrix4x4.CreateTranslation(dd.Draw) * dd.Camera
            * new Matrix4x4(1,0,0,0, 0,1,0,0, 0,0,1,-1 / Math.Max(1, dd.PerspectiveDistance ?? 1000), 0,0,0,1);
        center.TransformMatrix = Matrix3x2.CreateTranslation(width / 2f, height / 2f);
        crop.Rectangle = new Vector4(0, 0, width, height);
        float alpha = (float)Math.Clamp(dd.Opacity, 0, 1);
        opacity.Matrix = new Matrix5x4 { M11=alpha, M22=alpha, M33=alpha, M44=alpha };
        uncenter.TransformMatrix = Matrix3x2.CreateTranslation(-width / 2f, -height / 2f);
        var constants = item.Constants(desc.ItemPosition.Frame, desc.ItemDuration.Frame, desc.FPS, new Vector4(0,0,width,height));
        shader.SetInput(1, null, true);
        var past = history.Update(scene, constants.Rect, desc.ItemPosition.Frame, (int)Math.Clamp(constants.A.X, 1, 30));
        shader.SetInput(1, past ?? scene, true);
        constants.History.X = past is null ? 0 : 1;
        shader.Configure(constants);
        return dd with { Draw=Vector3.Zero, CenterPoint=Vector2.Zero, Zoom=Vector2.One,
            Rotation=Vector3.Zero, Camera=Matrix4x4.Identity, Opacity=1, Invert=false };
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ClearInput();
        project.SetInput(0,null,true); center.SetInput(0,null,true); crop.SetInput(0,null,true);
        opacity.SetInput(0,null,true); shader.SetInput(0,null,true); uncenter.SetInput(0,null,true);
        owned.Dispose();
    }
}
