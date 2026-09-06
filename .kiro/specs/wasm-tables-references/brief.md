# ブリーフ: wasm-tables-references

## 課題

利用者は関数参照や外部参照を保持し、テーブルを使って間接呼び出しを行いたい。スカラー機能だけでは参照の型と同一性、null、element segment、間接callのtrapを表現できない。

## 現状

`WasmTable`は空クラス、`WasmValueKind`にはFuncRef/ExternRefがあるが実装済みではない。着手前に基盤の値・型契約と数値・制御、固定公式素材が整備される。

## 望む結果

Core 2.0の`funcref/externref`、複数table、element segment、table命令、`call_indirect`を4段階で扱える。参照の同一性とnullを保ち、型・添字・境界・間接呼び出しの不一致を適切に判定できる。

## 方針

基盤のWasmValue・型表現と単一実行機構を拡張する。参照固有の検証とtableの意味論を本仕様に集め、ホスト連携へ同じリソースと参照を渡す。3.0のheap typeやGCを先行実装しない。

## 範囲

- **対象**: Core 2.0の参照型、ref.null/ref.func/ref.is_null、参照のlocals・globals・引数・結果・typed select、宣言済み関数参照の規則。
- **対象**: table型・limits、複数table、table.get/set/size/grow、table.copy/fill/init、elem.drop。
- **対象**: active/passive/declarative element、初期化式と添字、初期化trap。
- **対象**: call_indirectのtable要素・関数型照合とtrap、通常のホスト利用に必要なtable・参照の明示的公開操作。
- **対象外**: 型付き関数参照、call_ref、GC、再帰型、exnref、64bit table、ホストimportの名前・型照合。

## 責務の接点

- function呼び出しと結果保持は数値・制御の機構を使い、間接callのための第二の実行系を作らない。
- table/elementの初期化処理は後続のリンク済みInstantiateでも同じ意味論を使う。
- 外部参照を作成・受け渡す際も明示的なWasm値・参照契約を使い、汎用object/dynamic引数や暗黙のboxing規約を公開APIへ持ち込まない。

## この仕様が所有しないこと

ホストmodule登録、import/exportの接続、WASI、テストだけの参照レジストリをランタイムへ置くことは所有しない。

## 上流・下流

- **上流**: `wasm-numeric-control`。
- **下流**: `wasm-host-linking`。`wasm-linear-memory`とは並行できる。

## 既存仕様との関係

- **拡張する既存仕様**: なし。
- **隣接**: globalsとcallの共通経路は数値・制御を利用し、参照型固有の処理だけを追加する。共有リソースとしての接続はホスト連携が担当する。

## 制約と確認事項

固定公式素材でnull、同一参照、型不一致、table境界、element mode、間接call等を公開APIから確認する。ホストimportが必要なケースはホスト連携・ランナーの統合確認へ引き継ぐ。CLR参照の保持方法は実装詳細であり、Wasmの同一性を失わないことを契約とする。文書は日本語（`ja`）。
