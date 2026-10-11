# numeric-controlの調査記録

前半は要件未承認時に実施したギャップ分析を保持した記録である。2026-10-09の設計調査と決定は末尾の「設計調査」にまとめる。現在の承認状態は`spec.json`を参照する。

## 分析の位置付け

`kiro-validate-gap numeric-control`として、[要件文書](requirements.md)の10要件・73受入基準と現在のコードを静的に照合した。ランタイム、数値意味論、公式受入を別々に調査し、以下へ統合した。本書は設計の判断材料であり、設計の確定や実装検証のGOを示すものではない。

- 対象HEAD: `39a2748a10a606678e4d4c847ae4d8baf7fe9d1a`
- 要件の状態: `requirements-generated`、`approvals.requirements.approved=false`。要件未承認のまま分析を進め、承認状態は変更していない。
- 読み込んだ方針: `CONTEXT.md`、core steering、roadmap、関連ADR、C#規則。先行仕様との分担は現在の要件とロードマップで照合した。
- 固定spec: `05ca4182176763112561ae20153975c12bd689e4`。固定WABT: `03a00a1334e6121fb0cce4fccbd6bb109b68acaa`。今回、`git ls-files --stage`と`git submodule status`でgitlinkとcheckoutの一致を確認した。
- 調査範囲: 現在のソース・テストの読取り、固定仕様・参照実装・公式テストとの照合、既存受入記録・baselineの読取り、Microsoftの一次資料確認。
- 実施していないこと: 製品コード・テストの変更、ビルド、テスト実行、公式スイート再実行、baseline更新、設計・タスク生成。

## 分析要約

- 公開4段階、値の保持、関数呼出し、ホスト連携、start、深さ制限、公式ランナーは再利用できる。新しい公開APIや別の実行エンジンを必要とする根拠は見つかっていない。
- const以外の対象数値命令と構造化制御・selectは未実装。命令番号の登録済みを、デコード・検証・実行対応済みと扱わない。
- 最大の構造上の不足は、入れ子式、複合即値、制御ごとの型検証状態、線形化した分岐情報である。現在の関数底だけの多相性と、endが必ず関数を終了する前提を拡張する必要がある。
- 数値trapのReasonは既存だが、数値trapの公開診断文は不足している。NaN、符号付き0、整数変換境界は.NET演算への単純な委譲だけでは保証できない。
- 混合案を設計時の有力候補とする。工数はXL、リスクはHigh。公式runの終了0に加え、本仕様の未対応が残っていないことを確認する受入証拠が必要である。

## 1. 既存資産と拡張する箇所

以下の行番号は調査時点のもの。ソースへのリンクはリポジトリ内の該当ファイルを示す。

| ID | 既存資産・根拠 | 確認できたことと拡張点 |
| --- | --- | --- |
| E1 | [InstructionSet](../../../src/WasmSharp/Instructions/InstructionSet.cs) 8〜144・172〜310行、[InstructionAttribute](../../../src/WasmSharp/Instructions/InstructionAttribute.cs) 65〜74行 | end、unreachable、call、return、drop、locals/global、4種constの14命令が実行対応。数値・制御・selectの3引数宣言は即値・型規則・handlerが未対応。 |
| E2 | [ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs) 29〜69・219・381〜443行 | byte列/Streamが使う解析と診断再走査は共通。式読取りは最初のelse/endで停止する。global初期化式も同じ読取りを使うため、関数だけを拡張する別parserは不要。 |
| E3 | [ModuleBinaryReader](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs) 270〜305・341〜374行、[DecodedInstruction](../../../src/WasmSharp/Modules/DecodedInstruction.cs) | LEB128の内部処理は幅を受け取るがs33の入口はない。デコード済み命令はopcode、WasmValue、単一index、ByteOffsetだけで、複合即値の保持が不足する。 |
| E4 | [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs) 227〜475行 | 1回の巡回で型検査と線形化を行う。状態は`List<WasmValueKind>`と関数全体の`bool unreachable`。数値の型効果、制御frame、ラベル、未知operand型の保持が不足する。 |
| E5 | [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs) 20〜38・80〜170行、[WasmModule](../../../src/WasmSharp/WasmModule.cs) 234〜249行 | 定数式の許可命令を独立に検査し、module全体の成功後だけFunctionCodes等を公開確定する。関数内の対応拡大を定数式の許可拡大にしない。 |
| E6 | [Instruction](../../../src/WasmSharp/Execution/Instruction.cs)、[FunctionCode](../../../src/WasmSharp/Execution/FunctionCode.cs)、[ExecutionFrame](../../../src/WasmSharp/Execution/ExecutionFrame.cs) | 実行コードは不変配列で、元ByteOffsetと最大operand数を保持。frameのPc、StackBase、OperandBaseは再利用できる。分岐先・保持値数・分岐表は未保持。 |
| E7 | [InterpreterContext](../../../src/WasmSharp/Execution/InterpreterContext.cs) 193〜222・268〜285・326〜332行 | 関数終了時の結果移動と除去参照の解除が既存。分岐によるPc変更と、同じ関数内での値移動を追加する必要がある。 |
| E8 | [Interpreter](../../../src/WasmSharp/Execution/Interpreter.cs)、[ExecutionBoundary](../../../src/WasmSharp/Execution/ExecutionBoundary.cs) 112〜154行、[WasmTrapReason](../../../src/WasmSharp/Exceptions/WasmTrapReason.cs) 16〜26行 | 単一ループ、直接call、同期再入、finally復元、trap/exhaustion変換を再利用可能。数値Reasonはあるが、公開trap診断はUnreachable以外が共通文言。 |
| E9 | [InstructionGenerator](../../../src/WasmSharp.Generators/InstructionGenerator.cs) 175〜224・344〜393行 | 命令宣言から直接handler呼出しを生成し、失敗時は停止する。handlerはInterpreter上の固定signature。7引数宣言は即値・型効果・検証規則・handlerの全対応を要求する。 |
| E10 | [WasmValue](../../../src/WasmSharp/WasmValue.cs) 53〜169行、[WasmValueTests](../../../tests/WasmSharp.Tests/WasmValueTests.cs) | 整数、浮動小数点の生ビット、v128、参照を保持する。符号操作・再解釈・制御による運搬に使える。数値演算自体の適合を証明するものではない。 |
| E11 | [SuiteExecutor](../../../tools/WasmSharp.TestSuiteRunner/Execution/SuiteExecutor.cs)、[ValueMatcher](../../../tools/WasmSharp.TestSuiteRunner/Execution/ValueMatcher.cs)、[AssertionJudge](../../../tools/WasmSharp.TestSuiteRunner/Execution/AssertionJudge.cs) | 全command実行、spectest/register、値・NaN・診断の判定は既存。ランタイム内部へのhookや判定基準の変更は不要。 |
| E12 | [BaselineComparer](../../../tools/WasmSharp.TestSuiteRunner/Baselines/BaselineComparer.cs)、[CompletionPolicy](../../../tools/WasmSharp.TestSuiteRunner/Reports/CompletionPolicy.cs)、[CLIガイド](../../../tools/WasmSharp.TestSuiteRunner/README.md) | 固定素材/profileの照合とpassedの退行・欠落検出を再利用できる。run/compare-runの終了0とCore 2.0全体のverify成功は別条件。 |

