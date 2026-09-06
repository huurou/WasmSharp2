# ブリーフ: wasm-conformance-runner

## 課題

実装者は公式テストの期待する失敗段階と実行結果を正しく判定し、合格・不具合・未実装・対象外・ツール不成立を区別したい。内部APIに依存した検証では、通常利用者が同じ能力を利用できる証拠にならない。

## 現状

ランナーとspectestは未実装。着手前に固定JSON/モジュール素材と、明示的な値・型・ホスト登録・共有リソースの公開APIを利用できる。

## 望む結果

固定されたCore 2.0のJSONと`.wasm`だけを読み、公開APIだけでspectestとmodule間の状態を構成して、公式assertionを実行・集計できる。元ファイルと位置、期待段階、実際の結果、未実行理由まで追跡できる。

## 方針

ランタイムの通常の利用者として別ツールを作る。生成済みJSONのcommandを順に実行し、module・名前・register・actionの状態を管理する。WAST構文やWasm演算を再実装しない。

## 範囲

- **対象**: 固定版JSON schema、module/action/register、名前付きmoduleと直近module、失敗時の後続commandの扱い。
- **対象**: 明示的な関数型によるspectestのhost関数、globals・memory・table・参照の構成と共有を公開APIだけで行う。
- **対象**: binaryのassert_malformed/assert_invalid、assert_unlinkable、assert_uninstantiable、assert_trap、assert_exhaustion、assert_return。
- **対象**: 引数と結果の型・個数、整数・floatのビット列、符号付き0、NaN pattern、v128 lane、参照の同一性とnullの期待値判定。
- **対象**: passed/failed/unsupported/out_of_scope/tool_error、セットアップとassertionの別集計、固定profileと入力・生成物hashを含む結果記録、回帰差分。
- **対象外**: WAST/WATの読み取り、変換器の実装、ランタイム内部の操作、独自のWasm演算、3.0固有commandやrelaxed-SIMDを初期の検証範囲へ追加すること。

## 責務の接点

- `wasm-test-corpus`のmanifestと生成物を入力契約にする。JSON未対応や破損・変換不能をランタイム未実装と報告しない。
- `.wat`は開かず、`module_type=text`を理由付き対象外とする。対象外を合格件数へ入れない。
- Instantiate内のtrapとリンク不成立を原因で区別する。`text`の参照診断を公開例外メッセージの完全一致契約へ置き換えない。
- specに必要な機能が公開APIで表現できなければ、通常利用にも必要な能力かを確認して所管仕様へ戻す。reflection・内部アクセス・専用hookで迂回しない。

## この仕様が所有しないこと

SIMD命令の実行、メモリ・table・globalの意味論、importの型照合はライブラリが所有する。公式期待値を実装結果に合わせて変更しない。

## 上流・下流

- **上流**: `wasm-test-corpus`、`wasm-host-linking`。
- **下流**: Core 2.0全体の公式適合確認と継続的な回帰確認。

## 既存仕様との関係

- **拡張する既存仕様**: なし。
- **隣接**: `wasm-simd`とは基盤のv128契約を共有する。ランナー自体はSIMD未実装を分類できるが、全体の適合完成にはSIMDも必要。

## 制約と確認事項

`unsupported`と前提commandの失敗による未実行を隠さず、全対象を処理した集計を出す。未実装が期待されたtrap/invalidとして合格にならないようにする。固定したCore 2.0バイナリ対象集合でfailed・unsupported・tool_error・依存未実行が0になることを全体の完成条件とする。テキスト対象と公式スイートが証明しない範囲を区別して報告する。文書は日本語（`ja`）。
