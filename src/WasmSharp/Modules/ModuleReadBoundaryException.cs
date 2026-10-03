using WasmSharp.Exceptions;

namespace WasmSharp.Modules;

/// <summary>
/// 確定した読取り範囲の不正を、既存のDecode診断とともに内部へ通知する
/// </summary>
internal sealed class ModuleReadBoundaryException : Exception
{
    /// <summary>
    /// 公開入口で通知する、元の範囲不正の診断
    /// </summary>
    internal WasmDecodeException Fallback { get; }

    /// <summary>
    /// 元のDecode診断を保持した内部通知を構築する
    /// </summary>
    /// <param name="fallback">確定した範囲不正の公開診断</param>
    internal ModuleReadBoundaryException(WasmDecodeException fallback)
        : base(fallback.Message, fallback)
    {
        Fallback = fallback;
    }
}
