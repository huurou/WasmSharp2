using System.Collections.Immutable;
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
    /// 参照型かどうか
    /// </summary>
    public bool IsReference => IsReferenceKind(Kind);

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

    internal static bool IsSupportedKind(WasmValueKind kind)
    {
        return kind is WasmValueKind.I32 or WasmValueKind.I64 or WasmValueKind.F32 or WasmValueKind.F64 or WasmValueKind.V128
            || IsReferenceKind(kind);
    }

    internal static bool IsReferenceKind(WasmValueKind kind)
    {
        return GetTopKind(kind) is WasmValueKind.FuncRef or WasmValueKind.ExternRef or WasmValueKind.AnyRef;
    }

    /// <summary>
    /// 参照型を所属する階層の最上位型にまとめる 数値とベクトルの種類はそのまま返す
    /// </summary>
    internal static WasmValueKind GetTopKind(WasmValueKind kind)
    {
        return kind is WasmValueKind.EqRef or WasmValueKind.I31Ref or WasmValueKind.StructRef or WasmValueKind.ArrayRef
            ? WasmValueKind.AnyRef
            : kind;
    }

    /// <summary>
    /// 値の種類に対応する宣言型を取得する 参照型はnullを許可する
    /// </summary>
    public static implicit operator WasmValueType(WasmValueKind kind)
    {
        return new WasmValueType(kind, IsReferenceKind(kind));
    }

    /// <summary>
    /// 抽象参照型にnull可否を指定する
    /// </summary>
    public static WasmValueType Reference(WasmValueKind kind, bool isNullable)
    {
        if (!IsReferenceKind(kind))
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
    /// 指定した参照階層の底型であるnone、nofuncまたはnoexternへの参照型を取得する
    /// </summary>
    public static WasmValueType BottomReference(WasmValueKind kind, bool isNullable)
    {
        if (!IsReferenceKind(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "参照型を指定してください。");
        }
        return new WasmValueType(GetTopKind(kind), isNullable, isBottomHeapType: true);
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
        if (!IsNullable && actual.IsNullable)
        {
            return false;
        }
        if (actual.IsBottomHeapType)
        {
            return GetTopKind(Kind) == actual.Kind;
        }
        if (IsBottomHeapType)
        {
            return false;
        }
        if (Kind != actual.Kind)
        {
            return Kind switch
            {
                WasmValueKind.AnyRef => GetTopKind(actual.Kind) == WasmValueKind.AnyRef,
                WasmValueKind.EqRef => actual.Kind
                    is WasmValueKind.I31Ref
                        or WasmValueKind.StructRef
                        or WasmValueKind.ArrayRef,
                _ => false,
            };
        }
        return FunctionType is null || (actual.FunctionType is { } type && type.IsSubtypeOf(FunctionType));
    }

    /// <summary>
    /// 同じ個数の値を順に対応する型へ代入できるかを判定する
    /// </summary>
    internal static bool AcceptsAll(ImmutableArray<WasmValueType> expected, ImmutableArray<WasmValueType> actual)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }
        for (var index = 0; index < expected.Length; index++)
        {
            if (!expected[index].Accepts(actual[index]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 値の種類、参照先の有無と関数型をこの宣言型に照らして判定する
    /// </summary>
    public bool Accepts(WasmValue value)
    {
        if (!IsReference)
        {
            return value.Kind == Kind;
        }
        var actual = value.Kind switch
        {
            _ when value.IsNullReference => BottomReference(value.Kind, true),
            WasmValueKind.FuncRef => FunctionReference(value.AsFuncRef()!.Type, false),
            _ => ((WasmValueType)value.Kind).AsNonNullable(),
        };
        return Accepts(actual);
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
                (WasmValueKind.ExternRef, false) => "extern",
                (WasmValueKind.ExternRef, true) => "noextern",
                (WasmValueKind.AnyRef, true) => "none",
                (WasmValueKind.AnyRef, false) => "any",
                (WasmValueKind.EqRef, _) => "eq",
                (WasmValueKind.I31Ref, _) => "i31",
                (WasmValueKind.StructRef, _) => "struct",
                (WasmValueKind.ArrayRef, _) => "array",
                _ => throw new InvalidOperationException("参照型が不正です。"),
            };
        return IsNullable ? $"(ref null {heapType})" : $"(ref {heapType})";
    }
}
