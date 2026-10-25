# WasmSharp.Benchmarks

WasmSharp2の公開APIを使う性能測定ツール。固定素材の確認、ケースを選んだ測定と保存、保存run群の比較を行う。

固定した6素材・15ケースについては、[分類・開始状態・期待値の根拠](Assets/README.md)と[cases.json](Assets/cases.json)を参照する。測定結果の成功と、性能改善の受入成立は区別する。

採用する性能基準値、独立runのID/hashと再集計手順は[性能基準値](../../.kiro/specs/runtime-performance/baseline.md)を参照する。

## ビルドと契約テスト

.NET10 SDKを使い、リポジトリのルートから実行する。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build
```

ビルドの警告・エラー0を確認してからテストを実行する。TUnit1.66.16の通常テストは、出力先にコピーしたJSONの設定fixture・保存契約・15ケースの固定値を読み、固定Jobとexporter・diagnoserの構成や独立計算した期待値を確認する。BenchmarkDotNetの起動、WABT・Clangによる素材生成、性能の合否判定は行わない。

## 本測定用の設定

`run --kind measurement`は、同期処理用の`Measurement` Jobを.NET10・x64・Release・Throughput・1launchへ固定する。MemoryDiagnoser、Full JSON、measurement CSVを有効にし、MemoryRandomizationと電源planの自動変更を無効にする。pilot・warmup・本測定の反復数、1iterationの操作数とunrollは固定せず、BenchmarkDotNet0.15.8の自動調整を使う。`--kind`の省略時もmeasurementとなる。

各launchは新しいprocessで実行する。JIT・pilot・warmup・本測定に加え、MemoryDiagnoserによる割り当て観測用の追加反復がある。module・instanceの生成範囲と状態復元は測定ケースが定義し、設定クラスでは生成や復元を行わない。

outlier方針はThroughput既定の`RemoveUpper`を明示する。Full JSONとmeasurement CSVには除外前の`Workload/Actual`と、overhead中央値の控除とoutlier処理後の`Workload/Result`を残す。rawのlaunch・iteration・mode/stage・操作数・経過nsから反復数と操作当たり時間を再確認できる。平均・中央値・標準偏差・99.9%信頼区間はバッチ反復の操作当たり平均に対する統計であり、個々のInvokeの遅延分布を表さない。Jobの表示・実効設定と実際の反復数も試走記録に残す。

MemoryDiagnoserのGC総回数・操作数・割り当て量は時間測定とは別の観測区間に対応する。managed割り当ては保持量、ピーク量、nativeメモリ、guest線形メモリ内の利用量とは区別する。通常reportの丸めたbytes/opだけで割り当ての不存在を判断しない。

MeasurementとSmokeは`WasmAllocationEngineFactory`を使用する。時間測定と自動調整はBDN標準エンジンへ委譲し、追加の割り当て反復では、準備用の最後のGC後にも`GC.WaitForPendingFinalizers()`を実行する。BDN0.15.8の標準順序では、このGCが起動したArrayPool清掃の割り当てがカウンター間へ混入するためである。最終Actual反復の操作数と標準エンジンが選んだunrollを引き継ぎ、process全体の割り当てカウンターを使う。固定量の減算やスレッド単位のカウンターへの置換はしない。workload自身が発生させたGCや背景処理の割り当ては引き続き計測対象となる。

追加反復は標準エンジンの`Run`が返った後に行うため、BDNの`BenchmarkStop`イベントより後になる。通常の時間結果・割り当て結果は保存されるが、このイベントを終了条件とする外部トレースでは追加反復を採取できない。割り当ての診断ではprocess終了まで採取する。使用factoryは保存Job設定に含まれ、修正前のharnessとは同条件の受入比較に使わない。

## 素材確認・ケース選択・結果保存

下記の素材生成後、リポジトリルートで実行する。`$assetSet`は生成済み素材のディレクトリ。`check`はmanifest/profile/バイナリ/入力のhashを照合し、全ケースの公開操作と固定期待値を検査する。測定processからWABTやClangは起動しない。

```powershell
$manifest = Join-Path $assetSet 'asset-manifest.json'
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- check --assets $manifest
$checkExit = $LASTEXITCODE
$smokeRun = Join-Path (Get-Location) ('artifacts/runtime-performance/smoke-' + [guid]::NewGuid().ToString('N'))
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets $manifest --case invoke.single --kind smoke --output $smokeRun
$smokeExit = $LASTEXITCODE
```

`--case`は固定IDまたは`all`を受け付ける。Decode等の区間、1操作、仕事量、開始状態、計時外の準備はcase定義に保持し、Invokeは準備済みinstanceを使う。Smokeは新しい別processで1launch・1warmup・3測定iteration・各64操作・unroll1を使う。有限の信頼区間と保存経路を確認する短縮試走であり、数値は性能基準値へ採用しない。全体の配線確認には同じコマンドの`--case all --kind smoke`を使える。

本測定は毎回新しい保存先へ出力する。比較する両版で同じ引数とJob・環境条件を使い、独立runを交互に必要回数取得する。

```powershell
$measurementRun = Join-Path (Get-Location) ('artifacts/runtime-performance/measurement-' + [guid]::NewGuid().ToString('N'))
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets $manifest --case all --kind measurement --output $measurementRun
$measurementExit = $LASTEXITCODE
```

出力先は事前作成せず、既存ディレクトリ・ファイルへの出力は拒否する。`run.json`に予定case・用途・成否・指標、`environment.json`に実行processのコマンド・cwd・CPU/OS/SDK/runtime/GC/JIT/電源条件、`source/`と未追跡sourceコピーに使用版と差分を保存する。`inputs/`には素材・case・Job・子process設定、`raw/bdn/`にはFull JSON・measurement CSV・reportとBDNログ、`checks/`には同じcase/Job/PIDの前検査・実行開始・後検査を残す。親の終了値だけで成功とせず、`run.json`の子終了値、Errors、raw、前後検査、必須指標が揃ったことを確認する。未知の追加診断process終了値はnull、未完了はIncomplete、欠落指標は理由付きUnavailableのまま保持する。

終了値は成立0、保存できた測定失敗・必須指標取得不能・比較受入不成立1、引数/I/O/未完了2。`check`の期待値不一致も理由と終了1を返す。失敗時のrawや観測値は残すが、成功した性能測定へ採用しない。再試行は新しい出力先で行う。

## 保存した基準revisionの復元

PowerShell7.5以上とGitを使う。`Restore-RunSource.ps1`は保存runの`source_file`を読み、保存ファイルのhashを確認してから、HEADを独立cloneへcheckoutし、index差分、worktree差分、使用未追跡source/projectの順に復元する。元リポジトリのworktree・index・branchを変更せず、cloneのobjectも共有しない。使用runtime/harnessの全ファイルと、相対pathをOrdinal順に並べたSystem.Text.Json形式の集合hashを照合する。既存復元先と元リポジトリ配下の復元先は拒否する。元リポジトリ配下に同名projectを複製すると、BDNのproject探索にも影響する。

元リポジトリに保存HEADと必要なsubmoduleのobjectがあることを前提とする。初期化済みsubmoduleは保存revisionの独立cloneへ復元し、未初期化のものはgitlinkを保持する。使用ファイルの欠落・hash不一致、測定中のsource変更、dirtyなsubmoduleがある場合は停止する。失敗時も復元先を残すため、再試行には新しいpathを使う。保存runと素材の配置は変更しない。

```powershell
$repository = (Get-Location).Path
$savedRun = (Resolve-Path 'artifacts/runtime-performance/before-1').Path
$savedManifest = Get-Content (Join-Path $savedRun 'run.json') -Raw | ConvertFrom-Json
$savedEnvironment = Get-Content (Join-Path $savedRun $savedManifest.environment_file.relative_path) -Raw | ConvertFrom-Json
$restored = Join-Path ([IO.Path]::GetTempPath()) ('wasmsharp-baseline-' + [guid]::NewGuid().ToString('N'))
./tools/WasmSharp.Benchmarks/Restore-RunSource.ps1 -RunDirectory $savedRun -RepositoryDirectory $repository -DestinationDirectory $restored

