# ブリーフ: linear-memory

## 課題

利用者は線形メモリに対するload/storeやdata segmentを使う計算を実行したい。リソースの生成・import/exportができても、guest命令、segment初期化、bulk操作の意味論は別に必要である。

## 現状

着手時にはhost-linkingのmemory型・limits・生成・共有・公開アクセス、numeric-controlの演算と制御、conformance-runnerのspectest・register・段階別assertionとbaselineを利用できる。

## 望む結果

同じmemory実体を使ってCore 2.0のスカラーload/store、size/grow、data初期化、bulk memoryを実行できる。imported memoryにも同じ意味論を適用し、境界trap・grow失敗値・検証不成立を区別する。

## 方針

guest命令とdata segmentのDecodeから検証・初期化・実行までを本仕様へ集める。memoryの型・割当・共有機構を作り直さず、共通のリソース操作とInstantiate順序へ機能を追加する。

## 範囲

- **対象**: スカラーload/store、符号拡張load、狭幅store、size/grow、32bitアドレス・offset・alignmentの規則。
- **対象**: active/passive data、data count、memory.init/copy/fill、data.drop、初期化・実行の境界trap。
- **対象**: 定義memoryとimported memoryへの同じ命令・初期化の適用、共有memoryの変更とstart実行順序、初期化/start失敗時の観測可能な副作用。
- **対象**: 既存assert_uninstantiable等によるdata初期化trapの判定、公式スイート全体とbaseline差分による回帰確認。必要なツール側の対応も本仕様で追加する。
- **対象外**: memoryの基本型・limits・生成・import照合の再実装、memory64、multi-memory、shared memory/threads、SIMD load/store、WASI。

## 責務の接点

- memoryの型・割当・同一性・公開内容アクセスはhost-linkingの契約を使う。guest命令固有の境界・grow失敗値・trapを本仕様が所有する。
- data初期化を既存Instantiateへ追加し、その完了後に先行仕様のstart実行を行う。imported memoryに対する副作用を含め、初期化とstartの順序を確認する。
- SIMDは本仕様のメモリアクセスと境界処理を使う。table/elementの初期化はtables-referencesが所有する。

## この仕様が所有しないこと

ホストmodule登録、spectest、table/element、JSON共通判定、OS固有のメモリ最適化は所有しない。

## 上流・下流

- **上流**: numeric-control。host-linkingとconformance-runnerの能力を引き継ぐ。
- **下流**: simdと全体の統合確認。tables-referencesとは並行できる。

## 既存仕様との関係

- **拡張する既存仕様**: host-linkingのmemory実体とInstantiate経路、conformance-runnerの公式検証を拡張する。
- **隣接**: import型照合はhost-linking、table/elementはtables-references、vectorメモリ命令はsimd。

## 制約と確認事項

機能を追加するたびに固定公式スイートを実行し、[ロードマップの公式検証方針](../../steering/roadmap.md#公式検証の方針と完了条件)に従って対象ケースの合格と回帰がないことを確認する。unsignedアドレスとoffsetをCLRのoverflowや配列例外へ委ねず、grow失敗・trap・資源制限・未実装を区別する。後続機能待ちのケースは理由と所管を残す。文書は日本語（ja）。
