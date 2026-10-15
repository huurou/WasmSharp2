# 外部ソースの固定

WebAssembly Core 2.0の仕様参照と公式テスト変換に使うソースを、Git submoduleで固定する。
親リポジトリのgitlinkが採用commitを保持し、`.gitmodules`が取得元を保持する。
承認された上流ソースの修正はsubmodule内でコミットし、親リポジトリのgitlinkで固定する。
上流未公開の修正commitはGit bundleと差分パッチを親リポジトリに保存する。
版やパッチを更新するときは公式入力と変換結果を改めて確認する。

## 採用版

| パス | 取得元・タグ | 固定commit |
| --- | --- | --- |
| `WebAssembly-spec` | [WebAssembly/spec v2.0.0](https://github.com/WebAssembly/spec/tree/v2.0.0) | `05ca4182176763112561ae20153975c12bd689e4` |
| `wabt` | [WebAssembly/wabt 1.0.42](https://github.com/WebAssembly/wabt/tree/1.0.42)とDataCount修正 | `c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db` |

WABTの修正元は`ff0ef7e0009402740c805a9744c09b05be063e48`である。
修正commitは[Git bundle](patches/wabt-datacount-zero.bundle)から復元できる。

公式入力は`WebAssembly-spec/test/core/**/*.wast`の全147ファイルで、`simd`配下の57ファイルを含む。
WABT内の`third_party/testsuite`はWABT自身の検証用であり、本プロジェクトのCore 2.0公式入力には使用しない。
取得元のLICENSEとNOTICE等は各submodule内に保持する。

## 取得

リポジトリルートで実行する。WABTの修正commitをbundleから読み込んでから、親リポジトリが記録したcommitへ揃える。
bundleは上記の修正元commitを前提とするため、先に上流履歴を取得する。

```powershell
git submodule init
if (-not (Test-Path thirdParties/wabt/.git)) {
    git -c core.autocrlf=false clone --no-checkout https://github.com/WebAssembly/wabt.git thirdParties/wabt
}
git -C thirdParties/wabt fetch origin ff0ef7e0009402740c805a9744c09b05be063e48
git -C thirdParties/wabt fetch ../patches/wabt-datacount-zero.bundle HEAD
git -c core.autocrlf=false submodule update --init --checkout -- thirdParties/WebAssembly-spec thirdParties/wabt
git -C thirdParties/wabt submodule update --init --checkout -- third_party/picosha2
git submodule status
```

公式入力のSHA-256は固定commitのGit blobの生バイト列で確定するため、取得時に改行変換を無効にする。
既に改行変換された作業コピーは上記コマンドだけでは元に戻らない。新しい配置先で改行変換を無効にして取得する。
生成時は入力を正規化せず、改行を含むバイト列の相違を不一致として扱う。

下記の`wast2json`専用ビルドに必要なnested submoduleはPicoSHA2のみで、WABTが
`27fcf6979298949e8a462e16d09a0351c18fcaf2`に固定している。
specのKaTeXは仕様書の生成用であり、公式テストの変換には不要
上流ツールの全依存も必要な場合は`git submodule update --init --recursive`で取得できる。
固定版の復元に`--remote`は使わない。

## WABTのCore 2.0設定

採用commitの[`include/wabt/feature.def`](wabt/include/wabt/feature.def)は、全21機能の既定値を確認し、以下の実効値でCore2.0を維持する。
1.0.42で既定ONの`exceptions`、`tail-call`、`annotations`、`memory64`、`multi-memory`、`extended-const`、`relaxed-simd`は、対応する`--disable-*`を渡してOFFにする。その他の機能は既定値と実効値が一致する。固定値と論理引数は[`core2-profile.json`](../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)に保持する。

| 状態 | 機能 |
| --- | --- |
| ON | `mutable-globals`、`saturating-float-to-int`、`sign-extension`、`simd`、`multi-value`、`bulk-memory`、`reference-types` |
| OFF | `exceptions`、`threads`、`function-references`、`tail-call`、`annotations`、`code-metadata`、`gc`、`memory64`、`multi-memory`、`extended-const`、`relaxed-simd`、`custom-page-sizes`、`compact-imports`、`wide-arithmetic` |

`--enable-all`と`--no-check`は使用しない。未実装のランタイム機能を理由に入力やfeatureを減らさない。
既定値はWABTの版に依存するため、更新時にはこの表と実際の`--help`を照合する。

## Windowsでのビルドと変換確認

CMakeとVisual Studio 2026のC++ビルド環境を使用する。以下はPowerShell 7以降で、`cmake`がPATHにある前提
WABT1.0.42には、データセグメントが0件の場合に必要なDataCountを省略する不具合がある。
上記で取得する固定commitは修正済みなので、そのままビルドする。
[修正パッチ](patches/wabt-datacount-zero.patch)は差分確認用に保持しており、再適用は不要である。
詳細と検証結果は[DataCount移行記録](../.kiro/specs/test-suite-runner/wabt-datacount-migration.md)を参照する。

以下の専用ビルドではWABT自身のテストとlibwasm・WASIを無効にし、SHA-256実装には固定済みPicoSHA2を使用する。
旧実行ファイルを保持するため、修正前と異なる出力先を使う。

```powershell
cmake -S thirdParties/wabt -B artifacts/wabt-core2-1.0.42-datacount -G "Visual Studio 18 2026" -A x64 -DBUILD_TESTS=OFF -DBUILD_LIBWASM=OFF -DWITH_WASI=OFF -DUSE_INTERNAL_SHA256=ON -DWERROR=ON
cmake --build artifacts/wabt-core2-1.0.42-datacount --config Release --target wast2json --parallel 8
```

各コマンドが成功してから、リポジトリルートで全入力を変換する。

```powershell
$ErrorActionPreference = 'Stop'
$coreRoot = (Resolve-Path thirdParties/WebAssembly-spec/test/core).Path
$wast2json = (Resolve-Path artifacts/wabt-core2-1.0.42-datacount/Release/wast2json.exe).Path
$inputs = @(Get-ChildItem -LiteralPath $coreRoot -Recurse -File -Filter '*.wast' | Sort-Object FullName)
foreach ($inputFile in $inputs) {
    $relativePath = [IO.Path]::GetRelativePath($coreRoot, $inputFile.FullName)
    $outputPath = Join-Path 'artifacts/core2-conversion' ([IO.Path]::ChangeExtension($relativePath, '.json'))
    New-Item -ItemType Directory -Path (Split-Path $outputPath) -Force | Out-Null
    & $wast2json $inputFile.FullName --disable-exceptions --disable-tail-call --disable-annotations --disable-memory64 --disable-multi-memory --disable-extended-const --disable-relaxed-simd -o $outputPath
    if ($LASTEXITCODE -ne 0) { throw "変換失敗: $relativePath" }
}
"変換成功: $($inputs.Count)ファイル"
```

この確認は固定commitの選定と取得・変換の確認に限る。
JSONの`module_type=text`に対応する`.wat`も変換器の出力として残すが、ランタイムの対象外とし、合格件数に含めない。
正式なcorpus manifest、入力・生成物のhash一覧、spectest・registerを含む実行・リンク基盤の公式評価と初回baselineは、`host-linking`に続く`test-suite-runner`で整備する。固定スイートに現れる全値型の比較も同じツールが初期から扱う。

## WABT自身のテスト

上記の修正済みcommit、Python 3、WABTの固定依存を用意する。WASIを無効にするためuvwasiは不要である。
以下はWindowsの標準CIに対応するテスト群を実行する。Core 2.0素材の生成とは別の検証である。

```powershell
git -C thirdParties/wabt submodule update --init --checkout -- third_party/gtest third_party/ply third_party/simde third_party/testsuite third_party/wasm-c-api third_party/picosha2
cmake -S thirdParties/wabt -B artifacts/wabt-datacount-tests -G "Visual Studio 18 2026" -A x64 -DBUILD_TESTS=ON -DBUILD_LIBWASM=ON -DWITH_WASI=OFF -DUSE_INTERNAL_SHA256=ON -DWERROR=ON
cmake --build artifacts/wabt-datacount-tests --config Release --parallel 8
cmake --build artifacts/wabt-datacount-tests --config Release --target run-unittests
cmake --build artifacts/wabt-datacount-tests --config Release --target run-c-api-tests
cmake --build artifacts/wabt-datacount-tests --config Release --target run-tests
```

1.0.42のテストランナーは、spec実行の失敗件数が3件の場合もskipと集計する。
終了0とpassed件数だけで全件合格と判断せず、skipの内訳と直接実行の結果を確認する。
2026-10-16の実測結果と検証範囲は[DataCount移行記録](../.kiro/specs/test-suite-runner/wabt-datacount-migration.md)に記載する。

## 移行時の確認結果

2026-09-06に、当時のWABT1.0.41とCore2.0の既定設定で次を確認した。

- 空の作業用リポジトリへ`.gitmodules`と2つのgitlinkを設定し、spec・WABT・PicoSHA2の固定commitを公式リモートから取得できた。
- Windows x64、CMake 4.3.1-msvc1、MSVC 19.51.36256.0で、`WERROR=ON`のReleaseビルドが警告・エラーなしで成功した。
- 147入力すべての変換が成功した。出力はJSON147件、`.wasm`4,597件、`.wat`1,077件 JSON内の参照先ファイルもすべて存在した。
- `module_type=text`の1,077コマンドは対象外として識別した。WasmSharpによる実行・assertionの成否判定は行っていない。

2026-09-27に同じCMake設定、MSVC 19.51.36252.0で再ビルドした`wast2json.exe`のSHA-256は
`B0E1D0316A265F659566A0E3B6F1A00FEA9E315E3D25F2C3E0853E414D0BE245`
同じ147入力の変換件数は上記と一致した。
この値は実測値であり、別のビルド環境で同一バイナリになることを保証するものではない。

## WABT1.0.42への更新

2026-10-12のユーザー承認に基づき、空typed selectの情報を保持する上流修正を含む1.0.42へ更新した。旧素材・旧baselineは保持し、全147入力の再生成・変換比較と、着手前revisionを用いた新比較元の作成を行う。手順と受入結果は[移行記録](../.kiro/specs/test-suite-runner/wabt-1.0.42-migration.md)に記録する。
