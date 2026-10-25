# 性能改善計画

タスク9.5で、[基準値](baseline.md)と[原因調査](diagnosis.md)から1箇所だけを選定した。2026-10-24時点の変更前計画であり、ランタイムの変更と性能改善の受入はまだ行っていない。要件4.5、6.2、6.3、7.1〜7.5、および[design.md](design.md)の「基準値と改善計画」「再利用する場合の寿命と保持量」「完了条件」「公開契約の回帰テスト」に対応する。

## 採用する基準と固定条件

比較元は保存HEAD`6fdd7e28d629238306cd5e97960214cafdff8da4`と、そのrunに保存された差分・source集合で復元する。タスク9.5開始時のHEAD`e03bfdfac15c5ae351d793d9c5dee7df4767e96d`は文書等の進行位置であり、性能の基準revisionへ置き換えない。

| 採用Measurement | run ID | run.jsonのSHA-256 |
| --- | --- | --- |
| `task-9.2-20261024T104127Z/run-1` | `3206fba50e614febb8885410b7a05b13` | `535717bd4afd703c70a7264fb5a273cebf4db1565567a20c7ad11d6e6bc3f9a2` |
| `task-9.2-20261024T104127Z/run-2` | `57909a586a1644c0a00874fc932624b2` | `a4d48dae26184f15f189f7c8353e0c7c216982dd0ecdd188b6169dec490e73a0` |
| `task-9.2-20261024T104127Z/run-3` | `9340450da37b4a29896e076fc71e3b9c` | `aa696bd50dc8f35fd0ba00bdc7dd93aab3338321e0de8f513a0cd72db6c01c9e` |

上表のpathは`artifacts/runtime-performance/`からの相対path。保存統計は[summary/summary.json](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/summary/summary.json)と[measurements.csv](../../../artifacts/runtime-performance/task-9.2-20261024T104127Z/summary/measurements.csv)にある。CSVのSHA-256は`9a5ee71bac4b17c3e1ec544efe8dd10a86bd537498aaf8de96381c6046ad23f8`。

- runtime集合: `4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`
- harness集合: `bfdd7e94b71ecbf21c50669310920af24dd10616861790f5d668b926609c824d`
- 素材: `artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json`、SHA-256`b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a`
- cases: SHA-256`bf0a93272f03e4bbb0717634724f39cc6d0f2169bc9fb84825614cf566ad0389`。6素材/15case、区間・入力・期待値・仕事量・状態復元を維持する。
- 固定復元先: `C:/Users/taihe/AppData/Local/Temp/wasmsharp-baseline-9.1-10c52c20ac284cf89b401fce851df9a6`
- Windows x64、i7-13700K、SDK`10.0.402`、runtime`.NET 10.0.13; 10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`、BDN`0.15.8+1e8238ef6c08e18821edae83ac8d6b0a2bb1bb98`、Release/Measurement/Throughput/1launch、Workstation GC/RyuJITと既存の電源・AC条件を揃える。詳細Job/runtimeconfig/環境変数は採用runから照合する。

計時・検査・集計harness、project設定、依存版と素材は凍結する。診断用コピーは受入harnessへ混ぜない。harness修正が必要になれば候補の受入を中止し、旧runtimeと新harnessで基準値を取り直す。元の削減目標・許容増加・同等範囲を保持し、更新可能なのは基準側の再測定で根拠を得たノイズ・反復間判断幅だけとする。元の目標と許容幅で判断できなければ改善計画から見直す。

## 選定する1箇所と非選定の理由

選定は、定義関数の正常入口で不要な`WasmFailureLocation`を作る処理を、容量の保持上限を超える失敗経路へ移す変更とする。対象caseは`loop.integer-direct`と`loop.indirect`、狙う指標はmanaged割り当て量。全Invokeの入口も同じ箇所を通るが、別の改善件数には数えない。