# 素材は採用済みの同じmanifestを両版で参照する。再生成・期待値更新は行わない。
$manifest = (Resolve-Path 'artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json').Path
$restoredSmoke = Join-Path $repository ('artifacts/runtime-performance/restored-smoke-' + [guid]::NewGuid().ToString('N'))
Push-Location $restored
try {
    if ((dotnet --version) -ne $savedEnvironment.sdk_version) { throw '保存SDKの完全版と異なります。' }
    dotnet --info
    dotnet --list-runtimes
    dotnet build WasmSharp2.slnx -c Release --warnaserror
    if ($LASTEXITCODE -ne 0) { throw '復元版のReleaseビルドに失敗しました。' }
    dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- check --assets $manifest
    if ($LASTEXITCODE -ne 0) { throw '復元版の素材・期待値確認に失敗しました。' }
    dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets $manifest --case invoke.single --kind smoke --output $restoredSmoke
    if ($LASTEXITCODE -ne 0) { throw '復元版のSmokeに失敗しました。' }
} finally { Pop-Location }
$smokeManifest = Get-Content (Join-Path $restoredSmoke 'run.json') -Raw | ConvertFrom-Json
$smokeEnvironment = Get-Content (Join-Path $restoredSmoke $smokeManifest.environment_file.relative_path) -Raw | ConvertFrom-Json
if ($smokeEnvironment.runtime_version -cne $savedEnvironment.runtime_version) { throw 'runtimeの完全版が異なります。' }
```

SDKは`10.0`のような系列名で済ませず、保存した完全版を使う。今回のWindows x64はSDK`10.0.402`、runtime`.NET 10.0.13; 10.0.13-servicing.26504.104+4099f83977f4d29604a9aeb971fb0a7bc1d5f699`。選ばれるruntimeを`dotnet --info`で確認し、インストール先の`System.Private.CoreLib.dll`のProductVersionと保存値の後半を照合してから実行する。実行後も上記のように実際の`environment.json`で一致を確認する。異なる場合は同じ完全版を用意して両版を新規runへ再測定する。

基準値を採る前に、保存sourceの`harness.files`全体（計時・前後検査・集計コード、project設定、BDN依存版）と、`runtime.files`内のproject・共通build設定を固定する。改善の受入まで変更しない。GC/JIT関連環境変数、runtimeconfig、Job、電源plan・AC接続、CPU/OS・architectureも両版で揃える。sourceのhashは相対pathと内容で照合し、root/cwd・出力先・実行時刻・PID・assemblyの配置由来のhash差で代用しない。復元版のSmokeが成功し、保存source/hashと実行条件が揃ったことを確認してから本測定へ進む。Smokeは基準値に含めない。

本測定では、上記でbuildした復元版と候補版それぞれをcwdとして、次の同じ引数を使い、同じ環境・期間で順番を交互にして各3run以上を新規保存する。`$measurementRun`は毎回新しいpathとし、旧runを上書きしない。基準側runのsourceでharness集合hashが保存元と一致すること、候補側では変更対象のruntime以外に条件差がないことを、下記の保存run群の比較で確認する。条件差があれば性能改善の受入を保留する。

```powershell
Push-Location $restored # 候補版では候補の独立ディレクトリを指定する。
try {
    $measurementRun = Join-Path $repository ('artifacts/runtime-performance/baseline-measurement-' + [guid]::NewGuid().ToString('N'))
    dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- run --assets $manifest --case all --kind measurement --output $measurementRun
    if ($LASTEXITCODE -ne 0) { throw '本測定に失敗しました。' }
} finally { Pop-Location }
```

復元契約の検証は`./tools/WasmSharp.Benchmarks/Tests/Restore-RunSource.Tests.ps1`で実行する。Gitへの書込みは一時ディレクトリ内のfixtureと復元先だけで、staged/worktree差分・使用未追跡ファイルの復元、内容hash、元HEAD/index/statusの維持を確認する。

## 保存run群の比較

改善前に固定したpolicyを指定する。`--baseline`と`--current`にはrunディレクトリをカンマ区切りの1引数で渡し、空要素を含めない。比較入力とpolicyを読み取るだけで、期待値・基準値・policyを更新しない。入力run内の新規サブディレクトリへの出力も拒否する。

```powershell
$baselineRuns = @('artifacts/runtime-performance/before-1', 'artifacts/runtime-performance/before-2', 'artifacts/runtime-performance/before-3')
$currentRuns = @('artifacts/runtime-performance/after-1', 'artifacts/runtime-performance/after-2', 'artifacts/runtime-performance/after-3')
$policy = 'artifacts/runtime-performance/planned-comparison-policy.json'
$comparisonOutput = Join-Path (Get-Location) ('artifacts/runtime-performance/comparison-' + [guid]::NewGuid().ToString('N'))
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- compare --baseline ($baselineRuns -join ',') --current ($currentRuns -join ',') --policy $policy --output $comparisonOutput
$comparisonExit = $LASTEXITCODE
```

policyの型は[ComparisonPolicy.cs](Results/ComparisonPolicy.cs)にあり、各caseの時間・割り当ての許容幅、削減目標、同等範囲、ノイズ幅、反復間判断幅、必要独立run数を指定する。数値は基準値の同版再測定に基づいて事前に定める。`comparison.json`には入力manifest内のrun IDとhash、policy hash、caseごとの改善・同等・悪化・判定不能、個別値・差・理由・条件差を保存する。CLIの`--assets`の配置差や出力先は比較条件から除くが、case選択・用途・Job等の意味のある設定は照合する。

今回の変更前基準と選定1件は[性能改善計画](../../.kiro/specs/runtime-performance/optimization-plan.md)、全体と第1変更で共用する数値設定は[comparison-policy.json](../../.kiro/specs/runtime-performance/comparison-policy.json)を参照する。

最初の交互6runの結果と採用保留の理由は[第1変更の比較結果](../../.kiro/specs/runtime-performance/change-1-results.md)を参照する。

正常Measurementと必須比較が揃い、全caseの両指標が許容内で、選定目標の少なくとも1件を達成したときだけ受入成立となる。Smoke・失敗・取得不能・未完了のrunは改善判定へ採用しない。原因調査ではまずcase別の時間と割り当て、raw反復・GC・条件差・失敗段階を確認する。追加の診断や保持観測は通常Measurementと混在させず、専用手順でDiagnostic/Retentionとして保存する。

今回のWindows x64での操作確認と再実行scriptは[タスク8.1の記録](../../artifacts/runtime-performance/task-8.1-20261023T024900Z/evidence.md)を参照する。正常Smoke、隔離素材での後検査失敗、保存結果の比較除外を確認する。通常契約の比較成立は小さい保存fixtureで検証し、Smokeやfixtureを性能基準値へ採用しない。全15ケースの本測定基準値取得、改善policyの策定、最終公式全体検証は後続の受入作業であり、Ubuntuは未実行。

## 原因調査の診断コピーとDiagnostic出力

[採用基準値](../../.kiro/specs/runtime-performance/baseline.md)で観測した短いInvoke、guest内のcall、Decode/Validate等の時間・割り当ての寄与を調べるため、managed stackの時間sampleとGC割り当てsampleを同じprocessから取得する。以下は採取と読込の準備であり、改善対象や数値目標の選定は後続のprofile調査で行う。

Windows x64では`dotnet-trace`を`10.0.750501`に固定し、`--profile dotnet-sampled-thread-time,gc-verbose --buffersize 256 --buffering-mode Drop --format NetTrace`を使う。[公式NuGetの対象framework](https://www.nuget.org/packages/dotnet-trace/10.0.750501)と[公式リリース](https://github.com/dotnet/diagnostics/releases/tag/v10.0.750501)を確認し、下記の.NET10環境で採取を確認した。globalインストールやシステム設定変更は行わず、専用出力内へ`dotnet tool install --tool-path ... --version 10.0.750501 --arch x64`で配置する。

読込は`Microsoft.Diagnostics.Tracing.TraceEvent`の`3.1.21`を指定した小さい独立projectで行う。[公式NuGet](https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/3.1.21)の.NET Standard2.0対応を確認し、.NET10でのnettrace→ETLX変換とstack読込を確認した。読込project、依存lock、実行DLLとSHA-256は準備出力へ保存する。受入用projectの依存版は変更しない。

リポジトリルートから、採用runを**受入用の復元先とは別の、元リポジトリ外の新規path**へ復元する。PowerShell7.5以上と基準runと同じSDK/runtime完全版を使う。次のscriptは復元後、診断コピーの`Program.cs`だけを専用workloadへ替え、既存の`CaseCatalog`、`Initialize`、測定メソッド、`Cleanup`を直接再利用する。Wasmを処理するprocessを`dotnet-trace collect -- dotnet exec <workload.dll>`で起動するため、BDNの親だけを採取する経路はない。[公式の子process起動時の注意](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#launch-a-child-application-and-collect-a-trace-from-its-startup-using-dotnet-trace)も参照する。

```powershell
$repository = (Get-Location).Path
$diagnosticRecord = Join-Path $repository ('artifacts/runtime-performance/diagnostic-' + [guid]::NewGuid().ToString('N'))
$diagnosticCopy = Join-Path ([IO.Path]::GetTempPath()) ('wasmsharp-diagnostic-' + [guid]::NewGuid().ToString('N'))
./tools/WasmSharp.Benchmarks/Diagnostics/Prepare-Diagnostic.ps1 `
    -RunDirectory artifacts/runtime-performance/task-9.2-20261024T104127Z/run-1 `
    -DestinationDirectory $diagnosticCopy `
    -OutputDirectory (Join-Path $diagnosticRecord 'preparation') `
    -AssetManifestPath artifacts/runtime-performance/task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json

