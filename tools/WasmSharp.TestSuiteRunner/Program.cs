using System.Text;
using WasmSharp.TestSuiteRunner;
using WasmSharp.TestSuiteRunner.Corpus;

Console.OutputEncoding = Encoding.UTF8;
return new RunnerCli(Core2Profile.Load()).Run(args, Console.Out, Console.Error);
