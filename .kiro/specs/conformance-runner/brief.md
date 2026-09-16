# ブリーフ: conformance-runner

## 課題

実装者は機能を追加するたびに固定した公式スイートを実行し、期待する失敗段階と実行結果、既存合格ケースの回帰を確認したい。入力版やfeatureが変わると過去の結果と比較できないため、素材の固定・生成から実行・回帰比較までを一つの工程として整備する。内部APIに依存した検証では、通常利用者が同じ能力を利用できる証拠にならない。

## 現状

WasmSharpの最小実行基盤があり、公式specとWABTの採用版・取得・変換手順は[外部ソースの固定](../../../thirdParties/README.md)に記録されている。正式なmanifest、素材生成から実行までを扱うツールとspectestは未実装。本仕様は基盤の4段階API・値・型・失敗分類を利用し、数値・制御、メモリ、参照、ホスト登録・共有リソース、SIMD命令の完成は前提にしない。

## 望む結果

固定したCore 2.0公式入力全体を外部の`wast2json`で変換し、生成されたJSONと`.wasm`を基盤の公開APIで実行できる。素材の生成条件とhashを追跡し、全commandの結果から初回baselineと回帰差分を得られる。元ファイルと位置、期待段階、実際の結果、未実行理由まで、一つのツールで確認できる。

## 方針

ランタイムの通常の利用者として、素材の固定・生成から実行・判定・回帰比較までを担う一つのツールを作る。素材生成のみ・生成済み素材の実行のみも選択できる。生成はランタイムの実装状況に依存させず、実行時はJSONのcommandを順に処理して名前付きmoduleと直近module、前提commandへの依存を管理する。初期の実行対象は基盤の対応範囲とし、後続機能が必要な範囲も黙って落とさず記録する。各機能仕様が必要な対応を同じツールへ追加する。WAST構文やWasm演算を再実装しない。

## 範囲

- **対象**: SIMDを含むCore 2.0公式入力集合、仕様版・入力元commit・対象path、WABTソースと実行ファイルの同定、取得・ビルド条件、Core 2.0内ON・範囲外OFFの実効featureとCLI引数の固定。
- **対象**: 全入力の外部変換、JSON・`.wasm`・`.wat`の一覧とhash、生成条件のmanifest、変換成功・失敗・未処理範囲の報告、再生成と変換baselineの比較。
- **対象**: 固定版JSONのcommand全体の列挙、moduleとinvoke action、名前付きmoduleと直近module、失敗時の後続commandの扱い。
- **対象**: 基盤の公開APIによるimportなしのmoduleの4段階実行、binaryのassert_malformed/assert_invalid、引数なし・スカラー1結果のassert_return。
- **対象**: 初期の結果比較に必要な型・個数、整数・floatのビット列、符号付き0とNaN patternの判定。
- **対象**: passed/failed/unsupported/out_of_scope/tool_error、セットアップとassertionの別集計、固定profileと入力・生成物hashを含む結果記録、回帰差分。
- **後続仕様で追加**: 引数・複数戻り値・global取得・trap・exhaustion、segment初期化trap、参照、spectestとregister・リンク、v128 laneの対応。下表の所管仕様で実装・検証する。
- **対象外**: WAST/WATの自作解析、変換器の実装、ランタイム内部の操作、独自のWasm演算、3.0固有commandやrelaxed-SIMDを初期の検証範囲へ追加すること。

## 後続機能に伴う拡張

| 所管仕様 | 同じランナーツールへ加える受入作業 |
| --- | --- |
| `numeric-control` | 引数・複数戻り値、global取得、数値結果・NaNの比較を拡張し、assert_trap/assert_exhaustionを検証する。 |
| `linear-memory` | data初期化時のtrapを含むassert_uninstantiableを検証する。 |
| `tables-references` | 参照の引数・結果、null・同一性、element初期化時のassert_uninstantiableを検証する。メモリ仕様が未完成でも必要な判定を実装し、既存対応があれば再利用する。 |
| `host-linking` | 明示型のspectest関数・globals・memory・tableを公開APIで構成し、registerとmodule間の共有状態、assert_unlinkable、startを含むassert_uninstantiableを検証する。 |
| `simd` | v128の引数・結果とlaneごとのビット列・NaN patternを判定する。 |

