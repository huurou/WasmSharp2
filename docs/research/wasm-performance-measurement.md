# Core 2.0完了後の性能測定に向けた調査

調査日: 2026-10-19。外部ランタイムの公式文書・ソースと研究者が公開した素材を調べた。以下の「提案」「採否」はWasmSharp2への適用に関する判断であり、外部資料がWasmSharp2の性能や互換性を保証するものではない。ベンチマークのビルド・実行、依存追加、数値目標の決定は行っていない。上流の`main`を参照した箇所は調査時点の内容であり、採用時にはcommitと生成物を固定する。

## 推奨する進め方

Core 3.0の前に、**再現できる性能測定基盤とCore 2.0の基準値を作り、主要な負荷を特定する段階**を挟む。測定器はBenchmarkDotNet、素材は小さな自作WATとwasmiの選定ケースを中心にし、代表的な計算kernelを少数追加する。最初から全スイートの移植や実行方式の変更へ進まず、基準値とプロファイルから改善対象を選ぶ。

初回はC#からの埋め込み利用を想定して、短い公開Invokeの反復と、1回のInvoke内で長く計算する処理を同程度に重視する。起動時間とメモリも別軸で残す。具体的な利用アプリが決まった段階で重みを変え、恣意的な総合スコアや他エンジンの何倍以内という目標を先に置かない。

## 調査の前提

WasmSharp2はDecode→Validate→Instantiate→Invokeを公開し、Validateと線形化を同一パスで処理する。したがって、他ランタイムの「compilation」をそのままWasmSharp2のValidateだけと同一視しない。公開境界と処理範囲を対応付けて測る。[ADR 0001](../adr/0001-explicit-staged-runtime-api.md)、[ADR 0002](../adr/0002-single-pass-linear-interpreter.md)

初期対象はCore 2.0のバイナリ・検証・実行で、Core 3.0は別計画である。今回の測定候補もこの境界を維持し、素材を動かすためだけのWASIやCore 3.0機能追加は前提にしない。[ADR 0003](../adr/0003-core2-fixed-conformance-profile.md)

## 現在のコードから分かったこと

確認時のHEADは`20a031b45d7b73506ab303c628ebb746cd18448b`。作業ツリーにはREADMEとsteeringの既存変更があり、この調査では変更していない。通常のソース、プロジェクト定義、ツール、CIの検索ではベンチマークプロジェクトやBenchmarkDotNetの参照は見つからなかった。`.gitignore`の`BenchmarkDotNet.Artifacts/`は測定基盤の存在を示すものではない。

「性能を何も考えていない」という状態ではない。検証と線形化の同一パス、生成したswitchによる直接dispatch、Wasm内部のcallでの共有値スタック、内部trapを例外にせず結果で渡す構造がある。ただし、これらによる速度向上を今回測ったわけではない。[ADR 0002](../adr/0002-single-pass-linear-interpreter.md)、[ADR 0004](../adr/0004-trap-result-propagation.md)、[ADR 0006](../adr/0006-generated-instruction-dispatch.md)、[Interpreter.Call](../../src/WasmSharp/Execution/Interpreter.Call.cs)

