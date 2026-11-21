namespace WasmSharp.Tests.Fixtures;

internal static class GrowAllocationFixture
{
    internal static async Task<T> RunAsync<T>(Func<int, T> grow)
    {
        const int AdditionalCount = 1 << 30;
        if (!Environment.Is64BitProcess)
        {
            // 32ビットでは参照だけで4GiBに達し、配列がアドレス空間に収まらない。
            return grow(AdditionalCount);
        }

        // GC設定はプロセス全体に適用されるため、呼び出し側の2テストを単独で実行する。
        const string Setting = "GCHeapHardLimit";
        var originalData = AppContext.GetData(Setting);
        var originalLimit = Convert.ToUInt64(GC.GetConfigurationVariables()[Setting]);
        var limit = Math.Min(1L << 30, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
        try
        {
            AppContext.SetData(Setting, (ulong)limit);
            GC.RefreshMemoryLimit();
            // ヒープ別の上限が優先される場合も、参照配列を確保できないことを確認する。
            await Assert
                .That(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes)
                .IsLessThan((long)AdditionalCount * IntPtr.Size);
            return grow(AdditionalCount);
        }
        finally
        {
            // AppContextの値を消すだけではGCの上限が残るので、元の数値を再適用する。
            try
            {
                AppContext.SetData(Setting, originalLimit);
                GC.RefreshMemoryLimit();
            }
            finally
            {
                AppContext.SetData(Setting, originalData);
            }
        }
    }
}
