namespace WasmSharp.Exceptions;

/// <summary>
/// 失敗した処理段階と入力上の位置
/// </summary>
/// <param name="Stage">失敗した処理段階</param>
/// <param name="ByteOffset">Decode開始位置を0とした入力上のbyte offset</param>
/// <param name="FunctionIndex">失敗した関数のindex</param>
/// <param name="SectionId">失敗したsectionのID</param>
public sealed record WasmFailureLocation(
    WasmProcessingStage Stage,
    long? ByteOffset = null,
    uint? FunctionIndex = null,
    byte? SectionId = null
);
