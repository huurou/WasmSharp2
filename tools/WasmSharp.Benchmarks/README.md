# WasmSharp.Benchmarks

WasmSharp2の公開APIを使う性能測定ツール。固定素材の確認、ケースを選んだ測定と保存、保存run群の比較を行う。

固定した6素材・15ケースについては、[分類・開始状態・期待値の根拠](Assets/README.md)と[cases.json](Assets/cases.json)を参照する。測定結果の成功と、性能改善の受入成立は区別する。

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

正常Measurementと必須比較が揃い、全caseの両指標が許容内で、選定目標の少なくとも1件を達成したときだけ受入成立となる。Smoke・失敗・取得不能・未完了のrunは改善判定へ採用しない。原因調査ではまずcase別の時間と割り当て、raw反復・GC・条件差・失敗段階を確認する。追加の診断や保持観測は通常Measurementと混在させず、専用手順でDiagnostic/Retentionとして保存する。

今回のWindows x64での操作確認と再実行scriptは[タスク8.1の記録](../../artifacts/runtime-performance/task-8.1-20261023T024900Z/evidence.md)を参照する。正常Smoke、隔離素材での後検査失敗、保存結果の比較除外を確認する。通常契約の比較成立は小さい保存fixtureで検証し、Smokeやfixtureを性能基準値へ採用しない。全15ケースの本測定基準値取得、改善policyの策定、最終公式全体検証は後続の受入作業であり、Ubuntuは未実行。

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
