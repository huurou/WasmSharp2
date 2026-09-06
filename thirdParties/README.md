# 外部ソースの固定

WebAssembly Core 2.0の仕様参照と公式テスト変換に使うソースを、Git submoduleで固定する。
親リポジトリのgitlinkが採用commitを保持し、`.gitmodules`が取得元を保持する。
上流ソースは変更せず、版を更新するときは公式入力と変換結果を改めて確認する。

## 採用版

| パス | 取得元・タグ | 固定commit |
| --- | --- | --- |
| `WebAssembly-spec` | [WebAssembly/spec v2.0.0](https://github.com/WebAssembly/spec/tree/v2.0.0) | `05ca4182176763112561ae20153975c12bd689e4` |
| `wabt` | [WebAssembly/wabt 1.0.41](https://github.com/WebAssembly/wabt/tree/1.0.41) | `03a00a1334e6121fb0cce4fccbd6bb109b68acaa` |

公式入力は`WebAssembly-spec/test/core/**/*.wast`の全147ファイルで、`simd`配下の57ファイルを含む。
WABT内の`third_party/testsuite`はWABT自身の検証用であり、本プロジェクトのCore 2.0公式入力には使用しない。
取得元のLICENSEとNOTICE等は各submodule内に保持する。

## 取得

リポジトリルートで実行する。以下は親リポジトリが記録したcommitを取得する。

```powershell
git submodule update --init --checkout -- thirdParties/WebAssembly-spec thirdParties/wabt
git -C thirdParties/wabt submodule update --init --checkout -- third_party/picosha2
git submodule status
```

下記の`wast2json`専用ビルドに必要なnested submoduleはPicoSHA2のみで、WABTが
`27fcf6979298949e8a462e16d09a0351c18fcaf2`に固定している。
specのKaTeXは仕様書の生成用であり、公式テストの変換には不要。
上流ツールの全依存も必要な場合は`git submodule update --init --recursive`で取得できる。
固定版の復元に`--remote`は使わない。

## WABTのCore 2.0設定

採用commitの[`include/wabt/feature.def`](wabt/include/wabt/feature.def)は、全21機能が以下の既定値になる。
`wast2json`にはfeature変更の引数を渡さず、この設定を使用する。

| 状態 | 機能 |
| --- | --- |
| ON | `mutable-globals`、`saturating-float-to-int`、`sign-extension`、`simd`、`multi-value`、`bulk-memory`、`reference-types` |
| OFF | `exceptions`、`threads`、`function-references`、`tail-call`、`annotations`、`code-metadata`、`gc`、`memory64`、`multi-memory`、`extended-const`、`relaxed-simd`、`custom-page-sizes`、`compact-imports`、`wide-arithmetic` |

`--enable-all`と`--no-check`は使用しない。未実装のランタイム機能を理由に入力やfeatureを減らさない。
既定値はWABTの版に依存するため、更新時にはこの表と実際の`--help`を照合する。

## Windowsでのビルドと変換確認

CMakeとVisual Studio 2026のC++ビルド環境を使用する。以下はPowerShell 7以降で、`cmake`がPATHにある前提。
WABT自身のテストとlibwasm・WASIを無効にし、SHA-256実装には固定済みPicoSHA2を使用する。

```powershell
cmake -S thirdParties/wabt -B artifacts/wabt-core2 -G "Visual Studio 18 2026" -A x64 -DBUILD_TESTS=OFF -DBUILD_LIBWASM=OFF -DWITH_WASI=OFF -DUSE_INTERNAL_SHA256=ON -DWERROR=ON
cmake --build artifacts/wabt-core2 --config Release --target wast2json --parallel 8
```

両コマンドが成功してから、リポジトリルートで全入力を変換する。

```powershell
$ErrorActionPreference = 'Stop'
$coreRoot = (Resolve-Path thirdParties/WebAssembly-spec/test/core).Path
$wast2json = (Resolve-Path artifacts/wabt-core2/Release/wast2json.exe).Path
$inputs = @(Get-ChildItem -LiteralPath $coreRoot -Recurse -File -Filter '*.wast' | Sort-Object FullName)
foreach ($inputFile in $inputs) {
    $relativePath = [IO.Path]::GetRelativePath($coreRoot, $inputFile.FullName)
    $outputPath = Join-Path 'artifacts/core2-conversion' ([IO.Path]::ChangeExtension($relativePath, '.json'))
    New-Item -ItemType Directory -Path (Split-Path $outputPath) -Force | Out-Null
    & $wast2json $inputFile.FullName -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw "変換失敗: $relativePath" }
}
"変換成功: $($inputs.Count)ファイル"
```

この確認は固定commitの選定と取得・変換の確認に限る。
JSONの`module_type=text`に対応する`.wat`も変換器の出力として残すが、ランタイムの対象外とし、合格件数に含めない。
正式なcorpus manifest、入力・生成物のhash一覧、ランナーによるassertionの評価は`wasm-test-corpus`以降の仕様で整備する。

## 移行時の確認結果

2026-09-06に、上記commitと手順で次を確認した。

- 空の作業用リポジトリへ`.gitmodules`と2つのgitlinkを設定し、spec・WABT・PicoSHA2の固定commitを公式リモートから取得できた。
- Windows x64、CMake 4.3.1-msvc1、MSVC 19.51.36256.0で、`WERROR=ON`のReleaseビルドが警告・エラーなしで成功した。
- 147入力すべての変換が成功した。出力はJSON147件、`.wasm`4,597件、`.wat`1,077件。JSON内の参照先ファイルもすべて存在した。
- `module_type=text`の1,077コマンドは対象外として識別した。WasmSharpによる実行・assertionの成否判定は行っていない。

この環境で生成した`wast2json.exe`のSHA-256は
`615121655C9240CF354F5AF7D6E7DB036A4FE6C1F6417EA7E2670E0166238B4E`。
この値は実測値であり、別のビルド環境で同一バイナリになることを保証するものではない。
