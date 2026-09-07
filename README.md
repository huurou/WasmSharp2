# WasmSharp2

## 開発環境のセットアップ

クローン後、リポジトリのルートで次のコマンドを実行してください。

```sh
git submodule update --init thirdParties/WebAssembly-spec
dotnet tool restore
dotnet husky install
```

ランタイムのテストは、固定したCore 2.0仕様（`thirdParties/WebAssembly-spec`、commit `05ca4182176763112561ae20153975c12bd689e4`）の命令付録2ファイルをビルド時に埋め込みます。submoduleの取得はテストプロジェクトのビルドに必要です。WABTの取得・ビルドは基盤テストには不要です。

Husky.NetとCSharpierは、`.config/dotnet-tools.json`でバージョンを固定した.NETローカルツールです。
コミット時に、ステージ済みのC#ファイル（`.cs`、`.csx`）と.NET設定ファイル（`.csproj`、`.slnx`、`.props`、`.targets`、`.xml`、`.config`）をCSharpierで自動整形します。

フックを手動で実行する場合は、次のコマンドを使用してください。

```sh
git hook run pre-commit
```

## ビルドとテスト

```sh
dotnet build WasmSharp2.slnx -c Release
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build
dotnet csharpier check .
```

生成器テストは、`src/WasmSharp/Instructions`の命令宣言用5ファイルと`src/WasmSharp/Execution`の`ExecutionResult.cs`・`Instruction.cs`をソースとして埋め込み、テスト内のRoslynコンパイルに使用します。対象ファイルを移動・改名する場合は、`tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj`の`EmbeddedResource`も更新してください。

## GitHub Actions

[Unit tests](.github/workflows/unit-tests.yml)は、push・pull request・手動実行で起動します。Ubuntu上で.NET 10と固定したCore 2.0仕様を取得し、警告をエラーとして扱うReleaseビルド後に、ランタイムと生成器の両テストを実行します。

テスト結果のTRXファイルは、各実行のArtifactsに`unit-test-results`として保存します。片方のテストが失敗した場合も、もう片方の実行と結果の保存を行います。
