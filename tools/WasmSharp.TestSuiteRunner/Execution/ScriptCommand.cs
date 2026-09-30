using WasmSharp.TestSuiteRunner.Reports;

namespace WasmSharp.TestSuiteRunner.Execution;

/// <summary>
/// 列挙済みJSONの1commandを種類別に読み取った結果
/// </summary>
/// <param name="Index">入力内の0始まりcommand index</param>
/// <param name="Line">元入力の1始まり行番号 不正なcommandで取得できない場合はnull</param>
internal abstract record ScriptCommand(int Index, int? Line)
{
    /// <summary>
    /// 通常moduleを表すJSONのcommand種別
    /// </summary>
    internal const string MODULE = "module";

    /// <summary>
    /// 登録名への対応付けを表すJSONのcommand種別
    /// </summary>
    internal const string REGISTER = "register";

    /// <summary>
    /// assertionを伴わないactionを表すJSONのcommand種別
    /// </summary>
    internal const string ACTION = "action";

    /// <summary>
    /// 正常完了と値の一致を期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_RETURN = "assert_return";

    /// <summary>
    /// actionでのtrapを期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_TRAP = "assert_trap";

    /// <summary>
    /// actionでのexhaustionを期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_EXHAUSTION = "assert_exhaustion";

    /// <summary>
    /// Decodeでの構文不成立を期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_MALFORMED = "assert_malformed";

    /// <summary>
    /// Validateでの検証不成立を期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_INVALID = "assert_invalid";

    /// <summary>
    /// Instantiateでのリンク不成立を期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_UNLINKABLE = "assert_unlinkable";

    /// <summary>
    /// Instantiate中のtrapを期待するJSONのcommand種別
    /// </summary>
    internal const string ASSERT_UNINSTANTIABLE = "assert_uninstantiable";

    /// <summary>
    /// 元JSONのcommand種別 取得できない場合はnull
    /// </summary>
    internal abstract string? Type { get; }

    /// <summary>
    /// 集計区分 固定形式の種別として確定できない場合はnull
    /// </summary>
    internal CaseCategory? Category =>
        Type switch
        {
            MODULE or REGISTER => CaseCategory.Setup,
            ACTION => CaseCategory.Action,
            ASSERT_RETURN
            or ASSERT_TRAP
            or ASSERT_EXHAUSTION
            or ASSERT_MALFORMED
            or ASSERT_INVALID
            or ASSERT_UNLINKABLE
            or ASSERT_UNINSTANTIABLE => CaseCategory.Assertion,
            _ => null,
        };
}
