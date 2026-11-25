using System.Collections.Immutable;
using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 1入力のcommand処理で共有する、名前の対応・参照値・現在のcommand
/// </summary>
/// <remarks>
/// 入力ごとに新しく作り、別の入力の状態を持ち込まない。直近module・module識別子・直近の定義・定義名・登録名は別々に管理し、名前はOrdinalで比較する。
/// 名前は成功実体か原因付きの利用不能状態を指し、成立しなかったcommandの後に以前の成功へ戻さない。
/// </remarks>
/// <param name="inputPath">ケース識別に使うtest/core基準の入力相対path</param>
internal sealed class ScriptState(string inputPath)
{
    /// <summary>
    /// module識別子に対応する成功実体または利用不能状態
    /// </summary>
    private readonly Bindings<InstantiatedModule> _modules = new Bindings<InstantiatedModule>();

    /// <summary>
    /// 定義名に対応する検証済みの定義または利用不能状態 module識別子とは別の名前空間
    /// </summary>
    private readonly Bindings<ModuleDefinition> _definitions = new Bindings<ModuleDefinition>();

    /// <summary>
    /// importの登録名に対応する最新の提供元または利用不能状態
    /// </summary>
    private readonly Dictionary<string, ScriptBinding<WasmHostModule>> _registrations = [with(StringComparer.Ordinal)];

    /// <summary>
    /// 入力内の番号ごとに割り当て、ref.externとref.hostで共有するホスト値
    /// </summary>
    private readonly Dictionary<uint, object> _externrefs = [];

    /// <summary>
    /// 割り当てたホスト値から参照の同一性で引く元の番号
    /// </summary>
    private readonly Dictionary<object, uint> _externrefNumbers = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>
    /// 保存用の非null参照token 内容の等値性を使わず初出順に割り当てる対応
    /// </summary>
    private readonly Dictionary<object, int> _referenceTokens = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>
    /// 現在のcommandのprint呼出順の記録
    /// </summary>
    private readonly List<PrintRecord> _prints = [];

    /// <summary>
    /// ケース識別に使うtest/core基準の入力相対path
    /// </summary>
    internal string InputPath { get; } = inputPath;

    /// <summary>
    /// 直近の通常moduleまたはmodule_instanceの結果 どちらもまだ処理していない場合はnull
    /// </summary>
    internal ScriptBinding<InstantiatedModule>? LastModule => _modules.Latest;

    /// <summary>
    /// 処理中のcommand まだ開始していない場合はnull
    /// </summary>
    internal CaseId? CurrentCommand { get; private set; }

    /// <summary>
    /// 現在のcommandのprint記録を取得し、次のcommand開始後も取得済みの並びを保持する
    /// </summary>
    internal ImmutableArray<PrintRecord> Prints => [.. _prints];

    /// <summary>
    /// 現在のcommandでspectestのcallbackが投げた例外実体で、失敗していなければnull
    /// </summary>
    internal Exception? CallbackException { get; set; }

    /// <summary>
    /// 指定したcommandを処理中のcommandにし、printとcallback失敗の記録を初期化する。
    /// </summary>
    /// <param name="commandIndex">入力内の0始まりcommand index</param>
    /// <returns>処理中のcommandのケース識別</returns>
    internal CaseId BeginCommand(int commandIndex)
    {
        var id = new CaseId(InputPath, commandIndex);
        CurrentCommand = id;
        _prints.Clear();
        CallbackException = null;
        return id;
    }

    /// <summary>
    /// 現在のcommandへprintの1回の呼出しを追加する。
    /// </summary>
    /// <param name="print">関数名と引数の型・ビット列の記録</param>
    /// <exception cref="InvalidOperationException">まだcommandを開始していない場合</exception>
    internal void RecordPrint(PrintRecord print)
    {
        if (CurrentCommand is null)
        {
            throw new InvalidOperationException("printの記録には処理中のcommandが必要です。");
        }
        _prints.Add(print);
    }