$preparation = Join-Path $diagnosticRecord 'preparation/preparation.json'
$diagnosticRun = Join-Path $diagnosticRecord ('Diagnostic-' + [guid]::NewGuid().ToString('N'))
./tools/WasmSharp.Benchmarks/Diagnostics/Collect-Diagnostic.ps1 `
    -PreparationPath $preparation -CaseId loop.integer-direct -Seconds 5 -OutputDirectory $diagnosticRun
```

準備先・復元先・採取先の再使用は拒否する。失敗した出力も残すため、再準備・再採取には新規pathを使う。準備出力の`preparation.json`に固定した実行ファイルの絶対path・版・SHA-256、採用runのhash、素材、SDK/runtimeを保存し、`source-final.json`に基準HEADとruntime/harnessそれぞれの内容hash、`diagnostic-program.diff`にコピーへの変更を保存する。`commands.json`は引数・cwd・終了値、各`.log`は出力、`recipe/`は使用script/templateを保持する。診断コピー、ツールと準備記録は後続の再採取まで保持する。

`Collect-Diagnostic.ps1`の`-CaseId`は固定15ケースから1件、`-Seconds`は1〜300秒を指定する。素材は読込時にhash照合し、反復前・反復後は既存の固定期待値で検査する。Invokeのmodule/instance/export/引数準備と準備系の開始条件は既存実装に従う。準備後に1秒反復してウォームアップし、指定秒数の反復を行う。processは採取ごとに新規、Invokeのinstanceは同process内で反復利用、Decode/Instantiateは既存caseの各操作で新規生成する。反復用delegate・時計確認と診断自体の費用が加わるため、この操作数や経過秒から受入性能値を計算しない。

