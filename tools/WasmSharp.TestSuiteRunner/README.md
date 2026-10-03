# Test Suite Runner

固定したWebAssembly Core 2.0公式スイートを生成・実行し、保存済み結果との回帰比較を行うCLIです。WasmSharpの公開APIで全commandを処理し、結果と診断をJSONに保存します。

## 準備と起動

.NET 10を使用します。素材生成にはGitと固定版のspec・WABT・`wast2json`が必要です。[外部ソースの取得・ビルド手順](../../thirdParties/README.md)に従って用意してください。公式入力は改行変換を無効にして取得します。生成済み素材の実行には元WAST・WABTは不要です。

リポジトリルートでReleaseビルドを行い、生成されたapphostを起動します。以下はPowerShellでの例です。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
$runner = './tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe'
& $runner --help
```

Linuxでは実行ファイル名を`WasmSharp.TestSuiteRunner`に読み替え、`--wast2json`にもLinux用にビルドした変換器のpathを指定します。CLIの例は`artifacts/wabt-core2/wast2json`ですが、実際のビルド出力先に合わせてください。リンク先の変換器ビルド手順はWindows向けです。各操作の説明は`<操作> --help`でも表示できます。相対pathは起動時の作業ディレクトリが基準です。

## 操作

| 操作 | 必須引数 | 処理 |
| --- | --- | --- |
| `generate` | `--spec-root`、`--wabt-root`、`--wast2json`、`--output` | 固定147入力を変換・照合し、出力ディレクトリに`manifest.json`と素材を保存する。 |
| `run` | `--manifest`、`--output` | manifestと相対配置の素材から全commandを実行し、結果JSONを保存する。 |
| `baseline-save` | `--input`、`--output` | 完了したmanifestまたは実行結果JSONを同じ内容でコピーする。既存baselineの上書きもこの操作で行う。 |
| `compare-conversion` | `--baseline`、`--current`、`--output` | 保存した2つのmanifestの入力・生成物・変換条件を比較する。 |
| `compare-run` | `--baseline`、`--current`、`--output` | 保存した2つの実行結果をケース単位で比較し、以前passedだったケースの変化・欠落を回帰として記録する。 |
| `verify` | `--input` | 保存した1つの実行結果から、固定スイート全体の最終判定と理由を表示する。 |

保存・比較・最終判定は再生成や再実行を行いません。比較はbaselineを変更しません。全件の処理と記録が完了した結果はfailed等を含んでいてもbaselineに保存できますが、未処理・件数未確定・出力失敗が残る結果は保存できません。

生成先は未作成または空の専用ディレクトリにします。固定ソース配下とその祖先は指定できません。JSON出力は入力自身を指定できず、`baseline-save`以外では既存ファイルも上書きできません。繰り返し実行するときは新しい出力先を指定してください。

## 初回の生成・実行とbaseline保存

次の各行を個別に実行し、終了値と結果を確認します。`$runner`は上記の起動例で設定した実行ファイルです。

```powershell
& $runner generate --spec-root thirdParties/WebAssembly-spec --wabt-root thirdParties/wabt --wast2json artifacts/wabt-core2/Release/wast2json.exe --output artifacts/corpus
& $runner run --manifest artifacts/corpus/manifest.json --output artifacts/run-1.json
& $runner baseline-save --input artifacts/corpus/manifest.json --output artifacts/conversion-baseline.json
& $runner baseline-save --input artifacts/run-1.json --output artifacts/run-baseline.json
```

初回受入では全commandの記録に加え、spectest・register・共有リソースの必須公式ケースを確認します。具体的なケースは[設計の初回公式受入](../../.kiro/specs/test-suite-runner/design.md#初回の実cliによる公式受入)を参照してください。ランナーの初回受入では、後続仕様で対応する未実装機能や判定済みの不一致を記録に残します。Core 2.0全件合格は別途`verify`で判定します。

## 機能追加後の実行・比較・更新

```powershell
& $runner run --manifest artifacts/corpus/manifest.json --output artifacts/run-2.json
& $runner compare-run --baseline artifacts/run-baseline.json --current artifacts/run-2.json --output artifacts/run-diff.json
```

比較結果で分類・期待値・実際の値・診断・回帰を確認します。修正が必要なら、新しい出力先で実行と比較を繰り返します。問題を解消して結果を確認した後、保存済みJSONからbaselineを明示的に更新します。

```powershell
& $runner baseline-save --input artifacts/run-2.json --output artifacts/run-baseline.json
```

素材を再生成したときも、比較してから変換baselineを更新します。

```powershell
& $runner generate --spec-root thirdParties/WebAssembly-spec --wabt-root thirdParties/wabt --wast2json artifacts/wabt-core2/Release/wast2json.exe --output artifacts/corpus-2
& $runner compare-conversion --baseline artifacts/conversion-baseline.json --current artifacts/corpus-2/manifest.json --output artifacts/conversion-diff.json
& $runner baseline-save --input artifacts/corpus-2/manifest.json --output artifacts/conversion-baseline.json
```

manifestと生成物の相対配置を保てば、素材ディレクトリを移動しても`run`できます。実行結果JSONは素材の識別情報を含むため、baselineの保存・比較・`verify`には元のmanifestや素材を必要としません。

## 結果と終了値

各commandは`passed`（成立）、`failed`（期待との不一致）、`runtime_unsupported`（ランタイム未対応）、`runner_error`（素材・ランナー・公開契約外例外などの異常）、`out_of_scope`（text形式の実行対象外）、`blocked`（必要な先行commandの不成立）のいずれかになります。入力単位の異常はcommand件数と分けて記録します。詳細JSONには元入力・command順序・行・期待値・実際の値・診断・原因参照を保存し、spectestのprintも対応するcommandに記録します。

| 終了値 | 意味 |
| --- | --- |
| `0` | 操作ごとの成立条件を満たした。baseline保存の成功はスイート合格を意味しない。 |
| `1` | 結果を報告できたが、その用途の条件を満たさない。 |
| `2` | 引数不正、読取・保存失敗、比較未成立・未完了などで操作を完了できない。 |

`run`は記録が完了し、failedと入力・commandのrunner_errorが0件なら終了0です。未対応と対象外、未対応を原因とするblockedだけでは終了1になりません。`compare-run`は回帰0でも現結果にfailed・runner_errorがあれば終了1です。診断不一致は例外の型や段階が合っていてもfailedとして残します。

全仕様を統合した最終確認では、同じrevisionで固定スイート全体を1回実行した結果を使います。

```powershell
& $runner verify --input artifacts/run-2.json
```

`verify`は全対象の記録・出力が完了し、入力異常がなく、out_of_scope以外の全commandがpassedの場合だけ終了0になります。複数回の部分的な成功を合算する操作はありません。
