# 公式入力と変換器の取得

Core 2.0とCore 3.0の仕様・公式テストは、同じWebAssembly/specリポジトリを別々のサブモジュールとして固定しています。どちらも対象コミットの`test/core`配下にある全WASTを使用します。

| サブモジュール | 固定コミット | 公式入力 |
| --- | --- | ---: |
| `WebAssembly-spec-core2` | [`05ca4182176763112561ae20153975c12bd689e4`（v2.0.0）](https://github.com/WebAssembly/spec/tree/05ca4182176763112561ae20153975c12bd689e4/test/core) | 147ファイル |
| `WebAssembly-spec-core3` | [`d7aada502455b3d9176ca4cbe48be4724d511d2a`（wasm-3.0）](https://github.com/WebAssembly/spec/tree/d7aada502455b3d9176ca4cbe48be4724d511d2a/test/core) | 255ファイル |

## 仕様サブモジュールを取得する

新規クローンのルートで次のコマンドを実行します。入力のSHA-256には改行を含むGit上のバイト列を使うため、チェックアウト時の改行変換を無効にします。

```sh
git -c core.autocrlf=false submodule update --init --checkout
git submodule status
```

親リポジトリが記録した版が両方とも同時に展開されます。ランタイムのテストはCore 2.0側の命令付録を使います。両版の入力一覧・SHA-256・対象機能は、それぞれの検証プロファイルに記録しています。[Core 2.0](../tools/WasmSharp.TestSuiteRunner/Corpus/core2-profile.json)・[Core 3.0](../tools/WasmSharp.TestSuiteRunner/Corpus/core3-profile.json)

## wasm-toolsを取得する

[wasm-tools v1.263.0の公式リリース](https://github.com/bytecodealliance/wasm-tools/releases/tag/v1.263.0)から使用するOSとアーキテクチャのアーカイブを取得して展開します。Windows x64で本リポジトリの操作例に従う場合は、`artifacts/wasm-tools-1.263.0-x86_64-windows/wasm-tools.exe`に配置します。

```powershell
$wasmTools = './artifacts/wasm-tools-1.263.0-x86_64-windows/wasm-tools.exe'
& $wasmTools --version
(Get-FileHash -LiteralPath $wasmTools -Algorithm SHA256).Hash
```

採用コミットは`7a2fa422cdf9aeeaf488a2acac8bc94a078bfa32`で、版表示には`1.263.0`と`7a2fa422c`が含まれます。Windows x64公式配布版の実行ファイルのSHA-256は`3be416990124225fb4b683c8add1d910ea5bccb67de0963dab13a5599b685829`です。

公式テスト素材には両版とも`json-from-wast`を使います。不正なモジュールも含めて変換し、ランナーが各版の規則と期待診断に照らして判定します。ベンチマークではWATを`parse`で変換し、Clang/LLDの出力を含む全素材を`validate --features wasm2`で検証します。[公式テストの生成と実行](../tools/WasmSharp.TestSuiteRunner/README.md)・[ベンチマーク素材の準備](../tools/WasmSharp.Benchmarks/Assets/README.md)

以前のCore 2.0比較基準には、WABT 1.0.42にDataCount修正を加えた版を使用しています。[当時のコミット・パッチ・再生成手順](https://github.com/huurou/WasmSharp2/blob/44ce1cfff3c01b8a6f643f0d1daacfede596fe50/thirdParties/README.md)