採取先には`kind: Diagnostic`の`diagnostic.json`、`trace.nettrace`、`workload.json`、`readability.json`、変換済みETLXとコマンド・条件・hashを保存する。`workload.json`の対象PID、実際のruntime完全版、GC/JIT設定、前後検査、反復区間のUTCを確認する。`conditions.json`にCPU/OS、電源planと取得可能なbattery状態、採取profileを保存する。給電状態をbattery情報から特定できない場合は未確認として扱う。JITの最適化設定の実効値は公開APIで網羅できないため、保存したruntimeconfig・GC/JIT環境変数とRyuJIT識別を使い、推測で埋めない。

`readability.json`は対象PIDかつウォームアップ後の反復区間に絞り、イベント数、`GC/AllocationTick`の型とstack、時間sampleのstackを保存する。イベント欠落がなく、両方でWasmSharpのstackを読めた場合に読込コマンドが終了0となる。`top-methods.log`の`dotnet-trace report ... topN --inclusive`はprocess全体（準備・warmupを含む）の補助表示であり、反復区間だけの寄与は`readability.json`で確認する。

既存traceだけを再読込する場合も、ETLXの新規保存先を使う。

```powershell
$prepared = Get-Content $preparation -Raw | ConvertFrom-Json
$reread = Join-Path $diagnosticRun ('reread-' + [guid]::NewGuid().ToString('N') + '.json')
& $prepared.dotnet exec $prepared.reader_assembly (Join-Path $diagnosticRun 'trace.nettrace') (Join-Path $diagnosticRun 'workload.json') $reread
```

