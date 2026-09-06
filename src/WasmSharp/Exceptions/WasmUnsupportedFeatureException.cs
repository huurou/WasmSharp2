using System.Collections.Immutable;

namespace WasmSharp.Exceptions;

/// <summary>
/// 未実装の機能に遭遇したことを示す例外
/// </summary>
public class WasmUnsupportedFeatureException : WasmException
{
    /// <summary>
    /// 未実装の機能を識別する安定した名前。診断情報がない場合はnull
    /// </summary>
    public string? Feature { get; }

    /// <summary>
    /// 構文検査または検証が完了していない入力範囲。空であることは検査済みの証明ではない
    /// </summary>
    public ImmutableArray<WasmUnverifiedRange> UnverifiedRanges { get; } =
        ImmutableArray<WasmUnverifiedRange>.Empty;

    /// <summary>
    /// 例外を初期化する
    /// </summary>
    public WasmUnsupportedFeatureException() { }

    /// <summary>
    /// メッセージを指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    public WasmUnsupportedFeatureException(string? message)
        : base(message) { }

    /// <summary>
    /// メッセージと原因となった例外を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmUnsupportedFeatureException(string? message, Exception? innerException)
        : base(message, innerException) { }

    /// <summary>
    /// 未実装の機能と検査が完了していない範囲を指定して例外を初期化する
    /// </summary>
    /// <param name="message">例外の原因を説明するメッセージ</param>
    /// <param name="feature">未実装の機能を識別する安定した名前</param>
    /// <param name="location">処理を中断した段階と入力上の位置</param>
    /// <param name="unverifiedRanges">構文検査または検証が完了していない入力範囲</param>
    /// <param name="innerException">原因となった例外</param>
    public WasmUnsupportedFeatureException(
        string? message,
        string feature,
        WasmFailureLocation location,
        ImmutableArray<WasmUnverifiedRange> unverifiedRanges,
        Exception? innerException = null
    )
        : base(message, location, innerException)
    {
        Feature = feature;
        UnverifiedRanges = unverifiedRanges.IsDefault
            ? ImmutableArray<WasmUnverifiedRange>.Empty
            : unverifiedRanges;
    }
}
