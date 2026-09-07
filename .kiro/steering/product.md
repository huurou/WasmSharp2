---
updated_at: 2026-09-07
---

# プロダクト概要

WasmSharp2は、WasmバイナリをC#から扱うランタイム。初期の完成目標はWebAssembly Core 2.0のバイナリ・検証・実行への準拠とし、利用者が処理段階、値の型、失敗原因を明示的に扱えることを重視する。

## 中核となる方針

- `Decode → Validate → Instantiate → Invoke`を独立した公開操作として提供する。静的なmodule定義、検証成功状態、instance、functionの実体を区別する。
- 値は`WasmValue`、function typeは`WasmFunctionType`で明示する。数値のビット列と参照の同一性を保ち、CLR型からの暗黙変換やdelegateからの型推論を導入しない。
- 破損、検証不成立、リンク不成立、trap、exhaustion、実装上限、APIの誤用、未実装を区別する。未実装で中断した場合は未確認範囲を示し、入力全体が有効であるとは扱わない。
- 公式適合検証のランナーも通常の利用者として公開APIを使う。APIの追加は通常の埋め込み利用に意味のある能力を根拠とする。

## 主な利用場面

C#の呼び出し元が、バイト列またはStreamからmoduleをデコードし、検証してからインスタンス化する。export名からfunctionを取得し、型を明示した値で呼び出す。失敗時には、成立した処理段階と中断原因を呼び出し元が判断できる。

ホスト連携ではfunction typeを明示宣言し、メモリ・テーブル・globalの共有ではリソースの同一性を保つ。具体的な対応は各機能仕様で段階的に実装する。

## 実装範囲と完成目標

2026-09-07時点の最小基盤には、import・引数・localsを必要とせず、`i32`・`i64`・`f32`・`f64`の定数のいずれかを1個返すfunctionの公開4段階が実装されている。対応範囲と進捗の詳細は[基盤タスク](../specs/wasm-runtime-foundation/tasks.md)と[ロードマップ](roadmap.md)を参照する。

数値演算・制御構文・メモリ・テーブル・ホスト連携・SIMDと公式ランナーは後続仕様で扱う。公開型やopcodeの登録、値の保持ができることと、その機能を実行できることを区別する。

Core 2.0全体の完成は、固定した公式テスト集合による検証と仕様規則との対応で判断する。最小経路の成功、素材の変換成功、個別テストの成功だけで全体への準拠を宣言しない。

## 対象外と未決定事項

- ランタイムと自作ツールによるWAT・WASTの解析、WASI、Component Model、JavaScript/Web API、JIT/AOT、既存エンジンへの実行委譲は対象外。
- Core 3.0とCore 2.0外の機能は将来の別計画とする。
- 性能の数値目標、NuGet公開、追加TFM・OSへの対応は未決定。

## 方針の根拠

用語は[CONTEXT.md](../../CONTEXT.md)、公開APIの判断は[ADR 0001](../../docs/adr/0001-explicit-staged-runtime-api.md)、完成目標は[ADR 0003](../../docs/adr/0003-core2-fixed-conformance-profile.md)に従う。仕様ごとの受入条件は個別仕様に保持する。