採用profileは[タスク9.4の分析](../../../artifacts/runtime-performance/task-9.4-20261024T113533Z/analysis/)の`loop.integer-direct.readability.json`、`loop.indirect.readability.json`と同名のwindow JSON。中央の約4.8秒、準備/検査を除いた同じ区間を使用する。割当重みの99.660%/99.788%が`WasmFailureLocation`、stackが`EnsureFrameCapacity→CallFunction→RunLoop`だった。対応する元traceのSHA-256はdirect`bca6187feedfe0f7a6f79bc6e75698af43b1a597c3a404c9c9fbd6b638830562`、indirect`ca25334a165a5d2b4e2e5cc803b3a58b9c80e11314d003cabd5f19b9bc4e3bea`。

[Interpreter.EnsureFrameCapacity](../../../src/WasmSharp/Execution/Interpreter.cs:222)は通常成功時も位置を生成し、[CallFunction](../../../src/WasmSharp/Execution/Interpreter.Call.cs:113)からguestのcallごとに通る。固定素材は4096guest callと公開入口1回、合計4097回/操作。基準はdirect230176bytes/op、indirect229928bytes/opで3runともほぼ安定している。生成回数と参照型の生成箇所が対応し、失敗時にだけ必要な情報なので、公開所有・寿命を変更せず原因を減らせる。

時間sampleはPollGC/inliningの影響を含み、個別メソッドの独占CPU費用ではない。型別割当重みもsampling推定なので、99%台の重みを正確なbytes/opや削減率へ換算しない。低割当caseの少数tickから細かな順位を決めない。

結果の再コピー削減は公開結果の所有受渡し、Instruction builderは検証完了時の可変/不変コードの所有受渡しという別の責務を変更する。今回は上記1件で十分な削減を狙え、これらを追加すると検証範囲が広がるため選定しない。条件付きの第2・第3の改善（タスク11・12）は非選定。host引数の借用化、buffer/context再利用、memoryの必要な初期化省略、dispatch/SIMDの設計変更も対象外とする。

## 責務と所有を先に整理する

Moduleは`Interpreter`と`InterpreterContext`。Seamは定義関数の入口から容量確保へ渡す内部Interfaceであり、公開Interfaceは変更しない。`Interpreter`は引数・locals・operandの必要数と呼び出す定義関数を知り、`InterpreterContext`は配列の容量・使用数・保持上限・現在Stageを所有する。`ExecutionBoundary`は現在の例外伝播と公開結果の所有を維持する。

| 段階 | 局所変更と検証可能な範囲 | 予定files |
| --- | --- | --- |
| 10.1、責務整理、1〜3時間 | `EnsureCapacity(operandBase, maxOperandStack, function)`へ診断元の`DefinedFunction`を渡す。contextは既にframeの関数を扱うため新しい依存先は増えない。contextのメソッド入口で現行同様に1個のlocationを生成し、既存の容量計算へ渡す。位置の組立てを容量失敗を報告するModuleへ揃えるだけで、生成回数・例外・算術・拡張順を維持する。 | `src/WasmSharp/Execution/Interpreter.cs`、`InterpreterContext.cs`と内部Interfaceの既存テスト呼出箇所 |
| 10.2、最適化、1〜3時間 | 整理後のcontext内で、`CalculateCapacity`を現在Stageと渡された関数を扱う小さいinstanceメソッドへ揃え、`requiredCount > Array.MaxLength`の分岐だけでlocationを構築する。成功経路の事前生成を除く。delegate/closure/boxingや汎用factoryへ置き換えない。 | `src/WasmSharp/Execution/InterpreterContext.cs`、必要な内部容量テスト |

`WasmFailureLocation`は既存の公開sealed recordのままにする。新しい型、adapter、将来用hook、pool、分岐frameworkは追加しない。`CalculateCapacity`の内部テストも新しいInterfaceを通し、限界算術と失敗診断の確認を維持する。内部呼出箇所は[internal-call-sites.txt](../../../artifacts/runtime-performance/task-9.5-20261024T120137Z/internal-call-sites.txt)に展開した。fixture等の引数移行は同じSeamの機械的な修正に限定し、命令・資源処理を変更しない。

両段階で、加算前の`ulong`化、frame/value両方の必要数を検査してからの配列拡張、倍増と`Array.MaxLength`による制限、失敗前の外側の値・frame・深さを保つ。実装上限は同じ`WasmImplementationLimitException`、Reason/Limit/MessageとStage・呼び出し先FunctionIndex・BodyOffset・SectionId=nullを維持する。guest callでは呼び出し元call命令の位置へ変えない。trap/exhaustionと混同せず、既存の`finally`によるStage/深さ/参照復元、同期再入、最外側のpolicy共有、同じホスト例外実体の伝播を維持する。

