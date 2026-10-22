# 固定素材の由来と利用条件

`sources/invoke.wat`、`sources/calls.wat`、`sources/memory.wat`、`sources/scan.wat`、`sources/kernel.c`は、このリポジトリの性能測定用に作成した素材で、外部のbenchmarkコードを含まない。利用・再配布にはリポジトリの配布方針を適用し、このファイルで追加の利用許諾は定めない。外部コードに由来する表示義務はない。

生成には固定WABTを使用する。WABT自体のApache-2.0ライセンスと表示はその配布物に保持する。既存Core2 profileを使用し、Core2外の機能を有効にしない。生成条件、source・tool・profile・binaryのhashは生成出力へ記録する。

`kernel.c`の生成にはClang/LLD21.1.8を使う。LLVM自体のライセンスと表示はツール配布物に保持し、Wasmへ外部ライブラリをlinkしない。固定Core2 profileによる検査には同じWABTを使う。素材の基点revisionと未コミット追加の区別、C source・tool・profile・binaryのhash、コンパイルとlinkの引数はタスク2.3の生成記録へ保持し、素材集合のmanifestへの統合はタスク2.5で行う。
