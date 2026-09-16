# ブリーフ: tables-references

## 課題

利用者は関数・外部参照をguest命令で扱い、tableとelement segmentによる間接呼出しを実行したい。リソースの生成・共有だけでは参照命令、element初期化、call_indirectの検証とtrapを扱えない。

## 現状

着手時にはhost-linkingの参照保持・table型/limits・null初期化・生成・import/export、numeric-controlの制御機構、conformance-runnerのspectest・register・初回baselineを利用できる。

## 望む結果

同じ参照・table実体でCore 2.0のref命令、table命令、element、call_indirectを4段階で扱える。null・同一性・型・添字・境界を保ち、imported tableにも同じ意味論を適用する。

## 方針

共通のWasmValue・型・関数呼出し・リソース機構へ参照固有の検証と命令を追加する。基本のtable生成・リンクを重複実装せず、3.0のheap typeやGCを先行導入しない。

## 範囲

- **対象**: ref.null/ref.func/ref.is_null、参照を使うlocals・globals・引数・結果・typed selectの固有規則、宣言済み関数参照の検証。
- **対象**: table.get/set/size/grow/copy/fill/init、elem.drop、複数tableの命令処理。
- **対象**: active/passive/declarative element、参照の初期化式・添字・mode、初期化trap。
- **対象**: call_indirectの要素・関数型照合とtrap、既存の直接callと共通の結果受渡し。
- **対象**: imported table・共有参照への命令とelement初期化、startとの順序と失敗時の観測可能な副作用。
- **対象**: 同じランナーの参照引数・結果、null・同一性比較、element初期化を含む公式検証と回帰比較。必要な判定はメモリ仕様の完成を待たず追加する。
- **対象外**: tableの基本型・limits・生成・リンク・公開ホスト操作の再実装、型付き関数参照、call_ref、GC、再帰型、exnref、64bit table。

## 責務の接点

- 関数呼出しとフレームはhost-linking、構造化制御はnumeric-controlの機構を使う。間接callのための別実行系を作らない。
- tableの保持・null初期化・同一性は先行契約を使い、guest命令とelement固有の規則を本仕様が所有する。
- 参照globalの初期化式・ref命令の型規則を既存global実体へ追加する。外部参照も明示的なvalue契約を使い、object/dynamic汎用引数で代用しない。
- element初期化を既存Instantiateへ追加し、完了後に先行仕様のstartを実行する。data固有の処理はlinear-memoryが所有する。

## この仕様が所有しないこと

ホストmodule登録とimport照合、WASI、ランタイムへのテスト専用参照レジストリ、scalar数値演算は所有しない。

## 上流・下流

- **上流**: numeric-control。host-linkingとconformance-runnerの能力を引き継ぐ。
- **下流**: 全体のCore 2.0統合確認。linear-memoryとは並行できる。

## 既存仕様との関係

- **拡張する既存仕様**: host-linkingの参照・table・Instantiate、numeric-controlの制御、conformance-runnerの値比較を拡張する。
- **隣接**: globalの共通実体はhost-linking、参照固有の初期化と検証は本仕様、data初期化はlinear-memory。

## 制約と確認事項

機能追加ごとに固定公式スイートを実行し、[ロードマップの公式検証方針](../../steering/roadmap.md#公式検証の方針と完了条件)に従って対象ケースの合格と回帰がないことを確認する。null・同一参照・型不一致・table境界・element mode・間接callを公開APIから検証する。後続機能待ちのケースは理由と所管を残す。文書は日本語（ja）。