### 維持する設計上の制約

- [ADR 0002](../../../docs/adr/0002-single-pass-linear-interpreter.md): 型検査と線形化を同一パスで行い、分岐を`(targetPc, stackHeight, keepCount)`へ落とす。実行時に入れ子ツリーを走査しない。
- [ADR 0006](../../../docs/adr/0006-generated-instruction-dispatch.md): 命令情報と実行dispatchを一つの宣言から生成する。数値の入出力型を別のopcode一覧へ重複して手書きしない。
- [ADR 0004](../../../docs/adr/0004-trap-result-propagation.md): 内部trapはExecutionResultで伝播し、共通境界だけで公開例外へ変換する。
- [ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md): 固定期待診断へのOrdinal前方一致と、確認済みの原因選択規則を維持する。公式ケースID・期待文字列をランタイムへ渡さない。
- リソース、ホスト登録、深さ制限、startの順序は先行実装を利用する。loopの反復を関数呼出し深さとして数える設計にはしない。
- 内部の配置は既存の`Modules`・`Execution`・`Instructions`へ合わせる。汎用の差替え機構や新しい技術レイヤーを設けない。

## 2. 要件と既存資産の対応

分類は`Missing`=実装または証拠の不足、`Unknown`=設計時の追加調査、`Constraint`=維持すべき既存契約を表す。「再利用可能」は今回のテスト成功を意味しない。

| 受入基準 | 資産 | ギャップと制約 |
| --- | --- | --- |
| 1.1〜1.4 | E1〜E3 | **Missing**: s33、blocktypeの3形式、入れ子else/end、br_table候補列、select型vector。定数と0xFC後のu32読取りは既存。非最短符号化、切断、式/section境界を拡張対象へ接続する。 |
| 1.5〜1.6 | E2・E5 | **Constraint**: 検証成功の一括確定と未確認範囲。**Unknown**: 共通制御/selectをDecode可能にした後の、参照/v128のselect等に対する未対応通知の段階と位置。 |
| 2.1〜2.2 | E1・E4・E9 | **Missing**: 単項・二項・比較・変換の入力型/個数と結果型。eqz・比較結果はi32。暗黙変換を追加しない。 |
| 2.3〜2.5 | E3・E4 | **Missing**: 両selectの型規則。型vector長0/複数を保持してValidateで拒否する。型注釈なしの参照候補は型不成立、後続仕様の有効な型のselectは未対応として区別する。 |
| 2.6 | E2・E5 | **Constraint**: 定数式の適格性検査を保持。**Missing**: 対応を広げた数値・制御・selectが関数では使えても定数式では拒否される証拠。 |
| 3.1〜3.3 | E3・E4 | **Missing**: 型添字、制御引数/結果、if両側の検証、else省略時の空列検証。条件が定数でも片側を省略しない。 |
| 3.4〜3.6 | E4 | **Missing**: 関数の暗黙ラベルまでの深さ解決、loop引数とblock/if結果の区別、br_ifの値復元、br_tableの全候補検査。未知operand型の適合を具体的ラベル型の一律一致に置き換えない。 |
| 3.7〜3.8 | E4・E6 | **Missing**: 制御ごとのスタック底と到達不能状態、通常終了時の余剰検査。未知型と具体型の矛盾を区別し、内側の到達不能を外側へ漏らさない。最大operand数も新しい経路に合わせて計算する。 |
| 3.9 | E4・E10 | **Constraint**: 共通の全7値型を制御で扱う。**Missing**: v128・funcref・externrefを運ぶ制御の型検証。 |
| 4.1〜4.7 | E1・E8・E10 | **Missing**: 全整数演算handlerと境界証拠。通常のwrap、0除算、div_sのMIN/−1、rem_sのMIN/−1→0、幅によるシフト/回転、符号付き/なし比較を区別する。 |
| 5.1〜5.8 | E1・E10 | **Missing**: 全浮動小数点演算handler。**Unknown**: Math/MathFで満たせる範囲と、NaN・符号付き0・各命令の丸めを補う最小の処理。 |
| 6.1〜6.2 | E1・E10 | **Missing**: 整数幅の変換と符号拡張。符号付き/なしの解釈と、上位bitの破棄を明示する。 |
| 6.3〜6.5 | E8・E10 | **Missing**: trunc/trunc_satの全組合せ。**Unknown**: 変換元で表せる隣接値による範囲判定。NaNのReasonと、それ以外のoverflowを分ける。 |
| 6.6〜6.7 | E10 | **Missing**: convert/demote/promote/reinterpret。**Unknown**: i64/ulong→f32の二重丸め回避。再解釈は既存の生ビット操作で構成できる。 |
| 7.1〜7.7 | E4・E6〜E9 | **Missing**: nop、制御markerの線形化、前方分岐の解決、if/br/br_if/br_tableの実行。br_tableの選択値は符号なしとし、空候補も既定分岐へ送る。 |
| 7.8〜7.9 | E6〜E10 | **Missing**: 抜ける範囲だけの一時値破棄と全値型の順序保持。**Constraint**: OperandBaseとStackBaseを区別し、locals・外側operand・呼出し元の値と参照の同一性を維持する。 |
| 8.1〜8.2 | E4・E8・E10 | **Missing**: select handlerと副作用順序の証拠。候補計算の短絡化は行わず、選択値をビット列のまま渡す。 |
| 8.3〜8.6 | E5〜E10 | **Constraint**: call・return・global共有、関数の所属instance、ホスト例外、start失敗前の副作用と保存参照。**Missing**: 数値・構造化制御との組合せの確認。 |
| 9.1〜9.3 | E2・E4・E8 | **Missing**: 新しい構文/型診断と数値trapの検出・公開診断。既存Reason、例外型、Invoke/startの段階、関数index・ByteOffsetを接続する。 |
| 9.4〜9.5 | E7・E8 | **Constraint**: 既存の深さ制限とfinally復元。**Missing**: 制御を含む直接/相互再帰、同期再入、trap/exhaustion/ホスト例外後の独立した呼出しの証拠。 |
| 9.6〜9.7 | E2・E4・E8・E11 | **Missing**: 新たに到達できる複合不正の診断選択の照合。**Constraint**: 診断再走査の条件・通常入力への一般化・未確認範囲を保つ。 |
| 10.1〜10.3 | E11・E12 | **Missing**: 実装後の147入力全体結果と、S1〜S8→要件→実装→公式/直接テストの対応。ランナーの値・診断判定は再利用する。 |
| 10.4〜10.6 | E11・E12 | **Missing**: 残存未対応の所有先とblocked起点の更新、本仕様の未実装が残らない監査、現baselineとの回帰比較。run終了0だけで完了としない。 |
| 10.7〜10.8 | E12 | **Constraint**: 不一致を修正して全体を再実行・再比較し、受入後だけbaselineを明示更新する。Core 2.0全体の完成と区別する。 |