| コードで確認した構造 | そこから導く測定項目 |
| --- | --- |
| Wasm定義関数への最外側のInvoke、または既存コンテキストのないstart実行では、`InterpreterContext.Enter`が新しいコンテキストを作り、`Exit`で解除する。フレーム・値配列も必要容量を確保する。ホスト関数だけの直接Invokeでは新しく作らない。[InterpreterContext](../../src/WasmSharp/Execution/InterpreterContext.cs)、[ExecutionBoundary](../../src/WasmSharp/Execution/ExecutionBoundary.cs) | 短いWasm定義関数への公開Invokeの時間と割り当て量。ウォームアップ後もこの費用は残るため、guest内の反復と分ける。 |
| `Interpreter.Run`の結果を`CopyValues`でコピーし、公開境界の`new WasmResults`でも所有用コピーを行う。[Interpreter](../../src/WasmSharp/Execution/Interpreter.cs)、[ExecutionBoundary](../../src/WasmSharp/Execution/ExecutionBoundary.cs)、[WasmResults](../../src/WasmSharp/WasmResults.cs) | 結果なし・1結果・複数結果の差。コピー削減を検討する場合も、返却結果が後続呼び出しから独立する契約は保つ。 |
| Wasm内部の定義関数callは共有値スタックを使うが、ホストcallbackの引数は`ToArray()`する。[Interpreter.Call](../../src/WasmSharp/Execution/Interpreter.Call.cs)、[Interpreter.InvokeHost](../../src/WasmSharp/Execution/Interpreter.cs) | 直接call、間接call、guest→host呼び出しを同じ回数・同じ処理量で分ける。 |
| 線形メモリはページ別の配列で保持し、読み書き・転送はページ境界で区切る。growではページ参照配列を作り、新しいページを割り当てる。[WasmMemory](../../src/WasmSharp/WasmMemory.cs) | ページ内とページ跨ぎ、サイズ別のload/store・copy/fill、固定初期サイズからのgrow。 |
| SIMD整数演算にはlaneごとのループがある。[Interpreter.SimdInteger](../../src/WasmSharp/Execution/Interpreter.SimdInteger.cs) | 同じ処理のscalar版とSIMD版。SIMD対応済みであることだけから高速化を仮定しない。 |

以上は測定候補であり、遅さの順位や最適化の効果を示す結果ではない。公開Invokeに含まれる処理を測定の都合で省かず、利用者が支払う費用として観測する。

## 外部資料から得られる測定方針

### Sightglass: 段階と測定区間の設計を参考にする

