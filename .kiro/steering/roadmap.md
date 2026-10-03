---
updated_at: 2026-10-03
---

# WasmSharp2ロードマップ

## 概要

C#でWebAssemblyバイナリをデコード・検証・インスタンス化・実行するライブラリを一から実装する。初期の完了目標は**WebAssembly Core 2.0のバイナリ・検証・実行への準拠**とし、公式テストのバイナリ対象集合で検証する。WATの処理はライブラリの対象外とする。

下記8仕様を機能ごとに4段階を通して実装する。公式テスト素材の固定・生成から実行・回帰比較までは、`test-suite-runner`で一つの仕様・ツールとして扱い、初期実装から公式期待診断を前方一致で判定する。その後の`test-suite-conformance`で、テストスイートで判明したランタイムの実装上の問題をまとめて修正する。診断互換性はその一部とし、実行結果に応じて修正項目を追記・見直す。Core 3.0は将来の別計画とする。この分割方針の承認と、各仕様のrequirements・design・tasks・実装の承認は区別する。文書の言語は日本語とし、`spec.json.language`は`ja`とする。

`runtime-foundation`と`host-linking`は完了し、関数呼出し・リソース生成・import/exportの実行・リンク基盤まで整備済み `test-suite-runner`も、その公開能力を使うspectest・register、全体実行・回帰比較の実装と初回公式受入・baseline保存、仕様全体の最終実装検証GOまで記録済み 次に、観測した実装上の不一致を`test-suite-conformance`で解消してから各命令・初期化機能へ進む。両基盤の承認済み要件・設計・タスクと完成状態は維持する。後続機能では同じ公式スイートによる診断照合と回帰確認も完了条件に含める。

## discovery時点の現状（2026-09-06）

- `src/WasmSharp`は.NET 10の公開型とメソッドシグネチャの骨組み `Decode`・`Validate`・`Instantiate`・`Invoke`は未実装
- `tests/WasmSharp.Tests`は.NET 10・TUnit 1.66.10のプロジェクト定義のみで、テスト本体はない。`tools`は空
- `thirdParties`には公式specとWABTのソースがある。specは3.0系で、初期Core 2.0のテスト集合としてそのまま扱えない。
- 同梱WABTの宣言版は1.0.41だが、公式の同名タグとfeature既定値が異なる。上流commitは未特定であり、版名だけでは固定済みとは言えない。
- 既存の正式specとsteeringはない。既存コメントと公開型の意図を確認し、メソッドシグネチャの骨組みを完成済み契約とは扱わない。

## 進め方の選択

- **採用**: 最小基盤に実行・リンク基盤を加えてから、素材生成・spectest・register・実行・回帰比較を一つの公式ツールで整備する。数値・制御、メモリ命令、テーブル・参照命令、SIMDはその後に追加する。規模は大
- **理由**: 公式スイートのimportやmodule間共有を初期から利用し、各機能の実装時に仕様解釈と実行結果を確認できる。リソースの生成・共有とguest命令の意味論を分け、未実装と不具合を同じ固定集合で追跡する。
- **順序の別案**: ホスト連携を全命令・segment初期化の後まで遅らせると、importを使う公式ケースの検証も遅れる。共通の実行・リンク能力を先行し、data/element固有の処理は各機能へ置く。
- **検討した別案**: Core 2.0全体のDecode・Validateを先行し、その後Instantiate・Invokeを完成させる。段階内の作業はまとまるが実行結果による確認が遅くなるため採用しない。こちらも規模は大
- **範囲の別案**: Core 1.0限定ではSIMD等が完了目標に入らず、Core 3.0から開始するとGC等の追加設計とWABTの変換対応不足を同時に扱う必要がある。今回はCore 2.0を選択した。

## 対象範囲

- **対象**: Core 2.0のバイナリ形式、型検証、インスタンス化、実行 スカラー数値、関数、構造化制御、複数値、globals、import/export、start、線形メモリ、data、テーブル、element、間接呼び出し、`funcref/externref`、bulk memory/table、sign-extension、non-trapping conversions、`v128`とSIMD
- **対象**: 明示的なホスト関数・共有リソースの連携、および公開APIだけを利用する公式テスト用の`spectest`とランナー
- **追加対象（2026-09-27）**: 公式期待診断への前方一致と、参照実装特有の診断選択への互換性 ランナーの判定は`test-suite-runner`の初期実装に含め、ランタイムへの対応は、スイートで判明した実装上の問題全般を扱う`test-suite-conformance`に含める。
- **対象外**: ランタイムと自作ツールによるWAT・WASTの解析、WASI、Component Model、JavaScript/Web API、JIT/AOT、既存エンジンへの実行委譲
- **初期対象外**: GC、型付き関数参照、Wasm例外処理、tail-call、memory64、multi-memory、extended-const、relaxed-SIMD、threads等、Core 2.0の外にある機能 3.0のdeterministic profileも初期の追加要件にしない。
- 性能の数値目標、NuGet公開、追加TFM・OSへの対応は今回決めていない。将来のためだけの抽象化や拡張口は設けない。

