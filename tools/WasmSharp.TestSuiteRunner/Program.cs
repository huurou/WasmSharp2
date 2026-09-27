if (args is ["--help"])
{
    Console.WriteLine("Test Suite Runner: WebAssembly Core 2.0の公式適合検証ツール");
    Console.WriteLine("使用法: WasmSharp.TestSuiteRunner --help");
    Console.WriteLine("  --help  操作説明を表示します。");
    return 0;
}

Console.Error.WriteLine("操作を指定してください。使用法は--helpで確認できます。");
return 2;
