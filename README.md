# WasmSharp2

## 実装状況

WebAssembly Core 2.0のバイナリのデコード・検証・インスタンス化・実行を実装済みです。固定公式スイート147ファイルでは、対象の52,830コマンドがすべて成立し、テキスト形式の1,077コマンドは対象外です。詳細は[Core 2.0の受入記録](https://github.com/huurou/WasmSharp2/blob/f4c91f7a47e727aac1fd05e5f040e34cb5076fdd/.kiro/specs/simd/acceptance.md#102-単一最終runによる公式比較と全体判定)を参照してください。

`WasmModule.Decode`は、バイト列とストリームのどちらでも、版を省略するとCore 3.0を使います。Core 2.0の規則で読むには、第2引数に`WasmSpecVersion.Core2`を指定します。検証にはデコード時と同じ版を使います。

Core 3.0の公式ランナーでは実装済みの命令で動くケースを実行できます。1つのモジュールで複数の32ビットメモリをimport・定義し、個別に初期化・exportできます。例外タグを定義するタグセクションはデコード時に未対応として報告します。Core 3.0固有の機能には未実装のものがあり、固定公式スイート全体はまだ合格していません。

両版の公式テストは、版ごとの仕様サブモジュールから取得し、同じ`wasm-tools 1.263.0`で変換します。[固定入力と変換器の取得手順](thirdParties/README.md)・[ランナーの操作ガイド](tools/WasmSharp.TestSuiteRunner/README.md)

## 開発環境のセットアップ

.NET 11 SDKをインストールし、クローンしたリポジトリのルートで次のコマンドを実行してください。

```sh
git -c core.autocrlf=false submodule update --init --checkout
dotnet tool restore
dotnet husky install
```

ランタイムの単体テストは、Core 2.0とCore 3.0それぞれの仕様サブモジュールから命令付録とマクロ定義をビルド時に埋め込み、各版の命令表に登録された番号と名前を照合します。`wasm-tools`は公式テストやベンチマーク素材を再生成するときに使用します。

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

両版の公式テスト素材の生成、全コマンドの実行、基準結果の保存・比較に対応しています。Core 2.0は最終合否も確認できます。[Test Suite Runnerの操作ガイド](tools/WasmSharp.TestSuiteRunner/README.md)

## GitHub Actions

[Unit tests](.github/workflows/unit-tests.yml)は、`main`向けのプルリクエストの作成・再オープン時と、マージ元ブランチの更新時に自動で起動します。Actions画面から手動でも実行できます。UbuntuとWindowsの両方で.NET 11と固定したCore 2.0・Core 3.0の仕様を取得し、警告をエラーとして扱うReleaseビルド後に、ランタイム、生成器、公式適合検証ランナー、測定ツールの全4テストプロジェクトを実行します。

Windowsでは、変換器と情報採取用コマンドの起動時に、新しいコンソールが開かれず、呼び出し元のコンソールも共有されないことを確認します。測定ツールの契約テストは小さい保存データfixtureを使います。ビルド後にいずれかのテストが失敗しても、残りのプロジェクトを実行してTRXを保存し、該当するOSのジョブは失敗として報告します。TRXはUbuntuでは`unit-test-results-Linux`、Windowsでは`unit-test-results-Windows`からダウンロードできます。