## 維持する10項目

1. **4段階を分離する**: `Decode → Validate → Instantiate → Invoke`を明示する。段階ごとの例外型で、入力の破損・検証不成立・リンク不成立・実行中のtrapを説明できるようにする。
2. **未実装を検証失敗にしない**: 対象仕様の機能が未実装の場合は`WasmUnsupportedFeatureException`で区別する。仕様違反の判定と実装状況を混同しない。
3. **明示APIのみ**: 値の受け渡しは`WasmValue` `object`・`dynamic`を受ける汎用引数、CLR型からの暗黙変換、4段階を畳むローダー、delegateからの関数型推論を導入しない。ホスト関数型は明示宣言する。
4. **線形バイトコードで実行する**: フラットな配列と単一の`switch`実行ループを使う。分岐は線形化時に`(targetPc, stackHeight, keepCount)`へ落とし、入れ子オブジェクトの走査や外側フレームへの完了値の伝播で実行しない。
5. **検証と線形化を同一パスにする**: 型検査しながら実行コードを生成し、型スタックを分岐情報にも使う。検証済みモデルと実行モデルを別々に二重保持しない。Decodeの入力表現と検証後の実行表現の違いまで禁止するものではない。
6. **内部のtrapに.NET例外を使わない**: 実行ループは列挙型の結果を返し、ホスト境界にある共通の1箇所で`WasmTrapException`へ変換する。Instantiateからのstart実行にも同じ境界処理を適用する。
7. **命令情報の唯一の定義元を持つ**: opcode・名前・即値形状・スタック効果・検証規則・実行ハンドラを1テーブルに集める。SIMDも同じ定義元と実行ループを拡張する。テーブルと`switch`の同期方法は基盤の設計で決め、命令情報を複数の一覧で別々に手動管理しない。
8. **検証ツールは通常の利用者**: `spectest`をツール側で公開APIだけから構成する。ツール専用の裏口を追加せず、通常利用にも意味のある能力だけを公開する。
9. **WASTは自前で解析しない**: `wast2json`がWASTをJSONと個別モジュールへ変換する。自作ツールの実行処理が読み込むのはJSONと`.wasm`だけとし、素材同定のためのWAST/WATのhash計算と区別する。
10. **feature flagは初期の最終範囲に固定する**: Core 2.0内をON、範囲外をOFFとし、実装の進捗で変更しない。`--enable-all`を使わない。未実装機能を無効化して検証対象から隠さない。

## 仕様間で共有する契約

