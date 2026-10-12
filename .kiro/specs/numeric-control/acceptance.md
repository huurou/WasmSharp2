# numeric-controlの受入規則対応と残存原因監査

タスク11.2として2026-10-12に、11.1の同じ保存結果を監査した。対象HEADは`5d3ee09`。数値・制御本体、テスト、ランナーは11.1の実行revision`cb665f2b8b48e0c2fcbda69feedaa77d48c67813`から不変で、Releaseビルドの警告・エラー0後に成功したruntime2,824件・生成器76件・runner702件を再利用した。追加テストと全suite/公式runの再実行は行っていない。

全73受入基準の対応を作成し、要件別件数を6・6・9・7・8・7・9・6・7・8と原文から再計数した。11.3の受入条件は後述の同一結果照合で確定した。10.8のbaseline保存は11.4待ち。本記録は11.4のbaseline保存、numeric-control全体のGO、後続未対応も0を要するCore2.0全体の完成判定を代替しない。

## 同一結果と監査出力

- run:[11.1のrun.json](../../../artifacts/numeric-control/task-11.1-20261012T003403Z/run.json)、SHA-256`02ab113a70b5dd1c6ceb5aca12c901dddb8735b1754a035c999953baae9ad779`。
- 比較:[11.1のcomparison.json](../../../artifacts/numeric-control/task-11.1-20261012T003403Z/comparison.json)、SHA-256`794f076ca34de52d786db1b4403af6003d02f37ddd5298b301098962676d454a`。
- 11.2監査出力:[task-11.2-20261012T-audit-5d3ee09](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/)。再実行する監査スクリプト、コマンド・native終了値・UTC・原ログ、最終変更hashを同じ出力先に保持する。
- 件数:全147入力/53,907command。passed16,924、failed0、runner_error0、runtime_unsupported2,100、blocked33,806、out_of_scope1,077。比較成立・完了、回帰・欠落・条件差・素材差0。旧passed2,491件を維持した。

## 表の参照と確認能力