Sightglassはcompilation・instantiation・executionを分けて記録する。ただし、WasmtimeとCraneliftの開発用であり、汎用的なWebAssemblyランタイム間比較スイートを目的としないことを明記している。共有ライブラリのengine adapterと専用APIを使うため、WasmSharp2用の測定基盤としてそのまま採用する判断とは分ける。[Sightglass README](https://github.com/bytecodealliance/sightglass#readme)

素材は`_start: [] -> []`をexportし、`bench.start`と`bench.end`をそれぞれ1回呼んで実行の測定区間を示す。入力ファイルはWASIで開くディレクトリ内に配置し、標準出力と標準エラーの期待ファイルを照合する。これは「起動・入力準備・対象処理・結果確認を区別する」参考になるが、WASIを持たない本件へ既存素材全体を無加工で持ち込めるという意味ではない。[素材要件](https://github.com/bytecodealliance/sightglass/blob/main/benchmarks/README.md#minimal-technical-requirements)

**提案:** 段階分離と期待結果の確認を取り入れ、最初の測定器はWasmSharp2の公開APIを直接呼ぶ。guest内部の純粋な処理を測りたい場合は、入力準備と結果取得を測定区間の外へ置けるexportを持つ素材を優先する。外部スイート向けのadapterやWASIの実装を最初の作業には含めない。

### wasmi: interpreterの比較条件と小さな素材が有用

wasmiの公式比較スイートは`execute`と`startup`を分け、後者をcompile+instantiateとして扱う。`eager`は全関数を事前に検証・変換し、`lazy`は初回利用まで両方を遅延する。`lazy-translation`は検証を先に済ませ、変換を遅延する。さらに検証を省く`unchecked`設定がある。同じ「起動時間」でも実施済みの処理が異なる。[wasmi-benchmarks README](https://github.com/wasmi-labs/wasmi-benchmarks#readme)

**提案:** 最初のinterpreter比較は検証を有効にしたeager設定を基準にする。lazyを比較する場合は初回Invokeまでの合計も示し、検証や変換を後へ移した効果と定常実行の速度を区別する。`unchecked`はWasmSharp2の通常経路と同じ表へ混ぜない。

実行測定の実装は準備後に`run`を繰り返し、prime sieve・Argon2等では測定後に結果を照合する。一方、counter等が使う共通helperは呼び出しの成功を確認しているものの、返却値を期待値と比較していない。ケースごとの判定を読まず、スイートに入っているだけで十分な結果検証があるとは扱えない。[execute.rs](https://github.com/wasmi-labs/wasmi-benchmarks/blob/main/benches/criterion/execute.rs)

素材のRust版は`wasm32-unknown-unknown`を対象にビルドされ、生成物も公開されている。小さなWATの例である`counter-local`はimportなしで、整数引数と結果を持つ`run`をexportする。これはWASIなしの入口に合う。ただし、現行素材にはtail-callのようなCore 2.0対象外のケースもあるため、一覧全部の採用ではなく、固定バイナリごとのfeature確認が必要になる。[Rust素材のREADME](https://github.com/wasmi-labs/rust-benchmarks#readme)、[counter-local.wat](https://github.com/wasmi-labs/wasmi-benchmarks/blob/main/res/wat/counter-local.wat)、[実行ケース一覧](https://github.com/wasmi-labs/wasmi-benchmarks#execution-benchmarks)

起動測定のソースでは、WASI関数も処理をしないホスト関数へ結び付けている。対象はimportを呼ぶstartを持たず、Instantiate後の関数実行を測らないという限定がある。WASI不要と記載された素材でも、CoreMarkには`env.clock_ms`、ERC-20にはseal系importがある。「WASI不要」と「importなし」は異なる。[startup.rs](https://github.com/wasmi-labs/wasmi-benchmarks/blob/main/benches/criterion/startup.rs)

**提案:** 素材の一部を初期候補にする。小さなループ・直接呼び出し・間接呼び出し・線形メモリ・bulk-memoryを別々に観測できるよう選ぶ。WATは準備時に固定したWABTで`.wasm`へ変換し、測定時間に変換を含めない。起動専用のstubを実行性能測定へ流用しない。

wasmi v0.32の開発者記事は、stack-based IRからregister-based IRへの変更と、計算中心・呼び出し中心の処理で効果が異なることを説明している。これはdispatchだけを見て全体を評価できない理由の参考になる。当時のCPU別グラフを現在のWasmSharp2の性能目標に置き換える根拠にはならない。[wasmi v0.32の記事](https://wasmi-labs.github.io/blog/posts/wasmi-v0.32/)

### WAMR: fast/classicの区別とメモリ負荷も記録する

WAMRはclassic interpreterとfast interpreterを切り替えられ、JIT/AOTは別の構成である。公式ビルド文書はfast interpreterの速度と変換後コードのメモリ使用量にトレードオフがあることを説明する。比較時は単に「WAMR」とせず、interpreterの種類、検証を省くmini loaderの有無、JIT/AOTの有無を記録する必要がある。[WAMRビルド構成](https://github.com/wasm-micro-runtime/wasm-micro-runtime/blob/main/doc/build_wamr.md#configure-interpreters)

WAMRのPerformance wikiにはCPU・OS・コンパイラを伴う比較があるが、測定日は2020年9月1日である。現行版同士の順位やWasmSharp2の達成目標を示す資料には使わない。時間だけでなくメモリ消費も観測する例として参照する。[WAMR Performance](https://github.com/wasm-micro-runtime/wasm-micro-runtime/wiki/Performance)

**提案:** 外部比較を追加する段階では、wasmiのeager設定とWAMRのfast/classicをinterpreter群として候補にする。全Core 2.0機能の対応をこの資料だけで仮定せず、共通の固定素材を実行できる構成を確認する。Wasmtime等のJIT系を追加するときは、実行方式と準備費用を併記する。

## ベンチマーク素材の候補と暫定採否

| 候補 | 一次資料で確認した性質 | WasmSharp2への適用判断 |
| --- | --- | --- |
| wasmi-benchmarksの小さなWAT・Rust素材 | 計算、関数呼び出し、メモリ等のケースを持つ。Rust素材は`wasm32-unknown-unknown`。[素材一覧](https://github.com/wasmi-labs/wasmi-benchmarks#execution-benchmarks)、[ビルド対象](https://github.com/wasmi-labs/rust-benchmarks#readme) | **初期候補。** Core 2.0内の少数ケースを固定し、importと期待値を個別確認する。上流runner全体の導入は別判断。 |
| PolyBench/C 4.2.1-beta | 30種の数値計算。入力初期化、結果配列の出力、不要コード除去を避ける仕組み、データ規模の切り替えを持つ。[作者の配布ページとREADME](https://sourceforge.net/projects/polybench/files/) | **少数のkernelを第二候補。** 数値計算・線形メモリの負荷には適するが、全領域を代表しない。独自のimportなしwrapperで準備・kernel・結果取得を分ける場合は、元kernelからの変更点を記録する。 |
| WABench（Wang、IISWC 2022） | standalone runtime向けで、PolyBench・MiBench・JetStream2・アプリを含む。[研究者のリポジトリ](https://github.com/wabench/wabench)。共通MakefileはWASI SDK 10.0と`-O2`を使用する。[Makefile.in](https://github.com/wabench/wabench/blob/main/Makefile.in) | **既定セットの直接導入は後回し。** WASI依存をなくす作業とCore 2.0確認が先に必要。特定kernelの選定材料にする。掲載コードの権利を一括で推定せず、採用元単位で確認する。 |
| WasmBench | Web/GitHub/NPM等から収集したバイナリと静的メタデータ。filtered版は8,461個のunique binary。収集・分析プログラムのMITライセンスは収集バイナリ全体の権利を意味しない。[研究者のREADME](https://github.com/sola-st/WasmBench#readme) | **Decode/Validateの大型入力候補。** 実行入口・入力・期待結果がそろったスイートとは確認できないため、初期Invoke比較には選ばない。Core 2.0内に絞り、正常入力か拒否対象かを区別する。リポジトリへの再配布も個別判断。 |
| Sightglassのアプリ素材 | WASIによる入力と出力照合、`bench.start/end`による区間指定。[素材要件](https://github.com/bytecodealliance/sightglass/blob/main/benchmarks/README.md) | **方法を参考にし、セット全体は後回し。** 本件のWASIなしという範囲では、取り出せるkernelを個別検討する。 |

PolyBench/CをWasmで動かす既存手順としてWAMRの公式ベンチマークがあるが、WASI SDKを要求し、native/AOT/interpreterのrun scriptを提供する。これも無加工でCoreだけの環境へ持ち込める証拠にはならない。[WAMR PolyBench README](https://github.com/wasm-micro-runtime/wasm-micro-runtime/blob/main/tests/benchmarks/polybench/README.md)

**提案:** PolyBench/Cでは少数の異なる処理を選ぶ。例えば行列計算とstencilを同じ「数値計算」内の別ケースとして測り、ケースを増やす前に結果検証と反復条件を安定させる。元の`POLYBENCH_TIME`やキャッシュflushをそのまま有効にするとホスト計時との範囲が変わるため、kernelの計時と準備・flushを分ける。これは本件向けの測定設計であり、PolyBenchの公式スコアを名乗る提案ではない。[PolyBench/Cの計時・出力オプション](https://sourceforge.net/projects/polybench/files/)

## .NETで使う測定器と診断資料

**測定の土台にはBenchmarkDotNetを推奨する。** 別プロセスでReleaseビルドの測定対象を動かし、pilot・ウォームアップ・本測定・ハーネスのオーバーヘッド評価を扱う。自作のStopwatch反復を主たる比較器にするより、初回JITや反復数の選択を扱いやすい。[測定の仕組み](https://benchmarkdotnet.org/articles/guides/how-it-works.html)

| 資料 | 本件で使う内容 |
| --- | --- |
| [BenchmarkDotNet Good Practices](https://benchmarkdotnet.org/articles/guides/good-practices.html) | Release・デバッガなし・結果の消費・電源条件と他プロセスの負荷を揃える。Windowsでの結果を他OS/CPUへ一般化しない。 |
| [Setup And Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html) | 準備と計時を分離する。`IterationSetup`は通常の短いマイクロベンチには不向きなので、可変状態のために機械的に付けない。 |
| [Choosing RunStrategy](https://benchmarkdotnet.org/articles/guides/choosing-run-strategy.html) | 定常処理はThroughput。ColdStartはpilotとwarmupを省く方式であり、それだけでOSのプロセス生成からの総時間にはならない。 |
| [Diagnosers](https://benchmarkdotnet.org/articles/configs/diagnosers.html) | `MemoryDiagnoser`で操作当たりのmanaged割り当てとGCを観測し、必要なケースだけ逆アセンブルやEventPipe/ETWを追加する。 |
| [MicrosoftのJIT設定](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation) | Tiered Compilation・Dynamic PGO・ReadyToRunの設定を記録する。Wasm自体がインタープリターでも、C#製ランタイムは.NET JITの影響を受ける。 |
| [Microsoftのdotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) | 時間のかかるケースを長めに反復し、呼び出しスタック・GC・JITを調べる。診断を付けたrunの時間を通常の基準値と混ぜない。 |

最初は現在の.NET 10の通常設定を固定して測る。PGOやtieringを無効にした値だけを製品の性能として報告しない。設定を変える実験は、変動の原因を絞る必要が生じたときに分けて追加する。

調査時点の`dotnet-trace collect`資料では、`dotnet-sampled-thread-time`はスレッドのwall-clockに基づくサンプルであり、CPU上で実行していた時間だけを測るものではない。過去の`cpu-sampling`というprofile名とは扱いが変わっている。採用するツール版のhelpを確認し、WindowsでCPU時間の厳密な帰属まで必要ならETW/PerfViewを追加する。[dotnet-traceのprofile説明](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace#dotnet-trace-collect)、[BenchmarkDotNetのETW診断](https://benchmarkdotnet.org/articles/configs/diagnosers.html)

## 測定区間の定義

以下は上記資料を踏まえた本件向けの提案であり、測定済みの性質ではない。

| 観測したいもの | 測定区間と条件 |
| --- | --- |
| Decode | 計測外で読み込んだ同じバイト列から、新しいmoduleを生成する。ファイルI/O・WAT変換は含めない。Stream入力はバッファへの読み込みも含むため別ケースにする。 |
| Validateと線形化 | 毎回未検証のmoduleに対して測る。初期導入ではDecode+Validateも独立した測定として持つ。他エンジンのcompileとの対応は含まれる処理で判断する。 |
| Instantiate | 検証済みmoduleを使い、毎回新しいinstanceを作る。importの接続、リソース割り当て、segment初期化を含む。startなしを基礎ケースにし、startありは別ケースにする。 |
| 最初の結果まで | 新しいmodule/instanceから初回Invoke完了まで。プロセス起動を含むかは別条件として示す。lazy変換やキャッシュを使う比較相手ではここへ移った負荷を捉える。 |
| 定常実行 | 準備済みinstanceの同じexportを、入力状態をそろえて反復する。guestの長いループと、ホストからの短いInvoke反復を分け、境界の負荷を区別する。 |
| メモリ負荷 | 時間と併せて割り当て量・GC・常駐メモリ等の必要な指標を記録する。managed割り当てとプロセス全体のメモリを同じ指標として混ぜない。 |

`cold`と`warm`は単独のラベルで済ませない。「新規プロセス」「新規module」「新規instance」「初回Invoke」「反復Invoke」「エンジンのコードキャッシュ有無」「CPUキャッシュを操作したか」を記録する。新規instanceでも同じプロセス内で既存のホストコードやエンジンを再利用していれば、プロセス起動からのcold測定とは異なる。

`Validate()`は成功済みなら即座に同じmoduleを返すため、`GlobalSetup`で1個だけDecodeしてValidateを反復する設計は禁止する。まずDecode+Validateを安定して測り、Validate単体の分離が必要なら、計測外で用意した未検証moduleのバッチを各1回だけ検証する。反復ごとにバッチを新しくし、ハーネスが同じバッチを再利用しない設定にする。`IterationSetup`と1回の短い検証だけを組み合わせない。別々に得たDecodeとDecode+Validateの平均値の差を、精度の保証されたValidate単体値として報告しない。[WasmModule.Validate](../../src/WasmSharp/WasmModule.cs)、[ADR 0005](../adr/0005-module-owned-validation-state.md)、[Setup And Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)

定常Invokeではmodule・instance・exportの取得・引数準備を計測外で済ませ、公開`Invoke`と結果の消費を測る。結果の消費は最適化による処理の除去を防ぐもので、期待値との照合は別に行う。正しさ確認やウォームアップが状態を変える場合は測定用の状態を復元する。初回Invokeの測定で、その関数を事前確認のために同じプロセスで呼んではならない。

プロセス起動から最初の結果までを測る場合は、ビルド済みアプリを毎回別プロセスで起動する専用シナリオを使い、外側の時計で開始から結果受領までを測る。`dotnet run`のビルド時間は含めない。OSのファイルキャッシュまで冷たいとは扱わず、初期の定常Invokeベンチとは別の結果にする。

返却値または出力メモリを、時間計測の外で固定期待値と照合する。初回だけでなく反復後も確認し、sort済み配列を繰り返しsortする等、反復で問題が変わる素材はresetの位置を定義する。浮動小数点の比較規則はケースごとに決める。合わない結果、trap、途中終了を短い実行時間として集計しない。これらは適合検証の代替ではなく、測定した処理が想定どおり完了したことを確認するための条件である。

比較には同じ`.wasm`のhash・入力・反復回数・期待結果を使う。ランタイム版、ビルド構成、Wasm feature設定、検証・境界チェック・fuel等の設定、CPU、OS、ホスト実行環境を保存する。native版との比較は、同じWasmを実行する比較とは分け、コンパイラ・最適化・SIMD・データ型の違いを記録する。ホスト言語やFFIが異なる場合はその境界も測定範囲へ明記する。

既定の主測定ではfuel・命令カウンター・詳細ログなどを追加しない。エンジン間で任意の計測機能を揃え、検証やメモリ境界検査を省く構成と比較しない。型付きの専用呼び出しAPIと汎用の値配列APIの差も記録する。正常入力、無効入力の拒否、trap/exhaustionは別ケースにする。

まずはWasmSharp2の同一環境・同一素材に対するbaselineを作り、ケース別の時間とばらつきを得る。その後にinterpreter比較を追加する。共通ケースの比率を集約する場合も、個別結果と対象集合を残す。異なる対応範囲の平均値や、古い公開グラフから性能目標を先に決めない。

## 初期ケースの提案

最初は10〜15ケース程度から始め、全opcodeの個別性能テストは作らない。下表は測定目的の分類であり、全パラメータの直積を初回から回す計画ではない。実際に採用した素材には固定の期待値または出力checksumを持たせる。

| 分類 | 最初に選ぶ処理 | 分かること |
| --- | --- | --- |
| 公開APIの固定費 | 短い関数の結果数0/1/複数 | コンテキスト・スタック・結果保持を含むInvokeの費用 |
| guest内部の計算 | localを更新する整数ループ、浮動小数点ループ、分岐 | 呼び出し境界を薄めた命令処理の速度 |
| 関数呼び出し | 直接call、tableを経由する間接call、同じ計算を行うhost callback | フレーム処理、型照合、ホスト境界の違い |
| メモリ | 配列走査とcopy/fill。後からページ跨ぎとgrowを追加 | データ量に対する時間、ページ処理、割り当て |
| SIMD | 同じ入力・同じ答えになるscalar版とSIMD版 | 命令数削減とlane処理を合わせた利用上の効果 |
| 準備処理 | 小・中・大のmodule。命令中心とdata中心を区別 | Decode+Validate、Instantiateの規模への依存 |
| 複合処理 | wasmi素材から結果を確認できる計算1件、PolyBench等から数値kernel1件 | 局所的な改善がまとまった処理にも効くか |

growは一定の初期サイズから同じ増加量で行い、反復により上限到達後の失敗経路へ変わらないようにする。table/memory/globalを書き換える素材も、反復前後の状態を明示する。初期は単一スレッドで測定し、現在の保証外である並行利用を性能目標へ追加しない。[実行コンテキストの保証範囲](../adr/0008-instance-options-and-execution-context.md)

WATは命令の意図が明確な小ケースに使い、C/Rust由来の素材で実際のコンパイラが生成する命令列も含める。素材のビルドは計測前に済ませ、コンパイラ版・最適化・SIMD等のflagを固定する。現在のコンパイラが既定でCore 2.0外の命令を出さないという仮定は置かない。互換性のある固定版を選び、importとfeatureを確認してから素材を採用する。

## 基準値の保存と改善の判定

最初に同じバイナリの再測定でばらつきを調べる。改善前後は同じマシン・同じ設定で順序を交互にして測り、時間経過による温度やバックグラウンド負荷の偏りを減らす。主要ケースは複数のプロセス起動でも結果が再現することを確認する。測定時間や反復数は短い試走で適正化し、誤差の大きいケースだけ増やす。

| 保存するもの | 内容 |
| --- | --- |
| 時間 | ケースごとの平均・中央値・標準偏差・信頼区間、比較元との比率。ns/op・ms/module・MiB/s等、処理に合う単位を明記 |
| 割り当て | managed bytes/opとGC。保持中のmodule/instanceのメモリやプロセスのworking setは別測定とし、累積割り当て量と混ぜない |
| 測定条件 | HEADと未コミットのコード差分、.NET SDK/runtime・BenchmarkDotNet版、CPU/OS/architecture、電源・GC/JIT設定、素材と入力のhash、コンパイラとflag |
| 再解析用の結果 | 個々の測定を含むJSON/CSV、実行コマンド、必要なケースのプロファイル。新しい出力先へ保存し、採用した基準値を上書きしない |

BenchmarkDotNetは詳細測定を含むJSONと測定CSVを出力できる。平均だけを残さず、ばらつきも判定に使う。バッチ反復の平均時間のpercentileを、利用者の1回のInvokeのp95として報告しない。起動や単発遅延のpercentileが必要なら、独立した操作を十分に採って別途評価する。[Exporters](https://benchmarkdotnet.org/articles/configs/exporters.html)、[Statistics](https://benchmarkdotnet.org/articles/features/statistics.html)

最初から「5%遅ければ失敗」等の一律条件をCIへ入れない。同一版の測定ノイズと用途上問題となる差を把握してから、ケース別の判定幅を決める。共有CIでは素材の正しさと測定コマンドの動作確認を優先し、速度の判定は条件を揃えられる環境で行う。総合値を出す場合も、少数ケースの悪化を隠さないよう個別結果を必ず残す。

## 実施順序と区切り

1. **測定基盤と最初の基準値。** `tools/WasmSharp.Benchmarks/`を候補とし、公開APIを使う独立した小さなプロジェクトを作る。素材・期待値・測定条件を固定する。準備済みバイト列のDecode、Decode+Validate、Instantiate、定常Invokeから始める。計測のためのランタイム公開APIや汎用adapter frameworkは追加しない。
2. **上位の原因を特定。** 短いInvoke、長いguest計算、複合処理の代表ケースをプロファイルし、時間と割り当ての寄与が大きい1〜3箇所を特定する。必要になったときにValidate単体、起動全体、メモリ保持量の測定を追加する。
3. **根拠のある改善を1件ずつ。** 境界・所有権・責務を保つ形に整理してから、測定で裏付けられた変更を行う。同一素材で改善と他ケースへの影響を再測定する。コンテキストの再利用、結果コピー、SIMD等は候補であり、採用済みの変更方針ではない。register IRやJITは最初の前提にしない。
4. **正しさと継続測定。** 挙動を変える改善では警告・エラー0のReleaseビルド、関連テスト、現行方針に従う固定公式スイートとの比較を行う。性能用の基準値と公式適合検証のbaselineは別に保持する。[現行の検証方針](../../.kiro/steering/tech.md)
5. **外部比較とCore 3.0への引き継ぎ。** 同じ素材を使えるwasmi等を加え、実行方式・検証条件・準備費用を揃えて比較する。Core 3.0で内部を変えるときも、このCore 2.0の固定ケースを回帰確認に使う。

先行作業の区切りは、**再現可能な基準値があり、主要な負荷が分かり、変更前後の性能と正しさを比較できること**とする。その時点で、観測した問題の重要度をもとに最初の改善範囲と数値目標を決められる。

この文書の候補は実行可能性の最終確認を済ませた採用一覧ではない。選んだ固定版についてCore 2.0への適合、import、出力検証、ビルド再現性を確認する作業が残る。