- `WasmModule`は静的定義、`WasmInstance`は実行時の実体という既存の区別を保つ。検証前のInstantiateを防ぐ契約、検証成功後の実行表現の所有権は基盤で決める。
- 基盤はCore 2.0の値・型・添字空間と、後続が使うデコード結果・命令情報・スタック・実行結果の共通契約を持つ。各機能の具体的なデコード、検証、初期化、実行はその機能の仕様が所有する。
- scalarとvector・参照をやり取りできる`WasmValue`の契約を基盤で決める。各命令の意味論は該当機能が持つ。参照の同一性を明示的に表し、汎用`object`引数で代用しない。
- `host-linking`は関数の引数・結果0個/複数・locals・直接call/return・drop・unreachable、return・unreachable後の到達不能部分を含む関数本体の型検証、globalの生成・スカラー初期化・get/set、memory/tableの型・limits・生成・公開取得、4種のimport/exportと同一性を所有する。unreachableによる実際のWasm trapをInvokeとstartの公開経路で確認する。memory/tableのguest命令とdata/element初期化は各機能仕様が同じ実体へ追加し、リソース表現を複製しない。
- `host-linking`は通常利用に必要なimportの識別情報を公開する。依存の把握をinstance生成や無関係な未実装命令のDecode成功へ依存させず、完全に取得した一覧だけを返し、空一覧によるimportなしと取得失敗を区別する。失敗時の部分一覧は公開しない。ランナーはこの能力で失敗したregisterへの依存を特定し、独自のバイナリ解析やWAST解析で補わない。
- startの型検証・実行と共通のInstantiate順序は`host-linking`が所有する。start前に構築・接続・リソース初期化を完了し、start失敗時はInstantiateを例外で終了するが、副作用を戻さず保存済みのinstance・関数・リソースも無効化しない。startによる初期化完了は保証せず、利用継続はホストが判断する。data/element初期化と、共有リソースに対する初期化・startの複合挙動は各segmentの所有仕様が追加・検証する。未対応のsegmentを無視してstartを実行しない。
- `WasmInstance`の既存コメント「Exportsは持たせない」を守る。実体は名前による公開取得操作で扱い、export名と種類の列挙はmodule側の`WasmModule.GetExports()`だけで認める。公開取得操作と共有リソースの扱いは通常の埋め込み利用を根拠に設計する。`GetGlobal`の値取得だけでmutable globalの共有を表現できると決めつけない。
- ホスト関数は取得元instanceへ固定せず、C#からは呼び出し時にinstanceを明示可能とし、Wasmからは呼び出し元instanceを与える。登録時にInstance引数を取る形式と取らない形式を分け、前者の省略・nullはcallback実行前に拒否する。Instance引数はWasmの関数型・値引数に含めず、Instantiateの成功も証明しない。
- 関数呼出しのフレーム・引数と結果の受渡しは`host-linking`で基盤を拡張する。`numeric-control`の分岐・loopも同じスタック基準を使う。既存コンテキストのない単独ホスト呼び出しはinstanceの指定・省略やリソース操作だけでは開始せず、Wasmへ入る時点で、その入口のinstanceの上限を使って開始する。開始したWasm実行が終了してホストへ戻った後の別のWasm呼び出しは、新しいコンテキストを使う。直接call・start・既存Wasm実行からのホスト再入はcallbackの形式と無関係に同じコンテキストと深さ制限を使う。guest間の再帰をCLRの再帰呼び出しへ依存させない。
- Instantiate中のstartやsegment初期化のtrapをリンク不成立へ変換しない。呼び出し契約違反、ホスト処理の例外、実装制限・資源枯渇も、Wasmの仕様trapと無差別に混同しない。具体的な型・reasonの割り当てはrequirements/designで確定する。
- 未実装に遭遇して検証を終えられない場合、`runtime_unsupported`は「有効性を証明済み」を意味しない。判定できなかった範囲も記録し、不正なバイナリを一括して未実装へ分類しない。Core 2.0外の命令についても、選定仕様の否定テストの期待値を勝手に変更しない。

## 公式検証の方針と完了条件

公式specのCore 2.0固定版から対象ファイルを選ぶ。採用版は[外部ソースの固定](../../thirdParties/README.md)に従い、`v2.0.0`、commit `05ca4182176763112561ae20153975c12bd689e4`の`test/core`で、SIMD配下も含む。`test-suite-runner`で全入力の変換結果、manifestとhash、同じ条件での再現性を確認する。3.0系テストやproposal集合との混在を避ける。

仕様版、suite取得元・commit・対象path、WABTの取得元とcommit、ビルド条件を代表する実行ファイルhash、全featureの実効ON/OFF、CLI引数、入力・生成物hashを記録する。CLIの有効化・無効化オプションはWABTの版と既定値に依存するため、実測前のコマンドを確定値にしない。

`wast2json`はtext構文エラーのテスト等に`.wat`も生成する。ランナーの実行処理はJSONの`module_type=text`を対象外として記録し、そのファイルを開かない。これらを合格件数に含めない。バイナリのmalformed/invalid、unlinkable、Instantiate中のtrap、Invoke中のtrap、exhaustion、戻り値の型・個数・ビット列・NaN pattern（v128はlaneごと）・参照のnullと同一性はそれぞれの意味で判定する。

対象の否定assertionは初期ランナーから、期待する段階・例外型・失敗分類に加えて`actualException.Message.StartsWith(expectedText, StringComparison.Ordinal)`を必須とする。JSONの期待診断と実際のMessageを加工せず、大文字小文字・空白・数値を保持する。別名への置換、Reasonによる代替、ケース別除外、照合を無効にする合格モードを設けない。診断不一致は`failed`として記録する。

