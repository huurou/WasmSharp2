# ブリーフ: conformance-runner

## 課題

実装者は機能を追加するたびに固定公式スイートを実行し、仕様違反・実装不足・既存合格ケースの回帰を確認したい。初回からspectestとregisterを使ってimportやmodule間共有を検証でき、素材の出典と結果を同じケース単位で追跡できる必要がある。

## 現状

最小基盤と、公式spec・WABTの固定版および取得・ビルド手順がある。正式manifestと公式ランナーは未実装。本仕様の実行・判定は、先行するhost-linkingが提供する関数実行・リソース生成・import/export・start・依存情報の公開契約を利用する。素材生成とJSON処理の作業は先行できる。

## 望む結果

一つのツールで公式素材の固定・生成から、公開APIによる実行、期待値判定、結果保存・回帰比較までを行う。spectest・register・スカラー引数と結果・global取得・段階別assertionを初期から扱い、最小基盤と実行・リンク基盤の対応範囲を公式ケースで確認する。

## 方針

固定したCore 2.0の全公式入力をwast2jsonでJSONと個別モジュールへ変換する。ランナーは通常の埋め込み利用者としてWasmSharpの公開APIのみを使い、入力ごとにmoduleと登録状態を分離してcommandを順に処理する。素材生成のみ・生成済み素材の実行のみも可能とし、後続仕様は同じツールへ必要な比較能力を追加する。

## 範囲

- **対象**: SIMDを含む固定Core 2.0のtest/core全WAST入力、採用spec/WABTの取得元・commit・source hash・実行ファイルhash・ビルド条件、全featureの既定値と実効ON/OFF、変換引数。
- **対象**: 全件変換、入力とJSON/wasm/watの対応・path・hashを保持するmanifest、照合、変換失敗・未処理・未確定の報告、再生成と変換baselineの比較。
- **対象**: JSON全commandの列挙、通常module・名前付きmodule・直近module、spectest、registerと再登録、invoke/get、前提commandへの依存と共有状態。
- **対象**: spectestの固定Core 2.0環境。明示型のホスト関数7個、immutable数値global 4個、funcref tableとmemoryを、先行仕様の公開APIで生成する。
- **対象**: スカラー引数、結果0個・1個・複数、スカラーglobal取得、型・個数・ビット列・符号付き0・NaN patternの比較。
- **対象**: binaryのassert_malformed/assert_invalid、assert_return、assert_unlinkable、assert_trap/assert_exhaustion、assert_uninstantiableの段階別判定。実行自体が後続命令やsegmentを必要とするケースは、その前提が揃った段階で成立させる。
- **対象**: 7分類による全結果の保存、セットアップ・単独action・assertion別の集計、実行結果baseline、ケース単位の回帰比較、用途別終了コード。
- **後続仕様で追加**: 参照の引数・結果とnull・同一性比較、v128 laneのビット列・NaN比較。各命令やsegment追加に伴う公式ケースは既存の段階別判定で実行し、必要なツール対応を同じ仕様の受入作業として加える。
- **対象外**: WAST/WATの自作解析、Wasm演算・import型照合の再実装、ランタイム内部アクセス、専用hook、Core 3.0/proposal profile、baseline共有サービス。

## 責務の接点

- host-linkingは通常利用の関数・リソース・リンク・依存情報を所有する。本ツールはspectestの具体値、JSONのregisterと登録名、command状態と期待値判定を所有する。
- 依存するregister/moduleが失敗したケースは原因commandを参照するblockedとし、以前の成功moduleへ代替しない。名前付きmodule名とimport用登録名を区別し、否定assertion内のmoduleで直近moduleを変更しない。
- 公開能力で得たimport情報から実際の登録依存を特定し、無関係な後続moduleまで停止しない。存在しない登録名による期待されたリンク不成立と、既知の前提commandの失敗を区別する。依存情報を取得できない場合は、取得操作の実際の失敗を記録し、架空の依存を作らない。
- binaryの構文不成立はDecode、型検証不成立はValidate、リンク不成立はInstantiateで判定する。start/segment初期化のtrapとリンク不成立を混同せず、trapとexhaustionも分ける。参照診断textと公開例外メッセージの完全一致を条件にしない。
- WASTと生成watは素材同定のhash対象とするが、自作の構文解析・意味解釈は行わない。module_type=textは実行処理で参照先を開かずout_of_scopeにし、素材照合の入力異常とは別に記録する。

