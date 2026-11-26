using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// 型セクションで読み取った関数型または構造体型の宣言 同じ再帰グループ内の型番号は読み取り後に解決する
/// </summary>
internal readonly record struct TypeDeclaration(
    ImmutableArray<WasmValueType> Parameters,
    ImmutableArray<WasmValueType> Results,
    bool IsFinal,
    ImmutableArray<WasmValueType> Supertypes,
    ImmutableArray<WasmFieldType> Fields = default
);