- `passed`: 該当assertionが期待どおり成立、または必要なセットアップ・単独actionが正常に完了
- `failed`: 判定した結果が期待と不一致
- `runtime_unsupported`: ランタイムの未実装を観測
- `runner_error`: 素材・JSON・ランナー自身の異常、実行環境の資源限界、またはランタイムの公開契約にない例外で、期待値の判定が成立しない。
- `out_of_scope`: テキスト形式など明示された対象外
- `blocked`: 前提が成立せず、依存するcommandを実行できない。

前のmodule/register失敗で実行できない後続commandは`blocked`とし、依存先の失敗を理由付きで記録して合格扱いにしない。集計ではセットアップ・単独action・assertionの件数を区別する。

生成済み素材の実行は保存したmanifestと生成物だけで行い、元WASTやWABTを必要としない。baselineは保存済みの結果JSONと同じ形式とし、git管理外の利用者の環境に保存する。共有・共同レビューを必須にしない。実行結果の比較では、検証プロファイルと入力・生成物の一覧・hashが一致すれば、変換器の実行ファイルhashが異なっても同じ固定スイートとして比較し、変換器の差異は出典情報として残す。

baselineの保存と比較は別々のコマンドにし、保存済みの結果JSONを入力として再生成・再実行せずに処理する。比較コマンドはbaselineを変更せず、保存コマンドは全対象の処理を完了した結果JSONだけを受け付ける。各機能の実装後は全体を実行して現baselineと比較し、問題があれば修正・再実行・比較を繰り返す。問題が解消した結果JSONを保存コマンドで現baselineへ上書きし、旧baselineの別途保存は必須にしない。

素材生成では、入力と出力先の配置ディレクトリだけが変わっても生成物の内容とhashを維持する。変換baseline比較は、全対象の比較と結果の記録・出力が完了し、入力・生成物の一覧とhash、変換結果、生成条件が一致して`runner_error`が0件の場合だけ終了コード0とし、差異や比較未完了を含むそれ以外は非0とする。変換器の実行ファイルhashだけの違いは出典差異として記録し、生成条件の不一致にしない。

`runtime-foundation`と`host-linking`は公開APIの直接テストによる受入まで完了している。この完了は公式スイートの受入とは区別する。`test-suite-runner`の初回公式受入では、全入力の素材生成と再現性、spectestとregisterを使う実行・リンク経路、全commandの記録と初回baselineをまとめて検証する。

2026-10-01の初回公式受入と素材移動後の再実行・回帰比較は[ランナーのタスク記録](../specs/test-suite-runner/tasks.md)に保持する。残る診断不一致、未対応とそれに依存する前提不成立、baselineの所在と再開手順は[後続仕様への引継ぎ](../specs/test-suite-conformance/handoff.md)を参照する。タスク単位の公式受入、仕様全体の最終実装検証、Core 2.0全件合格はそれぞれ区別する。

初回のランナー受入では、固定スイート全体を処理し、ランナー自体と素材に起因する入力単位・command単位の`runner_error`を0件にする。初期必須の公式ケースを実行・判定できない問題はランタイム側も含めて`test-suite-runner`で修正し、独自テストだけで受入を代替しない。判定で得た`failed`と、初期必須の実行経路を妨げず原因をランタイム側と確認できた`runner_error`は、ケース・期待・実際・原因を記録して`test-suite-conformance`へ引き継げる。必要な前提が成立しない後続commandは原因付きの`blocked`として残す。`failed`や`runner_error`が残る実行・回帰比較の終了コードは非0のままとし、ランナーの受入とランタイムの全件合格を区別する。

import・register・ホスト関数・共有リソースを、ランナーや公開APIの不足を理由に未対応へ残さない。固定スイートに現れる全値型の引数構築と結果比較はランナーが初期から扱う。後続のguest命令・segmentを必要とするケースは、ランタイムが報告した未実装機能を記録した`runtime_unsupported`と、それに依存する`blocked`を残せる。Core 2.0全件合格はこの段階では要求しない。

次の`test-suite-conformance`は、テストスイートで判明したランタイムの実装修正を集約し、実行結果に応じて仕様を追記・修正する。診断文と診断選択への対応も含める。完了時には全体を実行・記録し、先行機能で前提が揃うケースを診断照合込みで合格させ、`failed`と`runner_error`を0件にする。後続機能を必要とする`runtime_unsupported`と、それに依存する`blocked`だけを残せる。ランナーとbaselineの判定基準は初回から変えず、以前の`passed`の退行・欠落を認めない。

`numeric-control`以降は、各機能の実装単位ごとに固定スイート全体をコマンドで実行する。実装中の絞り込み実行は可能だが、機能の完了確認では全体の実行結果と直前のbaselineとの差分を残す。TUnitの個別テストは補助として使い、公式JSONの期待値判定や全体の回帰確認を代替しない。WAST用の簡易パーサーは作らない。

