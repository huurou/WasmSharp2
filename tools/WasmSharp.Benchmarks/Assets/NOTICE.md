# 固定素材の由来と利用条件

`sources/invoke.wat`、`sources/calls.wat`、`sources/memory.wat`、`sources/scan.wat`、`sources/kernel.c`と`Prepare-Assets.ps1`が生成する`preparation-large.wat`は、このリポジトリの性能測定用に作成した素材で、外部のbenchmarkコードを含まない。利用・再配布にはリポジトリの配布方針を適用し、このファイルで追加の利用許諾は定めない。外部コードに由来する表示義務はない。

WATの変換にはwasm-tools 1.263.0を使い、ライセンスと表示はその配布物に保持する。全6素材を同じツールの`validate --features wasm2`でCore 2.0の機能範囲に照らして検査する。

`kernel.c`の生成にはClang/LLD21.1.8を使う。LLVM自体のライセンスと表示はツール配布物に保持し、Wasmへ外部ライブラリをlinkしない。出典と利用条件は同梱する`sources.json`とこのファイルに保持する。生成出力の`asset-manifest.json`と`sources.json`には基点revisionと使用source内容hashを分けて保存する。全6素材のsource・tool・profile・binaryと生成引数のhash、ツール固定版、コンパイル・link・検査条件を生成ごとに記録する。
