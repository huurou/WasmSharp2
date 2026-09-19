using System.Collections.Immutable;

namespace WasmSharp;

/// <summary>
/// ホスト関数の処理を定義するデリゲート
/// </summary>
/// <param name="arguments">ホスト関数に渡すvalueのコレクション</param>
/// <returns>ホスト関数が返すvalueのコレクション</returns>
public delegate WasmResults WasmHostCallback(ReadOnlySpan<WasmValue> arguments);

/// <summary>
/// 呼び出し時のinstanceを受け取るホスト関数の処理を定義するデリゲート
/// </summary>
/// <param name="instance">呼び出し時に指定されたinstance</param>
/// <param name="arguments">ホスト関数に渡すvalueのコレクション</param>
/// <returns>ホスト関数が返す所有済みのvalueのコレクション</returns>
public delegate WasmResults WasmHostInstanceCallback(
    WasmInstance instance,
    ReadOnlySpan<WasmValue> arguments
);

/// <summary>
/// importへ提供する名前付きの関数やリソースをまとめたホストモジュール
/// </summary>
public sealed class WasmHostModule
{
    private ImmutableDictionary<string, WasmExternalValue> items_ = ImmutableDictionary.Create<
        string,
        WasmExternalValue
    >(StringComparer.Ordinal);

    /// <summary>
    /// importの名前解決で使うmodule名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 現在の名前と実体の対応を、後続の定義変更に影響されない形で取得する
    /// </summary>
    internal ImmutableDictionary<string, WasmExternalValue> Snapshot => items_;

    /// <summary>
    /// 空のmodule名を持つ提供元を構築する
    /// </summary>
    public WasmHostModule()
        : this("") { }

    /// <summary>
    /// 指定したmodule名を持つ空の提供元を構築する
    /// </summary>
    /// <param name="name">空文字列を含むmodule名</param>
    public WasmHostModule(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// 関数を指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="function">共有する関数実体</param>
    public void Define(string name, WasmFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        Define(name, new WasmExternalValue.Function(function));
    }

    /// <summary>
    /// globalを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="global">共有するglobal実体</param>
    public void Define(string name, WasmGlobal global)
    {
        ArgumentNullException.ThrowIfNull(global);
        Define(name, new WasmExternalValue.Global(global));
    }

    /// <summary>
    /// memoryを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="memory">共有するmemory実体</param>
    public void Define(string name, WasmMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        Define(name, new WasmExternalValue.Memory(memory));
    }

    /// <summary>
    /// tableを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="table">共有するtable実体</param>
    public void Define(string name, WasmTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Define(name, new WasmExternalValue.Table(table));
    }

    private void Define(string name, WasmExternalValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        items_ = items_.Add(name, value);
    }
}