JSON処理・期待値比較・spectestはツール側の責務を維持する。後続仕様ではこの拡張を実装タスクと完了条件に含め、初期ランナーを未完了のまま待たせる依存関係にしない。

## 責務の接点

- 素材生成と実行の間では、本ツールが記録するmanifestと生成物を使う。変換baselineと実行結果baselineを区別し、JSON未対応や破損・変換不能をランタイム未実装と報告しない。
- ランナー側にcommandや期待値の対応がない場合は`tool_error`とする。`unsupported`は公開APIでランタイムの未実装を観測した場合に使う。前提module/registerが成立しなかった後続commandは、元の原因を参照する理由付き未実行として記録する。
- `.wat`は素材一覧とhashの追跡対象とするが、実行処理では開かず、`module_type=text`を理由付き対象外とする。対象外を合格件数へ入れない。
- Instantiate内のtrapとリンク不成立を原因で区別する。`text`の参照診断を公開例外メッセージの完全一致契約へ置き換えない。
- specに必要な機能が公開APIで表現できなければ、通常利用にも必要な能力かを確認して所管仕様へ戻す。reflection・内部アクセス・専用hookで迂回しない。

## この仕様が所有しないこと

SIMD命令の実行、メモリ・table・globalの意味論、importの型照合はライブラリが所有する。本仕様の初期完了に、後続機能の公開APIやspectestの完成は要求しない。公式期待値を実装結果に合わせて変更しない。

## 上流・下流

- **上流**: `runtime-foundation`。[ロードマップ](../../steering/roadmap.md)のCore 2.0範囲と固定方針、既存の外部ソース取得・変換手順。
- **下流**: `numeric-control`以降の全機能の公式実行・回帰確認と、全仕様統合後のCore 2.0全体の公式適合確認。

## 既存仕様との関係

- **拡張する既存仕様**: なし。
- **隣接**: 各機能仕様が公開APIと必要なツール側の対応を一緒に追加する。`host-linking`と`simd`は初期ランナーの上流ではなく、その拡張を担う後続仕様。

## 制約と確認事項

`--enable-all`と`--no-check`は禁止し、入力やfeatureをランタイムの実装進捗で変更しない。公式入力・期待値と既存の外部ソース、LICENSE・NOTICEを保持する。固定条件の更新は通常の再生成と区別した明示操作とし、旧baselineとの差分を追跡できるようにする。

初期完了では、固定公式入力全体の変換に成功し、manifest・生成物の照合と同じ条件での再生成による一致を確認する。変換失敗・変換未処理・照合不一致は0件とする。生成済み素材だけを使った再実行も確認する。

初期完了では、基盤の対応範囲で前提が揃う公式ケースの実行・合格、全対象commandの結果記録、初回baselineと同じケース単位での差分比較を確認する。対応範囲の公式ケースが未成立の場合は原因を確認し、ランナー側の不足を解消する。基盤の不具合であれば基盤へ修正を戻し、後続機能への依存とは区別する。初期完了を全対象合格と扱わない。

`unsupported`・`tool_error`と前提commandの失敗による未実行を隠さず、全対象を処理した集計を出す。未成立のケースには出典・理由・必要な機能と所管仕様を記録し、未実装が期待されたtrap/invalidとして合格にならないようにする。後続機能では[ロードマップの公式検証方針](../../steering/roadmap.md#公式検証の方針と完了条件)に従い、追加機能の対象ケースの合格と既存合格ケースの退行がないことを確認する。最終的には全仕様を統合した状態で、Core 2.0バイナリ対象集合のfailed・unsupported・tool_error・依存未実行が0になることを要求する。テキスト対象と公式スイートが証明しない範囲を区別して報告する。文書は日本語（`ja`）。