- 追加機能の対象ケースのうち、必要なランタイム機能が揃ったものは合格を要求する。必要なJSON command・期待値比較・ホスト設定の不足を理由に検証を後回しにしない。
- 同じ固定スイートで以前`passed`だったケースが別の分類へ変わる、または結果から欠落する場合は、回帰としてNGとする。合格件数だけでなく、元ファイルとcommandを特定して比較する。
- 後続のランタイム機能を必要とするケースは、出典とランタイムが報告した未実装機能を記録する。その機能が揃った段階で再検証し、対象集合やfeature flagから除外しない。
- 固定スイートの前提にないcommand種別や値の形式は`runner_error`とし、ランタイムの`runtime_unsupported`や期待失敗の成立へ置き換えない。前提commandの失敗による`blocked`も元の原因に結びつけて記録する。

実行は、全対象の結果記録・出力が完了して`failed`・`runner_error`が0件なら終了コード0とし、これらがある場合は非0とする。後続機能に伴う`runtime_unsupported`と、それを原因とする`blocked`が残るだけでは非0にしない。回帰比較は、以前の`passed`の別分類への変化・欠落や比較条件の不一致も非0とし、比較が成立して`failed`・`runner_error`・回帰がなければ0とする。最終判定は、保存済みの1つの実行結果JSONを入力とし、下記のCore 2.0全体の完了条件をすべて満たす場合だけ0とし、それ以外は非0とする。

最後は同じランナーで全対象を実行し、固定スイートの`out_of_scope`を除く全ケースが`passed`であることを確認する。対象の否定assertionは診断の前方一致も必須とする。未処理・欠落・件数未確定を残さず、`failed`・`runtime_unsupported`・`runner_error`・`blocked`はすべて0件とする。全8仕様が揃う前に、Core 2.0準拠と参照診断互換性の完成を宣言しない。

最後の機能を統合する担当が、全8仕様を統合した状態で固定スイート全体を1回実行し、その結果JSONだけを最終判定に使う。固定profile・生成物と結果を対応づけて記録し、ホスト連携とSIMDを別々の実行で検証した結果を足し合わせて最終確認の代わりにしない。

公式テスト合格は固定した集合での証拠として扱い、未収録の動作まで証明したとは言わない。各仕様では該当するCore 2.0の規則と実装・検証の対応も確認する。テストを追加・修正した場合は先に警告・エラーのないビルドを確認してからコマンドで実行し、既存のC#・TUnit規則に従う。

## 分割と依存関係の意図

Decode・Validate・Instantiate・Invokeを別々の機能仕様にせず、機能ごとに4段階を通す。最小基盤の次に関数実行と外部要素の生成・リンクを`host-linking`へ集め、数値演算・構造化制御、memory/table命令とsegment、SIMDはそれぞれの仕様が追加する。公式適合検証は、素材生成から実行結果の確認までを一つの仕様・ツールで扱い、生成済み素材も再利用できる。

`test-suite-runner`の初期仕様は`host-linking`を上流とし、spectest・register、固定スイートに現れる全値型の引数構築と結果比較、結果0個/複数、global取得、公開段階・失敗分類・診断の前方一致に基づくassertion判定を含める。素材生成とJSON処理は実行・リンク基盤と並行して作業できるが、初回公式受入は両方が揃ってから行う。後続機能はランタイムの命令・初期化を追加し、ランナーの値処理を後から追加しない。ランタイム内部への専用hookや別ランナーは作らない。

`test-suite-conformance`は公式スイートで判明した先行ランタイムの不具合と、診断互換性などの追加要求をまとめて扱う。具体的な修正項目は実行後に追記・見直し、完了済み仕様の過去の受入記録とは分けて管理する。ランナーの初期実装に含まれる判定をそのまま使い、本仕様で比較機能や判定基準を追加・切り替えることはしない。

独立した仕様の並行作業は可能だが、共通の命令テーブル・モジュール解析・実行ループへの編集は衝突し得る。設計で共通契約を先に固め、実装時は同じファイルの並行編集を避けて統合する。依存関係は仕様作成・完了確認の前提を表し、不要な実装レイヤーや公開拡張口を要求するものではない。

## Specs (dependency order)

