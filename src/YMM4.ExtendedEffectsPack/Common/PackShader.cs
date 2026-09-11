using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4.ExtendedEffectsPack.Common;

// Exactly seven 16-byte registers; the order is mirrored in Pack.hlsl.
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderConstants
{
    public Vector4 Rect;
    public Vector4 Meta;
    public Vector4 A;
    public Vector4 B;
    public Vector4 C;
    public Vector4 Tint;
    public Vector4 History;
}

internal sealed class PackShader(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<PackShader.Impl>(devices))
{
    public void Configure(ShaderConstants c)
    {
        SetValue(0, c.Rect); SetValue(1, c.Meta); SetValue(2, c.A);
        SetValue(3, c.B); SetValue(4, c.C); SetValue(5, c.Tint); SetValue(6, c.History);
    }

    [CustomEffect(2)]
    public sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private ShaderConstants constants;
        public Impl() : base(ShaderCompiler.GetBytecode()) { }
        [CustomEffectProperty(PropertyType.Vector4, 0)]
        public Vector4 Rect { get => constants.Rect; set { constants.Rect = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 1)]
        public Vector4 Meta { get => constants.Meta; set { constants.Meta = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 2)]
        public Vector4 A { get => constants.A; set { constants.A = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 3)]
        public Vector4 B { get => constants.B; set { constants.B = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 4)]
        public Vector4 C { get => constants.C; set { constants.C = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 5)]
        public Vector4 Tint { get => constants.Tint; set { constants.Tint = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Vector4, 6)]
        public Vector4 History { get => constants.History; set { constants.History = value; UpdateConstants(); } }
        protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(constants);

        private RawRect Bounds => new((int)MathF.Floor(Rect.X), (int)MathF.Floor(Rect.Y),
            (int)MathF.Ceiling(Rect.X + Math.Max(1, Rect.Z)), (int)MathF.Ceiling(Rect.Y + Math.Max(1, Rect.W)));

        public override void MapInputRectsToOutputRect(RawRect[] inputs, RawRect[] opaque,
            out RawRect output, out RawRect outputOpaque)
        {
            // Bounds come from the host, never from a tile-sized input rectangle.
            output = Bounds;
            outputOpaque = default;
        }
        public override RawRect MapInvalidRect(int inputIndex, RawRect invalidInputRect) => Bounds;

        public override void MapOutputRectToInputRects(RawRect output, RawRect[] inputs)
        {
            // Nonlocal warps/radial sampling need the entire finite input image.
            for (int i = 0; i < inputs.Length; i++) inputs[i] = Bounds;
        }
    }
}
