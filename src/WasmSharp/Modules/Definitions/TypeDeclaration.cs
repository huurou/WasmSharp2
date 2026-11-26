using System.Collections.Immutable;

namespace WasmSharp.Modules.Definitions;

/// <summary>
/// 型セクションで読み取った関数型または構造体型の宣言 同じ再帰グループ内の型番号は読み取り後に解決する
/// </summary>
internal readonly struct TypeDeclaration
{
    internal bool IsStruct { get; }
    internal bool IsFinal { get; }
    internal ImmutableArray<WasmValueType> Supertypes { get; }
    internal ImmutableArray<WasmValueType> Parameters { get; }
    internal ImmutableArray<WasmValueType> Results { get; }
    internal ImmutableArray<WasmFieldType> Fields { get; }

    internal TypeDeclaration(
        ImmutableArray<WasmValueType> parameters,
        ImmutableArray<WasmValueType> results,
        bool isFinal,
        ImmutableArray<WasmValueType> supertypes
    )
    {
        IsFinal = isFinal;
        Supertypes = supertypes;
        Parameters = parameters;
        Results = results;
    }

    internal TypeDeclaration(ImmutableArray<WasmFieldType> fields, bool isFinal, ImmutableArray<WasmValueType> supertypes)
    {
        IsStruct = true;
        IsFinal = isFinal;
        Supertypes = supertypes;
        Fields = fields.IsDefault ? [] : fields;
    }
}
