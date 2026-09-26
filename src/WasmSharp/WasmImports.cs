using System.Collections.Immutable;
using WasmSharp.Modules.ExternalValues;

namespace WasmSharp;

/// <summary>
/// importのmodule名とitem名に対応する外部実体の提供登録集合
/// </summary>
public sealed class WasmImports
{
    /// <summary>
    /// module名とitem名から共有する外部実体への対応
    /// </summary>
    private ImmutableDictionary<string, ImmutableDictionary<string, ExternalValue>> modules_ =
        ImmutableDictionary.Create<string, ImmutableDictionary<string, ExternalValue>>(
            StringComparer.Ordinal
        );

    /// <summary>
    /// 提供元の現在の定義を登録する。重複する名前の組があれば全件を追加せず拒否する
    /// </summary>
    /// <remarks>
    /// 名前は大文字・小文字を区別して完全一致で照合する。登録後に提供元へ追加した定義は、この登録集合へ反映しない。
    /// 関数とリソースは複製せず共有するため、登録後のリソース更新も同じ実体から参照できる。
    /// </remarks>
    /// <param name="module">登録する関数やリソースの提供元</param>
    /// <exception cref="ArgumentNullException">moduleがnullの場合</exception>
    /// <exception cref="ArgumentException">同じmodule名とitem名の組が登録済みの場合。登録集合は変更しない</exception>
    public void Add(WasmHostModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var items = module.Snapshot;
        if (!modules_.TryGetValue(module.Name, out var existing))
        {
            existing = ImmutableDictionary.Create<string, ExternalValue>(StringComparer.Ordinal);
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
    /// <returns>現在の対応表。表が参照する関数とリソースは共有する</returns>
    internal ImmutableDictionary<string, ImmutableDictionary<string, ExternalValue>> Snapshot()
    {
        return modules_;
    }
}
