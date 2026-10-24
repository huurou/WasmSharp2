# 時間と割り当ての原因調査

タスク9.4で、[採用基準値](baseline.md)と同じruntime・素材の代表12ケースを診断した。記録日は2026-10-24。要件2.3、3.4、5.6、6.2、design.mdの「基準値と改善計画」「再利用する場合の寿命と保持量」「検証方針」に対応する。時間sampleと割り当てsampleの両方をコードへ対応付けた。改善の最終選定・数値目標・ComparisonPolicy・コード変更は9.5以降で行う。

## 採用条件と保存先

記録の基点を[`artifacts/runtime-performance/task-9.4-20261024T113533Z/`](../../../artifacts/runtime-performance/task-9.4-20261024T113533Z/)（以下`記録/`）とする。Task Brief、採取・読込・集計script、コマンド・終了値、詳細stackと最終source確認を保存した。

- 基準側は独立3runのMeasurement（[baseline.md](baseline.md)のID/hash）を維持した。診断は新規`Diagnostic-<case ID>/`へ保存し、受入の改善率・bytes/op・policyには使わない。
- 元repoの開始HEADは`2fa6848`。runtime集合は`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`、受入harness集合は`bfdd7e94b71ecbf21c50669310920af24dd10616861790f5d668b926609c824d`。元repoと受入用復元先の最終集合が一致した。
- 診断コピーは`C:/Users/taihe/AppData/Local/Temp/wasmsharp-diagnostic-9.3-20261024T111451Z-final`。runtimeは上記と一致、診断harnessは`48458c07040e4aebb314b2b4e85e89e90d167fc262db0a32b1bea63b499f667f`。9.3で変更した`Program.cs`が既存caseのInitialize・測定メソッド・Cleanupを再利用する。今回はコピーもツールも変更していない。
- 準備記録は`artifacts/runtime-performance/task-9.3-20261024T111451Z/preparation-final/preparation.json`、SHA-256は`90574cde499c2f53cb02534226854ce8b7d84eb8f61b2f8786fc7fa9cdd356c4`。各採取の前後と最終確認で固定105ファイル・コピーsourceを照合し、不一致0件。
- SDK`10.0.402`、runtime完全版`.NET 10.0.13; 10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`、BDN`0.15.8`、dotnet-trace`10.0.750501`、TraceEvent`3.1.21`。Release、Windows`10.0.26300`、X64、i7-13700K/24論理プロセッサ、Workstation GC、RyuJIT。各`workload.json`に実際のruntime・GC/JIT環境変数を保存した。
- profileは`dotnet-sampled-thread-time,gc-verbose`、256MB、Drop、NetTrace。各caseを新規processで直列採取し、1秒warmup後5秒反復した。高パフォーマンスplanを変更せず使用した。battery情報は空で、今回の採取から給電状態を確認できない。JITの内部実効値は保存した設定以上に推定しない。
- 素材はタスク2.5の`accepted-assets/asset-manifest.json`（SHA-256`b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a`）、casesは`bf0a93272f03e4bbb0717634724f39cc6d0f2169bc9fb84825614cf566ad0389`。全15ケースの定義・期待結果・計時harness・依存版は不変。

最初に`invoke.empty`、`loop.integer-direct`、`decode-validate.large`、`scan.scalar`を採取した。入口の配列・callごとの診断位置・準備時の配列コピーが見えたため、結果数、host/indirect、Decode、Instantiate、SIMDとkernelへ追加し、原因と範囲を確認した。全15profileを揃えることは目的にしていない。

## 採用profileと分析区間

各行の保存先は`記録/Diagnostic-<case ID>/`。case IDと実PIDでprofileを識別する。全12件で採取/read/topN終了0、前後検査Succeeded、EventsLost0、実PID・runtime一致、両系列のWasmSharp stack取得を確認した。完全なコマンド・cwd・終了値は各`commands.json`、外側の呼出しは`記録/<case ID>.command.json`・`.log`・`.exit.txt`、trace以外のhashは`diagnostic.json`と`stack-summary.json`から辿れる。