結果・callback引数・locals・import実体・定義instanceの所有や、memory/table/segment/startの副作用を変えない。計画外の所有の変更や先行SIMD判断の変更が必要なら、実装範囲を拡張せず設計レビューへ戻る。

再利用は選定しない。locationの生成を省く変更はbufferやcontextの保持期間・容量を増やさず、instance/externrefのrootを追加しない。このため今回の計画にはRetention系列やWeakReferenceテストを追加しない。後から再利用へ広げる場合は、この計画を流用せず、容量上限の具体値と根拠、小→大→小の入力・process数・反復数、強制GC後の保持量/root比較、全終了経路の参照寿命確認を先に計画する（4.5）。

## 変更前に固定する数値基準

全体policyと第1変更policyは同じ[comparison-policy.json](comparison-policy.json)を参照する。policy IDは`runtime-performance-original-and-change-1-20261024`、SHA-256は`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`。全15case・各時間/割り当て・MinimumIndependentRuns=3を設定した。第1変更の比較元は元の基準revisionであり、第1変更と最終版に別々の数値を持たせない。

削減目標はdirect/indirectそれぞれ**180000bytes/op以上**。採用基準に対してdirectの候補中央値50176bytes/op以下（約78.201%削減）、indirect49928bytes/op以下（約78.285%削減）に相当する。4097回の不要な参照型生成と約230kbytes/opの基準値から、原因を十分減らす目標として定めた。100%削減や表示0を要求せず、残るcontext/frame/値/結果の必要割当を含める。型の正確なサイズやprofileの重みから将来の測定値を保証しない。対象外の13caseと全caseの時間に削減目標は置かない。

時間は各runの平均ns/opの中央値、割り当ては丸め前bytes/opの中央値で比較する。時間の`kind: ratio`は`fraction: 0.05`なら5%、割り当ての`kind: absolute`は`difference: 1`なら1bytes/op。時間をabsoluteで設定する場合の単位はns/opである。

時間の導出では、`M=独立3runの平均値の中央値`、`S=(最大−最小)/M`、`C=各runの99.9%CI半幅の最大/M`、`D=max(|各runの平均−M|)/M`とした。Noiseは`max(S,2C)`を0.1パーセントポイント単位で切り上げ、AllowedIncreaseとEquivalenceの減少/増加幅は同じ値を0.5ポイント単位で切り上げた。RunVariationは`D+C`を0.5ポイント単位で切り上げ、群内の全runをその群の中央値からの偏差で確認する。run間の全幅を優先しつつ、run内CI半幅2個分より狭い同等判定を避けるための運用上の幅であり、独立群間差の信頼区間として計算したものではない。

割り当ての観測幅は0〜0.0625bytes/opで、差が出た4caseはいずれも観測総量の差32bytesに対応した。Noiseは`max(観測幅,32/最小観測操作数)`とし、観測幅0のcaseも厳密な0ノイズとしない。許容増加、同等の両側、RunVariationは全caseで1bytes/op。最少128操作でも32bytes/128=0.25bytes/opの4倍を確保し、操作ごとの新規object追加に相当する増加を許す広い百分率にはしない。32bytesは今回見えた観測差に基づく余裕であり、測定器の普遍的な分解能とは断定しない。

以下の時間列は基準値に対する比率、割当列はbytes/op。全caseの割り当て許容/同等/RunVariationは上記1bytes/op。正確な値と全3runの値・導出は[policy-rationale.json](../../../artifacts/runtime-performance/task-9.5-20261024T120137Z/policy-rationale.json)に保存した。観測幅は将来の保証ではなく、幅を超えた群は判定不能として受入を止め、候補の結果から閾値を緩めない。

