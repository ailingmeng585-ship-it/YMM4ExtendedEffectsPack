using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace YMM4.ExtendedEffectsPack.Common;

/// <summary>Compiles the embedded SM5 source once. No external assets or SDK needed at runtime.</summary>
internal static class ShaderCompiler
{
    private static readonly Lazy<byte[]> Bytecode = new(Compile, true);
    public static byte[] GetBytecode() => Bytecode.Value;

    private static byte[] Compile()
    {
        using var stream = typeof(ShaderCompiler).Assembly.GetManifestResourceStream(
            "YMM4.ExtendedEffectsPack.Shaders.Pack.hlsl")
            ?? throw new InvalidOperationException("Embedded Pack.hlsl is missing.");
        using var reader = new StreamReader(stream);
        var source = Encoding.UTF8.GetBytes(reader.ReadToEnd());
        IntPtr code = IntPtr.Zero, errors = IntPtr.Zero;
        try
        {
            int hr = D3DCompile(source, (nuint)source.Length, "Pack.hlsl", IntPtr.Zero,
                IntPtr.Zero, "main", "ps_5_0", (1u << 11) | (1u << 15), 0, out code, out errors);
            if (hr < 0)
                throw new InvalidOperationException("Extended Effects shader compilation failed: " +
                    (errors != IntPtr.Zero ? Encoding.UTF8.GetString(ReadBlob(errors)) : $"HRESULT 0x{hr:X8}"));
            return ReadBlob(code);
        }
        finally
        {
            if (errors != IntPtr.Zero) Marshal.Release(errors);
            if (code != IntPtr.Zero) Marshal.Release(code);
        }
    }

    private static unsafe byte[] ReadBlob(IntPtr blob)
    {
        var table = *(IntPtr**)blob;
        var getPointer = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr>)table[3];
        var getSize = (delegate* unmanaged[Stdcall]<IntPtr, nuint>)table[4];
        return new ReadOnlySpan<byte>((void*)getPointer(blob), checked((int)getSize(blob))).ToArray();
    }

    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, nuint size, string name, IntPtr defines,
        IntPtr include, string entry, string target, uint flags, uint effectFlags,
        out IntPtr code, out IntPtr errors);
}
