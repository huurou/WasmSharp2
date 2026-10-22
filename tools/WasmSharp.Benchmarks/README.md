# WasmSharp.Benchmarks

WasmSharp2の公開APIを使う性能測定ツール。現在の実行入口はBenchmarkDotNetの導入確認用の試走で、性能基準値の取得には使用しない。

固定した6素材・15ケースについては、[分類・開始状態・期待値の根拠](Assets/README.md)と[cases.json](Assets/cases.json)を参照する。素材の統合生成は利用できるが、15ケースの測定CLIと検査器は後続タスクで接続する。

## ビルドと契約テスト

.NET10 SDKを使い、リポジトリのルートから実行する。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build
```

ビルドの警告・エラー0を確認してからテストを実行する。TUnit1.66.16の通常テストは、出力先にコピーしたJSONの設定fixture・保存契約・15ケースの固定値を読み、固定Jobとexporter・diagnoserの構成や独立計算した期待値を確認する。BenchmarkDotNetの起動、WABT・Clangによる素材生成、性能の合否判定は行わない。

## 本測定用の設定

`Measurements/BenchmarkConfig.Create()`は、同期処理用の`Measurement` Jobを.NET10・x64・Release・Throughput・1launchへ固定する。MemoryDiagnoser、Full JSON、measurement CSVを有効にし、MemoryRandomizationと電源planの自動変更を無効にする。pilot・warmup・本測定の反復数、1iterationの操作数とunrollは固定せず、BenchmarkDotNet0.15.8の自動調整を使う。現在のCLI入口は下記の短縮試走設定を使用し、この本測定設定と15ケースの接続は後続タスクで行う。

各launchは新しいprocessで実行する。JIT・pilot・warmup・本測定に加え、MemoryDiagnoserによる割り当て観測用の追加反復がある。module・instanceの生成範囲と状態復元は測定ケースが定義し、設定クラスでは生成や復元を行わない。

outlier方針はThroughput既定の`RemoveUpper`を明示する。Full JSONとmeasurement CSVには除外前の`Workload/Actual`と、overhead中央値の控除とoutlier処理後の`Workload/Result`を残す。rawのlaunch・iteration・mode/stage・操作数・経過nsから反復数と操作当たり時間を再確認できる。平均・中央値・標準偏差・99.9%信頼区間はバッチ反復の操作当たり平均に対する統計であり、個々のInvokeの遅延分布を表さない。Jobの表示・実効設定と実際の反復数も試走記録に残す。

MemoryDiagnoserのGC総回数・操作数・割り当て量は時間測定とは別の観測区間に対応する。managed割り当ては保持量、ピーク量、nativeメモリ、guest線形メモリ内の利用量とは区別する。通常reportの丸めたbytes/opだけで割り当ての不存在を判断しない。

## Windows x64での別process試走

BenchmarkDotNet0.15.8を固定し、.NET10/x64/Releaseの別processで、空moduleの公開`Decode`を実行する。1操作は1回のDecodeで、バイト列は事前に用意し、戻り値を保持する。setupで事前検証し、cleanupで最後のmoduleをValidate・Instantiateする。素材コンパイラは不要。

Job名は`Smoke`、`RunStrategy.Throughput`、launch/warmup/測定は各1回、1iterationは64操作、unrollは1とする。MemoryRandomizationを無効にし、電源planは変更しない。JIT・overhead・割り当て観測用の反復はBenchmarkDotNetも実行する。少数反復による統計の不足や短いiterationの警告は試走の制限であり、時間・bytes/opを性能基準値や改善の判定に採用しない。

上記ビルドが成功した後、毎回新しい保存先で実行する。

```powershell
$smokeRoot = Join-Path (Get-Location) ("artifacts/runtime-performance/smoke-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot | Out-Null
dotnet --info | Set-Content (Join-Path $smokeRoot 'dotnet-info.txt')
git rev-parse HEAD | Set-Content (Join-Path $smokeRoot 'revision.txt')
git status --short | Set-Content (Join-Path $smokeRoot 'git-status.txt')
git diff --binary HEAD | Set-Content (Join-Path $smokeRoot 'changes.patch')
powercfg /getactivescheme | Set-Content (Join-Path $smokeRoot 'power-plan.txt')
dotnet run --project tools/WasmSharp.Benchmarks/WasmSharp.Benchmarks.csproj -c Release --no-build -- --filter '*SmokeBenchmarks*' --artifacts (Join-Path $smokeRoot 'bdn') 2>&1 | Tee-Object -FilePath (Join-Path $smokeRoot 'smoke.log')
```

初回実装中など未追跡ファイルがある場合は、`changes.patch`には含まれないため、対象ソースをzip等にまとめて試走版を保持する。同名の`.csproj`をリポジトリ配下へそのままコピーすると、BenchmarkDotNetのproject探索が失敗する。SDK/runtimeと実行コマンド、電源接続状態、手動指定したGC/JITの環境変数を試走記録に残す。

`smoke.log`に子processの起動と終了0、1benchmarkの実行、MemoryDiagnoserの出力があることを確認する。`bdn/results/`の`*-report-full.json`には環境・統計・個々の測定値・Memoryが、`*-measurements.csv`には反復ごとの測定値が出力される。JSONの`BytesAllocatedPerOperation`が取得でき、CSVにも測定行があることを確認する。空moduleでもランタイムのオブジェクト生成によるmanaged割り当てが生じる。

この試走は測定器の導入確認に限定する。固定15ケースの本測定、反復条件、結果の採用判定、比較、公式適合検証は後続の実装・受入で確認する。

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

今回の素材採用では既存13ケースの入力・期待値・binary hashを保持し、小moduleの準備2ケースを追加した。公開APIの期待値確認はタスク2.1〜2.5の検証記録へ保存する。利用者向け`check`、15ケースの本測定・比較、最終公式全体runは後続タスクの実装・受入であり、この生成成功だけでは完了しない。

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