| case ID | 時間中央値ns/op | S | C | 時間許容/同等 | 時間Noise | 時間RunVariation | 割当中央値 | 割当Noise |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `decode-validate.large` | 154,499.470 | 6.402% | 1.921% | 6.5% | 6.5% | 7% | 723600 | 0.0078125 |
| `decode-validate.small` | 2,301.340 | 1.782% | 1.561% | 3.5% | 3.2% | 3% | 9112 | 0.00012207 |
| `decode.large` | 53,747.967 | 4.528% | 2.047% | 5% | 4.6% | 5.5% | 360496 | 0.00195312 |
| `decode.small` | 809.104 | 4.409% | 2.041% | 4.5% | 4.5% | 5% | 4328 | 3.05176e-05 |
| `instantiate.large` | 6,240.249 | 2.148% | 1.960% | 4% | 4% | 4% | 78720 | 0.000244141 |
| `invoke.empty` | 52.314 | 7.070% | 2.073% | 7.5% | 7.1% | 6% | 184 | 1.90735e-06 |
| `invoke.multi` | 316.695 | 1.149% | 1.216% | 2.5% | 2.5% | 2% | 640 | 1.52588e-05 |
| `invoke.single` | 275.607 | 0.471% | 0.424% | 1% | 0.9% | 1% | 384 | 1.52588e-05 |
| `kernel.matrix` | 4,601,953.348 | 4.737% | 1.604% | 5% | 4.8% | 5% | 656 | 0.25 |
| `loop.float-host` | 1,246,765.081 | 3.980% | 2.073% | 4.5% | 4.2% | 6% | 557504 | 0.0625 |
| `loop.indirect` | 1,033,469.095 | 2.293% | 0.250% | 2.5% | 2.3% | 2% | 229928 | 0.0625 |
| `loop.integer-direct` | 1,101,456.452 | 1.878% | 1.064% | 2.5% | 2.2% | 2.5% | 230176 | 0.0625 |
| `memory.copy-fill` | 2,561.699 | 1.200% | 1.689% | 3.5% | 3.4% | 3% | 416 | 0.00012207 |
| `scan.scalar` | 1,637,723.513 | 7.887% | 1.355% | 8% | 7.9% | 6.5% | 448 | 0.0625 |
| `scan.simd` | 422,562.943 | 6.569% | 0.838% | 7% | 6.6% | 5.5% | 448 | 0.015625 |

時間が事前の同等範囲内で割り当て目標を達成した変更は、時間短縮を追加条件にせず評価する。両対象caseの削減目標を確認し、他caseを含む30指標の許容内と比較前提の成立も確認する。比較ツールの総合受入は少なくとも1目標達成を要求するため、両対象の`allocation.target_achieved`も個別に確認する。同等側より大きい副次的な改善は表示してよいが、別の選定箇所として数えない。

## 必要な既存テストと守る契約

表のpathは`tests/WasmSharp.Tests/`からの相対path。新規テストを増やすのは、位置情報の遅延構築により変わる失敗位置/容量状態の検出力が既存テストで不足する箇所だけとする。

| 既存テスト | 保持する契約 |
| --- | --- |
| `Execution/InterpreterContext_StackTests.cs`内のCalculateCapacity/EnsureCapacity/Restore/EnterFrame | `uint.MaxValue`/`ulong.MaxValue`/加算前拡張、倍増上限、容量不足と十分な場合、失敗時に外側の値/frame/深さを保つこと、参照除去、locals初期値。変更した内部Interfaceでも同じ入力の検出力を保つ。 |
| `Execution/ExecutionBoundary_InvokeTests.cs` | 入口の保持上限をCollectionSizeのまま伝播し、Invoke段階・長いBodyOffset・関数index・InnerException=null・context解除を保つ。 |
| `Execution/Interpreter_CallTests.cs`、`Execution/Interpreter_CallIndirectTests.cs` | 呼び出し先の位置とInstantiate段階を持つ保持上限、外側Stage/深さの復元、直接/間接callの型検査・参照・所属instanceを保つ。 |
| `WasmFunction_InvokeExhaustionTests.cs`、`WasmFunction_InvokeReentryTests.cs` | 最外側の上限共有、正常/trap/exhaustion/ホスト例外後の復元と後続呼び出し、同じホスト例外実体、再入中の引数/localsの分離を保つ。 |
| `WasmFunction_InvokeTests.cs`、`WasmFunction_InvokeFunctionBodyTests.cs`、`WasmFunction_InvokeHostTests.cs` | 返却結果の独立性、値のビット列と参照、callback引数の有効性、呼び出しごとのlocalsを保つ。 |
| `Execution/ExecutionBoundary_RunStartTests.cs`、`WasmModule_InstantiateStartTests.cs`（classは`WasmModule_InstantiateTests`） | startのInstantiate段階、同期再入の状態復元、失敗までの副作用と保存参照の有効性を保つ。 |