## 結果とbaseline

結果はpassed、failed、runtime_unsupported、runner_unsupported、runner_error、out_of_scope、blockedを区別する。runtime_unsupportedは公開APIで観測した未実装であり、有効性の証明や期待invalid/trapとして扱わない。runner_unsupportedはcommand・値比較の未対応、runner_errorは変換・素材・JSON等の異常である。後続未対応には必要機能と所管仕様、blockedには原因commandを残す。

元入力の相対pathとJSON内command順序をケースの識別に使い、行番号も保存する。同じ行の複数commandを区別し、全commandを一つの分類で数える。入力単位の異常はcommand数へ重複加算しない。JSONを列挙できない入力の件数は未確定とし、0や推測のblocked件数にしない。

変換baselineと実行結果baselineは別に利用者の環境へ保存し、比較で自動置換しない。変換比較は入力・生成物の一覧/hash、変換状態、変換器source/executable hash、ビルド条件・profile・引数等を比較する。実行結果比較はprofileと入力・生成物の一覧/hashが一致すれば可能とし、変換器実行ファイルhashだけの違いは出典差異にする。以前passedだったケースの別分類への変化・欠落は回帰としてNGにする。

配置rootだけの変更では生成物の内容/hashを変えない。生成時は元入力と生成物、実行のみでは保存した生成物をmanifestと照合する。実行のみは元WAST・WABT・生成時の配置先を必要としない。固定条件の更新は通常の再生成と区別した明示操作にし、旧baselineと差分を残す。

通常実行は全対象の記録・出力完了とfailed・入力/command runner_errorが0で終了0にする。理由・所管を持つ後続未対応と、それが原因のblockedだけでは非0にしない。変換比較は比較完了・条件一致・runner_error 0、回帰比較は比較成立・完了・failed/runner_error/回帰0を要求する。最終判定は未処理・欠落・件数未確定・runner_errorなし、out_of_scope以外の全commandがpassedの場合だけ0とする。複数工程のいずれかが非0条件なら操作全体も非0にする。

## この仕様が所有しないこと

ランタイムの関数・global・memory・tableの意味論やimport照合は所有しない。実装結果に合わせて公式入力・期待値・featureを変更しない。ランナーと公開APIの不足によって初期範囲のimport/registerを先送りしない。

## 上流・下流

- **上流**: host-linking。runtime-foundationの公開契約と、[外部ソースの固定](../../../thirdParties/README.md)を引き継ぐ。
- **下流**: numeric-control、linear-memory、tables-references、simdの公式検証と全体の最終判定。

## 既存仕様との関係

- **拡張する既存仕様**: なし。先行ランタイムの不具合は所管へ戻し、ツール側で期待値や失敗分類を変更して補わない。
- **隣接**: 数値・制御と各リソース仕様は命令・初期化の意味論と公式統合確認、参照/SIMD仕様は対応するツール比較も所有する。spectest・registerは本仕様の初期範囲で完成させる。

## 制約と確認事項

--enable-allと--no-checkは禁止し、固定Core 2.0内ON・範囲外OFFを維持する。公式入力・外部ソース・LICENSE/NOTICEを変更しない。全入力の変換成功、照合と同条件での再生成一致を実コマンドで確認する。

初回受入では固定suite全体を処理し、最小基盤と実行・リンク基盤で前提が揃う全ケースのpassed、全体failed・入力/command runner_error 0、全command記録、初回baseline保存と再実行・回帰比較を確認する。対象ケースをpassed一覧から逆算せず、対応範囲と依存関係から特定する。ホスト関数、共有global、memory/tableのimport/export、register後の別moduleからの利用を実際の公式ケースで確認する。

後続命令・segment・参照/SIMD比較に依存する未成立ケースは理由と所管を残す。この段階でCore 2.0全件合格は要求しない。機能追加後の全体実行・回帰比較と最終完了は[ロードマップ](../../steering/roadmap.md)に従う。コマンド名、保存形式、内部構造、hash方式と0以外の終了値は後続の設計で定める。文書は日本語（ja）。