S1〜S8は[requirements.mdの固定commitの参照表](requirements.md#参照するwasm仕様)に従う。S1は命令構造・値、S2は符号化、S3は型検証、S4は数値意味論、S5は命令実行、S6はmodule/start・資源上限、S7は局所多相性と参照validator、S8は参照実行/変換/診断と公式期待結果である。原文は`thirdParties/WebAssembly-spec`の固定commit`05ca4182176763112561ae20153975c12bd689e4`を使用した。

実装参照は次の現在ファイルを指す。表の行はopcodeだけでなく型・構文・公開段階の規則も対応付ける。

| 参照 | 現在実装 |
| --- | --- |
| A1 | [ExpressionDecoder.cs](../../../src/WasmSharp/Modules/ExpressionDecoder.cs)、[ModuleBinaryReader.cs](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs)、[ModuleDecoder.cs](../../../src/WasmSharp/Modules/ModuleDecoder.cs) |
| A2 | [FunctionValidator.cs](../../../src/WasmSharp/Modules/FunctionValidator.cs)、[ModuleValidator.cs](../../../src/WasmSharp/Modules/ModuleValidator.cs) |
| A3 | [WasmModule.cs](../../../src/WasmSharp/WasmModule.cs)、[ModuleInstantiator.cs](../../../src/WasmSharp/Modules/ModuleInstantiator.cs) |
| A4 | [InstructionSet.cs](../../../src/WasmSharp/Instructions/InstructionSet.cs)、[NumericSignature.cs](../../../src/WasmSharp/Instructions/NumericSignature.cs)、[InstructionGenerator.cs](../../../src/WasmSharp.Generators/InstructionGenerator.cs) |
| A5 | [Interpreter.Integer.cs](../../../src/WasmSharp/Execution/Interpreter.Integer.cs) |
| A6 | [Interpreter.Float.cs](../../../src/WasmSharp/Execution/Interpreter.Float.cs) |
| A7 | [Interpreter.Conversion.cs](../../../src/WasmSharp/Execution/Interpreter.Conversion.cs) |
| A8 | [ExecutionBoundary.cs](../../../src/WasmSharp/Execution/ExecutionBoundary.cs)、[ExecutionResult.cs](../../../src/WasmSharp/Execution/ExecutionResult.cs)、[Interpreter.cs](../../../src/WasmSharp/Execution/Interpreter.cs) |
| A9 | [Interpreter.Control.cs](../../../src/WasmSharp/Execution/Interpreter.Control.cs)、[InterpreterContext.cs](../../../src/WasmSharp/Execution/InterpreterContext.cs)、[Interpreter.cs](../../../src/WasmSharp/Execution/Interpreter.cs) |
| A10 | [tools/WasmSharp.TestSuiteRunner](../../../tools/WasmSharp.TestSuiteRunner/)、本記録 |

確認資料の略号を次のように定義する。公式IDは元入力と0起算のcommand_indexであり、WASTの行番号ではない。全明細はignored出力に置き、本書は規則と代表ID・直接境界を結び付ける。

| 参照 | 確認資料と確認能力 |
| --- | --- |
| N / N番号 | [opcode-inventory.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/opcode-inventory.json)。全136非const数値命令のopcode・型宣言・handler・現在実装・対応要件番号・`Interpreter_<Handler>Tests`の正式method名・Argumentsの境界bit・11.1の成功TRXを1命令ずつ列挙。N4.1等はその要件番号の行全件。内部handlerテストはliteralの期待bit、結果型、成功/trap、下位sentinel、operand数を確認し、公開例外と全dispatchはD38/D44/D46/公式runで補う。 |
| J | [11.1実行記録](../../../artifacts/numeric-control/task-11.1-20261012T003403Z/verification.md)と[独立監査](../../../artifacts/numeric-control/task-11.1-review-20261012T-independent/audit.json)。全caseのexpected/actual、NaN pattern、否定assertionの型・段階・Reason・StartsWithを照合済み。11.2は同じrunのhashを確認した。 |
| P | [prerequisite-passed.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/prerequisite-passed.json)。前提を満たす16,924caseのIDと元JSONのcommand。除外していない全結果を監査し、残りはU/B/元JSONがtextの対象外1,077件だけ。 |
| U | [unsupported-ownership.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/unsupported-ownership.json)。全2,100件のFeature、ID、source command、stage/location、unverified_ranges、opcode属性、所有brief。 |
| B | [blocked-origins.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/blocked-origins.json)。全33,806件のdirect/originsと再帰的に解決した未対応起点。 |
| Q | [coverage.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/coverage.json)と[audit.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/audit.json)。原文73基準・Dの現在source/正式method/全TRX成功・代表公式IDのpassed・固定付録437opcodeとの一致を検査。意味論の判断は表の具体的規則と直接テストのassertionを読んで行い、ファイル名の存在を網羅性としない。 |
| F | [selected-cases.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/selected-cases.json)。表の代表公式commandと、`float_exprs`全passed assertionの入力bit/expected/actual、canonical/arithmetic NaNの代表。各命令丸めや演算結合を複数命令の結果で確認する。 |
| T | N6.3の直接method`入力が切捨て後の範囲内_0方向への整数値を同幅のbit列で返す`等。i32.trunc_f64_sの`0xC1E00000001CCCCD`(−2147483648.9)→MIN、直下`0xC1E0000000200000`はoverflow、unsignedの−0.9→0、全8組合せの隣接bitを確認。 |
| R | N6.6のi64→f32直接method`整数の通常値と境界値_符号付き解釈で目的幅へ変換する`等。signed/unsigned各4個の二重丸め回避bitを含む。signedの`0x0020000020000001`→`0x5A000001`、`0x7FFFFF4000000001`→`0x5EFFFFFF`、負値2個とunsigned4個を現在Arguments/成功TRXで照合する。 |
| C | [ModuleValidator.cs](../../../src/WasmSharp/Modules/ModuleValidator.cs)の`ValidateInitializers`/`ValidateConstantInstruction`はconst/global.get等の先行Core2定数式規則を保持。D08の数値全群とD48のselect/制御禁止を確認。 |
| K | [InterpreterContext_StackTests.cs](../../../tests/WasmSharp.Tests/Execution/InterpreterContext_StackTests.cs)の`内側で領域を拡張して復元する_外側の値とフレームを保ち除いた参照を解除する`、D49の値移動・破棄参照。内部参照解除と公開D28/D41を組み合わせる。 |
| H | [validation.mdのWABT更新後の再検証](validation.md#タスク96wabt更新後の再検証)と[上流WABT移行記録](../test-suite-runner/wabt-1.0.42-migration.md)。今回不一致0で修正ループは発動しない。過去9.6の素材変換不成立は上流承認の更新後に全体run/比較をやり直した履歴として区別する。 |

## 73受入基準の対応

「確認」はこのrun・現在source・成功済み直接テストの範囲で当該規則を確認した意味であり、未対応依存を持つ全公式caseの実行成功を意味しない。制御正例の公式caseには後続memory/参照を含んでblockedのものもあり、その規則はD22〜D41等の公開4段階テストで直接確認する。

| 要件番号 | S規則 | 対象opcode・型規則 | 実装 | 具体的な公式ID・実行済みmethod参照 | 確認状態 |
| --- | --- | --- | --- | --- | --- |
| 1.1 | S1/S2 | 数値・制御、bytes/Stream、副作用なし | A1/A2/A3 | D01、D46 | 確認 |
| 1.2 | S2/S3 | block/loop/if、空/単値/s33型添字 | A1/A2 | D02、D13 | 確認 |
| 1.3 | S2 | const、br/br_if/br_table、typed select、FC番号、非最短LEB | A1 | D02、D03、D04、D46 | 確認 |
| 1.4 | S2 | 未割当/即値/切断/else/end/式・section境界 | A1 | D05、D06、binary.wast/command4、binary-leb128.wast/command25 | 確認 |
| 1.5 | S3 | module全体成功後のみInstantiate、検証中副作用なし | A2/A3 | D01、D07、D13 | 確認 |
| 1.6 | S2/S3 | 後続機能のUnsupported、位置・未確認範囲 | A1/A2 | D12、D42、U | 確認 |
| 2.1 | S3 | 全136数値signature、const4、比較/eqz結果i32 | A2/A4 | D43、D44、D47、N2.1 | 確認 |
| 2.2 | S3 | 到達可能入力不足・型不一致を拒否 | A2 | D07、D47 | 確認 |
| 2.3 | S3 | untyped select、同じscalar候補とi32条件、参照拒否 | A2 | D09、D10、select.wast/command119 | 確認 |
| 2.4 | S3 | typed scalar select、型列1/候補型/i32条件 | A1/A2 | D04、D09、D10 | 確認 |
| 2.5 | S2/S3 | typed select型列0/2以上はValidate不成立 | A1/A2 | D04、D11、select.wast/command120 | 確認 |
| 2.6 | S3 | const式の許可命令を拡張しない | A2 | D08、D48、C | 確認 |
| 3.1 | S3 | block/loop/if引数・結果0/1/複数、型添字存在 | A2 | D13、D14、D22、D28 | 確認 |
| 3.2 | S3 | if条件i32、定数でも両側検証 | A2 | D13、D14、if.wast/command135 | 確認 |
| 3.3 | S3 | 省略elseは空列、入力型列=結果型列 | A2 | D14、D15、D23 | 確認 |
| 3.4 | S3/S5 | depth0〜関数暗黙ラベル、block結果/loop引数/関数結果 | A2 | D16、D17、D28 | 確認 |
| 3.5 | S3 | br_if条件i32、0でも分岐適合、非分岐値保持 | A2 | D16、D24 | 確認 |
| 3.6 | S3/S7 | br_table全ラベル/個数/型、Unknown独立適合 | A2 | D18、D19、unreached-valid.wast/command6 | 確認 |
| 3.7 | S3/S7 | 制御内底・通常end結果・余剰・外側値保護 | A2 | D13、D14、D21、D28 | 確認 |
| 3.8 | S3/S7 | unreachable/br/br_table/return後の局所多相性 | A2 | D16、D18、D20、D21 | 確認 |
| 3.9 | S1/S3 | v128/funcref/externrefの制御型/分岐型 | A2 | D15、D22、D28 | 確認 |
| 4.1 | S4/S5 | i32/i64 add/sub/mulの幅内wrap | A5 | N4.1、i32.wast/command4、i64.wast/command4 | 確認 |
| 4.2 | S4/S5 | div_s/u・rem_s/u、符号・0方向商・剰余 | A5 | N4.2、i32.wast/command29、i64.wast/command29 | 確認 |
| 4.3 | S4/S5/S8 | div/rem除数0、div_s MIN/-1のtrap | A5/A8 | N4.3、D38、i32.wast/command25、i64.wast/command27 | 確認 |
| 4.4 | S4 | rem_s MIN/-1→0、trapなし | A5 | N4.4、i32.wast/command68、i64.wast/command68 | 確認 |
| 4.5 | S4 | and/or/xor、shift/rotate幅mod、signed/unsigned右shift | A5 | N4.5、i32.wast/command130 | 確認 |
| 4.6 | S3/S4 | clz/ctz/popcnt、0→幅/幅/0、i64結果i64 | A4/A5 | N4.6、D44、i32.wast/command195、i64.wast/command195 | 確認 |
| 4.7 | S3/S4 | eqz、整数比較、signed/unsigned、結果i32 0/1 | A4/A5 | N4.7、D44、i32.wast/command230 | 確認 |
| 5.1 | S1/S4 | f32/f64 add/sub/mul/div/sqrt、各幅丸め・subnormal | A6 | N5.1、f32.wast/command48、f64.wast/command48 | 確認 |
| 5.2 | S4 | 各命令で幅へ丸め、結合/中間丸め省略なし | A6 | N5.2、float_exprs.wast/command1、F | 確認 |
| 5.3 | S4 | ceil/floor/trunc/nearest、方向・偶数・∞・0符号 | A6 | N5.3、f32.wast/command2481、f64.wast/command2481 | 確認 |
| 5.4 | S1/S4 | abs/neg/copysign、NaN payload含む符号以外のbit保持 | A6 | N5.4、f32_bitwise.wast/command1、f64_bitwise.wast/command1 | 確認 |
| 5.5 | S4 | min/max、NaN、min(-0,+0)=-0/max=+0 | A6 | N5.5、f32.wast/command1602、f32.wast/command2002、f64.wast/command1602、f64.wast/command2002 | 確認 |
| 5.6 | S4 | float 0除算/負sqrt/不定/overflowはNaN/∞ | A6 | N5.6、f32.wast/command1204、f32.wast/command2409 | 確認 |
| 5.7 | S1/S4 | canonical/arithmetic NaN許容bit範囲 | A6 | N5.7、f32.wast/command1204、F | 確認 |
| 5.8 | S3/S4 | float比較、±0同値、NaNはneのみ、i32結果 | A4/A6 | N5.8、f32_cmp.wast/command1、f64_cmp.wast/command1 | 確認 |
| 6.1 | S4 | wrap_i64/extend_i32_s/u、下位bit/符号/ゼロ拡張 | A7 | N6.1、conversions.wast/command1、conversions.wast/command7、conversions.wast/command13 | 確認 |
| 6.2 | S4 | extend8/16/32_s、上位bit無視 | A7 | N6.2 | 確認 |
| 6.3 | S4/S8 | trunc全8型組合せ、切捨て後範囲、(-1,0)unsigned→0 | A7 | N6.3、conversions.wast/command68、T | 確認 |
| 6.4 | S4/S8 | trunc NaN→invalid-conversion、∞/範囲外→overflow | A7/A8 | N6.4、D38、conversions.wast/command84 | 確認 |
| 6.5 | S4 | trunc_sat全8、NaN0、有限飽和、∞上下限、trapなし | A7 | N6.5、D46、conversions.wast/command215 | 確認 |
| 6.6 | S1/S4 | convert_s/u・demote/promote、最近接偶数・±0/∞/NaN | A7 | N6.6、R | 確認 |
| 6.7 | S1/S4 | reinterpret全4、NaN符号/payload/±0全bit | A7 | N6.7 | 確認 |
| 7.1 | S5 | nop、値・観測状態不変 | A9 | D01、D23、D45 | 確認 |
| 7.2 | S3/S5 | block/loop通常終了、引数/結果宣言順、loop再反復なし | A2/A9 | D22、D23 | 確認 |
| 7.3 | S5 | if条件0/非0、選択側のみ、引数/結果 | A2/A9 | D22、D23、D31 | 確認 |
| 7.4 | S3/S5 | br→block/if/関数結果、飛越し非実行 | A2/A9 | D24、D28、D31、unwind.wast/command2 | 確認 |
| 7.5 | S3/S5 | br→loop引数保持・後方入口、結果終了と区別 | A2/A9 | D17、D25 | 確認 |
| 7.6 | S3/S5 | br_if条件消費、非0分岐/0値保持・後続 | A2/A9 | D24、D28 | 確認 |
| 7.7 | S5 | br_table unsigned selector、候補外/空は既定 | A2/A9 | D26、D27 | 確認 |
| 7.8 | S5 | 入れ子/return、keep順序・一時値破棄、外側/locals/caller保護 | A9 | D27、D28、D29、K | 確認 |
| 7.9 | S1/S5 | 全7型、生bit/null/非null参照同一性 | A9 | D22、D28 | 確認 |
| 8.1 | S5 | typed/untyped scalar select、0第2/非0第1、生bit | A9 | D30、D35 | 確認 |
| 8.2 | S5 | select候補・条件の左から右、副作用・非短絡 | A9 | D30、D34 | 確認 |
| 8.3 | S5 | call/return/locals/drop、0/1/複数順序、locals独立 | A9 | D28、D29、local_get.wast/command1 | 確認 |
| 8.4 | S3/S5 | global型/可変性/import共有、後続呼出しで状態観測 | A2/A9 | D30、D31、global.wast/command59 | 確認 |
| 8.5 | S5/S6 | import定義/host、関数同一性・所属/caller情報・host例外 | A9/A8 | D32、D33、D41 | 確認 |
| 8.6 | S6 | start順序、数値制御、更新/保存参照を維持 | A3/A8 | D36、D37 | 確認 |
| 9.1 | S2/S3 | Decode/Validate例外型・段階・module未検証 | A1/A2 | D05、D06、D07、D13 | 確認 |
| 9.2 | S4/S5/S6/S8 | 数値trap/unreachable、後続非実行、Reason、Invoke/start段階 | A8 | D34、D37、D38、unwind.wast/command1 | 確認 |
| 9.3 | S4/S8 | ゼロ除算/overflow/NaN変換の診断先頭 | A7/A8 | D37、D38、N4.3、N6.4、i32.wast/command25、i32.wast/command27、conversions.wast/command84 | 確認 |
| 9.4 | S6 | 直接/相互/host同期再入、深さ上限・exhaustion | A8/A9 | D39、D40、D41、fac.wast/command7 | 確認 |
| 9.5 | S6 | trap/exhaustion/host例外後の一時値・深さ復元と保持状態 | A8/A9 | D39、D41、K | 確認 |
| 9.6 | S8 | Ordinal StartsWith、段階/例外型/Reason/位置の整合 | A1/A2/A8/A10 | D05、D11、D37、D38、J | 確認 |
| 9.7 | S3/S7/S8 | 複合不正の共通原因選択、ケースID非依存、未確認範囲 | A1/A2 | D05、D16、D19、D42、U | 確認 |
| 10.1 | S8 | 固定Core2 profile、全147入力/全53,907command | A10 | J、P | 確認 |
| 10.2 | S1/S2/S3/S4/S5/S6/S7/S8 | 73基準/opcode/型規則/source/case・境界の対応 | A10 | 本表、D43、D44、D45、N、Q | 確認 |
| 10.3 | S1/S3/S4/S5/S8 | 必要前提が揃う全caseの値/NaN/分岐/状態/否定診断passed | A10 | J、P、U、B | 確認 |
| 10.4 | S1/S2/S8 | 後続機能のFeature/元case/blocked起点、付替え禁止 | A10 | U、B | 確認 |
| 10.5 | S8 | 同じ固定条件のcompare、旧passed退行/欠落0 | A10 | J | 確認 |
| 10.6 | S8 | 全体run/比較完了、failed/runner_error/回帰0、対象case passed | A10 | J、P、U、B、Q、後述の11.3現在整合確認 | 11.3で受入条件成立を確定。10.8は11.4待ち |
| 10.7 | S8 | 不一致/回帰時に原因修正して全体run/比較再実行、判定不変 | A10 | J、H | 今回は条件不成立。不一致/回帰0。過去の対応はH参照 |
| 10.8 | S8 | 比較済み同じrunからbaseline明示保存、本仕様/Core2全体を区別 | A10 | 11.1の同じrun/comparisonを11.4へ引継ぐ | 11.4待ち |

## 実行済み直接methodと境界入力

D番号はファイルリンクの行にある正式methodを指す。引数による全展開結果・全Arguments・source hashは[test-evidence.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/test-evidence.json)に保持する。

| ID | 現在sourceと正式method | 主な境界入力・観測 |
| --- | --- | --- |
| D01 | [WasmModule_ValidateControlIntegrationTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateControlIntegrationTests.cs:10)の`WasmModule_ValidateTests.制御とselectを含むstartがある_両入力のDecodeとValidateは副作用を起こさない` | bytes/ChunkedReadStream、callback回数0→0→1、共有global7→7→9 |
| D02 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:22)の`ExpressionDecoder_ReadTests.制御型が空か値型か非負s33である_形式と値を保持して型添字を検証しない` | 40、全7値型、00/8000/8100/FFFFFFFF0F、block/loop/if、Bounded/Diagnostic |
| D03 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:93)の`ExpressionDecoder_ReadTests.分岐添字と空を含む分岐表がある_非最短u32と候補順と末尾の既定を保持する` | u32非最短8000、uint.MaxValue、候補0/最大値/2、空候補 |
| D04 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:132)の`ExpressionDecoder_ReadTests.select型列が空か単一か複数である_値型を保持して長さを検証しない` | 00、017F、8型、非最短81007E、両読取りmode |
| D05 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:173)の`ExpressionDecoder_ReadTests.制御配置か複合即値が不正である_後続未対応より構文診断と位置を優先する` | block/loop内else、重複else、負s33、多byte値型、型vector不正 |
| D06 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:211)の`ExpressionDecoder_ReadTests.制御終端か複合即値が切れる_通常解析と診断走査で元の境界位置を保持する` | 024003400B、044005、0280、0E01/0E0100、1C01/1C027F |
| D07 | [WasmModule_ValidateNumericTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateNumericTests.cs:21)の`WasmModule_ValidateTests.到達可能な数値命令の入力不足または型不一致_Validateで命令位置を通知しInstantiateを許可しない` | 67/6A、混在i32/i64、f32/f64、FC00/FC07、公開段階・位置・未検証状態 |
| D08 | [WasmModule_ValidateNumericTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateNumericTests.cs:95)の`WasmModule_ValidateTests.数値演算をglobal定数式へ置く_Decode後にValidateで禁止命令の位置を通知する` | 整数/float/変換/FCのglobal式、禁止命令位置 |
| D09 | [FunctionValidator_ValidateSelectTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateSelectTests.cs:18)の`FunctionValidator_ValidateTests.selectの候補が同じスカラー型で条件がi32_型注釈の有無によらず検証して元の命令位置を保持する` | i32/i64/f32/f64×typed/untyped、条件i32 |
| D10 | [FunctionValidator_ValidateSelectTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateSelectTests.cs:108)の`FunctionValidator_ValidateTests.selectの型や個数が不正_到達不能や後続型でもUnsupportedへ読み替えず元の位置で拒否する` | 候補不一致/条件不一致/不足/参照候補/到達不能の具体不一致 |
| D11 | [FunctionValidator_ValidateSelectTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateSelectTests.cs:150)の`FunctionValidator_ValidateTests.typed_selectの型ベクトル長が1以外_入力不足や後続型より先にValidateのarity診断で拒否する` | 型列0/2/複数、候補不足・後続型との複合不正、invalid result arity |
| D12 | [ModuleValidator_ValidateSelectTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateSelectTests.cs:13)の`ModuleValidator_ValidateTests.後続型のselectがその場の型検証に成功_ValidateのFeatureと未完了範囲を保持し部分コードを公開しない` | v128のtyped/untypedとtyped funcref/externrefの4Feature、Validate位置・範囲 |
| D13 | [FunctionValidator_ValidateControlTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateControlTests.cs:243)の`FunctionValidator_ValidateTests.制御の型や個数や添字が不正_元の命令位置で拒否しmoduleを未検証に保つ` | 不正型添字、if条件/両側、内側不足、余剰、外側operand保護 |
| D14 | [FunctionValidator_ValidateControlTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateControlTests.cs:284)の`FunctionValidator_ValidateTests.型添字の入力順序や結果や省略elseが不適合_宣言に対応する命令位置で拒否する` | 複数入力順序、結果不一致、省略elseの入力≠結果 |
| D15 | [FunctionValidator_ValidateControlTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateControlTests.cs:186)の`FunctionValidator_ValidateTests.else省略の引数型列と結果型列が同じ_if後へ入口値を保持する分岐を確定する` | i32の同じ入力/結果型列、省略elseの前方分岐・入口値保持・最大operand数 |
| D16 | [FunctionValidator_ValidateBranchTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateBranchTests.cs:217)の`FunctionValidator_ValidateTests.分岐ラベルや局所的型規則が不正_ラベル先行の診断と元の位置を保つ` | 不正depth、条件0のbr_if、条件型/分岐型、Unknownと具体値 |
| D17 | [FunctionValidator_ValidateBranchTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateBranchTests.cs:134)の`FunctionValidator_ValidateTests.分岐値がblock結果またはloop引数と不一致_他方の宣言型で補わず拒否する` | block結果とloop引数を区別して拒否 |
| D18 | [FunctionValidator_ValidateBranchTableTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateBranchTableTests.cs:160)の`FunctionValidator_ValidateTests.meet_bottomの分岐値とselectorが多相的_内側f32と外側f64の型を一律一致させない` | 到達不能br_tableのUnknownを各ラベルへ独立に適合 |
| D19 | [FunctionValidator_ValidateBranchTableTests.cs](../../../tests/WasmSharp.Tests/Modules/FunctionValidator_ValidateBranchTableTests.cs:201)の`FunctionValidator_ValidateTests.分岐表のラベルや型が複合的に不正_既定と候補順を優先して元の命令位置で拒否する` | 既定/全候補の存在・個数・型、selector、複合不正の優先 |
| D20 | [ModuleValidator_ValidateUnreachableTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateUnreachableTests.cs:20)の`ModuleValidator_ValidateTests.到達不能部分で底から値を取り出す_型多相性で成立する命令列を受理する` | unreachable、return、drop、call、locals、複数結果の底Unknown |
| D21 | [ModuleValidator_ValidateUnreachableTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateUnreachableTests.cs:48)の`ModuleValidator_ValidateTests.到達不能部分に具体型の不一致や不正添字や余剰値がある_命令位置付きで拒否する` | 具体不一致/不正添字/余剰値、元命令位置 |
| D22 | [ModuleValidator_ValidateControlTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateControlTests.cs:12)の`ModuleValidator_ValidateTests.制御の通常経路へ全値型を渡す_結果のbit列と参照と宣言順を保持する` | block/loop/if条件0/-1、全7値型、非null funcref/externref、生NaN、v128 |
| D23 | [ModuleValidator_ValidateControlTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateControlTests.cs:80)の`ModuleValidator_ValidateTests.通常終了とifの選択側を実行_後続へ結果を渡す` | 空block/loop、省略else、条件0/1、型添字付きif入力保持 |
| D24 | [ModuleValidator_ValidateBranchTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateBranchTests.cs:17)の`ModuleValidator_ValidateTests.前方分岐または非分岐を実行_保持値を使い飛び越えた命令を実行しない` | br/br_if、非分岐の値保持と後続、飛び越し |
| D25 | [ModuleValidator_ValidateBranchTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateBranchTests.cs:70)の`ModuleValidator_ValidateTests.loop引数を持つ後方分岐を多数回実行_値や関数frameや深さを蓄積しない` | loop引数と反復、上限を固定した多数回実行 |
| D26 | [ModuleValidator_ValidateBranchTableTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateBranchTableTests.cs:13)の`ModuleValidator_ValidateTests.分岐表の選択値が候補内または範囲外_保存した順序と空候補の既定へ分岐する` | selector0/1/2/-1、候補[0,1]または空、既定2 |
| D27 | [ModuleValidator_ValidateBranchTableTests.cs](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateBranchTableTests.cs:70)の`ModuleValidator_ValidateTests.分岐表が複数値を持つ内側制御を終了する_外側operandと結果の宣言順を保持する` | 外側i32=42と分岐結果i32=7/i64=9、内側制御退出 |
| D28 | [WasmFunction_InvokeControlValueTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlValueTests.cs:16)の`WasmFunction_InvokeTests.制御値を直接callと内側分岐またはreturnで運ぶ_個数と順序と外側値と保存済み結果を保つ` | 0/1/7引数結果、br_if/return、全7型、null/非null、NaN0xFF812345/0xFFF0123456789ABC |
| D29 | [WasmFunction_InvokeControlValueTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlValueTests.cs:141)の`WasmFunction_InvokeTests.制御値を同じ呼出し元から連続callする_内側分岐後も外側値とlocalsを保ち呼出し先localsを初期化する` | 入力5/6→8、10/11→13、連続callのlocalsと外側operand |
| D30 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:13)の`WasmFunction_InvokeTests.selectの両候補と条件で共有globalとhostを更新する_非短絡で命令順に評価して選択値を返す` | typed/untyped、条件0/-3、両候補・条件のcallback順序と共有global |
| D31 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:90)の`WasmFunction_InvokeTests.brとifで共有globalへの更新を飛び越える_実行した側だけが別instanceの後続呼出しから見える` | 条件0/-1、別instanceのimport共有global、副作用飛越し |
| D32 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:172)の`WasmFunction_InvokeTests.数値条件の制御からimport定義関数と再exportしたhostを呼ぶ_同じ関数実体と所属instanceを保つ` | 定義関数の提供元instanceとhost呼出し元instance、再export同一性 |
| D33 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:272)の`WasmFunction_InvokeTests.数値条件のif内でhostが例外を投げる_型と元実体を保ち後続更新とcallbackを実行しない` | instance引数あり/なし、同一host例外、後続共有状態・callback非実行 |
| D34 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:360)の`WasmFunction_InvokeTests.selectで選ばれない第2候補にtrapがある_候補計算で中断し条件以後の副作用を実行しない` | div_sの0除算とunreachable、非選択候補のtrap |
| D35 | [WasmFunction_InvokeControlStateTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlStateTests.cs:434)の`WasmFunction_InvokeTests.公開経路でf32とf64のselectを呼ぶ_選択したNaNpayloadと符号付き0の生bitを保つ` | typed/untyped、条件0/-1、sNaN/qNaNのpayloadと±0 |
| D36 | [WasmModule_InstantiateStartTests.cs](../../../tests/WasmSharp.Tests/WasmModule_InstantiateStartTests.cs:11)の`WasmModule_InstantiateTests.Start中のcallbackからexportを使う_定義資源と関数は初期化済みで再入できる` | 数値加算/if、start callback再入、定義資源初期化・参照 |
| D37 | [WasmModule_InstantiateNumericTrapTests.cs](../../../tests/WasmSharp.Tests/WasmModule_InstantiateNumericTrapTests.cs:16)の`WasmModule_InstantiateTests.制御内のStartで数値trapに到達する_公開診断と失敗前の更新と保存参照を保ち以後の副作用を実行しない` | divide-by-zero/overflow/invalid-conversionの3Reason、Instantiate位置、保存参照と共有global |
| D38 | [WasmFunction_InvokeNumericTrapTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeNumericTrapTests.cs:16)の`WasmFunction_InvokeTests.数値trapに到達する_原因別の公開診断とInvokeの元位置を保ち以後の副作用を実行しない` | i32.div_sの0除算とMIN/-1、f32 NaNのi32.trunc_f32_sの3入力。Invoke位置・Reason・診断先頭・後続非実行 |
| D39 | [WasmFunction_InvokeControlExhaustionTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlExhaustionTests.cs:11)の`WasmFunction_InvokeTests.ifから直接または相互再帰する_小さい深さ上限で止まり保持状態と新しいlocalsで再実行できる` | 直接/相互、上限4、入力10でexhaustion→入力1成功、参照/locals/global保持 |
| D40 | [WasmFunction_InvokeControlExhaustionTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlExhaustionTests.cs:86)の`WasmFunction_InvokeTests.loop内で定義関数とhostを繰り返し呼ぶ_終了済みcallと反復が深さへ蓄積せず参照を保つ` | 深さ2、loop内の完了済み定義/host call、参照保持 |
| D41 | [WasmFunction_InvokeControlExhaustionTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeControlExhaustionTests.cs:152)の`WasmFunction_InvokeTests.制御内でtrapまたは同期再入またはhost例外に至る_最外の上限と例外実体を保ち次の呼出しを妨げない` | trap/exhaustion/host例外、外側上限4共有、内側独立入口上限20、保持状態/locals/保存結果 |
| D42 | [ExpressionDecoder_ReadTests.cs](../../../tests/WasmSharp.Tests/Modules/ExpressionDecoder_ReadTests.cs:473)の`ExpressionDecoder_ReadTests.未対応命令の後に不正がある_後続を調べず機能と位置と未確認範囲を保持する` | 未対応opcode後の不正、Bounded/Diagnostic、Featureと未確認範囲 |
| D43 | [InstructionSet_TryGetTests.cs](../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs:13)の`InstructionSet_TryGetTests.保存版の命令割当を照合する_番号と名前と欠番が一致し対応済み命令だけを実行対象にする` | prefix0/FC/FDの183/18/236割当、実行対象160、256/uint.MaxValue欠番 |
| D44 | [InstructionSet_TryGetTests.cs](../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs:167)の`InstructionSet_TryGetTests.全数値命令の宣言を固定仕様と照合する_入力個数と型と結果型とhandlerが一致する` | 0x45〜0xC4の128個とFC0〜7の8個、独立期待signature・handler名 |
| D45 | [InstructionSet_TryGetTests.cs](../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs:133)の`InstructionSet_TryGetTests.制御とselectの宣言を確認する_即値と型効果と規則と共有handlerが一致する` | 制御/select11opcode、即値・ValidationRule・共有handler |
| D46 | [WasmFunction_InvokeNumericTests.cs](../../../tests/WasmSharp.Tests/WasmFunction_InvokeNumericTests.cs:72)の`WasmFunction_InvokeTests.全飽和変換を最短と非最短FC番号で呼び出す_両Decode入口から目的整数型の切捨て結果を返す` | FC0〜7×最短/非最短、bytes/StreamからInvoke |
| D47 | [WasmModule_ValidateNumericTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateNumericTests.cs:59)の`WasmModule_ValidateTests.数値命令の結果型と関数宣言が不一致_関数endでValidateに失敗する` | i64.eqz/i64.clz/f32比較/FC06の結果型とend |
| D48 | [WasmModule_ValidateInitializersTests.cs](../../../tests/WasmSharp.Tests/WasmModule_ValidateInitializersTests.cs:12)の`WasmModule_ValidateTests.selectやblockをGlobal初期化式へ置く_関数内の対応によらずValidateで拒否する` | scalar select、typed select、blockをglobal初期化式へ配置、Decode成功後Validateでconstant expression required |
| D49 | [InterpreterContext_BranchTests.cs](../../../tests/WasmSharp.Tests/Execution/InterpreterContext_BranchTests.cs:68)の`InterpreterContext_BranchTests.末尾の値を保持して分岐する_順序と全値型を保ち破棄範囲を解除して現在の値数とPcだけを変える` | keepCount0/1/11、discardCount1/2/11、重なる全7型とnull/非null参照を保持、破棄参照解除・locals/caller保護 |

