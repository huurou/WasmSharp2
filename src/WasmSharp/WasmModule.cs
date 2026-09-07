using System.Collections.Immutable;
using WasmSharp.Exceptions;
using WasmSharp.Execution;
using WasmSharp.Modules;

namespace WasmSharp;

/// <summary>
/// 静的なmodule定義を表現するクラス
/// </summary>
public sealed class WasmModule
{
    /// <summary>
    /// 全関数の検証成功時に設定する関数index順の実行コード
    /// </summary>
    internal ImmutableArray<FunctionCode> FunctionCodes { get; set; } = [];

    /// <summary>
    /// 型index順の関数型を保持する不変配列
    /// </summary>
    internal ImmutableArray<WasmFunctionType> Types { get; }

    /// <summary>
    /// 関数index順のデコード済み関数定義を保持する不変配列
    /// </summary>
    internal ImmutableArray<DecodedFunction> Functions { get; }

    /// <summary>
    /// 関数exportの宣言を保持する不変配列
    /// </summary>
    internal ImmutableArray<FunctionExport> Exports { get; }

    /// <summary>
    /// デコード元の入力バイナリのバイト数
    /// </summary>
    internal long InputLength { get; }

    /// <summary>
    /// デコードした型、関数とexportをコピーしてmodule定義を構築する
    /// </summary>
    /// <param name="types">型index順の関数型</param>
    /// <param name="functions">関数index順の関数定義</param>
    /// <param name="exports">関数exportの宣言</param>
    /// <param name="inputLength">入力バイナリのバイト数</param>
    internal WasmModule(
        ReadOnlySpan<WasmFunctionType> types,
        ReadOnlySpan<DecodedFunction> functions,
        ReadOnlySpan<FunctionExport> exports,
        long inputLength
    )
    {
        Types = ImmutableArray.Create(types);
        Functions = ImmutableArray.Create(functions);
        Exports = ImmutableArray.Create(exports);
        InputLength = inputLength;
    }

    /// <summary>
    /// 入力バイト列をmoduleにデコードする
    /// </summary>
    /// <param name="bytes">入力バイト列</param>
    /// <returns>デコードされたモジュール</returns>
    public static WasmModule Decode(ReadOnlySpan<byte> bytes)
    {
        return ModuleDecoder.Decode(bytes);
    }

    /// <summary>
    /// 入力ストリームをmoduleにデコードする
    /// </summary>
    /// <param name="stream">入力ストリーム</param>
    /// <returns>デコードされたmodule</returns>
    public static WasmModule Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("入力ストリームが読み取り不可でした。", nameof(stream));
        }

        using var buffer = new MemoryStream();
        Span<byte> chunk = stackalloc byte[4096];
        while (true)
        {
            var count = stream.Read(chunk);
            if (count == 0)
            {
                break;
            }

            // 入力元のLengthやseekに依存せず、実際に読んだ量で保持上限を確認する。
            if (count > Array.MaxLength - buffer.Length)
            {
                throw new WasmImplementationLimitException(
                    "入力バイナリが保持上限を超えています。",
                    WasmImplementationLimitReason.InputSize,
                    Array.MaxLength,
                    new(WasmProcessingStage.Decode, buffer.Length)
                );
            }

            buffer.Write(chunk[..count]);
        }

        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>
    /// moduleがWasmの型規則と構造規則を満たすか検証する
    /// </summary>
    /// <returns>検証済みのmodulle</returns>
    public WasmModule Validate()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// host moduleでimportを解決しmoduleをインスタンス化する
    /// </summary>
    /// <param name="hostModules">importする関数やリソースを提供するhost moduleのリスト</param>
    /// <param name="options">実行時のオプション設定</param>
    /// <returns>instance</returns>
    public WasmInstance Instantiate(
        ReadOnlySpan<WasmHostModule> hostModules,
        WasmExecutionOptions? options = default
    )
    {
        throw new NotImplementedException();
    }
}