## 3. 重点となる統合課題

### 3.1. 入力表現と線形実行表現

複合即値の保持は新しい内部表現が必要である。`DecodedInstruction`へ不変payloadを追加する案と、関数所有の表に格納して添字で参照する案がある。blocktypeをWasmValueへ無理に詰めたり、型vectorの長さ情報をDecodeで捨てたりしない。

現在のendは`ValidationRule.FunctionEnd`と`Interpreter.Return`へ結び付いている。内側endまでそのまま実行命令へ移すと関数を終了してしまう。block/loop/else/endを線形化時に消すか、必要な動作へ変換するかは設計で決める。型検査のパスを増やさず、前方分岐の未確定先を制御終了時に解決する必要がある。

現在の命令builderは入力数で容量を確保し、`MoveToImmutable()`で確定する。命令を省略・展開する方式では出力数が入力数と一致しなくなるため、この前提も見直す。実行命令と分岐表はmodule所有の不変データに確定し、instanceごとのPcや値スタックと分ける。

### 3.2. 検証状態と値の受渡し

制御frameには入口の高さ、引数型、結果型、ラベル型、到達不能状態が必要になる。実行時の関数frameを増やすこととは別の責務である。既存Popは未知型を供給できても戻り値を持たないため、selectやbr_tableで未知型を保存・照合する操作には不足する。

重点例は、elseなしの引数付きif、到達不能後の具体型不一致、内側blockが外側の値を消費する不正、関数の暗黙ラベル、br_ifの非分岐、`unreached-valid.wast`の`meet-bottom`である。br_tableをラベル型の単純な完全一致で検証しない。

実行時の分岐は、検証で確定した高さを現在のOperandBaseへ対応付け、末尾のkeepCount値を順序どおり移し、不要範囲の参照を解除してPcを更新する必要がある。既存CompleteFrameは関数終了を伴うため、そのままloop分岐へ流用する契約ではない。元のByteOffsetを維持し、分岐後のtrapも入力命令の位置で説明できるようにする。

### 3.3. 宣言と生成器の契約

現在は「即値・検証・実行がすべて対応済み」か「すべて未対応」の宣言契約である。線形化で消える制御markerや、Decodeした後で値型により未対応となるselectをどう表現するかは未確定である。必ず生成器を変更するとは決めず、現在のhandler契約を維持できる表現と比較する。

数値の入力型/結果型をStackEffectKind等で表す場合も、同じ知識をdecoder・validator・generatorへ別々に列挙しない。handlerを部分クラスへ分ければInterpreter上の契約を保てる。別の内部型へ意味論を置く場合は、handlerから狭い操作で呼び出し、汎用の演算backendやDI契約は導入しない。

### 3.4. 数値意味論と.NET

