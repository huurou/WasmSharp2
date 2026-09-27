# ブリーフ: test-suite-runner

## 課題

実装者は機能を追加するたびに固定公式スイートを実行し、仕様違反・実装不足・既存合格ケースの回帰を確認したい。初回からspectestとregisterを使ってimportやmodule間共有を検証でき、素材の出典と結果を同じケース単位で追跡できる必要がある。

## 現状

最小基盤と、公式spec・WABTの固定版および取得・ビルド手順がある。正式manifestと公式ランナーは未実装。本仕様の実行・判定は、先行するhost-linkingが提供する関数実行・リソース生成・import/export・start・依存情報の公開契約を利用する。素材生成とJSON処理の作業は先行できる。

## 望む結果

`tools/`配下に配置する独立したCLIで、公式素材の固定・生成から、公開APIによる実行、期待値判定、結果保存・回帰比較、最終判定までを行う。spectest・register・固定スイートに現れる全値型の引数と結果・global取得・段階別assertionと公式期待診断の前方一致を初期から扱い、最小基盤と実行・リンク基盤の対応範囲を公式ケースで確認する。

## 方針

固定したCore 2.0の全公式入力をwast2jsonでJSONと個別モジュールへ変換する。ランナーは通常の埋め込み利用者としてWasmSharpの公開APIのみを使い、入力ごとにmoduleと登録状態を分離してcommandを順に処理する。素材生成・実行・baseline保存・baseline比較・最終判定は個別のコマンドとし、複数工程をまとめた操作は設けない。ランナーの値処理は初期に完成させ、後続仕様は命令・初期化をランタイムへ追加して同じツールの既存判定で公式統合確認を行う。

## 範囲

- **対象**: SIMDを含む固定Core 2.0のtest/core全WAST入力、採用spec/WABTの取得元・commit、入力WASTのhash、ビルド条件を代表する変換器実行ファイルhash、全featureの既定値と実効ON/OFF、変換引数。
- **対象**: 全件変換、入力とJSON/wasm/watの対応・path・hashを保持し変換結果を兼ねるmanifest、照合、変換失敗・未処理・未確定の報告、再生成と変換baselineの比較。
- **対象**: JSON全commandの列挙、通常module・module識別子付きmodule・直近module、spectest、register、invoke/get、前提commandへの依存と共有状態。
- **対象**: spectestの固定Core 2.0環境。明示型のホスト関数7個、immutable数値global4個、funcref tableとmemoryを、先行仕様の公開APIで生成する。print系の呼び出しは標準出力へ出さず、該当commandの詳細結果に記録する。
- **対象**: i32・i64・f32・f64・v128・funcref・externrefの引数構築と結果比較、結果0個・1個・複数、global取得。型・個数・ビット列・符号付き0・NaN pattern（v128はlaneごと）、参照のnullとexternrefの同一性を比較する。
- **対象**: binaryのassert_malformed/assert_invalid、assert_return、assert_unlinkable、assert_trap/assert_exhaustion、assert_uninstantiableの段階別判定。否定assertionは公式期待診断との前方一致も必須とする。実行自体が後続命令やsegmentを必要とするケースは、その前提が揃った段階で成立させる。
- **対象**: 6分類による全結果の保存、セットアップ・単独action・assertion別の集計、実行結果baseline、ケース単位の回帰比較、用途別終了コード。
- **対象**: registerに必要な、instanceのexport一覧を取得する公開APIのランタイムへの追加。
- **後続仕様の扱い**: 各命令やsegmentの追加に伴う公式ケースは、既存の値比較と段階別判定で実行する。ランナーの値処理や比較能力を後続仕様へ残さない。
- **対象外**: WAST/WATの自作解析、Wasm演算・import型照合の再実装、ランタイム内部アクセス、専用hook、所管仕様の記録、Core 3.0/proposal profile、baseline共有サービス。

## 責務の接点

