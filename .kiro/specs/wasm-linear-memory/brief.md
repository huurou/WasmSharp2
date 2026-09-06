# ブリーフ: wasm-linear-memory

## 課題

利用者はWasmのバイト列メモリとdata segmentを利用した計算を実行したい。数値・制御だけでは、load/store、初期化、メモリ拡張、bulk操作とそのtrapを扱えない。

## 現状

現行の`WasmMemory`は空クラス。着手時には基盤・数値制御・固定公式素材を利用できる。メモリの具体的な意味論は未実装として扱う。

## 望む結果

Core 2.0の線形メモリを定義・初期化・公開取得して、スカラーload/store、size/grow、bulk memoryを実行できる。境界違反によるtrap、growの失敗値、検証不成立を仕様どおり区別できる。

## 方針

メモリの宣言・即値・data情報のDecodeから、型検証、segment初期化、実行までを一つの機能仕様として実装する。数値・制御の実行機構へ登録し、WasmMemoryの所有と利用契約をホスト連携へ渡す。

## 範囲

- **対象**: Core 2.0のメモリ型・limits・32bitアドレス・ページ、1モジュールのメモリ数制約。
- **対象**: スカラーload/store、符号拡張load、狭幅store、size/grow、アドレスとoffset・alignmentの規則。
- **対象**: active/passive data、data count、memory.init/copy/fill、data.drop、初期化と実行の境界trap。
- **対象**: 通常のホスト利用にも意味のあるメモリ生成・取得・内容アクセスの公開契約、所有と同一性。
- **対象外**: memory64、multi-memory、shared memory/threads、SIMDのload/store命令、importの照合と名前解決、WASI。

## 責務の接点

- Instantiate時のdata初期化は本仕様の意味論を使い、後続のホスト連携も同じ処理を呼ぶ。start実行はホスト連携が所有する。
- imported memoryの型・宣言表現は共通moduleの形に合わせるが、解決と同一性の接続はホスト連携が行う。
- SIMDは本仕様のメモリアクセスと境界処理を使い、別の線形メモリを作らない。

## この仕様が所有しないこと

table/elementやreference値、ホストmoduleの登録、spectest、全JSON assertion、OS固有メモリ最適化は所有しない。

## 上流・下流

- **上流**: `wasm-numeric-control`（共通基盤と公式素材を含む）。
- **下流**: `wasm-host-linking`、`wasm-simd`。`wasm-tables-references`とは並行できる。

## 既存仕様との関係

- **拡張する既存仕様**: なし。
- **隣接**: data初期化のtrapをInstantiate共通境界へ返す。メモリimport照合を本仕様とホスト連携で二重実装しない。

## 制約と確認事項

固定公式素材の該当ケースを公開APIで実行する。大きなunsignedアドレスとoffsetをCLRの整数overflowや配列例外の偶然の挙動へ委ねない。grow失敗を一律trapにせず仕様の戻り値を守る。資源制限と機能未実装を混同せず、対応範囲を記録する。文書は日本語（`ja`）。