| 領域 | 確認した境界と設計への含意 | 固定した根拠 |
| --- | --- | --- |
| 整数 | MIN/−1はdiv_sではtrap、rem_sでは0。0除算と通常overflowも区別してExecutionResultへ接続する。 | [numerics.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/numerics.rst) 303行以降、[i32.wast](../../../thirdParties/WebAssembly-spec/test/core/i32.wast) 66・109行 |
| NaN・符号 | abs/neg/copysignは符号bitだけを操作する。それ以外のNaN生成はcanonical/arithmetic条件を満たす必要がある。min/maxはoperand順を変えても±0の規則を守る。 | [numerics.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/numerics.rst) 1031・1233行以降、[fxx.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/fxx.ml) 172・190行 |
| 丸め | nearestのties-to-evenと負の0、各f32/f64命令ごとの丸め、非正規化数を確認する。複数命令を結合した演算へ置換しない。 | [f32.wast](../../../thirdParties/WebAssembly-spec/test/core/f32.wast) 2505行以降、[float_exprs.wast](../../../thirdParties/WebAssembly-spec/test/core/float_exprs.wast) 17行以降 |
| trunc | −2147483648.9→i32_s、−0.9→i32_uは切捨て後に範囲内。変換元で表せる隣接値を使い、NaNと範囲外を分ける。 | [conversions.wast](../../../thirdParties/WebAssembly-spec/test/core/conversions.wast) 123・148行、[i32_convert.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/i32_convert.ml) 5・25行 |
| i64変換 | 2^63・2^64境界と、i64/ulong→f32での二重丸めに注意する。全変換をdouble経由へまとめる案は追加調査が必要。 | [i64_convert.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/i64_convert.ml) 29行以降、[f32_convert.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/f32_convert.ml) 27行以降 |
| trap診断 | `integer divide by zero`、`integer overflow`、`invalid conversion to integer`へのReason別変換を既存公開境界へ追加する。 | [eval.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/eval.ml) 35行以降、E8 |

Microsoftの資料では、.NET 9以降のx86/x64におけるfloat/doubleからint/uint/long/ulongへの変換は飽和動作である。したがって.NET 10のcast結果だけからWasmのtruncの成功/trapを判定できない。castやBCLを使う範囲と、その前後でWasm規則を補う範囲の確認が必要である。[浮動小数点から整数への変換](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/9.0/fp-to-integer)

C#の符号付き最小値÷−1はuncheckedでも例外になり得るため、内部trapを.NET例外で表す根拠にはできない。[C#算術演算子](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators)

Math/MathF、BitOperations、WasmValueの既存ビット操作を利用候補とする。現時点で追加NuGet依存を必要とする証拠はない。対象環境でのNaN・丸め・変換の動作実験は設計段階へ残す。

### 3.5. 診断と未対応の境界

通常解析は宣言範囲内で行い、境界不正が確定した場合だけ同じ構文処理をDiagnosticモードで再走査する既存方式を維持する。新しい命令を読むと再走査が以前より先へ進むため、元の診断を保持すべき場合と、参照実装に合わせた診断を選ぶ場合の回帰確認が必要になる。

数値・制御をglobal初期化式で読めるようになっても、許可命令の検査はValidate側に残す。型付きselectの長さ不正もValidateで扱う。後続機能が混在する入力では、まだ調べていない部分まで有効と宣言せず、未対応の機能・段階・元位置・未確認範囲を保持する。

## 4. 実装方針の比較

以下はいずれも公開4段階と単一実行ループを維持する案である。AGENTS.mdに従い、責務整理が必要な箇所は振舞いを保って先に整理し、その後で命令の対応範囲を広げる。

| 案 | 内容・主な変更箇所 | 利点 | 負担・適する条件 |
| --- | --- | --- | --- |
| A:既存型を中心に拡張 | ModuleDecoder/ModuleValidator内の関数処理をメソッドや非公開の状態へ整理し、同じ型へ制御解析・検証を追加。Interpreterの部分クラスへhandlerを追加し、Instruction類を拡張する。 | 型間の受渡しが少なく、既存経路を追いやすい。公開契約と生成器への変更を抑えられる可能性がある。 | 現在791行のModuleValidatorへ制御状態と分岐解決が集中する。型効果や数値の特殊処理が大きなswitchへ集まりやすい。整理後も見通しが保てる場合の候補。 |
| B:関数処理を内部の型へ分離 | 既存の関数内検証・線形化を一つの内部型へ移し、制御状態もそこに閉じる。式読取り、数値意味論も必要な責務ごとに内部型へ置き、既存入口から呼ぶ。 | module全体の検査と関数内状態を分けられる。複雑な処理と不変条件を狭い操作に閉じ込めやすい。 | 最初の移動範囲と内部契約が大きい。単に通過するだけの型、検証モデルの二重保持、数値専用の別実行経路を作らない注意が必要。 |
| C:共通機構を拡張し、状態と数値処理を必要な範囲で分離 | reader、module全体の検査、生成dispatch、公開境界は再利用。関数内の型検査/線形化状態を先に整理し、制御frame・分岐情報を追加。handlerは意味論ごとにまとめ、NaN/変換の共通知識だけを内部処理へ集約する。 | 既存資産の利用と責務の分離を両立し、変更が数値・制御からホスト連携へ広がるのを抑えられる。 | 移す状態と残す処理の境界を先に決める必要がある。分離を曖昧にすると関数状態が複数箇所へ散る。 |

**設計時の有力候補はC**。主な不足が関数内状態と数値意味論へ集中し、呼出し・ホスト境界・ランナーは既存のまま利用できるためである。採用は未確定とし、複合即値、制御marker、生成器との接点を具体化してA/Bと比較する。

別インタープリタの新設、外部Wasmエンジンへの実行委譲、検証と線形化の二重パス化は、既存ADRと対象範囲に合わない。機能flagの縮小も比較案に含めない。

### 実装順序を設計する際の材料

1. 既存の関数内検証/線形化とhandler配置を、振舞いを変えずに整理する。既存の関数・診断・再入契約を保つ。
2. 命令宣言、複合即値、検証用制御frame、実行分岐情報の契約を合わせる。共有ファイルの並行編集は避ける。
3. 数値・選択・構造化制御をそれぞれDecodeからInvokeまで接続する。数値trapは公開境界まで含めて実装する。
4. 全値型の運搬、call/return、global、start、再入との組合せを確認し、固定公式スイート全体とbaseline比較で受け入れる。

この順序はタスク一覧や実装承認の代わりではない。

## 5. 検証資産と不足する証拠

### 既存テストの利用と更新

