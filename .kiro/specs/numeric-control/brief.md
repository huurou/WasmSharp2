# ブリーフ: numeric-control

## 課題

利用者は数値計算と構造化制御を持つWasmを実行したい。先行する関数実行・リンク基盤だけでは、演算、条件分岐、loop、複数値blockや計算trapを扱えない。

## 現状

着手時にはruntime-foundationとhost-linkingの関数引数・結果・locals・直接call・unreachable・global・import/export、conformance-runnerのspectest・register・初回baselineを利用できる。スカラー数値演算と構造化制御は本仕様で追加する。

## 望む結果

Core 2.0のスカラー数値演算と構造化制御を4段階で扱い、正常結果、検証不成立、数値trap、再帰によるexhaustionを公式スイートで確認できる。importやホスト連携を使うケースも、必要な命令が揃った段階で実行する。

## 方針

先行仕様の関数フレームと単一実行ループへ数値・制御命令を追加する。型検査と線形化を同一パスで行い、分岐をtargetPc・stackHeight・keepCountへ落とす。呼出し・locals・globalの機構を再実装しない。

## 範囲

- **対象**: i32/i64/f32/f64の数値演算、sign-extension、non-trapping conversions、再解釈、比較、スカラーselect。
- **対象**: block/loop/if、br/br_if/br_table、複数値blockとloop引数。host-linkingのreturn・unreachableに必要な型検証を引き継ぎ、構造化制御に伴う型スタックの多相性へ拡張する。
- **対象**: 先行するcall/return・locals・global・ホストcallbackと構造化制御の統合、数値trap、制御構文を使う再帰と深さ制限の公式検証。
- **対象**: 固定公式スイートの全体実行と回帰比較。既存のscalar入出力・NaN比較・assert_trap/assert_exhaustionを使い、追加機能に必要なツール側の不足も解消する。
- **対象外**: 関数の基本呼出し・引数と結果・locals・drop・unreachableの再実装、globalの生成/get/set・import解決・startの再実装、memory/table命令、参照命令、SIMD。

## 責務の接点

- 関数呼出し時のスタック基準はhost-linkingの契約に従う。loopラベルが保持する引数とblockラベルが保持する結果を区別する。
- globalの型・実体・スカラー初期化・get/setはhost-linkingが所有する。本仕様は制御・演算との組み合わせを扱い、別のglobal表現を作らない。
- 共通の値・型を使い、後続の参照/v128も運べる制御機構にする。参照固有の型検証とvector命令の意味論は各機能仕様が追加する。
- trapとexhaustionは既存の実行結果と共通ホスト境界を使う。呼出し深さの上限適用は先行仕様から有効であり、本仕様で初めて導入するものではない。

## この仕様が所有しないこと

共有リソース・ホスト登録・spectest・JSON共通処理は所有しない。公式期待値の判定をランタイムへ持ち込まず、必要なツール対応は同じconformance-runnerへ追加する。

## 上流・下流

- **上流**: host-linking、conformance-runner。
- **下流**: linear-memoryとtables-references。simdも同じ制御・実行機構を使う。

## 既存仕様との関係

- **拡張する既存仕様**: host-linkingが提供する実行機構と、conformance-runnerの公式検証能力を拡張する。
- **隣接**: memory/table・参照・SIMDに固有の命令と初期化は各仕様が所有する。

## 制約と確認事項

機能の追加ごとに固定公式スイートを実行し、[ロードマップの公式検証方針](../../steering/roadmap.md#公式検証の方針と完了条件)に従って追加対象の合格と既存passedの退行がないことを確認する。後続のmemory/table命令・segment・参照/SIMDに依存するケースは理由と所管を残す。floatのbits・NaN・符号付き0・丸め・整数境界を守り、CLRのStackOverflowでプロセスを落とさない。文書は日本語（ja）。
