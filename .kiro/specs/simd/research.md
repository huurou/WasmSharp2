# 調査と設計判断

## 概要

- 対象: simd
- 調査種別: 既存システムへの拡張。既存API・生成器・メモリ・ランナーの接点に絞って調査した。
- 調査日: 2026-10-18
- 要点: 0xFDの236命令は名前だけ登録済み。v128保持と共通の値移動は既存。異種入力と3入力を表す数値型効果、SIMD即値、lane制約、handlerが必要。
- 使用スキル: kiro-spec-quick、kiro-spec-requirements、kiro-spec-design、kiro-spec-tasks、csharp-conventions、codebase-design。規則は責務分離、既存契約の再利用、挙動追加前の整理、独立したタスクレビューに適用する。完了報告にはkiro-verify-completionを使う。

## 調査記録

### 既存ランタイム

参照: `src/WasmSharp/Instructions/InstructionSet.cs`、`InstructionAttribute.cs`、`NumericSignature.cs`、`src/WasmSharp.Generators/InstructionGenerator.cs`、`src/WasmSharp/Modules/ExpressionDecoder.cs`、`FunctionValidator.cs`、`ConstantInitializer.cs`。

- 0xFD命令は3引数属性で未対応宣言。未知番号のDecode失敗と既知未対応は既に区別される。
- NumericSignatureは同型入力1/2個に限定され、生成器もscalarだけを許す。shift・replaceの異種入力とbitselectの3入力は現契約で表せない。
- v128 selectはFunctionValidatorで明示的に拒否するが、実行側Selectは値を丸ごと選ぶ。
- ConstantInitializerは定数のImmediate.Kindを検査し、Immediateを評価結果とする。v128専用評価器は不要。
- WasmValue.FromV128/AsV128、localsのゼロ初期化、call/control/globalの値移動は既存。
- 結論: 数値型効果の表現を先に整理し、既存ループ・定数式・値保持を拡張する。

### メモリと数値処理

参照: `src/WasmSharp/WasmMemory.cs`、`Execution/Interpreter.Memory.cs`、`Interpreter.Float.cs`、`Interpreter.Conversion.cs`。

- TryRead/TryWriteはulongの範囲を先に検査し、ページ境界をまたぐ。範囲外writeは未変更。
- scalar handlerは最大8byteの変換を担当する。vectorの最大16byteやlane合成は新handlerで扱い、メモリ実体・範囲処理を複製しない。
- 既存のNaN結果化、符号付きゼロを保つ丸め、飽和i32変換は再利用できる。pmin/pmaxは通常min/maxと異なる。
- 結論: lane単位の純粋操作をSimdValueへまとめ、命令の意味論はInterpreterのpartial群へ分ける。

### 固定仕様と236命令

参照: `thirdParties/WebAssembly-spec/document/core/appendix/index-instructions.py`のFD行、`binary/instructions.rst`、`valid/instructions.rst`、`exec/instructions.rst`、`exec/numerics.rst`、`interpreter/binary/decode.ml`、`interpreter/valid/valid.ml`。

| 群 | 件数 |
| --- | ---: |
| メモリ | 22 |
| 定数・lane操作 | 23 |
| v128ビット演算・any_true | 7 |
| 整数比較 | 36 |
| 整数演算・変換・縮約 | 96 |
| 浮動小数点比較 | 12 |
| 浮動小数点演算 | 30 |
| 浮動小数点関連変換 | 10 |
| 合計 | 236 |

仕様の表記差を次のように解決した。

- 付録のload32_zero/load64_zeroにはlaneidxが付くが、binary本文と参照Decodeにはない。即値はmemargだけを採用する。
- store_laneのvalidation本文の文章には結果v128とあるが、推論規則・参照validatorでは結果なし。[設計](design.md)は[i32,v128]→[]とする。
- narrow_uでも入力はsignedとして解釈してからunsignedの範囲へ飽和する。
- alignment上限は出力vector幅ではなくアクセスbyte数を使う。拡張loadは8byte、zeroは4/8byte、laneとsplatはlane幅/8。
- pmin/pmaxは比較で選ぶoperandのビットを保持する。同値・NaN・±0の入力順を確認対象にする。
- q15の最小値同士の飽和、dotのi32折返し、shift量mod lane幅、変換後の上位zeroを受入へ含める。

### 公式ランナーと現在の比較元

参照: `tools/WasmSharp.TestSuiteRunner/Execution/ScriptReader.cs`、`ValueCodec.cs`、`ValueMatcher.cs`、[ランナーガイド](../../../tools/WasmSharp.TestSuiteRunner/README.md)、[tables-references受入](../tables-references/acceptance.md)。