| case ID | PID | 5秒全体の時間sample/割当tick | 分析区間の時間sample/割当tick | trace.nettraceのSHA-256 |
| --- | ---: | ---: | ---: | --- |
| `invoke.empty` | 47532 | 2552/106033 | 2452/101863 | `c3cb0c99d32024c8797bd7a788208bf8402051bf153f4c65cd99d4e7c3261b7e` |
| `invoke.single` | 68836 | 2549/60167 | 2442/57747 | `64e2416006158256e87ec863c9a67e40295c3fe17a40a2842112bac5a88906c9` |
| `invoke.multi` | 74812 | 2553/88237 | 2453/84739 | `a8b13f8818d21560372197edb39321e5e2d4d9d84656c174ba780569f4412788` |
| `loop.integer-direct` | 29088 | 2587/9500 | 2487/9121 | `bca6187feedfe0f7a6f79bc6e75698af43b1a597c3a404c9c9fbd6b638830562` |
| `loop.indirect` | 78956 | 2591/9822 | 2478/9432 | `ca25334a165a5d2b4e2e5cc803b3a58b9c80e11314d003cabd5f19b9bc4e3bea` |
| `loop.float-host` | 19172 | 2585/20016 | 2484/19224 | `4afd9ac235fb873efb62d4947a7b8a1cf3dafc15c7cb9aea0d618e6d6e57e3a5` |
| `decode.large` | 24004 | 2415/278110 | 2323/267121 | `279fd80253949cbe24db68734020e2286c0f4d27a23d322d797fb646321731ec` |
| `decode-validate.large` | 30432 | 2425/204466 | 2322/196449 | `3b77b937cb2dc4631667acccdeeafafcb727baaa6c7af12fbb1d9e9006c034d0` |
| `instantiate.large` | 61256 | 2469/389217 | 2366/374462 | `f40fc33eb9f220e7fe700e416b96fc03dbed5825e76378207214db4e2d76fb7d` |
| `scan.scalar` | 20632 | 2565/15 | 2464/13 | `01c56481cc20d3a8f9a04c1798569ae7f93982c5da38bd7be36e8c24c0b0bf0a` |
| `scan.simd` | 30668 | 2591/52 | 2491/49 | `21ccd70d8c0f4b5570faab418bc81e30e54a64d8d6d735c9abf74e0f95375b26` |
| `kernel.matrix` | 47744 | 2596/7 | 2496/7 | `a0c6f3b79b07e9e2857faab42f8e356045ae7da4d4ec4283f4b755d48bdeb6fe` |

元の`Diagnostic-scan.scalar/readability.json`には後検査の`Cleanup→CaseVerifier`の割当stackが2本含まれていた。UTCとtrace時刻の対応原因は未確認で、反復外処理を原因へ混ぜないため、全caseで元の反復UTC区間の前後各100msを除いた約4.8秒を分析に採用した。元trace・workload・抽出結果は保持し、`analysis/<case ID>.window.json`に元path/hash・元区間・除外幅・分析区間を明示した。既存readerを変えず再読込し、全12件でInitialize/Setup/Cleanup/CaseVerifier/InputFactory/LoadedAssetsを含むstackの時間sample・割当重みが0となった。

以下の寄与は`analysis/<case ID>.readability.json`を集計したもの。`workload.json`の操作数は元5秒全体であり、内側区間の重みを割ってbytes/op等を計算しない。時間はthreadのstack上のsample分布でCPU使用率ではない。leafのPollGCが全sampleの約80〜99%を占め、inliningでWasmSharpの内部frameが省略されたstackもある。最初のruntime frameは「sampleが現れた呼出し経路」であり、そのメソッドの独占CPU費用と断定しない。割当tickの型へ付く重みもsampling推定で、型別の正確な割当量ではない。