容量上限の例外に、両段階Invoke/Instantiateで正しいStage・呼び出し先FunctionIndex/BodyOffsetが残る既存ケースを優先する。算術/両必要数検査の網羅を減らさない。追加が必要なら最小の直接テストを容量テストへ置き、数値・SIMD・memory/table/segment全組合せの新規テストは作らない。変更しない値と資源の契約（7.2、7.5）は最終の既存全回帰と固定公式スイートで確認する。

10.1と10.2では次の順にReleaseビルドと関連テストを実行する。fixtureの引数移行を含むためランタイムの既存テストプロジェクト全体も確認する。生成器・埋込sourceへ影響した場合は生成器テストを追加実行する。SIMD処理は変更しないためHWIntrinsic無効の追加測定/テストは今回の条件へ加えない。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/(InterpreterContext_CalculateCapacityTests)|(InterpreterContext_EnsureCapacityTests)|(InterpreterContext_RestoreTests)|(InterpreterContext_EnterFrameTests)|(ExecutionBoundary_InvokeTests)|(Interpreter_CallTests)|(Interpreter_CallIndirectTests)|(WasmFunction_InvokeExhaustionTests)|(WasmFunction_InvokeReentryTests)|(WasmFunction_InvokeTests)|(WasmFunction_InvokeFunctionBodyTests)|(WasmFunction_InvokeHostTests)|(ExecutionBoundary_RunStartTests)|(WasmModule_InstantiateTests)/*'
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build
```

最終版では上記に加えて他の既存3project（`WasmSharp.Generators.Tests`、`WasmSharp.TestSuiteRunner.Tests`、`WasmSharp.Benchmarks.Tests`）をRelease/`--no-build`で実行し、[公式ランナーREADME](../../../tools/WasmSharp.TestSuiteRunner/README.md)に従い固定公式全体run→同じ結果へのcompare-run→verifyを行う。期待値や診断、feature設定、対象集合を緩めず、比較前のbaseline更新はしない。

## 測定と受入の手順

10.3と13.1では、固定復元版Aと候補Bを同期間にA1→B1→A2→B2→A3→B3の順で各3run以上、新規ID/出力へ直列測定する。他のbuild/test/profileを並行実行しない。各runのcheck・期待結果・source/assembly前後hash・条件一致を確認する。第1変更と最終版は同じ元基準と同じpolicyで全15caseを比較するため、元基準から累積した悪化も同じ許容幅で検出する。旧3runは導出用の基準として保持し、同期間の基準再測定を省略する根拠にはしない。

[測定ツールREADME](../../../tools/WasmSharp.Benchmarks/README.md#保存した基準revisionの復元)の復元・本測定手順とcompareコマンドを使い、`--policy .kiro/specs/runtime-performance/comparison-policy.json`を指定する。測定前と比較時に上記policy hashを照合する。各caseの両指標、全run値/中央値/最小最大/CI、絶対差と比率、改善/同等/悪化/判定不能を保存する。目標未達等の許容内理由と、比較不足/条件差/過大ばらつき/正しさ不成立を区別する。必要な比較が欠ける場合や許容超過が残る場合は完了にしない。

## 9.5で行った検証と未実施範囲

記録は[タスク9.5](../../../artifacts/runtime-performance/task-9.5-20261024T120137Z/)。`Generate-Policy.py`、生成コマンド/終了値、Task Brief、数値導出を保存した。`Verify-Policy.ps1`が固定復元版のproduction JSON型、RunStore、比較処理を使用した。

- 既存CLIに採用3runを両群へ指定し、全15case/30指標のpolicy・値を読込んだ。終了1で同run IDの両群重複を拒否した。条件差・policy不正・重複以外の前提不成立は0件。同じrunの自己比較を受入不成立にする独立run契約であり、目標未達だけを理由とする実測比較ではない。引数/JSON不正等の終了2と区別した。
- 同じproductionの`CompareMetric`へ採用3runの値をそのまま渡す数値自己比較では、30/30指標がEquivalentかつ許容内。direct/indirectの割り当てだけTargetNotReachedとなり、その他の不成立は0件。合成runやID/hash書換えは使っていない。これはpolicyの読込・閾値整合確認であり、独立した前後測定による受入ではない。
- [validation-final/verification.json](../../../artifacts/runtime-performance/task-9.5-20261024T120137Z/validation-final/verification.json)、CLIのcomparison.json、30指標のmetric-self-comparison.jsonを保存。採用入力336ファイルの前後hash不一致0件、policy hashも不変だった。
- 最初の検証scriptはPowerShellのPSObjectからstringへのreflection引数変換で失敗した。型変換を直し、新規`validation-final`で検証終了0を確認した。元の失敗ログも保持した。最初の生成書込はsandboxで拒否されたため、許可された記録生成/検証をrequire_escalatedで実行した。

今回の変更は計画・JSON・READMEリンクのみでruntime/harness/C#を変更していない。新規単体テストや測定用フラグは追加せず、同じ全回帰を重複実行しない。source集合照合と差分検査は[final-check.json](../../../artifacts/runtime-performance/task-9.5-20261024T120137Z/final-check.json)へ保存する。最適化、候補との交互測定、保持観測、最終公式全体run/比較/verify、Ubuntuは未実施。9.5の完了は性能改善や仕様全体の受入を意味しない。

## 10.3再試行前のharness修正と基準再特徴化計画

初回の交互6runは全caseの前後検査が成立した一方、時間7caseの過大ばらつきで未採用となった。[第1変更の初回結果](change-1-results.md)と元policyを保持し、候補の受入を中止した。[診断結果](../../../artifacts/runtime-performance/debug-10.3-r1-20261024T134500Z/debug-report.md)は、基準側にも比較前提を満たさない時間モードがあり、外部workerの実行位置がJobでDefaultのまま記録される設定欠落を確認した。元runのCPU位置は不明であり、CPUやPGOが全7caseの原因とは断定しない。

design.mdの「基準値と改善計画」に従い、Measurement Jobへ親processの現在のaffinityを明示する最小修正を行う。runtime、素材、15case、計時区間、BDNのLaunchCount=1とpilot/warmup/反復調整は変えない。新harness集合は`8b026cd35c8b10190d44355ffb075987e1ba2bdd151b20d0e314989eb66a3211`。保存9.2run-1から新規tempへ復元した旧runtime集合`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`へ同じ修正を組み合わせる。既存固定復元先は変更しない。

新条件は私有workerのCPU14/mask`0x4000`（16384）、`DOTNET_TieredPGO=0`とし、SDK/runtime/GC/電源・素材を維持する。元親の設定はfinallyで復元し、collectorとBDN子PIDの実際のmask、ProcessorCount、保存Job.Affinity、環境変数、前後sourceを記録する。新条件の旧runtimeだけで全15caseを独立3run直列測定する。候補probeや初回C群は導出へ使わず、大きい複数モードが残る場合は幅の拡大で覆わず停止する。

この基準3runのみから上記9.5の式・丸めでNoiseとRunVariationを再導出する。元`comparison-policy.json`は変更せず、別版`comparison-policy-recalibrated.json`へ保存する。全15caseの削減目標（direct/indirect各180000bytes/op）、AllowedIncrease、Equivalence、必要独立run数は型と値を完全に保持する。変更可能な値はNoiseとRunVariation、識別子と作成時刻だけとし、差分とhashを保存して独立レビューする。

記録先は`artifacts/runtime-performance/task-10.3-retry1-20261024T141717Z/`。harness修正・基準側45case・新版policy固定の部分範囲が揃った時点で独立レビューへ引き渡す。レビュー承認前に候補の本測定は開始しない。承認後の新規交互6runと全30指標のproduction比較、元目標と許容幅による判断、最終公式検証は未完了であり、この再特徴化を10.3の完了や性能受入とは扱わない。


### 再試行1の結果: 基準側の前提不成立で停止

旧runtimeと新harnessの全15case×3runは保存契約・前後検査・実PID/mask・条件照合に成功したが、scan.simdの約2倍の時間モード、memoryの13.652%偏差、matrix/indirectの1bytes/opを超える同版割当差が残った。巨大な判断幅で覆わず、新版policyは作成していない。元policyと全目標・許容幅を保持し、候補測定は未実施。詳細は[第1変更の再試行記録](change-1-results.md#103再試行1-affinity記録の修正と基準側の再特徴化)とtask-10.3-retry1-20261024T141717Z/instability-summary.jsonに保存した。10.3はBLOCKEDであり、独立レビューcheckpointの前提となる版付きpolicy固定にも到達していない。原因調査と判断可能な基準の確立が再開条件となる。

### 再試行2の結果: 実観測の保存と短期ゲートの前提不成立

[debug round2](../../../artifacts/runtime-performance/debug-10.3-r2-20261024T144426Z/debug-report.md)に従い、再試行1のWithAffinity再設定を省き、Job要求値の保存と子processの計時外の前後mask観測を分離した。ProcessCheck→RunStore→RunConditions/RunComparisonへ接続し、観測欠落/取得不能/前後差/子process間差/run間差を比較から除外する。旧runtime/候補runtime/素材/元policyは維持し、両側新harness集合は`5b66c040c551df97c7964984140dc75f88ad91f2e8da7dfb02c3df36dc4af6f8`。Release警告・エラー0、Benchmarks.Tests320件成功、15caseのcheckと別process Smokeが成立した。

旧runtimeだけの短期ゲートをscan.simd×3→memory.copy-fill×3→kernel.matrix×3→loop.indirect×3へ事前固定した。scan3run後、memoryの2runで同じ131072操作に対して54525952/54527632bytes、正確に1680bytes差が再出したため5runで停止した。全子終了0・Errors空・check/mask一致・条件差0・raw再計算一致・警告0でも、既知不安定性が残るため10.3はBLOCKED。API警告の除去を原因解消の証明にしない。差引き・run選別・追加取り直しはせず、残り7run/45case/新版policy/候補測定は開始していない。元policyの削減目標・許容幅・同等範囲・必要独立run数は完全保持。詳細は[再試行2の結果](change-1-results.md#103再試行2-子processのaffinity観測と短期ゲートの停止)と`artifacts/runtime-performance/task-10.3-retry2-20261024T150905Z/`を参照する。

## 10.3再開: 修正済み割り当て測定による基準値の取得

2026-10-25の利用者の指示により、追加調査で再現しなかったSIMD時間の約2倍モードは未解決として記録し、10.3を再開する。[割り当て測定の修正](allocation-fix.md)で原因を確認したGC後処理は修正済みだが、その修正をSIMD時間の原因解消とは扱わない。新しい測定で異常が再出した場合は全結果を保持し、幅の拡大やrun選別で覆わない。

記録先は[task-10.3-resume-20261025](../../../artifacts/runtime-performance/task-10.3-resume-20261025/)。旧runtime集合`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`と候補runtime集合`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`を保持し、両側で修正harness集合`c3f92f2403345611b998a7420dc9054be38c6e190c3618115d3c60170f09fa8f`を使う。CPU14/mask16384、DOTNET_TieredPGO=0、HighPerformance/AC、SDK/runtime完全版・BDN・素材を固定し、observerはCPU14から外す。通常Measurementへtraceや診断設定を混ぜず、他のbuild/test/profileと並行実行しない。

先に旧runtimeだけの全15ケースを独立3run測定する。この3runだけから9.5の式でNoiseとRunVariationを再導出し、元policyを保持したまま別版`comparison-policy-recalibrated.json`へ保存する。AllowedIncrease、Equivalence、ReductionTarget、MinimumIndependentRunsは元の型と値を完全保持する。direct/indirectそれぞれ180000bytes/op以上の削減目標を変えない。候補データは導出に使わない。

基準3runと版付きpolicyの固定後に独立レビューを行う。承認後だけ、A1→B1→A2→B2→A3→B3で各3runを新規取得し、production compareの全30指標と両割り当て目標を確認する。再利用は追加しておらずRetentionは非該当。公開契約は変更していないため10.2/13.2の検証を引き継ぎ、今回の測定準備に不要なテストやフラグは追加しない。

### 再開の基準checkpoint

旧runtime・修正harnessの独立3runは45ケースすべてSucceeded、子process終了0・Errors空・前後検査成功・前後mask16384、production条件差0、raw時間/割り当て/GCの再計算一致となった。全15ケースで3runのbytes/opは一致し、SIMDの平均時間は591896.185、615218.242、596998.678ns/opで約2倍モードは再現しなかった。

時間のばらつきは残る。`decode-validate.small`の3runは3818.136、2767.034、2747.133ns/opでS=38.706%、`memory.copy-fill`は2604.066、3008.477、2594.198ns/opでS=15.909%だった。[全15ケースの値・導出](../../../artifacts/runtime-performance/task-10.3-resume-20261025/numeric-table.md)を隠さず保存し、9.5の式でNoise/RunVariationを再導出した。これらの値を許容増加幅へ転用しない。元のAllowedIncreaseとEquivalenceは前者3.5%、後者3.5%のままで、candidateの中央値増加は元の幅で判断する。

別版policy IDは`runtime-performance-recalibrated-change-1-20261025-resume`、SHA-256は`a7386a5bc1c7dd07a1483f7359aab5f4aadd51190b4522c8e041988fd93500a1`。[policy-diff.json](../../../artifacts/runtime-performance/task-10.3-resume-20261025/policy-diff.json)で全15ケースの目標・許容増加・同等範囲・必要独立run数の完全保持を確認した。production型の読込と数値自己比較は30指標すべてEquivalent/許容内、CLIは同じrunを両群へ使う操作を予定どおり終了1で拒否した。入力342ファイルのhash不一致0。これはpolicyの整合確認であり、候補との改善受入ではない。

baseline-1の外部observerはPIDだけを追跡し、終了済みPIDへの観測が混入した。3PIDの最終観測は対応postcheck最終更新より約80〜160秒後で、元のBDN終了0行もある。各maskの時刻とprocess開始時刻を保存していなかったため、混在値の全発生時点は復元できず、`external_observer_verified=false`として保持する。process内の前後mask一致は確認できるが、途中に配置変更が一度もなかったとは証明しない。この制約は同runの生データを変更せず記録した。baseline-2/3はobserver側だけをPID+開始時刻・process名・pathの同定とpostcheck後の監視停止へ修正し、16processずつの外部観測も成立した。harnessは変更していない。

候補Measurementは独立レビュー承認まで未開始。10.3の承認後に行う交互6runも、13.1の別途必要な最終比較としては扱わない。

### 再開の個別評価結果

基準checkpointの独立レビューAPPROVED後、固定policyのままA1→B1→A2→B2→A3→B3を新規取得した。全90ケースはSucceeded、条件差0、前後mask・生データ再計算・source固定が成立した。全6runと比較前後684入力のhashを保持し、不一致0件だった。

direct/indirectの割り当てはそれぞれ230176→744、229928→496bytes/opで、両方とも229432bytes/op削減し180000bytes/op目標を達成した。一方、production compareは終了1・IsAccepted=false、許容内を確認できたのは28/30指標だった。invoke.emptyの旧群の最大中央値偏差11.717%がRunVariation10.5%を超え、kernel.matrixは旧群4.795%・候補群3.992%がRunVariation3%を超えたため、これら2ケースの時間は判定不能・完了阻害となった。悪化判定は0件だが、比較前提が揃っていないため第1変更の受入完了とはしない。

SIMD時間は旧群中央値590450.837ns/op、候補群602740.765ns/opで2.081%増、元の同等範囲7%内かつ反復間条件内だった。約2倍モードは再出していない。この結果を根拠に元の原因が解消したとは断定しない。

詳細は[comparison-values.md](../../../artifacts/runtime-performance/task-10.3-resume-20261025/comparison-values.md)、[verification.json](../../../artifacts/runtime-performance/task-10.3-resume-20261025/verification.json)、[variation-observations.json](../../../artifacts/runtime-performance/task-10.3-resume-20261025/variation-observations.json)。候補の結果を使ったpolicy更新、追加取り直し、run選別は行っていない。10.3の受入と13.1以降の最終受入は未完了のまま記録する。
