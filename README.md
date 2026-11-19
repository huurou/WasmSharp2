# WasmSharp2

## 実装状況

WebAssembly Core 2.0のバイナリのデコード・検証・インスタンス化・実行を実装済みです。固定公式スイート147ファイルでは、対象の52,830コマンドがすべて成立し、テキスト形式の1,077コマンドは対象外です。詳細は[Core 2.0の受入記録](https://github.com/huurou/WasmSharp2/blob/f4c91f7a47e727aac1fd05e5f040e34cb5076fdd/.kiro/specs/simd/acceptance.md#102-単一最終runによる公式比較と全体判定)を参照してください。

Core 3.0は、`WasmModule.Decode(bytes, WasmSpecVersion.Core3)`で選択でき、検証にも同じ版を使います。公式ランナーでは実装済みの命令で動くケースを実行できます。例外タグを定義するタグセクションはデコード時に、複数メモリは検証時に未対応として報告します。Core 3.0固有の機能には未実装のものがあり、固定公式スイート全体はまだ合格していません。

[固定入力と変換器の取得手順](thirdParties/README.md#core-30の公式テスト素材)、[素材の生成と比較](tools/WasmSharp.TestSuiteRunner/README.md#core-30の素材生成と比較)、[Core 3.0の実行手順](tools/WasmSharp.TestSuiteRunner/README.md#core-30の実行)を参照してください。

## 開発環境のセットアップ

.NET 11 SDKをインストールし、クローンしたリポジトリのルートで次のコマンドを実行してください。

```sh
git -c core.autocrlf=false submodule update --init --checkout -- thirdParties/WebAssembly-spec
dotnet tool restore
dotnet husky install
```

ランタイムのテストは、固定したCore 2.0仕様（`thirdParties/WebAssembly-spec`、commit `05ca4182176763112561ae20153975c12bd689e4`）の命令付録2ファイルをビルド時に埋め込みます。submoduleの取得はテストプロジェクトのビルドに必要です。WABTの取得・ビルドは基盤テストには不要です。

Husky.Net、CSharpier、Stryker.NETは、`.config/dotnet-tools.json`でバージョンを固定した.NETローカルツールです。ツールが対象とするランタイムが未導入なら、新しいランタイムで起動できます。

コミット時には、`src/`、`tests/`、`tools/`配下のステージ済みC#ファイル（`.cs`）を、ルートの`.editorconfig`に従って次の順で処理します。整形結果はHusky.Netがステージに反映します。

1. SDKの`dotnet format style`と`dotnet format analyzers`で、提案レベル以上の指摘に自動修正を適用します。
2. CSharpierでインデントや改行を整えます。
3. スタイルとアナライザーを`--verify-no-changes`で再実行し、追加の修正が必要ならコミットを中断します。

フックを手動で実行する場合は、次のコマンドを使用してください。

```sh
git hook run pre-commit
```

全C#ファイルの整形結果を確認する場合は、リポジトリのルートで次のコマンドを実行してください。WindowsではGit Bashで実行できます。

```sh
dotnet format style WasmSharp2.slnx --severity info --verify-no-changes
dotnet format analyzers WasmSharp2.slnx --severity info --verify-no-changes
git ls-files -z -- 'src/*.cs' 'tests/*.cs' 'tools/*.cs' | xargs -0 -n 64 dotnet csharpier check
```

## ビルドとテスト

```sh
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build
dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build
```

生成器テストは、命令宣言、値表現、実行コード、デコード結果に関するランタイムのソースを埋め込み、テスト内のRoslynコンパイルに使用します。埋め込み対象は[生成器テストのプロジェクト設定](tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj)の`EmbeddedResource`で管理します。対象ファイルを移動・改名する場合は、この設定も更新してください。

Stryker.NETによるテストの検出力の測定は[ミューテーションテストの手順](docs/mutation-testing.md)を参照してください。

## 公式スイートの検証

[Test Suite Runnerの操作ガイド](tools/WasmSharp.TestSuiteRunner/README.md)を参照してください。固定Core 2.0素材の生成、全commandの実行、baselineの保存・比較、最終判定を個別のコマンドで行えます。

## GitHub Actions

[Unit tests](.github/workflows/unit-tests.yml)は、push・pull request・手動実行で起動します。Ubuntu上で.NET 11と固定したCore 2.0仕様を取得し、警告をエラーとして扱うReleaseビルド後に、ランタイム・生成器・公式適合検証ランナー・測定ツールの4つのテストプロジェクトを実行します。測定ツールの契約テストは小さい保存データfixtureを使い、BDNの起動、性能の合否判定、Clang/WABTによる素材生成は行いません。

テスト結果のTRXファイルは、各実行のArtifactsに`unit-test-results`として保存します。いずれかのテストが失敗した場合も、残りの実行と結果の保存を行います。
