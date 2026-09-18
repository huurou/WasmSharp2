namespace WasmSharp;

/// <summary>
/// memoryのページ数またはtableの要素数の最小値と任意の最大値
/// </summary>
/// <remarks>大小関係や仕様上限の検証は資源生成またはValidateで行う</remarks>
/// <param name="Minimum">最小値</param>
/// <param name="Maximum">最大値 指定しない場合はnull</param>
public sealed record WasmLimits(uint Minimum, uint? Maximum = null);