- host-linkingは通常利用の関数・リソース・リンク・依存情報を所有する。本ツールはspectestの具体値、JSONのregisterと登録名、command状態と期待値判定を所有する。registerに必要なexport一覧の公開APIは、本仕様のランタイム修正として追加する。
- 依存するregister/moduleが失敗したケースは原因commandを参照するblockedとし、以前の成功moduleへ代替しない。module識別子とimport用の登録名を区別し、否定assertion内のmoduleで直近moduleを変更しない。
- commandは処理の順に進め、最初に継続できなくなった箇所で分類する。moduleは登録状態に依存しないDecode・Validateを先に行い、登録依存の不成立によるblockedはInstantiateへ進む時点でだけ判定する。固定スイートに現れる全値型の引数を構築できるため、actionを省略しない。
- 公開能力で得たimport情報から実際の登録依存を特定し、無関係な後続moduleまで停止しない。存在しない登録名による期待されたリンク不成立と、既知の前提commandの失敗を区別する。依存情報を取得できない場合は、取得操作の実際の失敗を記録し、架空の依存を作らない。
- binaryの構文不成立はDecode、型検証不成立はValidate、リンク不成立はInstantiateで判定する。start/segment初期化のtrapとリンク不成立を混同せず、trapとexhaustionも分ける。その条件に加えて、否定assertionは一律に`actualException.Message.StartsWith(expectedText, StringComparison.Ordinal)`で判定する。
- 期待診断はJSONのtextをそのまま使い、大文字小文字・空白・数値を保持する。正規化、別名への置換、Reasonだけの一致による代替、ケース別除外、診断を照合しない合格モードは設けない。実際のMessageも加工せず比較し、後ろに付加された補助説明は許容する。
- 診断不一致はfailedとし、期待診断、実際のMessage、例外型、段階、取得可能なReason・Locationを記録する。初期対応する公式ケースを実行・判定できるまでの修正は、ランタイム側も含めて本仕様で行う。判定で得たfailedの解消は診断互換性も含めてtest-suite-conformanceへ引き継ぐ。
- WASTと生成watは素材同定のhash対象とするが、自作の構文解析・意味解釈は行わない。module_type=textは実行処理で参照先を開かずout_of_scopeにし、素材照合の入力異常とは別に記録する。

## 結果とbaseline

結果はpassed、failed、runtime_unsupported、runner_error、out_of_scope、blockedを区別する。runtime_unsupportedは公開APIで観測した未実装であり、有効性の証明や期待invalid/trapとして扱わない。公開APIが報告した未実装機能・段階・未確認範囲を記録し、所管仕様は記録しない。runner_errorは変換・素材・JSON・ランナー自身の異常、実行環境の資源限界、ランタイムの公開契約にない例外であり、固定スイートの前提にないcommand種別や値の形式もここに含める。blockedには原因commandを残す。

元入力の相対pathとJSON内command順序をケースの識別に使い、行番号も保存する。同じ行の複数commandを区別し、全commandを一つの分類で数える。入力単位の異常はcommand数へ重複加算しない。JSONを列挙できない入力の件数は未確定とし、0や推測のblocked件数にしない。

変換結果はmanifestと兼ね、実行結果・比較結果とともに、集計と該当するケースごとの詳細をJSONファイルで保存する。baselineは保存済みの結果JSONを同じ形式で保存したものとし、変換baselineと実行結果baselineを別々にgit管理外の利用者の環境へ保存し、比較で自動置換しない。変換比較は入力・生成物の一覧/hash、変換状態、spec・WABTの採用commit、profile・引数等を比較し、実行結果比較はprofileと入力・生成物の一覧/hashが一致すれば可能とする。どちらも変換器実行ファイルhashだけの違いは出典差異にする。以前passedだったケースの別分類への変化・欠落は回帰としてNGにする。

baselineの保存とbaselineとの比較は別々の明示的なコマンドとし、どちらも保存済みの結果JSONを入力として再生成・再実行せずに処理する。比較コマンドはbaselineを変更しない。保存コマンドは全対象の処理を完了した結果JSONだけを受け付け、指定先のbaselineを作成または上書きし、旧baselineの別途保存を必須にしない。保存コマンドの終了コードは保存の成否を示し、スイートの合格や回帰なしを意味しない。

各機能の実装後は、全体を実行して現baselineと比較する。問題があれば修正・再実行・比較を繰り返し、問題が解消した結果JSONで現baselineを上書きする。初回baselineは初回受入で得た全commandの結果から作成し、後続へ引き継ぐfailedやrunner_errorもそのまま保持する。

配置rootだけの変更では生成物の内容/hashを変えない。生成時は元入力と生成物、実行時は保存した生成物をmanifestと照合する。実行は元WAST・WABT・生成時の配置先を必要としない。固定条件の変更に専用の操作は設けず、変換baseline比較で変更前後の条件と差分を記録し、baseline保存で明示的に確定する。

