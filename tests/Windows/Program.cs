using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin;
using YMM4.ExtendedEffectsPack.Common;
using YMM4.ExtendedEffectsPack.Effects;
using YMM4.ExtendedEffectsPack.Transitions;
using PixelFormat = Vortice.DCommon.PixelFormat;
using AlphaMode = Vortice.DCommon.AlphaMode;

namespace YMM4.ExtendedEffectsPack.Tests;

internal static class Program
{
    private const int Width = 64, Height = 48;
    private static int assertions;
    [STAThread]
    private static void Main()
    {
        Assert(Marshal.SizeOf<ShaderConstants>() == 112, "constant buffer size");
        var bytecode = ShaderCompiler.GetBytecode();
        Assert(bytecode.Length > 100 && System.Text.Encoding.ASCII.GetString(bytecode, 0, 4) == "DXBC", "SM5 compilation");
        Console.WriteLine($"SM5 shader: {bytecode.Length} bytes");
        var assembly = typeof(PackEffect).Assembly;
        var effects = assembly.GetTypes().Where(t => !t.IsAbstract && typeof(PackEffect).IsAssignableFrom(t)).ToArray();
        var transitions = assembly.GetTypes().Where(t => !t.IsAbstract && typeof(PackTransitionParameter).IsAssignableFrom(t)).ToArray();
        Assert(effects.Length == 17 && transitions.Length == 8, "registration count");
        foreach (var type in effects.Concat(transitions)) ValidateSettings(type);
        using var devices = new WarpDevices();
        using var red = devices.DeviceContext.CreateEmptyBitmap(Width, Height);
        using var blue = devices.DeviceContext.CreateEmptyBitmap(Width, Height);
        Fill(devices, red, new Color4(1, 0, 0, .5f));
        Fill(devices, blue, new Color4(0, 0, 1, .75f));
        using var shader = new PackShader(devices);
        Assert(shader.IsEnabled, "custom effect creation");
        using var output = shader.Output;
        shader.SetInput(0, red, true); shader.SetInput(1, blue, true);
        var rect = new Vector4(0, 0, Width, Height);
        foreach (var type in effects)
        {
            var item = (PackEffect)Activator.CreateInstance(type)!;
            var preset = type.GetProperty("Preset")!;
            foreach (var choice in Enum.GetValues(preset.PropertyType))
            {
                preset.SetValue(item, choice);
                foreach (int frame in new[] { 0, 12, 60 })
                {
                    shader.Configure(item.Constants(frame, 120, 60, rect));
                    ValidateAlpha(Render(devices, output), type.Name);
                }
            }
            Console.WriteLine($"Rendered: {type.Name}");
        }
        foreach (var type in transitions)
        {
            var item = (PackTransitionParameter)Activator.CreateInstance(type)!;
            shader.Configure(item.Constants(0, 61, 60, rect));
            Same(Render(devices, output), Render(devices, red), type.Name+" start");
            shader.Configure(item.Constants(60, 61, 60, rect));
            Same(Render(devices, output), Render(devices, blue), type.Name+" end");
            shader.Configure(item.Constants(30, 61, 60, rect));
            ValidateAlpha(Render(devices, output), type.Name+" midpoint");
            Assert(item.Constants(0, 1, 60, rect).Meta.Z == 1, "one-frame transition");
            Console.WriteLine($"Endpoints: {type.Name}");
        }
        var blink = new BlinkEffect { Progress = new Animation(100, 0, 100), AutoLoop = false };
        shader.Configure(blink.Constants(0, 60, 60, rect));
        Same(Render(devices, output), Render(devices, red), "fully open eyelids");
        var curtain = new TheaterCurtainEffect { Progress = new Animation(100, 0, 100) };
        shader.Configure(curtain.Constants(0, 60, 60, rect));
        Same(Render(devices, output), Render(devices, red), "fully open curtain");
        Fill(devices, red, new Color4(0, 0, 0, 0));
        foreach (var type in effects.Where(t => t != typeof(TheaterCurtainEffect) && t != typeof(BlinkEffect)))
        {
            var item = (PackEffect)Activator.CreateInstance(type)!;
            shader.Configure(item.Constants(5, 60, 60, rect));
            Assert(Render(devices, output).All(b => b == 0), type.Name+" transparent input");
        }
        shader.SetInput(0, null, true); shader.SetInput(1, null, true);
        using var history = new FrameHistory(devices);
        for (int frame = 0; frame < 8; frame++)
        {
            Fill(devices, red, new Color4(frame / 10f, 0, 0, 1));
            var past = history.Update(red, rect, frame, 3);
            if (frame < 3) Assert(past is null, "history warmup");
            else
            {
                Assert(past is not null, "history available");
                byte[] pixels = Render(devices, past!);
                Assert(Math.Abs(pixels[2] - (frame - 3) * 25.5) < 2, "actual three-frame delay");
                Same(pixels, Render(devices, history.Update(red, rect, frame, 3)!), "repeat-frame stability");
            }
        }
        Assert(history.Update(red, rect, 40, 3) is null, "seek resets history");
        Assert(history.Update(red, rect, 41, 8) is null, "delay change resets history");
        Console.WriteLine($"PASS: {assertions} assertions; 17 effects, 8 transitions; Direct3D WARP/Direct2D rendering.");
    }
    private static void ValidateSettings(Type type)
    {
        var value = Activator.CreateInstance(type)!;
        var preset = type.GetProperty("Preset")!;
        var choices = Enum.GetValues(preset.PropertyType);
        Assert(choices.Length is >= 3 and <= 5, type.Name+" preset count");
        int count = type.GetProperties().Count(p => p.GetCustomAttribute<DisplayAttribute>() != null && p.DeclaringType?.Assembly == typeof(PackEffect).Assembly);
        Assert(count <= 10, type.Name+" controls budget");
        foreach (var choice in choices)
        {
            preset.SetValue(value, choice);
            var numeric = type.GetProperties().First(p => p.PropertyType == typeof(Animation));
            numeric.SetValue(value, new Animation(17, -1000, 1000));
            string json = JsonConvert.SerializeObject(value);
            var restored = JsonConvert.DeserializeObject(json, type)!;
            Assert(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(JsonConvert.SerializeObject(restored))), type.Name+" JSON roundtrip");
        }
    }
    private static void Fill(WarpDevices devices, ID2D1Bitmap target, Color4 color)
    {
        var dc = devices.DeviceContext;
        dc.Target = target; dc.BeginDraw(); dc.Clear(color); dc.EndDraw(); dc.Target = null;
    }
    private static byte[] Render(WarpDevices devices, ID2D1Image input)
    {
        var dc = devices.DeviceContext;
        using var target = dc.CreateEmptyBitmap(Width, Height);
        dc.Target = target; dc.Transform = Matrix3x2.Identity;
        dc.BeginDraw(); dc.Clear(null); dc.DrawImage(input); dc.EndDraw(); dc.Target = null;
        using var readback = dc.CreateBitmap(new SizeI(Width, Height), IntPtr.Zero, 0,
            new BitmapProperties1(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96,
                BitmapOptions.CpuRead | BitmapOptions.CannotDraw));
        readback.CopyFromBitmap(target);
        var map = readback.Map(MapOptions.Read);
        byte[] data = new byte[Width * Height * 4];
        try
        {
            for (int y = 0; y < Height; y++)
                Marshal.Copy(map.Bits + y * (int)map.Pitch, data, y * Width * 4, Width * 4);
        }
        finally { readback.Unmap(); }
        return data;
    }
    private static void ValidateAlpha(byte[] pixels, string name)
    {
        for (int i = 0; i < pixels.Length; i += 4)
            Assert(pixels[i] <= pixels[i+3] && pixels[i+1] <= pixels[i+3] && pixels[i+2] <= pixels[i+3], name+" premultiplied alpha");
    }
    private static void Same(byte[] a, byte[] b, string name) => Assert(a.Zip(b).All(p => Math.Abs(p.First - p.Second) <= 1), name);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        assertions++;
    }
}

