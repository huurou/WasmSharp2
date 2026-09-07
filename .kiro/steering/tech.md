---
updated_at: 2026-09-07
---

# 技術方針

## 主要技術

| 対象 | 技術と役割 |
| --- | --- |
| ランタイム | C# / .NET 10（`net10.0`）。NullableとImplicitUsingsを有効にする。 |
| 命令生成器 | `netstandard2.0` / C# 13.0 / Roslynの`IIncrementalGenerator`。通常ビルドで命令情報と実行分岐を生成する。 |
| テスト | .NET 10 / TUnit。ランタイムと生成器を別の実行可能テストプロジェクトで検証する。 |
| 整形 | CSharpierとHusky.Net。.NETローカルツールとして固定し、コミット時にステージ済みの対象ファイルを整形する。 |

パッケージの版は各`.csproj`、ローカルツールの版は[ツールマニフェスト](../../.config/dotnet-tools.json)を正とする。生成器はコンパイラが読み込むため、ランタイムとはTFMを分け、拡張Analyzerルールを有効にする。

## 検証と実行

- `WasmModule`が静的定義と検証成功後の実行コードを所有する。`Validate()`は全体成功時だけ状態を確定し、成功済みの再呼び出しは同じmoduleを返す。判断の根拠は[ADR 0005](../../docs/adr/0005-module-owned-validation-state.md)。
- 型検査と線形化を同一パスで行い、フラットな実行コードを単一の`switch`ループで実行する。制御命令の拡張も[ADR 0002](../../docs/adr/0002-single-pass-linear-interpreter.md)の分岐表現に従う。
- 内部のtrapとexhaustionは`ExecutionResult`で返し、`ExecutionBoundary`で公開例外へ変換する。コンテキストとスタックの復元は`finally`で行う。[ADR 0004](../../docs/adr/0004-trap-result-propagation.md)に従い、startの追加時も共通境界を使う。
- ホスト処理の例外は型と実体を保って伝播する設計契約とする。実ホストcallbackとの統合は後続のホスト連携が所有する（[ADR 0007](../../docs/adr/0007-propagate-host-exceptions.md)）。

## 命令定義とコード生成

`Instructions/InstructionSet.cs`の宣言を命令情報のソースとし、実行handlerと組み合わせて命令一覧と実行分岐を生成する。生成した`switch`からhandlerを直接呼び出し、宣言の重複やhandlerの不整合は生成時の診断で検出する。

ランタイムから生成器への参照は`OutputItemType="Analyzer"`、`ReferenceOutputAssembly="false"`とする。生成器を実行時依存にせず、生成ソースはビルド中間出力として扱う。変更対象は宣言・handler・生成器とし、生成コマンドの別途実行や生成ソースの手管理を要求しない（[ADR 0006](../../docs/adr/0006-generated-instruction-dispatch.md)）。

## 値の所有と同期実行

バイト列や値・型のコレクション入力には`ReadOnlySpan<T>`を使い、保持する定義・型・結果はコピーして`ImmutableArray<T>`等で所有する。後続の呼び出しや元バッファの変更で、返却済みの結果が変わらない契約を保つ。数値の取得・構築ではWasmのビット列を保持する。

実行ポリシーはinstanceが保持し、最外側の呼び出しが開いたWasm実行コンテキストの上限を固定する。同一スレッドの同期的なネスト呼び出しは`[ThreadStatic]`のコンテキストを共有する。保証範囲は単一スレッドでの同期実行であり、並行利用・非同期フローへの伝播は対象外とする（[ADR 0008](../../docs/adr/0008-instance-options-and-execution-context.md)）。

## 開発と検証

環境構築と標準コマンドは[README](../../README.md)を参照する。テスト実行前にReleaseビルドの警告・エラー0を確認し、ビルド済みの両TUnitプロジェクトを`dotnet run --no-build`で実行する。テスト追加・変更時はコマンドでの実行を必須とする。

TUnitはAAA、日本語の条件・期待結果名、`await Assert.That(...)`を使う。絞り込みには`--treenode-filter`を使う。生成器テストは実際のランタイム契約ソースを埋め込むため、対象ファイルの移動・改名ではテストプロジェクトの`EmbeddedResource`も更新する。

ランタイムテストは固定Core 2.0仕様の命令付録をビルド時に埋め込む。基盤テストにはspec submoduleが必要で、WABTのビルドは不要。外部ソースの取得・固定・変換手順は[thirdParties/README.md](../../thirdParties/README.md)に保持する。

公式適合検証では、固定したCore 2.0 profileでWABTの`wast2json`からJSONとモジュール素材を得る。自作ツールの入力はJSONと`.wasm`に限定し、実装進捗で対象集合やfeature flagを減らさない。全体の回帰確認と結果分類は[ロードマップ](roadmap.md)に従う。
