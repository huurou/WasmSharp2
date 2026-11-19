# ミューテーションテスト

.NET SDKと固定Core 2.0仕様を[セットアップ手順](../README.md#開発環境のセットアップ)に従って用意する。Stryker.NETは`.config/dotnet-tools.json`で固定している。

## 通常テストとカバレッジ

リポジトリルートで警告をエラーとして扱うReleaseビルドを行い、TUnitの通常テストで行・分岐カバレッジを収集する。次はランタイムの実行例である。

```sh
dotnet tool restore
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings .config/test-coverage.config.xml --results-directory artifacts/coverage/WasmSharp.Tests
```

Cobertura XMLのうち対象アセンブリのpackageを集計する。`.config/test-coverage.config.xml`は本体4アセンブリを収集対象とし、`*.generated.cs`を除く。命令生成器の出力（`obj`配下の`generated`にある`InstructionSet.g.cs`と`Interpreter.g.cs`）はXMLに残るため、手書きコードとは分けて集計する。Strykerの`NoCoverage`は変異に到達したテストの有無を表し、通常テストの行・分岐カバレッジとは別に確認する。

## Stryker.NET

対応するテストプロジェクトのディレクトリから順に実行し、`--project`には本体のプロジェクトファイル名を渡す。ベンチマークツールは[共通設定](../stryker-config.json)の`mutate`で変異対象から除外し、測定ケースや比較判定は通常CIの契約テストで検証する。

| 作業ディレクトリ | `--project` |
| --- | --- |
| `tests/WasmSharp.Tests` | `WasmSharp.csproj` |
| `tests/WasmSharp.Generators.Tests` | `WasmSharp.Generators.csproj` |
| `tests/WasmSharp.TestSuiteRunner.Tests` | `WasmSharp.TestSuiteRunner.csproj` |

Stryker.NET 5.1.0はRoslyn 5.9を使う。本体によって異なる互換設定が必要になる。

- ランタイム: Strykerは通常ビルドでRoslyn 5.12を参照する命令生成器を読み込めない。`WasmSharpGeneratorRoslynVersion=5.9.0`を設定して、生成器をStrykerと同じRoslynでビルドする。
- 公式適合検証ランナーでは、Roslyn 5.9がC# 15のコレクション式の`with(...)`をプレビュー構文として扱うため、`WasmSharpStrykerLangVersion=preview`を設定する。これにより、プロジェクトがMSBuild経由でStrykerに言語バージョンを伝える。
- 生成器: 本体を分析器として読み込むのではなく単体テストから呼び出すため、互換設定は不要である。

```sh
cd tests/WasmSharp.Tests
WasmSharpGeneratorRoslynVersion=5.9.0 LangVersion=preview \
  dotnet stryker --config-file ../../stryker-config.json --project WasmSharp.csproj --concurrency 12 --skip-version-check --break-on-initial-test-failure --output ../../artifacts/mutation-testing/run-1/WasmSharp --log-to-file
```

PowerShellでは、ランタイムの測定前に`$env:WasmSharpGeneratorRoslynVersion = '5.9.0'`と`$env:LangVersion = 'preview'`、公式適合検証ランナーの測定前に`$env:WasmSharpStrykerLangVersion = 'preview'`を設定する。通常ビルドやテストはこれらを設定していないセッションで実行する。Stryker同梱のRoslynが.NET 11の構文と生成器を読み込めるようになったら、環境変数を外して再測定する。

同じホストで比較前後の並列数と設定を揃え、出力先を分ける。生成される`reports/mutation-report.json`で`Survived`と`NoCoverage`を確認し、`Timeout`や`CompileError`と区別する。終了値が0であっても、JSONが生成されなければ測定は完了していない。

- MTPの`perTest`で子プロセスを起動するテストが`marking as Dubious`になった場合、Stryker.NET 5.1.0はそのテストを各変異の候補に加える。その場合の`NoCoverage`0件は、全変異にテストが到達したことを示さない。該当箇所は通常カバレッジと併せて確認する。
