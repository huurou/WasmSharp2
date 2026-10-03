# test-suite-runnerからの診断結果の引継ぎ

## 引継ぎ時点

2026-10-01に`test-suite-runner`のタスク14.1〜14.8で行った初回公式受入の結果を引き継ぐ。全147入力・53,907commandの記録と必須66ケースの成立を確認し、生成・配置変更・再実行・baseline操作も独立レビューで承認された。ランナーと素材に起因する異常、必須経路を妨げる問題はなく、受入のための追加修正は発生しなかった。

後続で扱う確認済みの不一致は944件で、いずれも公開診断の前方一致が成立していない。本書は[ブリーフ](brief.md)の具体的な入力資料であり、requirements・design・tasks・実装の承認を行うものではない。`test-suite-runner`全体の`kiro-validate-impl`は、このタスク14の受入とは別で未実行である。Core 2.0全件合格も未達である。

| 条件 | 記録 |
| --- | --- |
| ランタイム・ランナーのrevision | `1bda5b9368f04541676213ea90a9418e1f241751` |
| spec | `05ca4182176763112561ae20153975c12bd689e4` |
| WABT | `03a00a1334e6121fb0cce4fccbd6bb109b68acaa`（1.0.41） |
| 変換器SHA-256 | `b0e1d0316a265f659566a0e3b6f1a00fea9e315e3d25f2c3e0853e414d0be245` |
| 実行環境・上限 | Windows x64、.NET 10.0.401、MaxCallDepth=1024 |
| 引継ぎに用いたrun ID | `c8ebac70-d9fe-4f68-808b-a6791e63171e`（移動後の全体再実行） |
| 実行結果・現baselineのSHA-256 | `032cc64b6c088e920331b420490617da6094454ab08a52381ab7c35b1be3c057` |

## 全体結果と成立済みの経路

| 分類 | 件数 | 扱い |
| --- | ---: | --- |
| passed | 1,547 | 既存合格として回帰・欠落を防ぐ |
| failed | 944 | 本仕様で調査・修正する診断不一致 |
| runtime_unsupported | 2,987 | 機能・段階・未確認範囲を記録済み。後続機能の新規実装は各仕様が所有する |
| runner_error | 0 | 入力単位・command単位とも0。今回引き継ぐランタイム由来runner_errorもない |
| out_of_scope | 1,077 | `module_type=text`。合格には含めない |
| blocked | 47,352 | 直接原因と起点を記録済み。全起点が同じ入力内の先行runtime_unsupported |
| 合計 | 53,907 | 欠落・重複・未処理・件数未確定0 |

