# WasmSharp2

## 開発環境のセットアップ

クローン後、リポジトリのルートで次のコマンドを実行してください。

```sh
git submodule update --init thirdParties/WebAssembly-spec
dotnet tool restore
dotnet husky install
```

ランタイムのテストは、固定したCore 2.0仕様（`thirdParties/WebAssembly-spec`、commit `05ca4182176763112561ae20153975c12bd689e4`）の命令付録2ファイルをビルド時に埋め込みます。submoduleの取得はテストプロジェクトのビルドに必要です。WABTの取得・ビルドは基盤テストには不要です。

Husky.Netは、`.config/dotnet-tools.json`でバージョンを固定した.NETローカルツールです。整形には.NET 10 SDKに含まれる`dotnet format`を使用するため、フォーマッターの追加インストールは不要です。
コミット時に、`src/`、`tests/`、`tools/`配下のステージ済みC#ファイル（`.cs`）を、`WasmSharp2.slnx`とルートの`.editorconfig`に従って自動整形します。`--severity info`で提案レベル以上のコードスタイルも適用し、整形結果はHusky.Netが自動でステージへ反映します。`.csproj`や`.slnx`などのXMLファイルは整形対象に含みません。

`.editorconfig`はSyncGrid_2.0からコピーしています。通常の改行はCRLF、シェルスクリプトと`.husky/pre-commit`はLFです。`.husky/pre-commit`を編集する際もLFを維持してください。

フックを手動で実行する場合は、次のコマンドを使用してください。

```sh
git hook run pre-commit
```

## ビルドとテスト

```sh
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build
dotnet format WasmSharp2.slnx --severity info --verify-no-changes
```

生成器テストは、命令宣言、値表現、実行コード、デコード結果に関するランタイムのソースを埋め込み、テスト内のRoslynコンパイルに使用します。埋め込み対象は[生成器テストのプロジェクト設定](tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj)の`EmbeddedResource`で管理します。対象ファイルを移動・改名する場合は、この設定も更新してください。

Stryker.NETによるテストの検出力の測定は[ミューテーションテストの手順](docs/mutation-testing.md)を参照してください。

## 公式スイートの検証

[Test Suite Runnerの操作ガイド](tools/WasmSharp.TestSuiteRunner/README.md)を参照してください。固定Core 2.0素材の生成、全commandの実行、baselineの保存・比較、最終判定を個別のコマンドで行えます。

## GitHub Actions

[Unit tests](.github/workflows/unit-tests.yml)は、push・pull request・手動実行で起動します。Ubuntu上で.NET 10と固定したCore 2.0仕様を取得し、警告をエラーとして扱うReleaseビルド後に、ランタイム・生成器・公式適合検証ランナー・測定ツールの4つのテストプロジェクトを実行します。測定ツールの契約テストは小さい保存データfixtureを使い、BDNの起動、性能の合否判定、Clang/WABTによる素材生成は行いません。

テスト結果のTRXファイルは、各実行のArtifactsに`unit-test-results`として保存します。いずれかのテストが失敗した場合も、残りの実行と結果の保存を行います。