    /// <summary>
    /// actionやregisterの対象moduleを解決する。
    /// </summary>
    /// <param name="name">module識別子 省略時は直近の通常moduleまたはmodule_instanceの結果を示すnull</param>
    /// <returns>生成したinstanceか利用不能状態 指定した識別子が存在しない場合や、省略時にまだどちらのcommandも処理していない場合はnull</returns>
    internal ScriptBinding<InstantiatedModule>? ResolveModule(string? name)
    {
        return _modules.Resolve(name);
    }

    /// <summary>
    /// 登録名が指す提供元を取得する。
    /// </summary>
    /// <param name="name">importのmodule名として使う登録名</param>
    /// <returns>成功した提供元か利用不能状態 一度も登録していない名前ではnull</returns>
    internal ScriptBinding<WasmHostModule>? GetRegistration(string name)
    {
        return _registrations.GetValueOrDefault(name);
    }

    /// <summary>
    /// 現在の成功登録から提供表を作り直す。再登録前のexportや失敗した提供元は含めない。
    /// </summary>
    /// <returns>成功した最新の提供元だけを持つ新しい提供表 関数とリソースは同じ実体を共有する</returns>
    internal WasmImports CreateImports()
    {
        var imports = new WasmImports();
        foreach (var registration in _registrations.Values)
        {
            if (registration.Value is { } host)
            {
                imports.Add(host);
            }
        }
        return imports;
    }

    /// <summary>
    /// 成功したインスタンス化で直近moduleと指定識別子を更新する。
    /// </summary>
    /// <param name="name">通常moduleまたはmodule_instanceが指定したmodule識別子 省略時はnull</param>
    /// <param name="module">生成したinstanceと生成元のmodule</param>
    internal void SetModule(string? name, InstantiatedModule module)
    {
        _modules.Update(name, ScriptBinding<InstantiatedModule>.Available(module));
    }

    /// <summary>
    /// module_instanceの対象定義を解決する。
    /// </summary>
    /// <param name="name">定義名 省略時は直近の定義を示すnull</param>
    /// <returns>検証済みの定義か利用不能状態 存在しない定義名や、定義を処理する前の省略ではnull</returns>
    internal ScriptBinding<ModuleDefinition>? ResolveDefinition(string? name)
    {
        return _definitions.Resolve(name);
    }

    /// <summary>
    /// 通常moduleまたはmodule定義のDecode・Validateの結果で、直近の定義と指定した定義名を更新する。
    /// </summary>
    /// <remarks>
    /// Decode・Validateは他のcommandに依存しないため、定義が成立しなかった場合は定義したcommand自身を元の失敗とする。
    /// </remarks>
    /// <param name="command">定義を処理したcommand</param>
    /// <param name="name">定義名 省略時はnull</param>
    /// <param name="definition">検証済みの定義 成立しなかった場合はnull</param>
    internal void Define(ScriptCommand command, string? name, ModuleDefinition? definition)
    {
        var id = new CaseId(InputPath, command.Index);
        _definitions.Update(
            name,
            definition is null
                ? ScriptBinding<ModuleDefinition>.Unavailable(new UnavailableCause(id, [id]))
                : ScriptBinding<ModuleDefinition>.Available(definition)
        );
    }

    /// <summary>
    /// 提供元のmodule名を登録名とし、既存の登録を置換する。
    /// </summary>
    /// <param name="module">登録名をmodule名に持つ提供元</param>
    internal void Register(WasmHostModule module)
    {
        _registrations[module.Name] = ScriptBinding<WasmHostModule>.Available(module);
    }

