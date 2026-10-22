using WasmSharp.Benchmarks.Cases;

namespace WasmSharp.Benchmarks.Results;

/// <summary>
/// 復元用のHEAD・差分と使用sourceの記録 ランタイムとharnessの同一性を別々に保持する。
/// </summary>
/// <param name="UntrackedFiles">測定に使用するsource/projectだけの相対pathと内容hash 保存時もリポジトリの相対構造を保つ。</param>
internal sealed record RunSource(
    string Head,
    ManifestFile IndexDiff,
    ManifestFile WorktreeDiff,
    List<ManifestFile> UntrackedFiles,
    List<SubmoduleRevision> Submodules,
    SourceSet Runtime,
    SourceSet Harness,
    List<AssemblyIdentity> AssembliesBefore,
    List<AssemblyIdentity> AssembliesAfter,
    List<ContentChange> Changes
);

/// <summary>
/// submoduleの使用revisionと未コミット変更の有無
/// </summary>
internal sealed record SubmoduleRevision(string RelativePath, string Revision, bool IsDirty);

/// <summary>
/// source・project・依存版を含む使用ファイル集合 Filesはリポジトリ相対path、Sha256は固定順のpathと内容による集合hash
/// </summary>
internal sealed record SourceSet(string Revision, string Sha256, List<ManifestFile> Files);

/// <summary>
/// 使用したassemblyの役割・実際のpath・内容hash source同一性や比較条件の代用にはしない。
/// </summary>
internal sealed record AssemblyIdentity(AssemblyRole Role, string Path, string Sha256);

/// <summary>
/// 測定するランタイムと計時・検査・集計を行うharnessの役割
/// </summary>
internal enum AssemblyRole
{
    Runtime,
    Harness,
}

/// <summary>
/// 実行前後の内容変化 nullのhashは該当時点でのファイル不存在を表す。
/// </summary>
internal sealed record ContentChange(
    ContentKind Kind,
    string Path,
    string? BeforeSha256,
    string? AfterSha256
);

/// <summary>
/// 途中変更を確認するファイルの種類
/// </summary>
internal enum ContentKind
{
    Source,
    Project,
    Assembly,
}