`Summarize-Stacks.ps1`はleaf、最初のruntime frameによる排他的分類、methodごとのinclusive分類、割当型を分ける。排他的分類は1stackを1回だけ数え、runtime frameがない分も分母へ残す。inclusiveは異なるmethod間で重複するため加算して全体比にしない。`top-methods.log`は準備/warmupを含むprocess全体の補助表示であり、以下の寄与へ使用しない。

## 観測とコードの対応

**guest内の直接・間接call。** 時間sampleの最初のruntime frameは`RunLoop`がdirect2485/2487件（99.920%）、indirect2474/2478件（99.839%）。割当重みでは`WasmFailureLocation`がdirect968,756,480（99.660%）、indirect1,003,073,440（99.788%）を占め、stackは`EnsureFrameCapacity→CallFunction→RunLoop`に対応する。[Interpreter.cs:222](../../../src/WasmSharp/Execution/Interpreter.cs:222)は成功する通常入口でも毎回`new WasmFailureLocation`を生成する。[Interpreter.Call.cs:113](../../../src/WasmSharp/Execution/Interpreter.Call.cs:113)はguest内callのたびに同じ容量確保境界を通る。固定`calls.wat`はdirect/indirectとも4096回のguestループで1回ずつcallするため、公開入口の1回を加えて1操作当たり4097回この生成箇所を通る。配列が足りる場合にも作る診断位置が、call回数に比例する割当原因であることと整合する。時間はRunLoop内の個別命令まで分離できず、型比較やdispatchを主要原因と断定する根拠はない。

**短いInvokeと結果。** 空Invokeの割当重みはcontext30.422%、frame配列26.137%、location30.425%、WasmResults13.016%。[InterpreterContext.cs:77](../../../src/WasmSharp/Execution/InterpreterContext.cs:77)のEnterが最外側ごとにcontextを生成し、Exitで解除する。最初のframe容量も各呼出しで確保する。single/multiはWasmValue[]が52.120%/72.139%で、`ImmutableArray.Create`経路が29.169%/37.535%、`EnsureCapacity`経路が22.952%/34.604%だった。[Interpreter.cs:185](../../../src/WasmSharp/Execution/Interpreter.cs:185)→[InterpreterContext.csのCopyValues](../../../src/WasmSharp/Execution/InterpreterContext.cs:329)でstackから所有配列へコピーし、[ExecutionBoundary.cs:97](../../../src/WasmSharp/Execution/ExecutionBoundary.cs:97)→[WasmResults.cs:15](../../../src/WasmSharp/WasmResults.cs:15)でも公開constructorがコピーする。profileではinliningのため2つのコピーを別々には定量できないが、所有済み結果から公開結果への再コピーがあることはコードと整合する。時間はRunLoop26〜31%、EnsureCapacity12〜28%が見え、runtime frameなし40〜62%もあるため、これらを足してAPI内の時間配分とは扱わない。

**host callback。** `loop.float-host`では最初のruntime frameがInvokeHost1415/2484件（56.965%）、RunLoop1066件（42.915%）。割当重みの99.917%がInvokeHost経路で、WasmValue[]82.510%、WasmResults17.464%。[Interpreter.cs:112](../../../src/WasmSharp/Execution/Interpreter.cs:112)の引数コピーとcallbackが返す所有結果に対応する。引数配列はcallbackが保持でき、結果の生成は[CaseVerifierの固定callback](../../../tools/WasmSharp.Benchmarks/Cases/CaseVerifier.cs)にも属する。この費用全体をランタイムだけの冗長割当とは扱わず、callback引数のコピーを単純にstack借用へ置き換えない。