## opcode集合と責務の接続

[InstructionSet_TryGetTests.cs](../../../tests/WasmSharp.Tests/Instructions/InstructionSet_TryGetTests.cs)のD43〜D45は、固定付録のprefix0=183・FC=18・FD=236、合計437割当と全欠番を照合する。今回の監査も固定付録`index-instructions.py`/`macros.def`から番号と名前を展開し、全437個と現在宣言を一致確認した。実行対象は次の160個と一致する。

| 集合 | opcode | 個数・確認 |
| --- | --- | --- |
| 非constスカラー数値 | 0x45〜0xC4、FC0〜7 | 128+8=136。NumericSignatureとhandlerを全件確認、個別境界テストの全methodが11.1のTRXでpassed。 |
| 既存スカラーconst | 0x41〜0x44 | 4。既存Constant規則を保持、NumericSignatureなし。 |
| 制御/select | 0x01/02/03/04/05/0B/0C/0D/0E/1B/1C | 11。block/loopは線形化時にmarkerを除去、ifはBranchIfZero、else/brはBranch、br_if/表は各handler、select2形式はSelectへ接続。 |
| 先行統合 | 0x00/0F/10/1A/20/21/22/23/24 | 9。unreachable/return/call/drop/locals/globalをD01/D28〜D41で数値・制御と統合確認。 |