| 対象 | 利用できるテスト | 追加・更新が必要な範囲 |
| --- | --- | --- |
| Decode | [ModuleBinaryReaderTests](../../../tests/WasmSharp.Tests/Modules/ModuleBinaryReaderTests.cs)、[WasmModule_DecodeTests](../../../tests/WasmSharp.Tests/WasmModule_DecodeTests.cs)、[DecodeFailureTests](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_DecodeFailureTests.cs) | s33、型vector、ラベル列、入れ子終端、byte列/Stream、境界再走査。i32.addやblockを未対応の例に使う既存ケースは、変更後も元の検証目的を保てる入力へ見直す。 |
| 型検査・線形化 | [ValidateFunctionTests](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateFunctionTests.cs)、[ValidateUnreachableTests](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateUnreachableTests.cs) | 局所的な多相性、全分岐の型、複数値、else省略、br_tableの未知型、分岐先・高さ・最大operand数、失敗時に検証成功を確定しないこと。 |
| 命令宣言・生成 | [InstructionSet_TryGetTests](../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs)、[InstructionGenerator_InitializeTests](../../../tests/WasmSharp.Generators.Tests/InstructionGenerator_InitializeTests.cs) | 現在の実行対応14命令一覧を新しい範囲へ更新。宣言契約を変える場合は生成診断と埋込み契約ソースを合わせる。固定仕様のopcode割当との照合は維持する。 |
| 数値・値運搬 | [WasmValueTests](../../../tests/WasmSharp.Tests/WasmValueTests.cs)、[InvokeFunctionBodyTests](../../../tests/WasmSharp.Tests/WasmFunction_InvokeFunctionBodyTests.cs)、[Interpreter_ReturnTests](../../../tests/WasmSharp.Tests/Execution/Interpreter_ReturnTests.cs) | 数値の境界、NaN分類、生ビット、複数値の移動、null/非null参照の同一性、loopの繰返し時に値が蓄積しないこと。 |
| 先行機能との統合 | [InvokeReentryTests](../../../tests/WasmSharp.Tests/WasmFunction_InvokeReentryTests.cs)、[InvokeExhaustionTests](../../../tests/WasmSharp.Tests/WasmFunction_InvokeExhaustionTests.cs)、[InstantiateStartTests](../../../tests/WasmSharp.Tests/WasmModule_InstantiateStartTests.cs) | selectの非短絡評価、分岐前後の副作用、importした関数の所属instance、start中の数値trap、再帰・再入・失敗後の復元。 |
| 公開診断 | [ExecutionBoundary_ThrowIfFailedTests](../../../tests/WasmSharp.Tests/Execution/ExecutionBoundary_ThrowIfFailedTests.cs) | 数値Reasonにも汎用診断を期待する既存テストを更新。Invoke/start双方でReason・先頭診断・位置・段階を確認する。 |

固定公式テストは、要件文書の代表テスト一覧とS1〜S8を起点に対応付ける。TUnitは公式判定を重複して作り直す用途ではなく、公式JSONからは確認できない公開Reason・位置・ホスト例外の同一性・非null funcrefの同一性などを補う。ValueMatcherは固定JSON形式の非null funcref期待値を扱わないため、制御を通る関数参照の同一性は公開APIの直接テストで確認する。

### 現在の比較元

今回読み取った保存済み結果は次のとおり。**新しい実行結果ではない**。

| 項目 | 確認値 |
| --- | --- |
| 素材manifest | `artifacts/test-suite-runner-acceptance-20261001/portable/corpus/manifest.json` |
| 実行結果baseline | `artifacts/test-suite-runner-acceptance-20261001/run-baseline.json` |
| run ID | `e97acfbc-387f-41de-9366-dcd3b15a5578` |
| 保存済み実行日時 | `2026-10-04T11:47:08Z` |
| SHA-256 | `0f47b1fc0b648b8e539297a60a3ef5a36c7d78c79ef3f8550ef86b10a5814285`（今回再計算） |
| 件数 | 147入力・53,907command。処理/出力完了、未処理と入力異常は0。 |
| 内訳 | passed=2,491、failed=0、runtime_unsupported=2,987、runner_error=0、out_of_scope=1,077、blocked=47,352 |

保存の経緯は[追加修正の受入記録](../../../artifacts/test-suite-conformance/illegal-opcode-20261004/review-summary.md)と[完了確認](../../../artifacts/test-suite-conformance/illegal-opcode-20261004/completion-proof.json)にある。初回handoffや、その後の修正前runを現在のbaselineと取り違えない。実装着手時には同じファイルが更新されていないか再確認する。

[remediation.json](../test-suite-conformance/remediation.json)のT001はnumeric-controlへ、最初に遭遇した未対応132種類・965ケースと依存blocked13,854ケースを引き継いでいる。これは命令対応後のpassed増分の予測ではない。同じケースで次のmemory/table/SIMD未対応や別の不正が見つかり得る。所有先の固定件数を受入条件へ流用せず、新しい結果から原因を追跡する。

### 実装後の受入で必要なもの

1. 警告・エラー0のReleaseビルド後に、変更対象と既存回帰範囲のTUnitをコマンドで実行する。最終受入ではランタイム・生成器・ランナーの3プロジェクトを確認する。
2. 既存ランナーの`run`で固定147入力すべてを実行し、結果出力の完了を確認してから、直前baselineとの`compare-run`を行う。新しい結果は既存証跡と別の出力先へ保存する。
3. failed・runner_error・回帰0に加えて、本仕様と先行機能で前提が揃うケースがpassedであることを確認する。残るruntime_unsupportedは後続仕様の機能と根拠を持ち、blockedは元の原因へたどれる必要がある。
4. S1〜S8と全対象命令・型規則・境界条件の対応を記録する。最初の未対応命令が消えたことだけで対象範囲の完成としない。
5. 不一致を修正した場合は全体を再実行・再比較する。受入条件を満たした保存結果に対してのみ`baseline-save`を明示的に実行する。