時間sampleはthreadがstack上にあった時間の推定であり、CPU使用率そのものではない。割り当ては`GCAllocationTick`によるsampleとその重みであり、全objectの記録や通常Measurementの正確なbytes/opではない。GC、待機、未解決stackも併せて読む。[profileの意味とEventPipe bufferの制約](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#options-1)に従い、欠落があれば採取条件を明記して新規出力へ取り直す。診断付きの値や本節の出力を通常Measurement・比較policyへ混ぜない。

今回の最小採取、固定ツールのhash、保持する診断コピーと再実行pathは[タスク9.3の記録](../../artifacts/runtime-performance/task-9.3-20261024T111451Z/evidence.md)に残す。保持量・rootの採取は再利用方式が選ばれた場合に、同じprocess数・入力・小→大→小の反復列と強制GC条件を決め、別のRetention系列として準備する。今回は再利用未選定のため保持観測、全ケースの原因調査、最適化選定・数値目標策定は未実施。

## 素材生成ツールの準備

PowerShell7.5以上、Windows x64、Visual StudioのC++ビルドツールとCMakeを使う。WABTは既存DataCount修正を含む固定commit`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`、Clang/LLDは21.1.8を使用する。`PATH`上の素材コンパイラは使わず、4実行ファイルを絶対pathで指定する。

最初に[既存の外部ツール手順](../../thirdParties/README.md)に従って固定submoduleを用意する。次をリポジトリルートで実行し、素材生成用の`wat2wasm`とバイナリ検査用の`wasm-validate`を同じRelease構成でビルドする。`$cmake`は利用環境の実行ファイルへ合わせる。

```powershell
$ErrorActionPreference = 'Stop'
$cmake = 'C:/Program Files/Microsoft Visual Studio/18/Professional/Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
$wabtBuild = Join-Path (Get-Location) ('artifacts/runtime-performance/wabt-' + [guid]::NewGuid().ToString('N'))
& $cmake -S thirdParties/wabt -B $wabtBuild -G 'Visual Studio 18 2026' -A x64 -DBUILD_TESTS=OFF -DBUILD_LIBWASM=OFF -DWITH_WASI=OFF -DUSE_INTERNAL_SHA256=ON -DWERROR=ON
if ($LASTEXITCODE -ne 0) { throw 'WABTの構成に失敗しました。' }
& $cmake --build $wabtBuild --config Release --target wat2wasm wasm-validate --parallel 8
if ($LASTEXITCODE -ne 0) { throw 'WABTのビルドに失敗しました。' }
```

Clang/LLD21.1.8がなければ、[LLVM公式リリース](https://github.com/llvm/llvm-project/releases/tag/llvmorg-21.1.8)のWindows x64 archiveを取得できる。次はシステムへインストールせず、リポジトリ内の新しいディレクトリへ展開する例。取得物のSHA-256は公式GitHub release assetのdigestと照合する。

```powershell
$llvmRoot = Join-Path (Get-Location) ('artifacts/runtime-performance/llvm-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $llvmRoot | Out-Null
$archive = Join-Path $llvmRoot 'clang+llvm-21.1.8-x86_64-pc-windows-msvc.tar.xz'
$url = 'https://github.com/llvm/llvm-project/releases/download/llvmorg-21.1.8/clang%2Bllvm-21.1.8-x86_64-pc-windows-msvc.tar.xz'
Invoke-WebRequest $url -OutFile $archive
$expected = '749D22F565FCD5718DBED06512572D0E5353B502C03FE1F7F17EE8B8ACA21A47'
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $expected) { throw 'LLVM配布物のhashが一致しません。' }
tar -xJf $archive -C $llvmRoot
if ($LASTEXITCODE -ne 0) { throw 'LLVM配布物の展開に失敗しました。' }
$llvmBin = Join-Path $llvmRoot 'clang+llvm-21.1.8-x86_64-pc-windows-msvc/bin'
```

WABTはApache-2.0、LLVMはApache-2.0 WITH LLVM-exceptionで配布される。取得物のライセンスと表示は各配布元のものを保持する。ツールは素材準備時にのみ使用し、通常のTUnitや測定processから起動しない。

### 6素材・15ケースの生成とCore2検査

```powershell
$assetSet = Join-Path (Get-Location) ('artifacts/runtime-performance/assets-' + [guid]::NewGuid().ToString('N'))
./tools/WasmSharp.Benchmarks/Assets/Prepare-Assets.ps1 `
    -Wat2WasmPath (Join-Path $wabtBuild 'Release/wat2wasm.exe') `
    -WasmValidatePath (Join-Path $wabtBuild 'Release/wasm-validate.exe') `
    -ClangPath (Join-Path $llvmBin 'clang.exe') `
    -WasmLdPath (Join-Path $llvmBin 'wasm-ld.exe') `
    -OutputDirectory $assetSet
```

この1コマンドでWABTのcheckoutとReleaseビルド条件、4ツールの版を確認し、5つの保存sourceと生成するlarge WATから6バイナリを作る。既存の[Core2 profile](../WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)のenabled/default_enabled差分からfeature引数を得て、全素材を`wasm-validate --verbose`で検査する。importはcallsとpreparation-largeが各1件、それ以外は0件と照合する。ツール準備用のsmokeバイナリはこの集合へ含めない。

Cの生成条件は`--target=wasm32-unknown-unknown -O2 -ffreestanding -nostdlib -fno-vectorize -fno-slp-vectorize -Werror -c`。Clangでobjectを作り、指定された`wasm-ld`を直接起動してmatrixとmemoryをexportし、未解決symbolを拒否する。stackを先頭64KiB、memoryを2ページに固定する。引数の全体は[sources.json](Assets/sources.json)のkernelへ保持し、コンパイルとリンクの実行引数・終了値を別々に保存する。Clang21.1.8のwasm targetでは`--ld-path`がunused警告となるため、Clangへのリンク指定で代用しない。

出力は`wasm/`の6バイナリ、`sources/`の6source、コピーした`cases.json`・`core2-profile.json`・`NOTICE.md`・`source-definitions.json`・生成script、`commands.json`・各ログ・`WABT-CMakeCache.txt`を含む。全検査と採用済みbinary hashの照合が成功した場合だけ、既存DTO形式の`asset-manifest.json`と生成記録`sources.json`を保存する。生成記録のscopeは`runtime-performance-fixed-assets`。6素材と15ケースの分類、出典・固定版・利用条件・表示、source/tool/profile/binaryと生成引数のSHA-256を追跡できる。

manifestの参照はmanifest基準の相対pathで、素材・source・profile・ケースは出力内に保存する。ツール実行ファイルはコピーせず、元のツール配置への相対pathとhashで参照する。ツール参照の再照合にはその配置が必要で、素材だけの移動後にツールを再配置しても測定素材の内容は変わらない。各生成stepの引数hashは順序を保持する文字列配列をSystem.Text.Json既定設定のコンパクトJSONへ直列化したUTF-8列のSHA-256である。`base:<HEAD>;sha256:<内容hash>`のrevisionは基点と使用内容を区別し、未コミットのsourceやscriptをHEADと同一視しない。

既存出力先は拒否し、失敗時もログと中間出力を残す。再実行には必ず新しい出力先を使う。素材・入力・期待値・操作を変えたら対象ケースの`case_revision`を上げ、独立計算で固定値を見直してから新しい集合を生成する。生成処理は`cases.json`の採用hashや期待値を更新しない。旧manifest・ケース・結果は保持し、版またはhashの異なるケースを同条件の性能比較へ使わない。

今回の素材採用では既存13ケースの入力・期待値・binary hashを保持し、小moduleの準備2ケースを追加した。公開APIの期待値確認はタスク2.1〜2.5の検証記録へ保存する。生成後は上記の`check`と別process Smokeを行い、その後に独立した本測定・比較・公式全体runへ進む。この生成成功だけでは性能改善の受入を意味しない。

### 最終公式受入の比較元

本仕様の最終受入では、既存の[公式ランナー手順](../WasmSharp.TestSuiteRunner/README.md)で全体runを行い、その同じ結果へ`compare-run`と`verify`を適用する。準備時点では以下の存在・SHA-256を確認する。

| ファイル | SHA-256 |
| --- | --- |
| `artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json` | `C60DEC91F788791A9B790835B2629C2D970E3FB44AFAB4D63F209DF9097E263E` |
| `artifacts/simd/task-10.3-20261018T215944Z/run-baseline.json` | `31B0282AF0CC5B7B528A660AE409349C2A748538AC8E58FD3A0DA2E31E387D6D` |

```powershell
Get-FileHash artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json -Algorithm SHA256
Get-FileHash artifacts/simd/task-10.3-20261018T215944Z/run-baseline.json -Algorithm SHA256
```

[先行SIMD受入の10.2・10.3](../../.kiro/specs/simd/acceptance.md#102-単一最終runによる公式比較と全体判定)には、固定スイートのpassed52,830件・out_of_scope1,077件、failed・runtime_unsupported・runner_error・blocked・回帰・欠落0件と、比較・verify済みの同じrunからのbaseline保存が記録されている。ツールの最小変換成功や比較元の存在確認は、本仕様の最終公式受入の完了を意味しない。

## 子processの実行位置の保存

Jobの`EnvironmentMode.Affinity`は設定要求として保存する。通常Jobは継承maskをBDNへ再設定せず、`Default`の要求記録を保持する。実際の実行条件は各子processがSetup/Cleanupの計時外で`Process.ProcessorAffinity`から取得した前後maskを`checks/`へ保存し、比較時に照合する。Windows/Linuxで同APIが取得できるmaskを対象とし、未対応OSや取得不能はnullと理由を保持する。0やDefaultを観測不能の代用にしない。

全予定case/Job/launchの観測不足、前後差、同じJobの子process間差、run間差は受入不能となる。旧保存runの読込は維持するが、実観測のないrunは新harnessでの性能比較へ採用しない。特定CPUを固定する場合は起動workerのmaskを外側で設定し、子processの保存値まで確認する。

今回のCPU14/mask16384・PGO0による短期ゲートは、memory.copy-fillの同操作数で総1680bytes差が再出したため5runで停止した。Job再設定のAPI警告は消えたが、不安定性の原因解消や性能受入は確認できていない。[再試行2の結果](../../.kiro/specs/runtime-performance/change-1-results.md#103再試行2-子processのaffinity観測と短期ゲートの停止)を参照する。新版policy・候補比較・最終公式検証は未実施。

その後、1680bytes差を準備GC後のArrayPool清掃と特定し、[割り当て測定の修正](../../.kiro/specs/runtime-performance/allocation-fix.md)を行った。修正後の旧runtimeによるmatrix・memory・SIMD各3runは追加総量0だった。[SIMD時間の調査](../../.kiro/specs/runtime-performance/simd-time-investigation.md)では約2倍モードが再現せず、利用者の指示により未解決のままBlockを解除した。新harnessで基準値を再取得し、第1変更と最終版の交互比較まで実施した。割り当て削減は再現したが、時間のばらつきにより性能受入は未成立である。固定公式検証は成立しており、現在の結果と未解決事項は[検証引継ぎ](../../.kiro/specs/runtime-performance/acceptance.md)を参照する。
