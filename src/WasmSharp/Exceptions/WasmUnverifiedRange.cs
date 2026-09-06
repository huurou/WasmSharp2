namespace WasmSharp.Exceptions;

/// <summary>
/// 構文検査または検証が完了していない入力範囲
/// </summary>
/// <param name="Stage">完了していない処理段階</param>
/// <param name="StartOffset">Decode開始位置を0とした範囲の先頭位置</param>
/// <param name="EndOffset">Decode開始位置を0とした範囲の終端位置。この位置は範囲に含まない</param>
/// <param name="Description">検査が完了していない内容の説明</param>
public sealed record WasmUnverifiedRange(
    WasmProcessingStage Stage,
    long StartOffset,
    long EndOffset,
    string Description
);
