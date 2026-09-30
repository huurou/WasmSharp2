using WasmSharp.Exceptions;

namespace WasmSharp.TestSuiteRunner.Reports;

/// <summary>
/// 元の診断を加工せずに保持するケース単位の診断
/// </summary>
/// <param name="Operation">失敗した操作</param>
/// <param name="Message">加工しない診断内容 例外ではMessageそのもの</param>
internal sealed record CaseDiagnostic(string Operation, string Message)
{
    /// <summary>
    /// 異常のある素材のpath 素材照合以外や取得できない場合はnull
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 失敗を観測した段階 公開処理の外で失敗した場合はnull
    /// </summary>
    public CaseStage? Stage { get; init; }

    /// <summary>
    /// 観測した例外の完全型名 例外以外の失敗ではnull
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// 公開例外が報告した理由の列挙名 取得できない場合はnull
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 公開例外が報告した上限値 取得できない場合はnull
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// 公開例外が報告した段階と入力上の位置 取得できない場合はnull
    /// </summary>
    public FailureLocationRecord? Location { get; init; }

    /// <summary>
    /// リンク不成立となったimportの識別 取得できない場合はnull
    /// </summary>
    public ImportRecord? Import { get; init; }

    /// <summary>
    /// 公開例外が報告した未実装機能 取得できない場合はnull
    /// </summary>
    public string? Feature { get; init; }

    /// <summary>
    /// 公開例外が報告した構文検査または検証の未確認範囲
    /// </summary>
    public List<UnverifiedRangeRecord> UnverifiedRanges { get; init; } = [];

    /// <summary>
    /// ランナーが提供したホスト関数のcallbackに由来するかどうか
    /// </summary>
    public bool FromCallback { get; init; }

    /// <summary>
    /// 不正なcommandから取得できた元JSON 該当しない場合はnull
    /// </summary>
    public string? SourceJson { get; init; }

    /// <summary>
    /// 例外の型・Messageと、公開例外が持つ診断情報を保存用の記録へ写す。
    /// </summary>
    /// <param name="operation">失敗を観測した操作の識別名</param>
    /// <param name="stage">公開処理で失敗を観測した段階 公開処理の外ではnull</param>
    /// <param name="exception">診断の元となる例外 Messageは変更せず保持する</param>
    /// <param name="fromCallback">ランナーのホスト関数のcallbackに由来する失敗の場合はtrue</param>
    /// <returns>例外の型と内容、および取得できた理由・位置・import・未確認範囲を保持する診断</returns>
    internal static CaseDiagnostic FromException(
        string operation,
        CaseStage? stage,
        Exception exception,
        bool fromCallback = false
    )
    {
        return new(operation, exception.Message)
        {
            Stage = stage,
            ExceptionType = exception.GetType().FullName,
            Reason = exception switch
            {
                WasmTrapException x => x.Reason?.ToString(),
                WasmExhaustionException x => x.Reason.ToString(),
                WasmImplementationLimitException x => x.Reason.ToString(),
                WasmInstantiateException x => x.Reason?.ToString(),
                WasmImportInspectionException x => x.Reason.ToString(),
                _ => null,
            },
            Limit = exception switch
            {
                WasmExhaustionException x => x.Limit,
                WasmImplementationLimitException x => x.Limit,
                _ => null,
            },
            Location = exception is WasmException { Location: { } location }
                ? new(
                    location.Stage.ToString(),
                    location.ByteOffset,
                    location.FunctionIndex,
                    location.SectionId
                )
                : null,
            Import = exception is WasmInstantiateException { ImportOrdinal: { } ordinal } link
                ? new(ordinal, link.ModuleName, link.ImportName, link.ExpectedKind?.ToString())
                : null,
            Feature = exception switch
            {
                WasmUnsupportedFeatureException x => x.Feature,
                WasmImportInspectionException x => x.Feature,
                _ => null,
            },
            UnverifiedRanges =
            [
                .. (
                    exception switch
                    {
                        WasmUnsupportedFeatureException x => x.UnverifiedRanges,
                        WasmImportInspectionException x => x.UnverifiedRanges,
                        _ => [],
                    }
                ).Select(x => new UnverifiedRangeRecord(
                    x.Stage.ToString(),
                    x.StartOffset,
                    x.EndOffset,
                    x.Description
                )),
            ],
            FromCallback = fromCallback,
        };
    }
}