**Decode/Validate。** `decode.large`の最初のruntime frameはExpressionDecoder69.780%、ReadCode13.775%、ReadData8.523%。割当型はDecodedInstruction[]51.397%、Byte[]40.856%。[ExpressionDecoder.cs:355](../../../src/WasmSharp/Modules/ExpressionDecoder.cs:355)のList成長と[ModuleDecoder.cs:568](../../../src/WasmSharp/Modules/ModuleDecoder.cs:568)の所有命令列へのコピー、[ReadData](../../../src/WasmSharp/Modules/ModuleDecoder.cs:394)の所有dataコピーが見える。`decode-validate.large`ではFunctionValidator60.896%、ExpressionDecoder24.074%で、割当型はDecodedInstruction[]32.940%、Instruction[]26.822%。Instruction[]はbuilder確保13.546%、ToArray13.276%へ分かれ、時間の代表stackも`Builder<Instruction>.ToArray→FunctionValidator.Validate`だった。[FunctionValidator.cs:51](../../../src/WasmSharp/Modules/FunctionValidator.cs:51)で入力命令数のbuilderを確保し、[同582行](../../../src/WasmSharp/Modules/FunctionValidator.cs:582)でToImmutableがコピーする。発行命令数が入力数より少ない分岐（102・382行）があり、無条件のMoveToImmutable置換を提案しない。Validate単体の時間やbytes/opは算出していない。

**Instantiate、走査、kernel。** Instantiateは最初のruntime frameがModuleInstantiator39.856%、WasmInstance constructor33.474%、InitializeDataSegments18.174%。割当tickの型はByte[]だけを捉え、[WasmMemory.cs:64](../../../src/WasmSharp/WasmMemory.cs:64)の初期ページ確保に対応する。これは必要なゼロ初期化であり、他型の割当が0という意味ではない。scalar/SIMD/matrixはRunLoopが99.878%/99.318%/99.880%で、命令別の時間原因までは分離できない。割当tickは13/49/7件と少なく、細かな型順位・比率を候補選定根拠にしない。基準値の448/448/656bytes/opと、1回の入口・stack・結果に属する割当stackが整合するという範囲に留める。

## 9.5へ渡す候補と制約

以下は観測に裏付けられた局所変更の候補であり、採用する1〜3箇所と数値目標は未確定。所有境界の整理を先に行い、既存境界が十分なら不要な抽出を増やさない。表のテストpathは`tests/WasmSharp.Tests/`からの相対path。

| 候補・対象case | 所有境界と狙う指標 | 守る契約・既存テスト・未解決点 |
| --- | --- | --- |
| 通常入口で使わないWasmFailureLocationの生成を容量超過時へ寄せる。direct/indirectと全Invoke | Interpreterの呼出し位置情報とInterpreterContextの容量計算。主に割当、時間は通常Measurementで確認 | 実装上限のStage・関数index・BodyOffset、広げた容量計算、失敗前の状態、深さ復元を維持。`Execution/InterpreterContext_StackTests.cs`、`Execution/ExecutionBoundary_InvokeTests.cs`、`Execution/Interpreter_CallTests.cs`の保持上限テスト、`WasmFunction_InvokeExhaustionTests.cs`を使用。位置情報の受渡し責務をどう分けるかは9.5で決める。 |
| 内部で所有済みの結果を公開結果へ渡す際の再コピーを減らす。single/multiと全結果ありInvoke | ExecutionBoundary・WasmResultsの内部所有契約。主に割当 | 公開constructorのコピー、返却済み結果の独立性、externref同一性、後続Invoke/同期再入を維持。`WasmResultsTests.cs`、`WasmFunction_InvokeFunctionBodyTests.cs`、`WasmFunction_InvokeReentryTests.cs`、`Execution/ExecutionResult_ValuesTests.cs`を使用。callbackの所有結果・借用stackを同じ扱いにしない。 |
| 検証完了時のInstruction builderの所有受渡しを整理する。decode-validate.large/small | FunctionValidator→FunctionCodeに閉じた不変コードの構築。時間と割当 | 成功時だけmodule状態確定、入力より少ない命令数、branch target/offset、全opcodeの検証と診断を維持。`WasmModule_ValidateTests.cs`、`WasmModule_ValidateControlIntegrationTests.cs`、`Execution/FunctionCode_ConstructorTests.cs`、制御・数値・SIMDの既存Validate/Invokeテストを使用。Count/Capacity差への対応が必要。 |

