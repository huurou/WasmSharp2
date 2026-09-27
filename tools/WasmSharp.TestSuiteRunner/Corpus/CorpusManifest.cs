using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace WasmSharp.TestSuiteRunner.Corpus;

/// <summary>
/// 生成成功・失敗・未処理を含む全対象と素材の保存用記録
/// </summary>
/// <param name="Profile">生成物が欠けても対象集合を失わない固定条件</param>
/// <param name="Provenance">生成条件とは分離して保持する実行時の参考出典</param>
internal sealed record CorpusManifest(ProfileSnapshot Profile, ConversionProvenance Provenance)
{
    /// <summary>
    /// 永続形式の版
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// 保存結果の種類
    /// </summary>
    public string Kind { get; init; } = "corpus_manifest";

    /// <summary>
    /// 未処理を含む入力ごとの変換結果
    /// </summary>
    public List<InputConversionResult> Inputs { get; init; } = [];

    /// <summary>
    /// 生成前提の不成立や中断など、操作全体に対する診断
    /// </summary>
    public List<CorpusDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// 入力の変換状態と生成物の集計
    /// </summary>
    public ConversionSummary Summary { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// 全入力の処理完了と出力の確定を分けた記録
    /// </summary>
    public ConversionCompletion Completion { get; init; } = new(false, false);

    /// <summary>
    /// 生成開始前に全入力を未処理として記録し、本来の対象集合を確定する。
    /// </summary>
    internal static CorpusManifest Create(Core2Profile profile, ConversionProvenance provenance)
    {
        return new(ProfileSnapshot.FromProfile(profile), provenance)
        {
            Inputs = [.. profile.Inputs.Select(x => new InputConversionResult(x))],
            Summary = new(profile.Inputs.Length, 0, 0, profile.Inputs.Length, 0),
        };
    }

    /// <summary>
    /// 実行結果へ渡すため、入れ子を含むすべての可変一覧をコピーする。
    /// </summary>
    internal CorpusManifest CreateSnapshot()
    {
        return this with
        {
            Profile = Profile.CreateSnapshot(),
            Diagnostics = [.. Diagnostics],
            Inputs =
            [
                .. Inputs.Select(x =>
                    x with
                    {
                        Arguments = [.. x.Arguments],
                        Diagnostics = [.. x.Diagnostics],
                        Artifacts =
                        [
                            .. x.Artifacts.Select(y =>
                                y with
                                {
                                    Script = y.Script is { } script
                                        ? script with
                                        {
                                            Commands = [.. script.Commands],
                                            References = [.. script.References],
                                        }
                                        : null,
                                }
                            ),
                        ],
                    }
                ),
            ],
        };
    }
}

/// <summary>
/// 保存JSONだけで入力集合と変換条件を復元できるprofileのDTO
/// </summary>
/// <param name="Id">profileの識別名</param>
/// <param name="Spec">公式仕様の取得元と採用commit</param>
/// <param name="Wabt">変換器ソースの取得元と採用commit</param>
/// <param name="WorkingDirectory">spec-root基準の変換作業ディレクトリ</param>
/// <param name="Conversion">生成物に影響する固定option</param>
internal sealed record ProfileSnapshot(
    string Id,
    SourceRevision Spec,
    SourceRevision Wabt,
    string WorkingDirectory,
    ConversionOptions Conversion
)
{
    /// <summary>
    /// 全featureの既定値と実効値
    /// </summary>
    public List<FeatureSetting> Features { get; init; } = [];

    /// <summary>
    /// 生成済み素材の有無に依存しない全入力と生バイトSHA-256
    /// </summary>
    public List<SourceInput> Inputs { get; init; } = [];

    /// <summary>
    /// 実際の配置rootを含まない変換引数のテンプレート
    /// </summary>
    public List<string> LogicalArguments { get; init; } = [];

    /// <summary>
    /// 不変モデルの全条件を、独立した一覧を持つ保存用DTOへコピーする。
    /// </summary>
    internal static ProfileSnapshot FromProfile(Core2Profile profile)
    {
        return new(
            profile.Id,
            profile.Spec,
            profile.Wabt,
            profile.WorkingDirectory,
            profile.Conversion
        )
        {
            Features = [.. profile.Features],
            Inputs = [.. profile.Inputs],
            LogicalArguments = [.. profile.LogicalArguments],
        };
    }

    /// <summary>
    /// 保存用DTOの一覧をコピーし、不変のprofileを復元する。
    /// </summary>
    internal Core2Profile ToProfile()
    {
        return new(
            Id,
            Spec,
            Wabt,
            [.. Features],
            [.. Inputs],
            WorkingDirectory,
            [.. LogicalArguments],
            Conversion
        );
    }

    /// <summary>
    /// 保存内容を維持したまま、可変一覧を独立させる。
    /// </summary>
    internal ProfileSnapshot CreateSnapshot()
    {
        return this with
        {
            Features = [.. Features],
            Inputs = [.. Inputs],
            LogicalArguments = [.. LogicalArguments],
        };
    }
}

/// <summary>
/// 素材同一性の条件に含めない、変換時の環境と取得元の記録
/// </summary>
internal sealed record ConversionProvenance
{
    /// <summary>
    /// 変換器のビルド条件を代表する実行ファイルのSHA-256。取得前の失敗時はnull
    /// </summary>
    public string? ExecutableSha256 { get; init; }

