# C#製Wasmランタイムのdiscovery調査

調査日: 2026-09-06。公式仕様、WebAssembly公式リポジトリ、手元の同梱ソースを参照した。ビルド、`wast2json`の実行、公式テストの実行は行っていない。

調査後のユーザー選択: 初期の最終スコープをCore 2.0とし、将来Core 3.0へ拡張できる方針とする。初期の2.0 profileを実装進捗で変更せず、3.0は将来明示的に追加する別スコープ・別profileとして扱う。拡張可能であることは公開APIや内部型体系を変更せず移行できる保証ではない。

## 確認できた仕様の範囲

| 版 | 含まれる機能と位置付け |
| --- | --- |
| Core 1.0 | 数値型`i32/i64/f32/f64`、構造化制御、関数、間接呼び出し、グローバル、線形メモリ、テーブル、import/export、start、data/element segmentを持つ基礎仕様。保存版はRelease 1.0（2019-07-20）。[公式保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-1.0.pdf) |
| Core 2.0 | 1.0にsign-extension、non-trapping float-to-int、multi-value、reference-types（`funcref/externref`、table操作、複数tableを含む）、bulk-memory、SIMDを追加。[公式変更履歴](https://webassembly.github.io/spec/core/appendix/changes.html#release-2-0)。保存版の表示はRelease 2.0（2025-09-16）。[公式保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf) |
| Core 3.0 | 2.0にextended-const、tail-call、exception-handling、multi-memory、memory64（tableの64bit indexも含む）、function-references、GC、relaxed-SIMDを追加。さらにdeterministic profileとテキスト形式のannotationsを定義。[公式変更履歴](https://webassembly.github.io/spec/core/appendix/changes.html#release-3-0) |

Core 3.0は2025-09-17に新しいlive standardとして発表された。今回取得したlive仕様本文は「3.0（2026-09-03）」と表示されており、単なる未確定の3.0 draftとして扱わない。[公式発表](https://webassembly.org/news/2025-09-17-wasm-3.0/)、[live仕様の版表示](https://webassembly.github.io/spec/core/intro/introduction.html)。W3C TRの公開段階とlive standardの版は別に記録する。

Core 3.0のGCはstruct/array/i31の命令追加だけでなく、再帰型、宣言されたサブタイプ、heap type、cast、外部参照との変換を含む。型付き関数参照は非null参照、`call_ref`、非defaultable localの初期化検査、table初期化式に影響する。例外処理はtag、`throw`、`throw_ref`、`try_table`を含み、旧proposalの`try/catch/rethrow/delegate`をそのまま最終仕様と見なせない。[Core 3.0変更履歴](https://webassembly.github.io/spec/core/appendix/changes.html#release-3-0)

Core仕様はISA、バイナリ、検証、実行、テキスト表現を定義し、特定環境のAPIは別仕様である。WATを実装しない本件は、選定Core版のバイナリ・検証・実行への適合として約束を明記できる。WASI、Component Model、JavaScript固有APIをCore実装から自動的に含める根拠はない。[公式Scope](https://webassembly.github.io/spec/core/intro/introduction.html#scope)

## 公式テストと版の固定

`WebAssembly/testsuite`は`WebAssembly/spec`のcore testsと各proposalリポジトリのtestsを集めたミラーで、通常は毎週更新される。`main`全体を特定Core版の適合集合と同一視しない。[公式testsuite README](https://github.com/WebAssembly/testsuite#readme)

仕様自体も、以前のmalformed/invalidが後の版では有効になることや、malformedとinvalidの分類が変わることを認めている。古いnegative testと新しいfeature設定の混在は誤判定の原因になる。[公式互換性規則](https://webassembly.github.io/spec/core/appendix/changes.html)

**提案:** 選定仕様の版・日付またはcommit、testsuiteのcommitと対象path、WABTの版・commit、全featureの実効ON/OFF、変換オプションを一緒に固定する。更新は実装進捗とは別操作とし、生成物のhashと結果baselineの差分を残す。合格は固定した対象集合に対して報告する。

独立確認で、Core 2.0の入力元候補として公式specの`v2.0.0`（commit `05ca4182176763112561ae20153975c12bd689e4`）を確認した。対象は`test/core`配下で、`simd`サブディレクトリも含める。現在同梱された3.0系のテスト全体を2.0用に流用せず、テスト素材を整備する仕様で保存版との対応と変換結果を確認して固定する。[公式Core 2.0タグ](https://github.com/WebAssembly/spec/tree/v2.0.0/test/core)、[同タグのSIMDテスト](https://github.com/WebAssembly/spec/blob/v2.0.0/test/core/simd/simd_const.wast)

## 手元WABTとCore 3.0の問題

同梱WABTの[CMakeLists.txt](/D:/source/repos/WasmSharp2/thirdParties/wabt/CMakeLists.txt:18)の宣言版は`1.0.41`。ディレクトリ内で`git rev-parse --show-toplevel`するとWasmSharp2のルートを返すため、その`HEAD`をWABT上流commitと解釈できない。上流commitは今回特定できていない。

公式の`1.0.41`タグではtail-call、memory64、multi-memory、extended-const、relaxed-SIMDが既定OFFで、同梱実体の既定ONとは一致しない。したがって版文字列だけでは同じソースと見なせない。採用時は取得元commitまたは特定可能なソース集合のhash、ビルド条件、実行ファイルhashも固定する。[公式1.0.41のfeature.def](https://github.com/WebAssembly/wabt/blob/1.0.41/include/wabt/feature.def)

同梱[README](/D:/source/repos/WasmSharp2/thirdParties/wabt/README.md:36)は例外、tail-call、memory64、multi-memory、extended-const、relaxed-SIMD、function-referencesについてbinary/text/validate対応を表示する。一方でGC行はなく、[opcode.def](/D:/source/repos/WasmSharp2/thirdParties/wabt/include/wabt/opcode.def)には`struct.new`、`array.new`、`ref.i31`、`ref.cast`等の登録が見つからない。`--enable-gc`フラグの存在だけではGC命令の変換対応を証明できない。

公式WABTの[GC opcode実装PR #2618](https://github.com/WebAssembly/wabt/pull/2618)と[GC tests対応PR #2622](https://github.com/WebAssembly/wabt/pull/2622)は今回の閲覧時点でOpenだった。したがって**現在のWABTを指定どおり使うだけでCore 3.0の全公式テストを変換できる、という前提は置けない**。PRの存在は採用や独自patchの承認ではない。

例外・typed refs・memory64等の対応表は個別テスト成功を保証しない。Core 3.0を選ぶ場合は最初に固定corpus全体の変換可否と生成JSON schemaを実測し、GCに限らず変換不能箇所を確定する必要がある。これは今後の検証項目であり、今回の実行結果ではない。

## 固定feature profile

同梱[feature.def](/D:/source/repos/WasmSharp2/thirdParties/wabt/include/wabt/feature.def)では次の既定値になっている。実装の出来高に合わせて変更しない。

| 実効設定 | 機能 |
| --- | --- |
| 既定ON | exceptions、mutable-globals、saturating-float-to-int、sign-extension、SIMD、multi-value、tail-call、bulk-memory、reference-types、annotations、memory64、multi-memory、extended-const、relaxed-SIMD |
| 既定OFF | threads、function-references、code-metadata、GC、custom-page-sizes、compact-imports、wide-arithmetic |

[feature.cc](/D:/source/repos/WasmSharp2/thirdParties/wabt/src/feature.cc:24)は既定ON機能には`--disable-*`、既定OFF機能には`--enable-*`を登録する。全機能に対し任意の`--enable-*`/`--disable-*`がある前提でコマンドを組まない。GCをONにするとfunction-referencesもONになる等の依存関係があるため、CLI文字列と実効profileを両方記録する。

- **Core 2.0候補:** 同梱版では`--disable-exceptions --disable-tail-call --disable-memory64 --disable-multi-memory --disable-extended-const --disable-relaxed-simd --disable-annotations`を指定し、既定OFFの範囲外proposalをONにしない。mutable-globalsとCore 2.0の機能は既定ONを維持する。正確なprofileは固定するツール・corpusで確認する。
- **Core 3.0候補:** Core 3.0の既定ON群を維持し`--enable-function-references --enable-gc`を指定する。ただしGC変換の欠落はフラグでは解決しない。threads等の範囲外proposalはOFFを維持する。
- annotationsは変換入力のテキスト構文機能であり、ONにしてもC#ランタイムにWAT対応を追加する意味にはならない。`--enable-all`は使わない。

## `wast2json`の出力と判定の境界

公式文書は`wast2json`がJSON、`.wasm`、`.wat`を出力すると明記する。同梱[WriteInvalidModule](/D:/source/repos/WasmSharp2/thirdParties/wabt/src/binary-writer-spec.cc:418)では通常のテキストmoduleとbinary moduleは`.wasm`/`module_type="binary"`、quote形式は`.wat`/`module_type="text"`になる。テキストが構文的に壊れている`assert_malformed`は有効なWasmバイナリに変換できない。[公式JSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)

**提案:** runnerはJSONの`module_type`だけを見てtext対象を`out_of_scope(text-format)`として記録し、`.wat`を開かない。バイナリ対象の失敗と分母を分ける。対象テキストassertionを合格として数えず、「公式スイート全件適合」ではなく「選定版のバイナリ対象集合」を報告する。

| JSON command | 検証する結果 |
| --- | --- |
| `assert_malformed`（binary） | Decodeでバイナリ構文不成立 |
| `assert_invalid`（binary） | Decode成功後、Validateで仕様上の不成立 |
| `assert_unlinkable` | import不足や型不一致によるリンク不成立 |
| `assert_uninstantiable` | module形式の`assert_trap`。Instantiate中のtrap |
| `assert_trap` | invoke/get actionでtrap |
| `assert_exhaustion` | actionでスタック枯渇。通常の仕様trapと識別が必要 |
| `assert_return` | 値のコレクション・型・個数・期待値patternの一致 |

上表の根拠は[公式JSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)。`text`は参照interpreterの期待診断であり、その英文をライブラリの公開例外メッセージ契約として採用する必要はない。Instantiate中のtrapとlink errorは同一段階でも違う原因なので、例外型または型付きreasonで区別する必要がある。

整数は精度保持のため10進文字列、floatは値そのものではなくIEEEビット列の10進文字列。floatとSIMD laneには`nan:canonical`/`nan:arithmetic`patternがあり、数値の単純な等値比較では不足する。[公式JSON値仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md#const)

同梱[JSON writer](/D:/source/repos/WasmSharp2/thirdParties/wabt/src/binary-writer-spec.cc:584)は`assert_return`の`expected`に加えて`either`を出力し、[同ファイル](/D:/source/repos/WasmSharp2/thirdParties/wabt/src/binary-writer-spec.cc:633)は`assert_exception`も出力する。Core 3.0ではWasm例外とtrapを別に扱い、relaxed-SIMDの許容結果集合を表現できるrunnerが必要になる。JSONドキュメントだけでなく固定版writerと生成例を契約根拠にする。

**提案:** `passed`、`failed`、`unsupported`（ランタイム未実装）、`out_of_scope`（対象外）、`tool_error`（変換不能・JSON未対応等）を区別する。WABTが`.wast`を処理できずJSONを生成しなかった場合は、ランタイムを一度も呼んでいないため`WasmUnsupportedFeatureException`の観測として数えない。同梱[wast2jsonの処理順](/D:/source/repos/WasmSharp2/thirdParties/wabt/src/tools/wast2json.cc:93)もparse→validate→writeで、変換成功後にはじめてランタイム入力が得られる。

## 選択肢と推奨

1. **Core 2.0を最終スコープとして明示的に選ぶ:** ユーザーの10項目とWABT中心の検証経路を保ちやすい。SIMDを含む固定集合に向けて実装できる。ただし「最新仕様すべて」ではなく2.0適合の約束になる。
2. **Core 3.0を最終スコープとして選ぶ:** 最新live standardを対象にできる。GC・型体系・例外・64bit indexまで最初から設計対象となり、WABT変換経路の不足を独立した課題として解消する必要がある。機能を途中でOFFにして回避しない。
3. **Core 1.0に限定する:** 最小の完成範囲は作れるがSIMDを含まず、将来の2.0/3.0実装は別のスコープ変更になる。単なる実装順のMVP到達点と最終スコープを混同しない。

**選択に対する提案:** 初期Core 2.0の固定profileとcorpusを独立して維持する。将来3.0を追加するときは、GC・再帰型・例外・64bit indexに必要な契約変更とWABT変換経路の課題を別仕様で扱う。将来のためだけの未使用抽象化は追加せず、2.0固有の制約を局所化しておく。両版で4段階API、未実装の専用分類、公開APIのみのspectest、検証と線形化の同一パス、単一の線形実行機構を保持する。