// Real Windows software GPU; unsupported host services are not used by this renderer.
internal sealed class WarpDevices : IGraphicsDevicesAndContext
{
    private readonly ID3D11Device device;
    private readonly ID3D11DeviceContext context;
    private readonly IDXGIDevice dxgi;
    public D2DDevices D2D { get; }
    public ID2D1DeviceContext6 DeviceContext { get; }
    public D3DDevices D3D => throw new NotSupportedException();
    public DXGIDevices DXGI => throw new NotSupportedException();
    public MFDevices MF => throw new NotSupportedException();
    public CacheProvider CacheProvider => throw new NotSupportedException();
    public WarpDevices()
    {
        D3D11.D3D11CreateDevice(null, DriverType.Warp, DeviceCreationFlags.BgraSupport,
            [Vortice.Direct3D.FeatureLevel.Level_11_0], out var createdDevice, out var createdContext).CheckError();
        device = createdDevice ?? throw new InvalidOperationException("WARP device unavailable");
        context = createdContext ?? throw new InvalidOperationException("WARP context unavailable");
        dxgi = device.QueryInterface<IDXGIDevice>();
        D2D = new D2DDevices(dxgi);
        DeviceContext = D2D.Device.CreateDeviceContext(DeviceContextOptions.None);
    }
    public IGraphicsDevicesAndContext CreateContext() => throw new NotSupportedException();
    public void Dispose()
    {
        DeviceContext.Dispose(); D2D.Dispose(); dxgi.Dispose(); context.Dispose(); device.Dispose();
    }
}