    /// <summary>
    /// 実際に起動する変換器の絶対path
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// 生成記録を開始した日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// 生成環境のOS
    /// </summary>
    public string? OperatingSystem { get; init; }

    /// <summary>
    /// 生成環境のアーキテクチャ
    /// </summary>
    public string? Architecture { get; init; }

    /// <summary>
    /// 実際の公式入力の配置root
    /// </summary>
    public string? SpecRoot { get; init; }

    /// <summary>
    /// 実際の変換器ソースの配置root
    /// </summary>
    public string? WabtRoot { get; init; }

    /// <summary>
    /// CLIで解決した出力先の絶対path
    /// </summary>
    public string? OutputRoot { get; init; }

    /// <summary>
    /// 公式入力の実際のorigin。管理外コピーや取得できない場合はnull
    /// </summary>
    public string? SpecOrigin { get; init; }

    /// <summary>
    /// 変換器ソースの実際のorigin。取得できない場合はnull
    /// </summary>
    public string? WabtOrigin { get; init; }

    /// <summary>
    /// 公式入力の配置自体がGit checkoutの場合に観測したHEAD
    /// </summary>
    public string? SpecHead { get; init; }

    /// <summary>
    /// 変換器ソースのcheckoutで観測したHEAD
    /// </summary>
    public string? WabtHead { get; init; }
}

/// <summary>
/// 一つの公式入力の変換状態と、部分生成を含む生成物
/// </summary>
/// <param name="Input">元入力の相対pathと固定SHA-256</param>
internal sealed record InputConversionResult(SourceInput Input)
{
    /// <summary>
    /// 変換・JSON読取・参照素材照合の結果。開始前は未処理
    /// </summary>
    public ConversionStatus Status { get; init; } = ConversionStatus.Unprocessed;

    /// <summary>
    /// 変換器の終了値。未開始・起動失敗など観測できなかった場合はnull
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// 変換器から回収した標準出力を加工せず保持する文字列
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// 変換器から回収した標準エラーを加工せず保持する文字列
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// 実際の起動引数。配置rootを含む参考出典であり、論理引数と区別する
    /// </summary>
    public List<string> Arguments { get; init; } = [];

    /// <summary>
    /// この入力が所有する生成物。変換失敗時の部分生成物も保持する
    /// </summary>
    public List<Artifact> Artifacts { get; init; } = [];

    /// <summary>
    /// 変換・読取・素材照合の操作と対象を区別する入力別診断
    /// </summary>
    public List<CorpusDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// 一つの生成物の同一性と所有元
/// </summary>
/// <param name="Path">manifest基準の/区切り相対path</param>
/// <param name="Kind">生成物の種類</param>
/// <param name="Sha256">生バイト列のSHA-256を表す小文字hex64桁</param>
/// <param name="InputPath">所有する元入力のtest/core基準の相対path</param>
internal sealed record Artifact(string Path, ArtifactKind Kind, string Sha256, string InputPath)
{
    /// <summary>
    /// JSONから取得できたcommand一覧と参照先。JSON以外または未読取ではnull
    /// </summary>
    public ScriptArtifact? Script { get; init; }
}

/// <summary>
/// JSONの意味解釈に先立って列挙したcommandと素材参照の保存用記録
/// </summary>
internal sealed record ScriptArtifact
{
    /// <summary>
    /// JSONに記録されたsource_filename。取得できなかった場合はnull
    /// </summary>
    public string? SourceFilename { get; init; }

