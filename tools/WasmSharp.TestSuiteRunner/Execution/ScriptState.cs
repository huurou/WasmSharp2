using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 1入力のcommand処理で共有する、名前の対応・参照値・現在のcommand
/// </summary>
/// <remarks>
/// 入力ごとに新しく作り、別の入力の状態を持ち込まない。直近module・module識別子・登録名は別々に管理し、名前はOrdinalで比較する。
/// 名前は成功実体か原因付きの利用不能状態を指し、成立しなかったcommandの後に以前の成功へ戻さない。
/// </remarks>
/// <param name="inputPath">ケース識別に使うtest/core基準の入力相対path</param>
internal sealed class ScriptState(string inputPath)
{
    private readonly Dictionary<string, ScriptBinding<InstantiatedModule>> modules_ = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, ScriptBinding<WasmHostModule>> registrations_ = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<uint, object> externrefs_ = [];
    private readonly Dictionary<object, uint> externrefNumbers_ = new(
        ReferenceEqualityComparer.Instance
    );
    private readonly Dictionary<object, int> referenceTokens_ = new(
        ReferenceEqualityComparer.Instance
    );

    /// <summary>
    /// ケース識別に使うtest/core基準の入力相対path
    /// </summary>
    internal string InputPath { get; } = inputPath;

    /// <summary>
    /// 直近の通常module。まだ通常moduleを処理していない場合はnull
    /// </summary>
    internal ScriptBinding<InstantiatedModule>? LastModule { get; private set; }

    /// <summary>
    /// 処理中のcommand。まだ開始していない場合はnull
    /// </summary>
    internal CaseId? CurrentCommand { get; private set; }

    /// <summary>
    /// 指定したcommandを処理中のcommandにする。
    /// </summary>
    /// <param name="commandIndex">入力内の0始まりcommand index</param>
    /// <returns>処理中のcommandのケース識別</returns>
    internal CaseId BeginCommand(int commandIndex)
    {
        var id = new CaseId(InputPath, commandIndex);
        CurrentCommand = id;
        return id;
    }

    /// <summary>
    /// actionやregisterの対象moduleを解決する。
    /// </summary>
    /// <param name="name">module識別子。省略時は直近の通常moduleを示すnull</param>
    /// <returns>成功実体か利用不能状態。存在しない識別子や、通常moduleを処理する前の省略ではnull</returns>
    internal ScriptBinding<InstantiatedModule>? ResolveModule(string? name)
    {
        return name is null ? LastModule : modules_.GetValueOrDefault(name);
    }

    /// <summary>
    /// 登録名が指す提供元を取得する。
    /// </summary>
    /// <param name="name">importのmodule名として使う登録名</param>
    /// <returns>成功した提供元か利用不能状態。一度も登録していない名前ではnull</returns>
    internal ScriptBinding<WasmHostModule>? GetRegistration(string name)
    {
        return registrations_.GetValueOrDefault(name);
    }

    /// <summary>
    /// 成功した通常moduleで直近moduleと指定識別子を更新する。
    /// </summary>
    /// <param name="command">成功した通常module</param>
    /// <param name="module">生成したinstanceと生成元のmodule</param>
    internal void SetModule(ModuleCommand command, InstantiatedModule module)
    {
        UpdateModule(command.Name, ScriptBinding<InstantiatedModule>.Available(module));
    }

    /// <summary>
    /// 提供元のmodule名を登録名とし、既存の登録を置換する。
    /// </summary>
    /// <param name="module">登録名をmodule名に持つ提供元</param>
    internal void Register(WasmHostModule module)
    {
        registrations_[module.Name] = ScriptBinding<WasmHostModule>.Available(module);
    }

    /// <summary>
    /// 成立しなかったcommandの種類に応じて、読み取れた更新対象の名前を利用不能にする。
    /// </summary>
    /// <remarks>
    /// 通常moduleは直近moduleと識別子、registerは登録名を更新する。否定module・action・assertionと、更新対象を取得できない名前は変更しない。
    /// </remarks>
    /// <param name="command">成立しなかったcommand</param>
    /// <param name="cause">commandがblockedの場合の原因。それ以外はnull</param>
    internal void Fail(ScriptCommand command, CaseCause? cause)
    {
        var id = new CaseId(InputPath, command.Index);
        // blockedは依存先の元の失敗を引き継ぎ、それ以外は自身を元の失敗とする。
        var unavailable = new UnavailableCause(id, cause is null ? [id] : [.. cause.Origins]);
        switch (command)
        {
            case ModuleCommand x:
                UpdateModule(x.Name, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                break;
            case InvalidCommand { Type: ScriptCommand.MODULE } x:
                UpdateModule(x.Name, ScriptBinding<InstantiatedModule>.Unavailable(unavailable));
                break;
            case RegisterCommand x:
                registrations_[x.As] = ScriptBinding<WasmHostModule>.Unavailable(unavailable);
                break;
            case InvalidCommand { Type: ScriptCommand.REGISTER, As: { } name }:
                registrations_[name] = ScriptBinding<WasmHostModule>.Unavailable(unavailable);
                break;
        }
    }

    /// <summary>
    /// externrefの番号に対応するホスト値を返す。同じ番号には入力内で同じobjectを割り当てる。
    /// </summary>
    /// <param name="number">JSONに記録された非nullのexternref番号</param>
    internal object GetExternref(uint number)
    {
        if (!externrefs_.TryGetValue(number, out var externref))
        {
            // 内容を持たない専用objectを割り当て、参照の同一性だけで比較する。
            externref = new object();
            externrefs_.Add(number, externref);
            externrefNumbers_.Add(externref, number);
        }

        return externref;
    }

    /// <summary>
    /// この入力で割り当てたexternrefの元番号を返す。
    /// </summary>
    /// <param name="reference">非nullの参照</param>
    /// <returns>割り当てた番号。この入力で割り当てていない参照ではnull</returns>
    internal uint? GetExternrefNumber(object reference)
    {
        return externrefNumbers_.TryGetValue(reference, out var number) ? number : null;
    }

    /// <summary>
    /// 非null参照の記録に使う、入力内で初出順のtokenを返す。
    /// </summary>
    /// <param name="reference">非nullの参照。内容の等値性ではなく参照の同一性で識別する</param>
    internal int GetReferenceToken(object reference)
    {
        if (!referenceTokens_.TryGetValue(reference, out var token))
        {
            token = referenceTokens_.Count;
            referenceTokens_.Add(reference, token);
        }

        return token;
    }

    private void UpdateModule(string? name, ScriptBinding<InstantiatedModule> binding)
    {
        LastModule = binding;
        if (name is not null)
        {
            modules_[name] = binding;
        }
    }
}
