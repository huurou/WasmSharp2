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
