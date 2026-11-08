using System.Text;
using WasmSharp.TestSuiteRunner;
using WasmSharp.TestSuiteRunner.Corpus;

Console.OutputEncoding = Encoding.UTF8;
var profileLoader = new Core2ProfileLoader();
return new RunnerCli(profileLoader.Load()).Run(args, Console.Out, Console.Error);
