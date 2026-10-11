using System.Globalization;
using WasmSharp.Exceptions;

namespace WasmSharp.Modules;

/// <summary>
/// module全体と関数内の検証診断を共通の形式で作成する
/// </summary>
internal static class ValidationDiagnostics
{
    /// <summary>
    /// 存在しない添字の種類と10進表記を原因に対応する診断へ含める
    /// </summary>
    /// <param name="category">添字空間の種類</param>
    /// <param name="index">存在しない添字</param>
    /// <param name="message">原因の補助説明</param>
    /// <param name="location">原因の位置</param>
    /// <returns>添字と位置を保持する検証例外</returns>
    internal static WasmValidateException UnknownIndex(
        string category,
        uint index,
        string message,
        WasmFailureLocation location
    )
    {
        return ValidationFailure(
            $"unknown {category} {index.ToString(CultureInfo.InvariantCulture)}",
            message,
            location
        );
    }

    /// <summary>
    /// 選択した原因の診断先頭に補助説明と位置を付ける
    /// </summary>
    /// <param name="prefix">原因を表す診断先頭</param>
    /// <param name="message">原因の補助説明</param>
    /// <param name="location">原因の位置</param>
    /// <returns>原因と位置が一致する検証例外</returns>
    internal static WasmValidateException ValidationFailure(
        string prefix,
        string message,
        WasmFailureLocation location
    )
    {
        return new WasmValidateException($"{prefix}: {message}", location, null);
    }
}
