namespace WasmSharp.Execution;

/// <summary>
/// 検証と線形化で確定した関数内の分岐先とoperandの配置
/// </summary>
/// <param name="TargetPc">現在の関数内の分岐先Pc</param>
/// <param name="StackHeight">現在の関数のoperand領域を基準とする移動先の高さ</param>
/// <param name="KeepCount">operand末尾から宣言順に保持する値の個数</param>
internal readonly record struct BranchTarget(int TargetPc, int StackHeight, int KeepCount);