[opcode-inventory.json](../../../artifacts/numeric-control/task-11.2-20261012T-audit-5d3ee09/opcode-inventory.json)に全160行を保持する。数値宣言はInputCount/InputType/ResultTypeからFunctionValidatorがPop/Pushし、比較とeqzはi32、i64のclz/ctz/popcntはi64を返す。生成器はビルド時のAnalyzerであり、ランタイムは公式case IDや期待値へ依存しない。Decodeは即値・配置だけ、Validateは型と線形化、handlerは実行、既存ランナーは値・診断比較と保存を担当する境界を維持した。

## 残存未対応とblocked起点

今回の全2,100件をFeature名だけで推測せず、現在opcode属性と[linear-memoryのbrief](../linear-memory/brief.md)、[tables-referencesのbrief](../tables-references/brief.md)、[simdのbrief](../simd/brief.md)の対象境界へ対応付けた。全件がDecodeの`WasmUnsupportedFeatureException`で元locationと未確認範囲を持つ。この分類は入力の有効性を認定しない。typed参照/v128 selectのValidate段階の契約はD12で確認しているが、今回の全体runではそれ以前の後続opcode/sectionが起点である。

| 後続所有仕様 | 件数 | 今回の起点と所有根拠 |
| --- | --- | --- |
| linear-memory | 645 | 0x28〜0x40のscalar load/store・size/grow、FC8〜11のbulk memory、section.data/data_count。briefのguest memory命令・data初期化の範囲。 |
| tables-references | 377 | call_indirect、table.get/set・FC12〜17、ref.null/is_null/func、section.element。briefのtable/参照命令・element初期化の範囲。 |
| simd | 1,078 | 0xFDのprefixed opcode、vector memoryを含むCore2 SIMD命令。briefのSIMD全命令群の範囲。 |

