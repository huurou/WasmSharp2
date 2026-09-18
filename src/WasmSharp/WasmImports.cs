using System.Collections.Immutable;

namespace WasmSharp;

/// <summary>
/// importのmodule名とitem名に対応する外部実体の提供登録集合
/// </summary>
public sealed class WasmImports
{
    private ImmutableDictionary<string, ImmutableDictionary<string, WasmExternalValue>> modules_ =
        ImmutableDictionary.Create<string, ImmutableDictionary<string, WasmExternalValue>>(
            StringComparer.Ordinal
        );

    /// <summary>
    /// 提供元の現在の定義を登録する。重複する名前の組があれば全件を追加せず拒否する
    /// </summary>
    /// <param name="module">登録する関数やリソースの提供元</param>
    public void Add(WasmHostModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var items = module.Snapshot();
        if (!modules_.TryGetValue(module.Name, out var existing))
        {
            existing = ImmutableDictionary.Create<string, WasmExternalValue>(
                StringComparer.Ordinal
            );
        }
        foreach (var name in items.Keys)
        {
            if (existing.ContainsKey(name))
            {
                throw new ArgumentException(
                    "同じmodule名とitem名の提供登録が存在します。",
                    nameof(module)
                );
            }
        }
        // 全件の照合と新しい対応表の作成が成功してから登録を確定する。
        modules_ = modules_.SetItem(module.Name, existing.AddRange(items));
    }

    /// <summary>
    /// 後続の提供登録に影響されない名前の対応表を取得する
    /// </summary>
    internal ImmutableDictionary<
        string,
        ImmutableDictionary<string, WasmExternalValue>
    > Snapshot() => modules_;
}

/// <summary>
/// 4種の外部実体を型付きで保持する閉じた階層
/// </summary>
internal abstract class WasmExternalValue
{
    private WasmExternalValue() { }

    /// <summary>
    /// 関数実体の提供
    /// </summary>
    internal sealed class Function(WasmFunction value) : WasmExternalValue
    {
        /// <summary>
        /// 共有する関数実体
        /// </summary>
        internal WasmFunction Value { get; } = value;
    }

    /// <summary>
    /// global実体の提供
    /// </summary>
    internal sealed class Global(WasmGlobal value) : WasmExternalValue
    {
        /// <summary>
        /// 共有するglobal実体
        /// </summary>
        internal WasmGlobal Value { get; } = value;
    }

    /// <summary>
    /// memory実体の提供
    /// </summary>
    internal sealed class Memory(WasmMemory value) : WasmExternalValue
    {
        /// <summary>
        /// 共有するmemory実体
        /// </summary>
        internal WasmMemory Value { get; } = value;
    }

    /// <summary>
    /// table実体の提供
    /// </summary>
    internal sealed class Table(WasmTable value) : WasmExternalValue
    {
        /// <summary>
        /// 共有するtable実体
        /// </summary>
        internal WasmTable Value { get; } = value;
    }
}
