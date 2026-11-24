using WasmSharp.Exceptions;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// Wasmの宣言で使用する値型 参照型ではnull可否と参照先の型を保持する
/// </summary>
public readonly struct WasmValueType : IEquatable<WasmValueType>
{
    /// <summary>
    /// モジュールの型宣言から解決できなかった型番号と入力位置
    /// </summary>
    internal sealed record UnresolvedReference(uint Index, WasmFailureLocation Location)
    {
        internal WasmValidateException ValidationFailure()
        {
            return ValidationDiagnostics.UnknownIndex(
                "type",
                Index,
                "参照先の関数型が存在しません。",
                Location with
                {
                    Stage = WasmProcessingStage.Validate,
                }
            );
        }
    }

    private readonly UnresolvedReference? _unresolved;

    /// <summary>
    /// 実行時の値の種類
    /// </summary>
    public WasmValueKind Kind { get; }

    /// <summary>
    /// 参照型でnullを許可するかどうか
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// 具体的な関数参照型が要求する関数型
    /// </summary>
    public WasmFunctionType? FunctionType { get; }

    /// <summary>
    /// 参照先を持つ値が存在しないヒープ型かどうか
    /// </summary>
    public bool IsBottomHeapType { get; }

    /// <summary>
    /// 関数または外部オブジェクトの参照型かどうか
    /// </summary>
    public bool IsReference => Kind is WasmValueKind.FuncRef or WasmValueKind.ExternRef;

    internal UnresolvedReference? Unresolved => _unresolved ?? FunctionType?.Unresolved;

    private WasmValueType(
        WasmValueKind kind,
        bool isNullable,
        WasmFunctionType? functionType = null,
        bool isBottomHeapType = false,
        UnresolvedReference? unresolved = null
    )
    {
        Kind = kind;
        IsNullable = isNullable;
        FunctionType = functionType;
        IsBottomHeapType = isBottomHeapType;
        _unresolved = unresolved;
    }

    /// <summary>
    /// 従来の値の種類に対応する宣言型を取得する funcrefとexternrefはnullを許可する
    /// </summary>
    public static implicit operator WasmValueType(WasmValueKind kind)
    {
        return new WasmValueType(kind, kind is WasmValueKind.FuncRef or WasmValueKind.ExternRef);
    }

    /// <summary>
    /// 関数または外部オブジェクトの抽象参照型にnull可否を指定する
    /// </summary>
    public static WasmValueType Reference(WasmValueKind kind, bool isNullable)
    {
        if (kind is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "参照型を指定してください。");
        }
        return new WasmValueType(kind, isNullable);
    }

    /// <summary>
    /// 指定した関数型への参照を表す宣言型を取得する
    /// </summary>
    public static WasmValueType FunctionReference(WasmFunctionType functionType, bool isNullable)
    {
        ArgumentNullException.ThrowIfNull(functionType);
        return new WasmValueType(WasmValueKind.FuncRef, isNullable, functionType);
    }

    /// <summary>
    /// 空のヒープ型nofuncまたはnoexternへの参照を表す宣言型を取得する
    /// </summary>
    public static WasmValueType BottomReference(WasmValueKind kind, bool isNullable)
    {
        if (kind is not (WasmValueKind.FuncRef or WasmValueKind.ExternRef))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "参照型を指定してください。");
        }
        return new WasmValueType(kind, isNullable, isBottomHeapType: true);
    }

    internal static WasmValueType UnresolvedFunctionReference(uint index, bool isNullable, WasmFailureLocation location)
    {
        return new WasmValueType(WasmValueKind.FuncRef, isNullable, unresolved: new UnresolvedReference(index, location));
    }

    internal WasmValueType Validate()
    {
        if (Unresolved is { } missing)
        {
            throw missing.ValidationFailure();
        }
        return this;
    }

    internal WasmValueType AsNonNullable()
    {
        return new WasmValueType(Kind, false, FunctionType, IsBottomHeapType, _unresolved);
    }

    /// <summary>
    /// この宣言型が引数の宣言型のすべての値を受け入れるか判定する
    /// </summary>
    public bool Accepts(WasmValueType actual)
    {
        if (Kind != actual.Kind || (!IsNullable && actual.IsNullable))
        {
            return false;
        }
        if (actual.IsBottomHeapType || IsBottomHeapType)
        {
            return actual.IsBottomHeapType;
        }
        return FunctionType is null || (actual.FunctionType is { } type && FunctionType.IsEquivalentTo(type));
    }

    /// <summary>
    /// 値の種類、参照先の有無と関数型をこの宣言型に照らして判定する
    /// </summary>
    public bool Accepts(WasmValue value)
    {
        if (value.Kind != Kind)
        {
            return false;
        }
        return Kind switch
        {
            WasmValueKind.FuncRef => value.AsFuncRef() is { } function
                ? !IsBottomHeapType && (FunctionType is null || FunctionType.IsEquivalentTo(function.Type))
                : IsNullable,
            WasmValueKind.ExternRef => value.AsExternRef() is null ? IsNullable : !IsBottomHeapType,
            _ => true,
        };
    }

    internal bool HasSameForm(WasmValueType other)
    {
        return Kind == other.Kind
            && IsNullable == other.IsNullable
            && IsBottomHeapType == other.IsBottomHeapType
            && (FunctionType is null) == (other.FunctionType is null)
            && _unresolved?.Index == other._unresolved?.Index;
    }

    /// <inheritdoc/>
    public bool Equals(WasmValueType other)
    {
        return HasSameForm(other) && (FunctionType is null || FunctionType.IsEquivalentTo(other.FunctionType!));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is WasmValueType other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, IsNullable, IsBottomHeapType, FunctionType?.StructuralHashCode, _unresolved?.Index);
    }

    /// <summary>
    /// 二つの宣言型が同値かどうかを返す
    /// </summary>
    public static bool operator ==(WasmValueType left, WasmValueType right) => left.Equals(right);

    /// <summary>
    /// 二つの宣言型が異なるかどうかを返す
    /// </summary>
    public static bool operator !=(WasmValueType left, WasmValueType right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
    {
        return Describe(FunctionType?.ToString());
    }

    internal string Describe(string? functionTypeName)
    {
        if (!IsReference || (IsNullable && !IsBottomHeapType && FunctionType is null && _unresolved is null))
        {
            return Kind.ToString();
        }
        var heapType = functionTypeName
            ?? _unresolved?.Index.ToString()
            ?? (Kind, IsBottomHeapType) switch
            {
                (WasmValueKind.FuncRef, false) => "func",
                (WasmValueKind.FuncRef, true) => "nofunc",
                (_, false) => "extern",
                (_, true) => "noextern",
            };
        return IsNullable ? $"(ref null {heapType})" : $"(ref {heapType})";
    }
}
