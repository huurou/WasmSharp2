---
updated_at: 2026-09-07
---

# プロジェクト構成

## 配置の原則

単一のドメイン文脈でWasmのランタイムを構成する。公開契約をランタイムプロジェクトのルートへ置き、内部処理はmodule処理・命令定義・実行などの責務でまとめる。機能追加では既存の境界へ関連する型と処理を同居させ、技術レイヤーや型の役割だけで全体を分割しない。

## 主要な配置パターン

| 配置 | 責務と例 |
| --- | --- |
| `src/WasmSharp/` | 利用者向けの値・型・操作。例: `WasmModule`、`WasmValue`。公開例外は`Exceptions/`にまとめる。 |
| `src/WasmSharp/Modules/` | 静的定義、バイナリ解析、型検査と線形化。例: `ModuleDecoder`、`ModuleValidator`。 |
| `src/WasmSharp/Instructions/` | 命令宣言と識別情報。例: `InstructionSet`。 |
| `src/WasmSharp/Execution/` | 線形コード、フレームと値スタック、実行ループ、公開呼び出しの境界。例: `Interpreter`、`ExecutionBoundary`。 |
| `src/WasmSharp.Generators/` | ランタイムの命令宣言を処理するビルド時生成器。 |
| `tests/<対象プロジェクト>.Tests/` | 対象プロジェクトごとのテスト。内部の機能フォルダを対応させ、共通入力・補助型は`Fixtures/`に置く。 |
| `thirdParties/` | 固定した公式仕様と変換ツールの上流ソース。取得元と採用版は同ディレクトリのREADMEで管理する。 |

`tools/`は今後の公式テスト素材生成・ランナーの配置先で、現時点では実装を持たない。ツール側のJSON処理・期待値比較・spectestはランタイムから分離する。

## 公開境界と依存関係

- 利用者向けの契約を`public`とし、内部実装は`internal`以下に保つ。テストによる内部参照には対象テストプロジェクトへの`InternalsVisibleTo`を用いる。
- `WasmModule`は静的定義と実行コード、`WasmInstance`は実行時の実体を所有する。functionは所属instanceと関数indexで定義・実行コードへ到達する。
- instanceの公開取得操作は`GetFunction`などの名前による操作とする。`WasmInstance`に`Exports`コレクションを追加しない。
- 命令宣言とhandlerからの生成方式、生成器のビルド時参照は[技術方針](tech.md)に従う。ランタイムをテスト・公式ランナー・WABTへ依存させない。
- 通常ビルドの生成ソースは`obj/`、外部ツールのビルドや変換成果物は`artifacts/`等の出力先に置き、手書きの正本と分ける。

## 名前空間と命名

- 名前空間はプロジェクト名とフォルダ階層に合わせる。例: `WasmSharp.Modules`、`WasmSharp.Tests.Modules`。手書きソースはfile-scoped namespaceを使い、`using`を先頭に置く。
- 型名と通常のメンバー名はPascalCase、非公開フィールドはcamelCaseに末尾`_`、`const`はUPPER_SNAKE_CASEとする。識別子は英語、説明コメントは日本語で記述する。
- テストクラスは`対象クラス_対象メソッドTests`、テストメソッドは日本語の`条件_期待される挙動や出力`とする。テストコードにドキュメントコメントを付けない。

## 文書の配置

ドメイン用語は[CONTEXT.md](../../CONTEXT.md)、確定した設計判断は[docs/adr/](../../docs/adr/)に保持する。Wasmの説明ではCore仕様の`value`・`function`・`function type`・`host function`等の用語を基準にし、独自の言い換えを増やさない。

プロジェクト全体の方針は`.kiro/steering/`、個別機能の要件・設計・タスクは`.kiro/specs/<feature>/`へ置く。新しいファイルの追加だけではステアリングを更新せず、配置原則や責務の境界が変わる場合に更新する。