全blocked33,806件は各1個のdirectとoriginsを持ち、directを再帰的に解決した起点と保存originsが一致した。各辺と起点は同じinput_path内でcommand_indexが先行し、起点は上表のruntime_unsupportedである。循環、不在ID、非先行ID、failed/runner_errorを起点とする依存、保存originの不一致は0。本仕様の136数値命令・制御/select・先行命令の未対応やランナー能力不足を後続へ移していない。

残りはpassed16,924件と、元JSONのmodule_type=textである対象外1,077件のみである。前提が揃うケースの一覧Pを保存し、期待値・actual・否定診断の成立は同一runの11.1独立全case監査Jに結び付ける。未対応へ到達していない部分の適合を、未対応module全体の有効性やblockedケースの成功に広げない。

## 11.2の検証と残る手順

新規の所有先・blocked・opcode監査と73行/source/正式method/TRX/代表case対応監査はnative終了0・問題0。既存文書のdiff-checkはnative終了0。新規文書の`git diff --no-index --check`は空ファイルからの差分を示すnative終了1で、stdout/stderrは空、空白不正0だった。監査自身の初回はtyped/untyped selectの共通名を重複として誤検出したため、重複検査をopcodeキーへ修正し、原ログを保持して再実行した。製品の不一致はなく、本体やテストの修正・全suite再実行は必要なかった。