- 元JSONのv128はlane_typeとvalue配列から構築し、結果は上下64bit、期待はlane別のbit/NaNで判定する。保存済みlanesと元のvalue配列を直接同一形式として比較しない。
- SIMD配下は57入力。共通select・global等を含め、受入は147入力全体を対象にする。
- 現在のspec HEAD: `05ca4182176763112561ae20153975c12bd689e4`。
- 現在のWABT HEAD: `c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`。過去のWABT 1.0.42更新時のcommitは現在の固定値として使用しない。
- baseline: `artifacts/tables-references/task-8.4-20261018T090714Z/run-baseline.json`。
- baseline SHA-256: `9d51acea7309d02af302118c932bf25d9725336304f370b48d04cae995176496`。
- manifest: `artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json`。
- manifest SHA-256: `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e`。
- 上記hashは今回の読み取り調査で再計算し一致した。保存記録はpassed=27,482、runtime_unsupported=1,079、blocked=24,269、out_of_scope=1,077、failed/runner_error=0。残る未対応とblockedはSIMD起因と記録されている。今回は再実行していない。
- simd_linking.wastのimmutable v128 global importは固定版でコメントアウトされているため、公開APIの直接テストも必要となる。

### 外部資料の確認

[Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)と[固定commitの実行規則](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/instructions.rst)を確認した。W3Cの分割HTML URLは404だったため採用しない。保存版PDFは版情報が変わり得るので、詳細な命令判断にはローカルの固定commit本文・参照実装・公式ケースを使用した。新しい外部ライブラリやCPU APIの採用はない。

## 構成案の比較

| 案 | 利点 | 不利益・判断 |
| --- | --- | --- |
| 共通signatureと既存ループへ追加 | 型効果・診断・値の経路を維持できる | 生成器の既存scalar回帰確認が必要。採用。 |
| SIMD専用命令表・validator・実行ループ | 局所的に追加しやすい | opcode・型・失敗の規則が重複し、ADR 0002/0006と不一致。不採用。 |
| CPU intrinsicごとのbackend | 性能最適化の余地 | 未要求の分岐・fallback・同値性検証が増える。初期実装では不採用。 |

## 設計判断

### 数値型効果の共通化

同型のscalar入力も異種のvector入力も、順序付きの数値型列と1結果型として表せる。NumericSignatureの内部表現をこれへ揃え、現在必要な1〜3入力だけを扱う。制御命令や関数の動的型列まで一般化しない。既存属性記法は同じ内部表現へ変換するため、型効果の知識を二重保持しない。

### 既存能力の採用と新規実装の範囲

128bit保持、定数式、メモリ範囲検査、数値補助、公式v128比較は既存能力を使う。新規実装はlaneのビット操作と固定SIMDの意味論に限定する。既存engineへの委譲はプロジェクト範囲外であり、汎用backendや公開vector型は作らない。

### 機能接続と受入

未接続handlerの単体確認と、命令宣言へ接続する統合作業を分ける。対応済み宣言を先に出して仮handlerで成功させない。機能を接続した単位で全体run・比較を行い、最後の単一runへverifyを適用する。baselineの保存を成功判定の代用にしない。

## リスクと対策

- 236命令の漏れ: 固定付録のopcode集合と生成metadataを照合し、規則・実装・公式ケースを対応付ける。
- 既存型効果の回帰: 挙動追加前にscalarの生成器・validatorテストを通す。EmbeddedResourceも変更と一緒に更新する。
- 浮動小数点の微妙な違い: bit操作と演算を分け、NaN、±0、丸め、operand順を別々に確認する。
- メモリ幅の誤り: ByteWidthをalignmentとアクセス範囲に使用し、lane保持・部分書込みなしを検証する。
- 受入の混同: SIMD機能受入、全体verify、baseline保存の結果を区別する。

## 参照

[ブリーフ](brief.md)、[要件](requirements.md)、[技術方針](../../steering/tech.md)、[ロードマップ](../../steering/roadmap.md)、[ADR 0002](../../../docs/adr/0002-single-pass-linear-interpreter.md)、[ADR 0004](../../../docs/adr/0004-trap-result-propagation.md)、[ADR 0006](../../../docs/adr/0006-generated-instruction-dispatch.md)、[ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)。


## 生成時のレビュー

2026-10-18に自動モードで実行した。

- 要件生成ゲート: inlineでPASS。8領域・51受入基準の範囲、EARS形式、観測可能性を確認した。
- 設計生成ゲート: independentでPASS。既存コード、固定仕様、責務と51項目の対応を確認した。
- タスクの網羅性: independentでPASS。
- タスクグラフ: independentの初回はNEEDS_FIXES。未接続命令の新規テストを接続後へ移し、最終検証から原因担当への修正の戻し方を明記した。1回の修正後の再レビューはPASS。
- 最終整合性レビュー: 履歴を引き継がない別エージェントが、保存済みのrequirements.md・design.md・tasks.mdを直接読み、PASS。
- 機械チェック: 全51項目の設計・タスク対応、10大項目・36実行単位、依存参照、ローカルリンク、未置換項目、空白とmetadataの整合を確認し、欠落0件。
- 任意のkiro-validate-gapとkiro-validate-designは実行していない。上記は生成時の内部レビューである。
- spec.jsonの承認は--autoによる計画の承認を表す。実装、ビルド、TUnit、公式スイートの再実行やbaseline更新は今回行っていない。
