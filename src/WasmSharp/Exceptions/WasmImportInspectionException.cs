using System.Collections.Immutable;

namespace WasmSharp.Exceptions;

/// <summary>
/// import情報を完全には取得できなかったことを示す例外。部分一覧は保持しない
/// </summary>
/// <param name="message">取得失敗の原因を説明するメッセージ</param>
/// <param name="reason">取得失敗の分類</param>
/// <param name="feature">未対応の機能。該当しない場合はnull</param>
/// <param name="location">処理を中断した入力上の位置</param>
/// <param name="unverifiedRanges">構文や検証が未確認の範囲</param>
/// <param name="innerException">原因となった元の診断</param>
public class WasmImportInspectionException(
    string? message,
    WasmImportInspectionReason reason,
    string? feature,
    WasmFailureLocation location,
    ImmutableArray<WasmUnverifiedRange> unverifiedRanges,
    Exception? innerException
) : WasmException(message, location, innerException)
{
    /// <summary>
    /// 取得失敗の分類
    /// </summary>
    public WasmImportInspectionReason Reason { get; } = reason;

    /// <summary>
    /// 未対応の機能。該当しない場合はnull
    /// </summary>
    public string? Feature { get; } = feature;

    /// <summary>
    /// 読み飛ばしたpayload、失敗以降の入力と全体の検証未実施範囲
    /// </summary>
    public ImmutableArray<WasmUnverifiedRange> UnverifiedRanges { get; } =
        unverifiedRanges.IsDefault ? [] : unverifiedRanges;
}

/// <summary>
/// import情報を完全には取得できない原因
/// </summary>
public enum WasmImportInspectionReason
{
    /// <summary>
    /// 必要なバイナリ構文が破損している
    /// </summary>
    MalformedBinary,

    /// <summary>
    /// importが要求する関数型を解決できない
    /// </summary>
    UnresolvedType,

    /// <summary>
    /// 必要な情報の読取が未対応である
    /// </summary>
    UnsupportedFeature,

    /// <summary>
    /// 入力またはコレクションを実装上限により保持できない
    /// </summary>
    ImplementationLimit,
}