`CompletionPolicy`は残存未対応がnumeric-controlか後続仕様かを判定しない。したがってrun/compare-run終了0だけでは本仕様の完成を証明できない。所有先の確認は仕様側の受入記録へまとめる案が最小であり、ランナーへ仕様固有の完成判定を埋め込む必要性は現時点ではない。

## 6. 規模とリスク

- **工数: XL（2週間以上の区分）**。数値命令群だけでなく、デコード表現、局所的な型検査、線形化、生成器との整合、ホスト連携との統合、公式受入にまたがる。これは設計前の相対見積もりで、日程の確約ではない。
- **リスク: High**。NaN/変換境界、入れ子の多相性、分岐時のスタック保持、診断再走査と原因選択が相互に影響する。公開APIとランナーを再利用できる点はリスクを抑える材料である。
- **外部依存: 追加の必要性は未確認**。固定仕様・WABT・既存ランナーと.NET 10を利用する。新しい数値ライブラリや実行backendの選定は行っていない。

## 7. 設計段階へ引き継ぐ調査

| ID | Research Needed | 設計で確認する内容 |
| --- | --- | --- |
| R1 | 複合即値の表現 | blocktype・br_table・型vectorの不変な所有方法、元位置、保持上限、byte列/Streamと診断再走査での共通利用。 |
| R2 | 関数内の検証状態 | 制御frame、未知operand型、br_if/select/br_tableの型適合、elseへの切替、定数式との責務分離。 |
| R3 | 線形化と生成器 | markerの消去/変換、内側endと関数終端、前方分岐の解決、分岐表、出力命令数、実行opcodeと型効果の宣言契約。 |
| R4 | 分岐時の値操作 | OperandBaseとの相対高さ、keepCountの順序保持、重なりのある移動、参照解除、関数の暗黙ラベルとCompleteFrameの接続。 |
| R5 | 浮動小数点の最小実装 | Math/MathFで満たせる規則、必要なNaN補正、signed zero、非正規化数、各命令の丸め。対象環境の小さな実験を設計判断の証拠にする。 |
| R6 | 変換の境界 | f32/f64ごとの隣接値、trunc/trunc_satのsigned/unsigned範囲、i64/ulong→f32の二重丸め回避、promote/demoteのNaN。 |
| R7 | 診断と後続機能 | 入れ子構文/複合不正の原因選択、型付きselectの長さ不正と未対応型の区別、定数式の適格性、未確認範囲の保持。 |
| R8 | 受入の対応表 | S1〜S8→73受入基準→全対象命令/規則→実装→公式/直接テストの追跡と、追加対応後の残存未対応・blockedの所有先。 |

次の段階は、本分析を踏まえた要件の確認・承認と`$kiro-spec-design numeric-control`である。本分析ではrequirements/spec.json、既存の受入記録、roadmap、baselineを変更しない。

## 設計調査（2026-10-09）

### 要約（Summary）

- **対象:** numeric-control。ユーザーの`$kiro-spec-design numeric-control -y`により要件を承認し、設計生成を実施する。
- **調査種別:** 既存システムの拡張。light discoveryで接点を確認した後、関数内の検証状態・線形化と数値境界に構造上の変更が必要なためfull discoveryへ拡張した。
- **主要所見:** 公開APIと既存ランナーは再利用できる。関数内の型検証と式構文を先に抽出し、分岐はADR0002の3値へ確定する。算術NaNのcanonical化と整数から目的浮動小数点型への直接変換により、余分な分岐や二重丸めを避けられる。
- **現在の根拠:** HEAD=`39a2748a10a606678e4d4c847ae4d8baf7fe9d1a`、spec/WABTのsubmoduleは前掲固定commitと一致。baselineのSHA-256も前掲値と一致した。保存済みrunを今回再実行したとは扱わない。
- **参照したスキル:** `kiro-spec-design`の原則・light/full discovery・synthesis・review gate、`codebase-design`の小さいInterfaceへ状態を閉じる方針、`csharp-conventions`の型・不変配列・配置・TUnit規則。設計テンプレートとcore steering、roadmap、関連ADRを適用した。
- **実施範囲:** 現行コード・固定仕様・公式テスト・一次資料の読取り、制御と数値の並列調査、主エージェントによる統合、単発BCL実験、設計文書のレビュー。製品・テストコード変更、製品ビルド、テスト実行、公式全体run、baseline更新は行わない。

### 調査ログ（Research Log）

#### 式構文・型検証・線形化の接点

- **背景:** R1〜R4の未確定事項を解決し、入力と実行コードの所有者を具体化する。
- **資料:** ModuleDecoder、ModuleBinaryReader、ModuleValidator、DecodedInstruction、FunctionCode、InterpreterContextの現行コード、ADR0002/0005/0006、固定`binary/instructions.rst`、`appendix/algorithm.rst`、`interpreter/valid/valid.ml`、`unreached-valid.wast`。
- **所見:** 内部型の現在名はInterpreterContextである。式読取りは関数とglobal初期化式で共用され、Bounded/Diagnosticの再走査も同じ経路を使う。関数検証の局所状態はModuleValidatorのprivateメソッドに集中している。
- **決定への影響:** 既存ReadInstructionsをExpressionDecoderへ、ValidateFunctionをFunctionValidatorへ抽出する。入れ子構文は明示スタック、型検証は制御frameとnullable operand型で扱う。module全体の検査順序と定数式の制限は移さない。
- **分岐:** FunctionCodeにBranchTargetの不変プールを所有させ、Instruction.Indexと候補数で参照する。heightはOperandBase相対、関数ラベルは最終ReturnのPc。内側endは消去し、loopへ分岐するときだけ入口へ戻す。
- **注意:** 出力命令数は入力数と一致しない。builderのMoveToImmutableに必要な容量一致を前提にしない。新しい契約型を生成器テストのEmbeddedResourceへ追加する。

#### Unknownと診断選択

