# WasmSharp.Benchmarks

WasmSharp2の公開APIを使う性能測定ツール。現在の実行入口はBenchmarkDotNetの導入確認用の試走で、性能基準値の取得には使用しない。

## ビルドと契約テスト

.NET10 SDKを使い、リポジトリのルートから実行する。

```powershell
dotnet build WasmSharp2.slnx -c Release --warnaserror
dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build
```

ビルドの警告・エラー0を確認してからテストを実行する。TUnit1.66.16の通常テストは、出力先にコピーした小さいJSONの設定fixtureを読み、固定Jobとexporter・diagnoserの構成を確認する。BenchmarkDotNetの起動、WABT・Clangによる素材生成、性能の合否判定は行わない。

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

この試走は測定器の導入確認に限定する。固定15ケース、本測定用の反復条件、結果の採用判定、比較、公式適合検証は後続の実装・受入で確認する。

## 素材生成ツールの準備

PowerShell7、Windows x64、Visual StudioのC++ビルドツールとCMakeを使う。WABTは既存DataCount修正を含む固定commit`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`、Clang/LLDは21.1.8を使用する。`PATH`上の素材コンパイラは使わず、4実行ファイルを絶対pathで指定する。

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

### 最小WAT/Cの変換とCore2検査

```powershell
$toolCheck = Join-Path (Get-Location) ('artifacts/runtime-performance/toolcheck-' + [guid]::NewGuid().ToString('N'))
./tools/WasmSharp.Benchmarks/Assets/Prepare-Assets.ps1 `
    -Wat2WasmPath (Join-Path $wabtBuild 'Release/wat2wasm.exe') `
    -WasmValidatePath (Join-Path $wabtBuild 'Release/wasm-validate.exe') `
    -ClangPath (Join-Path $llvmBin 'clang.exe') `
    -WasmLdPath (Join-Path $llvmBin 'wasm-ld.exe') `
    -OutputDirectory $toolCheck
```

スクリプトはWABTのcheckoutとReleaseビルド条件、4ツールの版を検査する。既存の[Core2 profile](../WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)を読み、既定値との差からfeature引数を得る。別のfeature一覧は定義しない。最小WATとfreestanding Cの`add`を生成し、双方を同じprofileの`wasm-validate --verbose`で検査してimport0件を確認する。

Cの生成条件は`--target=wasm32-unknown-unknown -O2 -ffreestanding -nostdlib -fno-vectorize -fno-slp-vectorize -Werror -c`。Clangでobjectを作り、指定された`wasm-ld`を直接起動して`--no-entry --export=add --unresolved-symbols=report-all`でリンクする。コンパイルとリンクの引数・終了値は別々に保存する。Clang21.1.8のwasm targetでは`--ld-path`がunused警告となるため、Clangへのリンク指定で代用しない。未解決symbolを許容しない。[Clangのfreestanding条件](https://clang.llvm.org/docs/UsersManual.html#freestanding-builds)と[LLDのWebAssembly条件](https://lld.llvm.org/WebAssembly.html)も参照できる。

出力には最小sourceと2バイナリ、コマンド・引数・終了値を持つ`commands.json`、各ログ、`WABT-CMakeCache.txt`を保存する。成功した場合だけ`sources.json`を保存し、4ツールの絶対path・版・SHA-256、WABTのcommit・ビルド条件、profileとsource・生成物のhashを記録する。`scope=toolchain-smoke`はツール準備の確認を表し、固定6素材のmanifestや性能測定結果として使用しない。WABTのビルドログも保持し、異なる環境での実行ファイルhash一致は前提にしない。

既存出力先は拒否する。失敗した出力はログ確認用に残り、再実行には新しい出力先を使う。この段階では公開APIによる期待値照合、固定6素材・15ケースの登録、本測定、公式全体runは未実施で、後続タスクの受入に含める。

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