    /// <summary>
    /// JSON全体のcommand列挙を完了したかどうか
    /// </summary>
    public bool EnumerationComplete { get; init; }

    /// <summary>
    /// 確定したcommand総数。構文破損などで総数が未確定の場合はnull
    /// </summary>
    public int? CommandCount { get; init; }

    /// <summary>
    /// 境界を確定できたcommandの順序付き一覧
    /// </summary>
    public List<ArtifactCommand> Commands { get; init; } = [];

    /// <summary>
    /// 各commandのfilenameとmanifest上の生成物の対応
    /// </summary>
    public List<ArtifactReference> References { get; init; } = [];
}

/// <summary>
/// 生成JSON内で境界を確定できた一つのcommand
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号。取得できない場合はnull</param>
/// <param name="Type">元JSONのcommand種別。取得できない場合はnull</param>
/// <param name="ModuleType">元JSONのmodule_type。未指定または取得できない場合はnull</param>
/// <param name="Filename">元JSONのfilename。未指定または取得できない場合はnull</param>
internal sealed record ArtifactCommand(
    int Index,
    int? Line,
    string? Type,
    string? ModuleType,
    string? Filename
);

/// <summary>
/// commandが参照する名前と、対応する生成物の相対path
/// </summary>
/// <param name="CommandIndex">参照元commandの0始まりindex</param>
/// <param name="Filename">JSON内に記録された参照名</param>
/// <param name="ArtifactPath">参照名をJSONの配置に対して解決したmanifest基準の相対path</param>
internal sealed record ArtifactReference(int CommandIndex, string Filename, string ArtifactPath);

/// <summary>
/// 素材の生成・列挙・照合における診断
/// </summary>
/// <param name="Operation">失敗した操作</param>
/// <param name="Message">加工しない診断内容</param>
/// <param name="Path">対象の入力または素材の相対path。操作全体の診断ではnull</param>
/// <param name="ExceptionType">観測した例外の完全型名。例外以外の失敗ではnull</param>
internal sealed record CorpusDiagnostic(
    string Operation,
    string Message,
    string? Path = null,
    string? ExceptionType = null
);

/// <summary>
/// commandの分類と区別する入力単位の変換状態
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConversionStatus>))]
internal enum ConversionStatus
{
    /// <summary>
    /// 変換を開始していない状態
    /// </summary>
    [JsonStringEnumMemberName("unprocessed")]
    Unprocessed,

    /// <summary>
    /// 変換・JSON読取・参照素材の照合が成功した状態
    /// </summary>
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    /// <summary>
    /// 変換・読取・照合のいずれかが失敗した状態
    /// </summary>
    [JsonStringEnumMemberName("runner_error")]
    RunnerError,
}

/// <summary>
/// manifestが同定する生成物の種類
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtifactKind>))]
internal enum ArtifactKind
{
    /// <summary>
    /// commandと素材参照を持つJSON
    /// </summary>
    [JsonStringEnumMemberName("json")]
    Json,

    /// <summary>
    /// 実行に使用するbinary module
    /// </summary>
    [JsonStringEnumMemberName("wasm")]
    Wasm,

    /// <summary>
    /// 素材照合の対象に含め、実行対象外として扱うtext module
    /// </summary>
    [JsonStringEnumMemberName("wat")]
    Wat,
}

/// <summary>
/// 全対象入力に対する変換状態と素材数の保存用集計
/// </summary>
/// <param name="InputCount">本来の全対象入力数</param>
/// <param name="SucceededCount">変換と照合に成功した入力数</param>
/// <param name="RunnerErrorCount">変換・読取・照合に失敗した入力数</param>
/// <param name="UnprocessedCount">処理を開始していない入力数</param>
/// <param name="ArtifactCount">部分生成を含む記録済み生成物数</param>
internal sealed record ConversionSummary(
    int InputCount,
    int SucceededCount,
    int RunnerErrorCount,
    int UnprocessedCount,
    int ArtifactCount
);

/// <summary>
/// 合格判定と区別する処理・出力の完了情報。保存時の値は読取側で再検証する
/// </summary>
/// <param name="ProcessingComplete">失敗を含めて全入力の処理と記録を完了したかどうか</param>
/// <param name="OutputComplete">出力を最後まで書き切り確定したかどうか</param>
internal sealed record ConversionCompletion(bool ProcessingComplete, bool OutputComplete);