11.2時点で10.6の監査条件は成立しており、同じ結果の受入確定を11.3へ引き継いだ。10.7の条件となる今回の不一致・回帰は0で、修正ループを実施したとは記録しない。10.8の比較済み同じrunからのbaseline保存は11.4で未実施であり、このタスクでは新旧baselineを更新していない。

## 11.3の受入条件確定

2026-10-12、HEAD`a05bf79dd44bae825175eb92b38a3351f6f3687a`で11.1の同じrun/comparison、11.2の対応監査と独立監査を現在状態へ照合した。要件10.3・10.4・10.5・10.6・10.7、designの「責務境界」「要件対応」「公式受入のBatch契約」「エラー処理」を確認し、10.6の受入条件成立を確定した。

- run/comparisonのSHA-256は「同一結果と監査出力」に記載した値と一致する。全147入力/53,907commandの処理と比較が完了し、failed・入力/commandのrunner_error・回帰・欠落・未比較・条件差・素材差は0。以前passedだった2,491件を保持した。同じ保存結果のrun/compare-runはnative終了0で、run完了後に比較している。
- 前提が揃う16,924caseは全てpassed。11.1の独立した値・NaN・否定診断監査と11.2のsource/coverage監査を同じ結果hashへ結び付けた。全73受入基準の対応、437割当/160実行対象/136数値命令、直接49method/280展開、代表815caseの監査原結果はnative終了0・問題0で、参照sourceの現在hashと一致する。10.8の手順はこの対応から完了扱いにしない。
- 残るruntime_unsupported2,100件はlinear-memory645・tables-references377・simd1,078で、blocked33,806件はその未対応起点だけに依存する。Feature・元case・位置・未確認範囲と所有先、全依存の監査明細は不変。対象外1,077件は元JSONのtextだけであり、本仕様の未実装やrunner能力不足の付替えはない。
- src/tests/toolsは11.1の実行revision`cb665f2b8b48e0c2fcbda69feedaa77d48c67813`から不変。不一致・runner_error・回帰・対象規則欠落は検出されず、原因タスクでの修正と境界テストを要する条件は成立しなかった。10.7の修正ループや、その後の11.1/11.2再実行を実施したとは記録しない。記録のみのためRED・feature flagは適用外で、build・3suite・公式run/比較も再実行していない。
- 現在整合監査と2文書のdiff-checkはnative終了0・問題0。原ログ、コマンド、UTC、native終了値、現在hashと検証範囲は[11.3の検証記録](../../../artifacts/numeric-control/task-11.3-20261012T-acceptance-a05bf79/verification.md)に保存した。11.3の独立レビューと親の完了確認は別途行う。

11.4には上記SHA-256の同じrun/comparisonを渡す。10.8のbaseline-saveは未実施で、新旧baselineは保持した。10.6の条件確定はnumeric-control全体のGOや、後続未対応・blockedも0を要求するCore2.0全体の完成判定を意味しない。
