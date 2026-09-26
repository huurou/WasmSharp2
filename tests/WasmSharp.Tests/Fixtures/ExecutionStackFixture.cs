using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace WasmSharp.Tests.Fixtures;

internal static class ExecutionStackFixture
{
    public static T RunAtLimit<T>(Func<T> action)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = Descend(action);
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            },
            512 * 1024
        );
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static T Descend<T>(Func<T> action)
    {
        // CLRの予約領域を残した時点で止め、枯渇させずに境界の1回の呼び出しだけを行う。
        Span<byte> reservation = stackalloc byte[4096];
        reservation[0] = 1;
        var result = RuntimeHelpers.TryEnsureSufficientExecutionStack()
            ? Descend(action)
            : action();
        GC.KeepAlive(reservation[0]);
        return result;
    }
}
