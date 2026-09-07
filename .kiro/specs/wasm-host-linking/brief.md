# ブリーフ: wasm-host-linking

## 課題

埋め込み利用者は、ホスト関数やリソースをWasmへ渡し、複数moduleを組み合わせたい。名前が合うだけの接続や値のコピーでは、関数型・可変性・limits・共有状態の仕様を満たせない。

## 現状

discovery時点の`WasmHostModule`は空、host callbackの引数・戻り値の定義だけがある。着手前に数値・制御・globals、メモリ、テーブル・参照の個別の実体と意味論、公式ランナーが整備される。spectestとregisterによるホスト連携の検証対応は本仕様で加える。

## 望む結果

明示的な関数型とWasmValueによるホスト関数登録、import/exportの接続、共有リソースの同一性、初期化とstartを公開APIで扱える。リンク不成立とInstantiate中のtrapを区別できる。通常の利用者として構成したspectestと既存ランナーで、先行機能のimport依存ケースも検証できる。

## 方針

既存のWasmHostModule・WasmFunctionType・WasmInstanceの意図を踏まえて公開操作を完成させる。個別機能が持つリソース・初期化処理を接続し、独立した複製を作らない。ホスト関数の型はdelegateから推測せず明示宣言する。

## 範囲

- **対象**: 関数・global・memory・tableのimport/export、名前解決、仕様の型・可変性・limits照合、importと定義の添字空間。
- **対象**: ホスト関数型と引数・結果の明示契約、host callback呼び出し、結果の所有・寿命、ホスト側の失敗とWasm trapの区別。
- **対象**: module間とホスト間のリソース同一性、mutable globalの共有、再export、instance状態。
- **対象**: Instantiateの初期化順序、importされたglobal等を使う初期化、data/element処理の接続、startの型検証と実行、失敗時に観測できる副作用。
- **対象**: 同じランナーツールへのspectestの明示型host関数・globals・memory・tableの構成、module/registerと共有状態、assert_unlinkableとstartを含むassert_uninstantiableの対応、公式検証・回帰確認。
- **対象外**: メモリ/table命令や数値演算の再実装、WASI、JS API、4段階を畳むローダー、delegate型の推論、ランタイム本体へのspectestの組み込み。

## 責務の接点

- 各機能が所有する初期化処理を順序どおり呼ぶ。importなしの既存経路も同じ処理を使い、意味論を二重化しない。
- startとhost呼び出し中のtrapは内部の実行結果で伝え、Instantiate/Invokeの共通ホスト境界で例外に変換する。リンク不成立へ置換しない。
- `GetGlobal`で取得した値をコピーするだけでは共有可変globalを表せない。既存の「Exportsは持たせない」を守りながら、通常利用に必要な共有操作を設計する。

## この仕様が所有しないこと

JSONのmodule/registerコマンド、spectestのprintや定義値、テスト結果の集計はツール側に置く。本仕様の受入作業として既存ランナーを拡張するが、テストの都合だけの公開能力をランタイムへ追加しない。

## 上流・下流

- **上流**: `wasm-linear-memory`、`wasm-tables-references`（共通基盤・公式素材・ランナーを含む）。
- **下流**: importに依存していた先行機能の公式ケースの再検証と、全体のCore 2.0適合確認。`wasm-simd`とは独立に進められる。

## 既存仕様との関係

- **拡張する既存仕様**: `wasm-conformance-runner`が整備したツールを拡張する。初期仕様の完了条件は変更せず、本仕様の要件・タスクで追加対応を扱う。
- **隣接**: globals・memory・tableの実体と初期化は既存機能仕様の所有。リンク側が同じ意味論を利用する。

## 制約と確認事項

ホスト連携機能を追加するたびに固定公式スイートをランナーで実行し、imports/linking/start/初期化とリソース共有を公開APIで検証する。完了時は[ロードマップの公式検証方針](../../steering/roadmap.md#公式検証の方針と完了条件)に従い、全体の結果差分、追加機能の対象ケースの合格、既存合格ケースの退行がないことを確認する。先行機能から引き継いだimport依存ケースも対象に含め、SIMD等の未完成機能が必要なものは出典・理由・所管仕様を記録して再検証へ引き継ぐ。関数呼び出しの型・引数個数・戻り値を曖昧に変換しない。既存callbackが返すSpanの安全な寿命・所有を設計で確定し、骨組みだから安全と見なさない。文書は日本語（`ja`）。