context/frame/value buffer再利用も空Invokeの原因に対応するが、保持容量・参照寿命・同期再入・異なるpolicyへの影響が大きい。今回は選定せず、採用する場合はdesign.mdの「再利用する場合の寿命と保持量」に従い、上限と根拠、小→大→小の保持観測、全終了経路の復元、不要なinstance/externrefのroot除去を変更前に計画する。host引数の借用化、必要なmemoryゼロ初期化の省略、profileだけを根拠にしたswitch/SIMD全面変更は、この調査から選定できない。

## Validate単体とgrow

今回の候補は既存Decode+Validate内でFunctionValidatorの具体的な配列確保・コピーまで対応し、独立のValidate費用がなくても所有受渡しの候補を示せるため、Validate単体を追加しなかった。DecodeとDecode+Validateの平均値の差をValidate結果とは報告しない。growは採用15ケースの操作にも観測した原因にも含まれず、追加する理由がない。再Validateや上限到達後のgrow失敗を成功操作へ混ぜていない。

今後切り分けが必要になった場合だけ、受入harnessとは別の診断コピーのPreparationBenchmarksに専用Jobを追加する。iteration外で未検証moduleまたは固定初期サイズinstanceをバッチ準備し、各要素を1回だけValidate/growする。InvocationCount・UnrollFactorを1、操作数をバッチ数と一致させ、growの増加量・成功期待値と開始状態を固定する。新規Diagnostic系列へ保存し、受入15ケースへ混ぜない。

## 再実行・確認結果と未実施範囲

採取はリポジトリルート、PowerShell7.5以上から[READMEの診断手順](../../../tools/WasmSharp.Benchmarks/README.md#原因調査の診断コピーとdiagnostic出力)を使う。今回のコマンド例は以下。採用出力は再使用せず、必ず未作成pathを渡す。

```powershell
./tools/WasmSharp.Benchmarks/Diagnostics/Collect-Diagnostic.ps1 -PreparationPath artifacts/runtime-performance/task-9.3-20261024T111451Z/preparation-final/preparation.json -CaseId loop.integer-direct -Seconds 5 -OutputDirectory artifacts/runtime-performance/Diagnostic-new-id
```

`Read-Interior.ps1`は保存traceから中央区間を既存readerで読み、`Recheck-Read.ps1`は同じ区間を別ETLXへ再読込する。今回12/12件でJSONのSHA-256が一致した（`analysis-recheck/recheck.json`）。`Summarize-Stacks.ps1 -OutputPath <新規JSON>`の再集計2回も一致し、両方のSHA-256は`8e0336d24b8916cb10b9d1900b19206b868ad592d40e401ec63898857c1dbf9e`（`reaggregation-check.json`）。分析の元区間・hash・全型/stack寄与は同JSONから再確認できる。`Verify-FinalSource.ps1`の結果は`final-source-check.json`で、不一致0、固定105ファイル、受入15ケースを確認した。

今回は診断と文書化のみでC#・テスト・source/project/依存版を変更していない。9.3のRelease build/契約確認・独立全4project成功を前提とし、同じテストを重複実行していない。通常CIへprofile起動テストや一時フラグを追加していない。最初のartifact作成はsandboxで拒否されたため、許可された診断記録の作成・採取・再集計をrequire_escalatedで実行した。採取・読込の失敗は0件。

未実施は小module2ケースとcopy/fillのprofile、Validate単体/grow、opcode別の時間帰属、CPU専用profile、保持メモリ/root観測、最適化・policy・候補版との性能比較、最終公式全体run/比較/verify、Ubuntu。今回の診断成功は性能改善や仕様全体の受入完了を示さない。