- [x] runtime-foundation -- 明示的な4段階APIと値・型・失敗分類、最小の線形実行基盤 Dependencies: none
- [x] host-linking -- 関数実行、global・memory・tableの生成と共有、import/export、ホストcallbackとstartを公開APIで扱う実行・リンク基盤 Dependencies: runtime-foundation
- [x] test-suite-runner -- 固定公式素材の生成、spectest・register、公開API実行・判定、全commandの結果記録・回帰比較と初回baseline（仕様全体の最終実装検証GO。記録は[test-suite-conformanceの準備記録](../specs/test-suite-conformance/acceptance.md#上流仕様の最終実装検証)） Dependencies: host-linking
- [ ] test-suite-conformance -- テストスイートで判明したランタイムの実装上の問題を集約して修正する。診断互換性も含め、実行結果に応じて修正項目を追記・見直す。 Dependencies: test-suite-runner
- [ ] numeric-control -- スカラー数値命令と構造化制御を共通実行機構へ追加し、数値trap・再帰・複数値制御を公式検証する。 Dependencies: host-linking, test-suite-runner, test-suite-conformance
- [ ] linear-memory -- スカラーload/store、data segment、bulk memoryと初期化・実行のtrapを公式検証する。 Dependencies: numeric-control
- [ ] tables-references -- 参照命令、table操作、element segment、間接呼出しと初期化・実行のtrapを公式検証する。 Dependencies: numeric-control
- [ ] simd -- v128とCore 2.0 SIMDを共通命令テーブル・実行ループに実装し、公式SIMDケースで検証する。 Dependencies: linear-memory

作業順は、①完成済みの最小基盤、②実行・リンク基盤、③公式適合検証ツールと初回baseline、④テストスイートで判明したランタイムの実装修正、⑤数値・制御、⑥メモリとテーブル・参照、⑦SIMD ⑥の2仕様は並行可能で、SIMDはメモリが揃えば着手できる。②は③を完了前提にせず、③で②の公式結果と残る実装上の問題を確認する。④以降も初回から同じ判定基準のスイートで回帰確認する。

初期ランナーはspectest・register、固定スイートに現れる全値型の入出力、global取得、段階別の否定assertionを扱う。以後の統合確認は次の機能仕様で行う。

| 機能仕様 | 統合確認 |
| --- | --- |
| `test-suite-conformance` | 公式スイートで判明したランタイムの動作・結果・失敗分類・診断の不一致を修正し、同じ判定基準で全体実行と回帰確認を行う |
| `numeric-control` | 数値演算・構造化制御・再帰の公式ケース 初期のscalar比較と段階別判定を再利用 |
| `linear-memory` | data初期化とstart・共有memoryの複合ケース 初期のassert_uninstantiable判定を再利用 |
| `tables-references` | 参照命令、element初期化とstart・共有tableの複合ケース 初期の参照比較を再利用 |
| `simd` | SIMD命令の公式ケース 初期のv128・lane比較を再利用 |

## 将来のCore 3.0

2.0の固定profile・corpus・結果baselineを残し、3.0の仕様と期待結果は別計画として追加する。版が変わると以前invalidだった入力が有効になる場合もあるため、3.0実行時に2.0の否定テストをそのまま適用できるとは約束しない。必要なら2.0の版別実行契約またはリリースを維持する方法を、その計画で決める。

GC・再帰型・型付き参照、例外処理、64bitアドレス等では型モデル・公開契約・実行機構の追加設計が必要になる。単一命令テーブルと線形実行の方針を保って拡張するが、無変更・破壊的変更なしの移行を保証しない。3.0用の未使用型や汎用plugin機構は今作らない。WABTのGC等の変換可否も、その時点で改めて確認する。

## 根拠

- ユーザー提示の10項目と2026-09-06のdiscovery回答
- 2026-09-27に採用した追加要求と、[参照診断互換性](../../docs/adr/0012-reference-diagnostic-compatibility.md)のADR
- [用語集](../../CONTEXT.md)と、[明示的な4段階API](../../docs/adr/0001-explicit-staged-runtime-api.md)、[同一パスの線形インタープリタ](../../docs/adr/0002-single-pass-linear-interpreter.md)、[Core 2.0の固定適合検証](../../docs/adr/0003-core2-fixed-conformance-profile.md)、[trapの伝播方式](../../docs/adr/0004-trap-result-propagation.md)のADR
- [調査ノート](../../docs/research/wasm-runtime-discovery.md) 公式仕様とWABTの出典、固定時の注意点を含む。
- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)、[Core 2.0公式テスト候補](https://github.com/WebAssembly/spec/tree/v2.0.0/test/core)、[wast2jsonのJSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)