    /// <summary>
    /// 成立しなかったcommandの種類に応じて、読み取れた更新対象の名前を利用不能にする。
    /// </summary>
    /// <remarks>
    /// 通常moduleとmodule_instanceは直近moduleと識別子、registerは登録名を更新する。読み取れない通常moduleとmodule定義は、直近の定義と定義名も更新する。
    /// 読み取れたmodule定義と通常moduleの定義側はDefineで更新済みのため変更しない。否定module・action・assertionと、更新対象を取得できない名前は変更しない。
    /// </remarks>
    /// <param name="command">成立しなかったcommand</param>
    /// <param name="cause">commandがblockedの場合の原因 それ以外はnull</param>
    internal void Fail(ScriptCommand command, CaseCause? cause)
    {
        var id = new CaseId(InputPath, command.Index);
        // blockedは依存先の元の失敗を引き継ぎ、それ以外は自身を元の失敗とする。
        var unavailable = new UnavailableCause(id, cause is null ? [id] : [.. cause.Origins]);
        switch (command)
        {
            case ModuleCommand x:
                _modules.Update(x.Name, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                break;
            case ModuleInstanceCommand x:
                _modules.Update(x.Instance, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                break;
            case InvalidCommand { Type: ScriptCommand.MODULE } x:
                _modules.Update(x.Name, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                _definitions.Update(x.Name, ScriptBinding<ModuleDefinition>.Unavailable(unavailable));
                break;
            case InvalidCommand { Type: ScriptCommand.MODULE_DEFINITION, SpecVersion: WasmSpecVersion.Core3 } x:
                _definitions.Update(x.Name, ScriptBinding<ModuleDefinition>.Unavailable(unavailable));
                break;
            case InvalidCommand { Type: ScriptCommand.MODULE_INSTANCE, SpecVersion: WasmSpecVersion.Core3 } x:
                _modules.Update(x.Name, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                break;
            case RegisterCommand x:
                _registrations[x.As] = ScriptBinding<WasmHostModule>.Unavailable(unavailable);
                break;
            case InvalidCommand { Type: ScriptCommand.REGISTER, As: { } name }:
                _registrations[name] = ScriptBinding<WasmHostModule>.Unavailable(unavailable);
                break;
        }
    }

    /// <summary>
    /// ref.externとref.hostで使う番号に対応するホスト値を返す。同じ番号には入力内で同じobjectを割り当てる。
    /// </summary>
    /// <param name="number">JSONに記録された非nullの参照の番号</param>
    /// <returns>指定番号に割り当てたホスト値 同じ入力の同じ番号では同じobjectを返す</returns>
    internal object GetExternref(uint number)
    {
        if (!_externrefs.TryGetValue(number, out var externref))
        {
            // 内容を持たない専用objectを割り当て、参照の同一性だけで比較する。
            externref = new object();
            _externrefs.Add(number, externref);
            _externrefNumbers.Add(externref, number);
        }

        return externref;
    }

    /// <summary>
    /// この入力で割り当てたホスト値の元番号を返す。
    /// </summary>
    /// <param name="reference">非nullの参照</param>
    /// <returns>割り当てた番号 この入力で割り当てていない参照ではnull</returns>
    internal uint? GetExternrefNumber(object reference)
    {
        return _externrefNumbers.TryGetValue(reference, out var number) ? number : null;
    }

    /// <summary>
    /// 非null参照の記録に使う、入力内で初出順のtokenを返す。
    /// </summary>
    /// <param name="reference">非nullの参照 内容の等値性ではなく参照の同一性で識別する</param>
    /// <returns>0始まりで初出順に割り当てたtoken 同じ参照の再取得では同じ値</returns>
    internal int GetReferenceToken(object reference)
    {
        if (!_referenceTokens.TryGetValue(reference, out var token))
        {
            token = _referenceTokens.Count;
            _referenceTokens.Add(reference, token);
        }

        return token;
    }

    /// <summary>
    /// 同じ名前空間の直近の値と、識別子に対応する値を保持する。
    /// </summary>
    private sealed class Bindings<T>
        where T : class
    {
        private readonly Dictionary<string, ScriptBinding<T>> _named = [with(StringComparer.Ordinal)];

        internal ScriptBinding<T>? Latest { get; private set; }

        internal ScriptBinding<T>? Resolve(string? name)
        {
            return name is null ? Latest : _named.GetValueOrDefault(name);
        }

        internal void Update(string? name, ScriptBinding<T> binding)
        {
            Latest = binding;
            if (name is not null)
            {
                _named[name] = binding;
            }
        }
    }
}
