using System.Collections.Immutable;
using WasmSharp.Modules.ExternalValues;

namespace WasmSharp;

/// <summary>
/// ホスト関数の処理を定義するデリゲート
/// </summary>
/// <remarks>
/// 引数は呼び出し専用のコピーで、同期再入による作業スタックの変化に影響されない。callback終了後も値を保持する場合はコピーする。
/// 結果のnull・個数・型の不一致はランタイムがInvalidOperationExceptionで拒否する。callbackが投げた例外は型と実体を変えずに伝播する。
/// </remarks>
/// <param name="arguments">宣言された引数型と順序に従う値。利用を保証する期間は同期callbackの実行中</param>
/// <returns>宣言された結果の個数・型・順序に一致する所有済みの値。nullは不可</returns>
public delegate WasmResults WasmHostCallback(ReadOnlySpan<WasmValue> arguments);

/// <summary>
/// 呼び出し時のinstanceを受け取るホスト関数の処理を定義するデリゲート
/// </summary>
/// <remarks>
/// instanceは関数の取得元に固定せず、Wasmの値引数や関数型にも含めない。start中のinstance受領はInstantiateの成功を保証しない。
/// 引数は呼び出し専用のコピーで、同期再入による作業スタックの変化に影響されない。callback終了後も値を保持する場合はコピーする。
/// 結果のnull・個数・型の不一致はランタイムがInvalidOperationExceptionで拒否する。callbackが投げた例外は型と実体を変えずに伝播する。
/// </remarks>
/// <param name="instance">Wasmからは呼び出し中の定義関数の所属instance、host startではstartを持つinstance、C#からは明示指定したinstance</param>
/// <param name="arguments">宣言された引数型と順序に従う値。利用を保証する期間は同期callbackの実行中</param>
/// <returns>宣言された結果の個数・型・順序に一致する所有済みの値。nullは不可</returns>
public delegate WasmResults WasmHostInstanceCallback(
    WasmInstance instance,
    ReadOnlySpan<WasmValue> arguments
);

/// <summary>
/// importへ提供する名前付きの関数やリソースをまとめたホストモジュール
/// </summary>
/// <remarks>module名とitem名は大文字・小文字を区別して完全一致で照合する。登録した実体は複製せず共有する</remarks>
public sealed class WasmHostModule
{
    /// <summary>
    /// item名から共有する外部実体への対応
    /// </summary>
    private ImmutableDictionary<string, ExternalValue> items_ = ImmutableDictionary.Create<
        string,
        ExternalValue
    >(StringComparer.Ordinal);

    /// <summary>
    /// importの名前解決で使うmodule名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 現在の名前と実体の対応を、後続の定義変更に影響されない形で取得する
    /// </summary>
    internal ImmutableDictionary<string, ExternalValue> Snapshot => items_;

    /// <summary>
    /// 空のmodule名を持つ提供元を構築する
    /// </summary>
    public WasmHostModule()
        : this("") { }

    /// <summary>
    /// 指定したmodule名を持つ空の提供元を構築する
    /// </summary>
    /// <param name="name">空文字列を含むmodule名</param>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
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
    /// <exception cref="ArgumentNullException">nameまたはfunctionがnullの場合</exception>
    /// <exception cref="ArgumentException">同じitem名が既に定義されている場合。既存の定義は変更しない</exception>
    public void Define(string name, WasmFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        Define(name, new FunctionExternalValue(function));
    }

    /// <summary>
    /// globalを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="global">共有するglobal実体</param>
    /// <exception cref="ArgumentNullException">nameまたはglobalがnullの場合</exception>
    /// <exception cref="ArgumentException">同じitem名が既に定義されている場合。既存の定義は変更しない</exception>
    public void Define(string name, WasmGlobal global)
    {
        ArgumentNullException.ThrowIfNull(global);
        Define(name, new GlobalExternalValue(global));
    }

    /// <summary>
    /// memoryを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="memory">共有するmemory実体</param>
    /// <exception cref="ArgumentNullException">nameまたはmemoryがnullの場合</exception>
    /// <exception cref="ArgumentException">同じitem名が既に定義されている場合。既存の定義は変更しない</exception>
    public void Define(string name, WasmMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        Define(name, new MemoryExternalValue(memory));
    }

    /// <summary>
    /// tableを指定名で定義する。同じ名前の外部要素があれば拒否する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="table">共有するtable実体</param>
    /// <exception cref="ArgumentNullException">nameまたはtableがnullの場合</exception>
    /// <exception cref="ArgumentException">同じitem名が既に定義されている場合。既存の定義は変更しない</exception>
    public void Define(string name, WasmTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Define(name, new TableExternalValue(table));
    }

    /// <summary>
    /// 名前の重複を拒否し、外部実体への対応を追加する
    /// </summary>
    /// <param name="name">空文字列を含むitem名</param>
    /// <param name="value">共有する関数またはリソースを包んだ外部要素</param>
    /// <exception cref="ArgumentNullException">nameがnullの場合</exception>
    /// <exception cref="ArgumentException">同じitem名が既に定義されている場合。既存の対応は変更しない</exception>
    private void Define(string name, ExternalValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        items_ = items_.Add(name, value);
    }
}