- **背景:** R2/R7で、到達不能部分を受け付けながら不正を検出する必要がある。
- **資料:** 固定`appendix/algorithm.rst`のpop_valとbr_table、`interpreter/valid/valid.ml`のselect/br_if/br_table、`test/core/unreached-valid.wast`のmeet-bottom、ADR0012。
- **所見:** br_tableは候補ごとに同じ実際のoperand型列を照合する。Unknownを最初のラベル型に変えると、f32/f64ラベルが混在する有効なmeet-bottomを拒否する。制御frameをまたぐ到達不能状態の伝播も不正な省略となる。
- **決定への影響:** nullable型のUnknownを検証内部に限定する。label arityは揃えるが具体型の一律一致は要求しない。typed selectの長さ不正、型不成立、後続型の未実装を区別する。
- **診断順序:** appendixの擬似コードはselector popが先だが、固定valid.mlのbr_tableは既定ラベル・分岐値・候補を確認してからselectorの適合を検査する。意味論と診断原因の選択を区別し、確認した原因選択を通常入力にも適用する。

#### 数値宣言と生成dispatch

- **背景:** R3で、数値の型情報をvalidatorのopcode一覧へ重複させない方式を決める。
- **資料:** InstructionAttribute、InstructionSet、InstructionGenerator、GeneratorTestSource、生成器テストcsproj。
- **所見:** handler署名は`static ExecutionResult Handler(InterpreterContext context, in Instruction instruction)`。Interpreterはpartialなので配置の分割だけで既存dispatchに参加できる。現行descriptorには数値の入出力型がない。
- **決定への影響:** 既存3/7引数属性に10引数の数値overloadを加え、NumericSignatureをdescriptorへ投影する。数値は同型の入力1/2個と結果1個で表現できる。制御markerにも有効handlerを与え、必要なものだけ線形化時に出力するため、handler必須契約を緩めない。

#### 浮動小数点と整数変換

- **背景:** R5/R6を.NET 10の能力と固定仕様で確定する。
- **資料:** 固定`exec/numerics.rst`、`syntax/values.rst`、`interpreter/exec/fxx.ml`、`f32_convert.ml`、`test/core/conversions.wast`、下記Microsoft資料。
- **所見:** canonical NaNはarithmetic NaNの部分集合。算術・丸め・demote/promoteのNaN結果を一律に正canonicalへ変換しても要件を満たす。一方、abs/neg/copysignと値運搬にはpayload保持が必要である。
- **決定への影響:** Math/MathFとBitOperationsを採用し、NaN結果、±0のmin/max、整数trapと変換境界を補う。外部ライブラリや汎用演算backendは採用しない。
- **変換境界:** float→intはNaNを先に区別し、Math.Truncate後の値を下限inclusive・2の冪の上限exclusiveで判定してからcastする。f32→doubleは正確なのでこの判定に限って共有できる。整数→f32は元の整数から直接castし、doubleを経由させない。
- **BCLとの違い:** .NET 9以降のx86/x64のfloat→int castは飽和するため、cast結果をWasm truncの成否判定に使わない。[Microsoftの変更説明](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/9.0/fp-to-integer)

#### 単発BCL実験

数値調査エージェントがPowerShellのAdd-Typeでメモリ上のC#をコンパイルし、NoInliningを付けた直接変換とdouble経由変換を比較した。環境は.NET 10.0.11/x64、SDK 10.0.401。リポジトリのソース・テストは変更していない。

| 入力64bit | 解釈 | 直接f32変換のbits | double経由のbits |
| --- | --- | --- | --- |
| `0020000020000001` | signed | `5A000001` | `5A000000` |
| `7FFFFF4000000001` | signed | `5EFFFFFF` | `5EFFFFFE` |
| `8000004000000001` | signed | `DEFFFFFF` | `DF000000` |
| `FFDFFFFFDFFFFFFF` | signed | `DA000001` | `DA000000` |
| `0020000020000001` | unsigned | `5A000001` | `5A000000` |
| `7FFFFFBFFFFFFFFF` | unsigned | `5EFFFFFF` | `5F000000` |
| `8000008000000001` | unsigned | `5F000001` | `5F000000` |
| `FFFFFE8000000001` | unsigned | `5F7FFFFF` | `5F7FFFFE` |

直接変換は固定conversions.wastの8期待値と一致し、double経由は8件とも1ULP異なった。再確認時は各入力をlong/ulongとして構築し、`BitConverter.SingleToUInt32Bits((float)value)`とdouble経由の値を比較する。

同じ実験で、f32のnearest(-0.5)、trunc(-0.9)、ceil(-0.1)、floor(-0)、sqrt(-0)は`80000000`、f64のnearest(-0.5)とtrunc(-0.9)は`8000000000000000`だった。最小f32非正規化数に1、1.5、0.5を乗じたbitsは順に`00000001`、`00000002`、`00000000`。各命令相当で保存した`(16777216f+1f)-16777216f`は0だった。

sNaN入力`7F800001`の加算・Round・Ceilingは`7FC00001`、qNaN`7FC00123`の加算は同payload、0/0は`FFC00000`を返した。NaN結果のcanonical化でこのホスト依存のpayloadを除ける。これは特定環境・選択入力の観測であり、全入力・全プラットフォームや製品の適合証明ではない。

### 方式比較（Architecture Pattern Evaluation）

| 方式 | 利点 | 不採用理由・採否 |
| --- | --- | --- |
| ModuleDecoder/ModuleValidatorへ全処理を追加 | 移動は少ない | module全体の規則と関数内状態が同居し続けるため不採用。 |
| 関数の検証・実行を別エンジン化 | 機能単位に閉じる | 呼出し・値・失敗の重複とADR違反になるため不採用。 |
| 関数内状態と式構文だけを抽出し既存実行を拡張 | 既存契約を保ち、複雑な状態を局所化できる | 採用。先に責務を整理してから対応命令を増やす。 |

### 設計決定（Design Decisions）

#### 制御の値運搬を共通化する

