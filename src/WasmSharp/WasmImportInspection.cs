using System.Collections.Immutable;
using WasmSharp.Exceptions;

namespace WasmSharp;

/// <summary>
/// 完全取得したimport一覧。module全体の有効性や実行可能性は保証しない
/// </summary>
public sealed class WasmImportInspection
{
    /// <summary>
    /// 宣言順のimport情報。空の場合はimportなしを確認済み
    /// </summary>
    public ImmutableArray<WasmImportInfo> Imports { get; }

    /// <summary>
    /// 読み飛ばした構文と、検証が未実施の入力範囲
    /// </summary>
    public ImmutableArray<WasmUnverifiedRange> UnverifiedRanges { get; }

    /// <summary>
    /// 全走査が成功したimport一覧と未確認範囲を保持する結果を構築する
    /// </summary>
    /// <param name="imports">宣言順の型付きimport情報を所有する不変配列</param>
    /// <param name="unverifiedRanges">構文と検証の未確認範囲を所有する不変配列</param>
    internal WasmImportInspection(
        ImmutableArray<WasmImportInfo> imports,
        ImmutableArray<WasmUnverifiedRange> unverifiedRanges
    )
    {
        Imports = imports;
        UnverifiedRanges = unverifiedRanges;
    }
}