必須66ケースはすべてpassedで、次の経路を公式ケースから確認した。詳細の位置は[ランナー設計の公式受入表](../test-suite-runner/design.md#初回の実cliによる公式受入)を参照する。

- `imports.wast#6/#7`と`start.wast#15〜#17`: invoke/startからprintへ到達。print_i32の引数13・1・2、引数なしのprint、結果0個を記録。
- `linking.wast#0〜#6`: 関数のexport/register/importと元の定義関数の呼び出しが成立。
- `linking.wast#11〜#28`: 共有mutable globalの142から241への更新を両instanceのget/invokeで観測。
- `imports.wast#41〜#45`: spectestの4数値globalをimportし、i32の666を読み出し。
- `imports.wast#0/#1/#82〜#101/#127〜#135`: table・memoryのexport/register/importとspectestへの接続が成立。

通常検証はReleaseビルドの警告・エラー0、TUnitの975件・37件・701件成功、CSharpier成功だった。これは上記revisionのWindowsでの記録であり、後続の修正後は改めて検証する。

## 診断不一致944件

[diagnostic-groups.json](diagnostic-groups.json)に全944件を保存した。元入力path、0始まりcommand index、WAST行番号、期待診断、実際の公開Message、操作・段階、例外型、Reason、Location、import情報を保持している。102組は同じ観測診断をまとめたもので、102個の根本原因や実装タスクを確定したものではない。このJSONは引継ぎ資料であり、ランナーへ渡すbaselineではない。

| command | 件数 | グループ | 観測 |
| --- | ---: | --- | --- |
| assert_malformed | 683 | D024〜D047（24組） | DecodeでWasmDecodeException。期待診断との前方一致が不成立 |
| assert_invalid | 183 | D001〜D023（23組） | ValidateでWasmValidateException。期待診断との前方一致が不成立 |
| assert_unlinkable | 77 | D049〜D102（54組） | InstantiateでWasmInstantiateException。MissingImport9件・TypeMismatch46件・KindMismatch22件。期待診断との前方一致が不成立 |
| assert_uninstantiable | 1 | D048 | InstantiateでWasmTrapException、Reason=Unreachable。期待診断との前方一致が不成立 |

全件で期待する処理段階・公開例外型を観測し、リンク不成立では許容するReasonも一致している。初回結果に値・結果個数・例外型・段階の不一致として分類されたfailedはない。ただし、診断文が一致しないため、参照実装と同じ根本原因を選んだとまでは確認できていない。文言の置換だけで全件解消できると仮定せず、固定版の入力と参照実装に照らして検査順序も調べる。

### 調査を始める単位

以下の件数は重複しない。担当箇所は既存コードから調べる入口であり、修正方法・タスク分割は後続の設計で決める。

| 観測された内容 | 件数 | 期待診断と実際の診断の例 | ケース例 | 調査する既存責務 |
| --- | ---: | --- | --- | --- |
| 名前のUTF-8不正 | 528 | `malformed UTF-8 encoding`に対し`名前のUTF-8が不正です。` | `utf8-custom-section-id.wast#0` | [ModuleBinaryReader.ReadName](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs) |
| LEB128の幅・未使用ビット | 76 | `integer representation too long`または`integer too large`に対し`整数の最大幅または未使用ビットが不正です。` | `binary-leb128.wast#25` | [ModuleBinaryReader.ReadInteger](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs)。現在は継続ビットとpayloadの異常を同じ文言へまとめている |
| magic・version | 23 | `magic header not detected`・`unknown binary version`・`unexpected end`に対し`magicまたはバイナリversionが不正です。` | `binary.wast#5`など | [ModuleBinaryFormat.ReadHeader](../../../src/WasmSharp/Modules/ModuleBinaryFormat.cs)。magicとversionの検査、途中で終わる入力の診断選択 |
| その他のDecode診断 | 56 | 長さ、limitsの符号化、section/import kind、function/code件数、式の終端など | `binary-leb128.wast#36`、`binary.wast#76` | [ModuleBinaryReader](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs)、[ModuleBinaryFormat](../../../src/WasmSharp/Modules/ModuleBinaryFormat.cs)、[ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs) |
| 型・添字・構造の検証 | 183 | `type mismatch`、`unknown local`、`duplicate export name`などに対し日本語の公開診断 | `call.wast#75`、`local_get.wast#30`、`exports.wast#24` | [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs)。入力不足37件・入力型35件・関数結果13件、export名19件、local19件など |
| importの接続 | 77 | `unknown import`9件、`incompatible import type`68件に対し現在の要求・提供内容の説明 | `imports.wast#17/#19` | [ModuleInstantiator.Link](../../../src/WasmSharp/Modules/ModuleInstantiator.cs)。名前・種類・型の不一致とReasonを維持 |
| startのunreachable | 1 | `unreachable`に対し`Wasmの実行中にtrapが発生しました。` | `start.wast#18` | [ExecutionBoundary.ThrowIfFailed](../../../src/WasmSharp/Execution/ExecutionBoundary.cs)。Reasonからの公開診断とInstantiate段階の保持 |

特にDecodeでは、同じ現在のMessageが複数の公式期待診断へ対応している。式の終端だけでも`END opcode expected`、`unexpected end of section or function`、`section size mismatch`に分かれる。固定した[参照decoder](../../../thirdParties/WebAssembly-spec/interpreter/binary/decode.ml)と[binary.wast](../../../thirdParties/WebAssembly-spec/test/core/binary.wast)を使い、入力位置・残量・検査の順番を確認する。

`assert_trap`と`assert_exhaustion`に今回のfailedがないことは、当該経路の診断互換性が完成した証拠にはならない。未対応機能が前提となるケースもあるため、後続機能で実行可能になった時点で再確認する。

## 受入資料と再開方法

大きな全体結果と生成素材は、リポジトリrootを基準に`artifacts/test-suite-runner-acceptance-20261001/`へ保存している。`artifacts/`はGit管理対象外なので、引継ぎ先の環境に自動で届くとは限らない。

| 相対path（上記ディレクトリ基準） | 用途 |
| --- | --- |
| `portable/corpus/manifest.json`と`portable/corpus/modules/` | 移動後の生成済み素材。以後のrunはこれを使用する |
| `run-initial.json` | タスク14.7で受け入れた初回全体結果 |
| `run-moved.json` | 素材移動後の全体再実行結果。初回と全ケース詳細が一致 |
| `run-baseline.json` | 現在の実行baseline。移動後の結果と同じバイト列 |
| `conversion-baseline.json` | 固定した変換baseline |
| `run-diff.json` | 初回と移動後の53,907件の前後詳細。比較成立・完了、変化・追加・欠落・回帰・未比較0 |
| `conversion-repeat-diff.json`、`conversion-relocated-diff.json` | 同条件と入力/出力root変更の再生成比較。ともに差分0・終了0 |
| `acceptance-audit.json`、`move-audit.json`、`baseline-audit.json` | 必須ケース・診断の分類、移動条件、baseline更新前後のhash・終了値 |

元の`corpus/`は移動済みで存在しない。`portable/runner/`は移動受入で使用した旧revisionのコピーなので、修正後の検証には通常のビルド出力を使う。元WAST/WABTはrunには不要だが、固定参照実装の調査と素材の再生成には必要となる。

ローカル資料が残っていれば、現在のbaselineのSHA-256を上記と照合してから作業を再開する。保存済みの全体結果を別の環境から入手できる場合も、上記のSHA-256で同一ファイルと確認できる。資料が失われた場合でも、このspecの全944件の一覧は残るが、それを全体baselineの代わりには使えない。

保存済みの全体結果を入手できない場合は、次の手順で旧revisionの比較元を再作成する。

1. 別の作業環境にrevision`1bda5b9368f04541676213ea90a9418e1f241751`を用意し、[外部ソースの手順](../../../thirdParties/README.md)で固定ソースと変換器を準備する。
2. 旧revisionのランナーをReleaseビルドし、[ランナーガイドの`generate`](../../../tools/WasmSharp.TestSuiteRunner/README.md)で`manifest.json`を含む素材を生成する。手動変換だけではmanifestを作れない。
3. 同じ旧revisionのランナーで生成済みmanifestを`run`し、全147入力・53,907commandの結果を新しいJSONへ保存する。修正後のランタイムの結果で代用しない。
4. 本書の全体集計、必須66ケースのpassed、入力異常・未処理・件数未確定0と、`diagnostic-groups.json`の全944件のケースID・期待診断・実際の診断・位置を照合し、`baseline-save`で比較元へ保存する。既知failed944件が残るため、ここではrunの終了1とbaseline-saveの終了0を確認する。

再実行した結果は実行ID・日時が変わるため、以前と同じファイルhashにはならない。再作成した条件と照合結果を記録する。元の全体結果が失われている場合、この引継ぎに残していないケース詳細まで以前の結果と一致したことは確認できない。

後続の修正時は[通常のビルド・テスト](../../../README.md#ビルドとテスト)を行った後、リポジトリrootから次の順で操作する。出力名は未使用のものを選ぶ。

```powershell
$runner = './tools/WasmSharp.TestSuiteRunner/bin/Release/net10.0/WasmSharp.TestSuiteRunner.exe'
$acceptance = './artifacts/test-suite-runner-acceptance-20261001'

& $runner run --manifest "$acceptance/portable/corpus/manifest.json" --output "$acceptance/run-conformance-1.json"
& $runner compare-run --baseline "$acceptance/run-baseline.json" --current "$acceptance/run-conformance-1.json" --output "$acceptance/conformance-diff-1.json"
```

分類・ケース詳細・回帰を確認し、問題が残れば修正して新しい出力名で全体実行・比較を繰り返す。問題が解消した結果だけを、別の操作として現baselineへ保存する。

```powershell
& $runner baseline-save --input "$acceptance/run-conformance-1.json" --output "$acceptance/run-baseline.json"
& $runner verify --input "$acceptance/run-conformance-1.json"
```

初回受入では既知failed944件を保持したbaseline保存を確認したため、run・compare-run・verifyは1、baseline-saveは0だった。後続の修正を終えた時点ではrun・compare-runの終了0を要求する。未対応とそのblockedが残る間はverifyの終了1を許容し、全8仕様統合後の全件合格とは区別する。

## 後続で守る判定と完了条件

- 全147入力・53,907commandを同じ固定profileで実行する。公式入力・期待値・featureを減らさず、`module_type=text`だけを従来どおり対象外とする。
- `Message.StartsWith(expectedText, StringComparison.Ordinal)`と例外型・段階・Reasonの条件を維持する。公開Messageは公式診断から始め、補助説明はその後ろへ置く。ランナーによる文言の付け替え、期待値の正規化、ケース別除外は行わない。
- ランタイムは入力と状態から診断を生成する。公式JSON・期待文字列・ケースIDやテスト専用hookに依存しない。破損入力の検査、公開例外の分類、Reason・Locationを保つ。
- 全体結果のfailedと入力単位・command単位のrunner_errorを0にし、初回の1,547passedに回帰・欠落を生じさせない。必須66ケースも維持する。変化した944件は単なる件数減少でなくケースごとのpassedとして確認する。
- 後続の新規命令・segmentが必要なruntime_unsupportedと、その記録済み原因に依存するblockedは残せる。現在のfailedを未対応や対象外へ移すことで解消扱いにしない。
- 修正により新たな不一致が観測された場合は、この一覧だけに範囲を固定せず、本仕様へケース・期待・実際・原因を追記する。ランナー自身の不具合は`test-suite-runner`、新規命令・segmentは該当機能仕様へ戻す。

判断の根拠は[ブリーフの責務境界](brief.md)、[参照診断互換性のADR](../../../docs/adr/0012-reference-diagnostic-compatibility.md)、[ランナーの要件14](../test-suite-runner/requirements.md#要件14-初回公式受入と後続機能への継続利用)による。
