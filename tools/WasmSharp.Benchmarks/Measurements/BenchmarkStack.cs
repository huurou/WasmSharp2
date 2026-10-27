using System.Runtime.CompilerServices;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Reports;

namespace WasmSharp.Benchmarks.Measurements;

/// <summary>
/// 時間測定の入口を固定したスタック配置へ揃える
/// </summary>
internal static class BenchmarkStack
{
    private const int ALIGNMENT_BYTES = 4096;

    internal static RunResults Run(IEngine engine) => Run(engine, 16);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static unsafe RunResults Run(IEngine engine, int markerBytes)
    {
        // 非inlineの引数でサイズを渡し、両領域を同じ可変stackallocで確保する。
        byte* marker = stackalloc byte[markerBytes];
        var paddingBytes = (int)((nuint)marker % ALIGNMENT_BYTES);
        if (paddingBytes == 0)
        {
            paddingBytes = ALIGNMENT_BYTES;
        }
        byte* anchor = stackalloc byte[paddingBytes];
        new Span<byte>(anchor, paddingBytes).Clear();
        if ((nuint)anchor % ALIGNMENT_BYTES != 0)
        {
            throw new InvalidOperationException("測定入口のスタック配置を固定できません。");
        }

        // 確保と位置検査を終えてからBDNへ渡し、時計と反復制御は標準エンジンに委ねる。
        var result = engine.Run();
        // 計時後まで両領域を保持し、tailcallによる入口配置の消失を避ける。
        Volatile.Write(ref marker[0], Volatile.Read(ref anchor[0]));
        return result;
    }
}