- **一般化:** block/if/loop/関数ラベルは、分岐先・高さ・保持個数で共通化できる。値の型別の実行分岐を持たない。
- **採用:** 型規則とラベル解決をFunctionValidatorへ、値移動をInterpreterContext.Branchへ閉じる。CompleteFrameは関数終了にだけ使う。
- **取捨選択:** 実行時構造は小さくなるが、入力構造と線形Pcの対応をたどる必要がある。元ByteOffsetを各実行命令へ保持する。
- **簡素化:** 独立した実行制御スタック、検証済みAST、分岐方式を差し替えるinterfaceは作らない。

#### 命令型情報は宣言から生成する

- **一般化:** 数値の単項・二項・比較・変換はNumericSignatureで扱う。特殊な制御・selectは専用の検証規則に残す。
- **採用:** 既存属性と生成器を拡張する。外部generatorや別のopcode辞書は導入しない。
- **取捨選択:** 数値属性の引数と生成器検査は増えるが、decoder/validator/dispatchの命令対応を一箇所から導ける。

#### BCLを利用しWasmとの差分だけを保持する

- **採用:** 幅ごとの.NET演算、直接cast、BitOperations、MathF/Math。NaNは算術結果だけcanonical化し、符号操作・再解釈・運搬は全bitを保持する。
- **不採用:** すべての変換をdoubleへ集約する方式、NaN入力payload別の出力選択、ソフトウェアIEEE754エンジン。
- **理由:** 単発実験と固定仕様から、必要な補正を明確にできた。追加ライブラリ・保守対象を増やす根拠がない。

#### 公式受入の機能境界を仕様側で確認する

- **採用:** run/compare-runの既存基準を維持し、validation.mdで全命令・規則の対応と残存Feature/blockedの原因・所有先を確認する。
- **理由:** CompletionPolicyの終了0はnumeric-controlの未実装が0であることを意味しない。ランナーへ仕様名に依存する条件を追加する必要はない。
- **確認事項:** 同じ固定素材・profileの全体実行、以前passedだったケースの退行・欠落0、受入後の同じ結果の明示baseline保存。

### リスクと対策（Risks & Mitigations）

- Unknownを具体ラベル型へ変えてしまう危険: meet-bottomと到達不能部分の具体型不一致を対で確認する。
- 内側endと関数終了の混同: 内側markerを消し、関数ラベルを最終ReturnのPcへ解決する契約を直接確認する。
- 値移動でlocalsや外側値を壊す危険: OperandBase相対と重なりのある移動、破棄範囲のclearを確認する。
- 算術NaN補正が値運搬へ漏れる危険: const・符号操作・reinterpret・select・制御は補正を通さない設計にする。
- BCL環境差と変換境界: 単発実験を適合結果とせず、固定公式スイートと表現可能な隣接値で実装を受け入れる。
- Decode対応拡大後の診断選択: 共通再走査と固定参照の原因選択を維持し、新しく観測する不一致を通常入力にも適用できる規則へ直す。

### ギャップ分析の調査項目の解決

| 項目 | 設計上の決定 |
| --- | --- |
| R1 | opcodeに対応する型付き即値、BlockType、不変vector。式構文は共通ExpressionDecoder。 |
| R2 | FunctionValidator内の制御frameとnullable operand型。 |
| R3 | marker消去と条件/無条件分岐、前方参照、数値signature生成、既存handler契約維持。 |
| R4 | OperandBase相対のBranchTarget、不変分岐プール、値移動と参照解除。 |
| R5 | 幅別BCL、一律canonical算術NaN、bit操作、±0の規則、単発実験。 |
| R6 | 切捨て後の半開区間、整数→目的浮動小数点型の直接cast、bit再解釈。 |
| R7 | selectの構文/型/未対応の分離、既存定数式制限、診断再走査、固定原因選択。 |
| R8 | 設計の73項目対応と、実装時validation.mdのopcode/規則・証拠・残存原因監査。 |

### 参照資料（References）

- [固定WebAssembly Core 2.0](https://github.com/WebAssembly/spec/tree/05ca4182176763112561ae20153975c12bd689e4) — 要件文書S1〜S8と同じcommit。
- [固定Validation Algorithm](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/appendix/algorithm.rst) — 型スタックと制御frame。診断順序は同commitのvalid.mlも参照。
- [C#の数値変換](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/numeric-conversions) — 型ごとの変換と丸め。
- [C#の算術演算子](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators) — signed整数の除算と例外。
- [MathF.Round](https://learn.microsoft.com/en-us/dotnet/api/system.mathf.round?view=net-10.0)、[MathF.Sqrt](https://learn.microsoft.com/en-us/dotnet/api/system.mathf.sqrt?view=net-10.0)、[BitOperations](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.bitoperations?view=net-10.0) — 採用する既存BCL。
- [Floatから整数への変換の変更](https://learn.microsoft.com/en-us/dotnet/core/compatibility/jit/9.0/fp-to-integer) — cast前のWasm境界判定が必要な理由。

### 設計レビューゲート

- 機械確認: 要件対応表は73/73受入基準を網羅し、未参照IDは0。責務・対象外・許可依存・再検証契機・ファイル構成を含む必須節に具体的な内容があり、未記入の仮置き記述は0。
- 境界確認: ファイル計画の未存在パスは新規作成対象に対応する。既存の呼出し・リソース・ランナーの責務を取り込まず、先に責務を整理する導入順序になっている。
- 主エージェントの確認に加え、調査履歴を引き継がない別エージェントが草案・要件・方針・現行コード・固定仕様を独立に照合し、GO、必須修正0件と判定した。レビュー後の修正パスは0回。
- ゲート通過後に`design.md`を保存した。要件承認はtrue、設計はgenerated=true/approved=false、tasksとready_for_implementationは未承認のままとする。
- この判定は設計の静的確認であり、実装検証、製品ビルド、TUnit、公式スイートの新しい実行結果を意味しない。
