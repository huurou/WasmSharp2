# ブリーフ: wasm-host-linking

## 課題

埋め込み利用者は、ホスト関数やリソースをWasmへ渡し、複数moduleを組み合わせたい。名前が合うだけの接続や値のコピーでは、関数型・可変性・limits・共有状態の仕様を満たせない。

## 現状

`WasmHostModule`は空、host callbackの引数・戻り値の定義だけがある。`WasmModule.Instantiate`も未実装。着手前に数値・制御・globals、メモリ、テーブル・参照の個別の実体と意味論が整備される。

## 望む結果

明示的な関数型とWasmValueによるホスト関数登録、import/exportの接続、共有リソースの同一性、初期化とstartを公開APIで扱える。リンク不成立とInstantiate中のtrapを区別できる。通常の利用者として公式ランナーを実装できる。

## 方針

既存のWasmHostModule・WasmFunctionType・WasmInstanceの意図を踏まえて公開操作を完成させる。個別機能が持つリソース・初期化処理を接続し、独立した複製を作らない。ホスト関数の型はdelegateから推測せず明示宣言する。

## 範囲

- **対象**: 関数・global・memory・tableのimport/export、名前解決、仕様の型・可変性・limits照合、importと定義の添字空間。
- **対象**: ホスト関数型と引数・結果の明示契約、host callback呼び出し、結果の所有・寿命、ホスト側の失敗とWasm trapの区別。
- **対象**: module間とホスト間のリソース同一性、mutable globalの共有、再export、instance状態。
- **対象**: Instantiateの初期化順序、importされたglobal等を使う初期化、data/element処理の接続、startの型検証と実行、失敗時に観測できる副作用。
- **対象外**: メモリ/table命令や数値演算の再実装、WASI、JS API、4段階を畳むローダー、delegate型の推論、spectest自体の定義。

## 責務の接点

- 各機能が所有する初期化処理を順序どおり呼ぶ。importなしの既存経路も同じ処理を使い、意味論を二重化しない。
- startとhost呼び出し中のtrapは内部の実行結果で伝え、Instantiate/Invokeの共通ホスト境界で例外に変換する。リンク不成立へ置換しない。
- `GetGlobal`で取得した値をコピーするだけでは共有可変globalを表せない。既存の「Exportsは持たせない」を守りながら、通常利用に必要な共有操作を設計する。

## この仕様が所有しないこと

JSONのmodule/registerコマンド、spectestのprintや定義値、テスト結果の集計はランナーが所有する。テストの都合だけの公開能力を追加しない。

## 上流・下流

- **上流**: `wasm-linear-memory`、`wasm-tables-references`。
- **下流**: `wasm-conformance-runner`。`wasm-simd`とは独立に進められる。

## 既存仕様との関係

- **拡張する既存仕様**: なし。
- **隣接**: globals・memory・tableの実体と初期化は既存機能仕様の所有。リンク側が同じ意味論を利用する。

## 制約と確認事項

固定公式素材のimports/linking/start/初期化とリソース共有を公開APIで検証する。関数呼び出しの型・引数個数・戻り値を曖昧に変換しない。既存callbackが返すSpanの安全な寿命・所有を設計で確定し、骨組みだから安全と見なさない。文書は日本語（`ja`）。