実行は全対象の記録・出力完了とfailed・入力/command runner_errorが0で終了0にし、runtime_unsupportedとそれが原因のblockedだけでは非0にしない。変換比較は比較完了・条件一致・runner_error 0、回帰比較は比較成立・完了・failed/runner_error/回帰0を要求する。最終判定は保存済みの1つの実行結果JSONを入力とし、未処理・欠落・件数未確定・runner_errorなし、out_of_scope以外の全commandがpassedの場合だけ0とする。

## この仕様が所有しないこと

ランタイムの関数・global・memory・tableの意味論やimport照合は所有しない。実装結果に合わせて公式入力・期待値・featureを変更しない。ランナーと公開APIの不足によって初期範囲のimport/registerを先送りしない。

## 上流・下流

- **上流**: host-linking。runtime-foundationの公開契約と、[外部ソースの固定](../../../thirdParties/README.md)を引き継ぐ。
- **下流**: test-suite-conformanceによるランタイムの実装修正、numeric-control、linear-memory、tables-references、simdの公式検証と全体の最終判定。

## 既存仕様との関係

- **拡張する既存仕様**: なし。初期必須の公式実行経路を成立させるためのランタイム修正（instanceのexport一覧を取得する公開APIの追加を含む）は本仕様で行い、判定で得たfailedの解消はtest-suite-conformanceへ引き継ぐ。完了済み仕様の過去の受入記録を変更せず、ツール側で期待値や失敗分類を変更して補わない。
- **隣接**: 数値・制御と各リソース・参照・SIMD仕様は命令・初期化の意味論と公式統合確認を所有する。spectest・registerとランナーの値処理は本仕様の初期範囲で完成させる。
- **実装修正の責務**: 公式期待値との比較と診断の前方一致判定、初期必須の公式ケースを実行・判定できるまでの修正は本仕様に含める。判定で得た動作・値・状態・失敗分類・診断の不一致と、初期必須の実行経路を妨げず原因をランタイム側と確認できたrunner_errorはtest-suite-conformanceで扱う。

## 制約と確認事項

--enable-allと--no-checkは禁止し、固定Core 2.0内ON・範囲外OFFを維持する。公式入力・外部ソース・LICENSE/NOTICEを変更しない。全入力の変換成功、照合と同条件での再生成一致を実コマンドで確認する。

公式ケースの公開APIによる実行は単一プロセス内で順に行う。入力ごとのプロセス隔離、タイムアウトによる強制終了、自動再起動、途中再開は設けず、ハングやプロセス異常終了からの継続は保証しない。中断を検知して記録可能な場合は未処理・未確定を残し、正常完了にはしない。

初回受入では固定suite全体を処理し、先行基盤で前提が揃うケースの結果、ランナー自体と素材に起因する入力/command runner_error 0、全command記録、初回baseline保存と再実行・回帰比較を確認する。初期必須の公式ケースを実行・判定できない問題はランタイム側も含めて本仕様で修正し、独自テストだけで受入を代替しない。判定で得たfailedと、初期必須の実行経路を妨げず原因をランタイム側と確認できたrunner_errorは、ケース・期待・実際・原因を記録してtest-suite-conformanceへ引き継げる。必要な前提が成立しない後続commandは原因付きのblockedとして残す。ホスト関数、共有global、memory/tableのimport/export、register後の別moduleからの利用を実際の公式ケースで確認する。

ランナーの受入とランタイムの全件合格を区別し、failedやrunner_errorが残る実行・回帰比較は非0のまま記録する。初回baselineも最初から前方一致を含む同じ基準で保存し、後から判定を強化する段階や既知不一致を合格にする特例は設けない。前方一致・不一致・補助説明付きの診断判定と、不一致の記録・非0終了をランナーのテストで確認する。

後続の命令・segmentに依存する未成立ケースは、ランタイムが報告した未実装機能と原因commandを残す。v128と参照の値処理は、対応する命令が実装されるまでランナーのテストで確認する。この段階でCore 2.0全件合格は要求しない。機能追加後の全体実行・回帰比較と最終完了は[ロードマップ](../../steering/roadmap.md)に従う。コマンド名、JSONファイルの構造、内部構造、hash方式と0以外の終了値は後続の設計で定める。文書は日本語（ja）。
