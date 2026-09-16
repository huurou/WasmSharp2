---
updated_at: 2026-09-16
---

# WasmSharp2ロードマップ

## 概要

C#でWebAssemblyバイナリをデコード・検証・インスタンス化・実行するライブラリを一から実装する。初期の完了目標は**WebAssembly Core 2.0のバイナリ・検証・実行への準拠**とし、公式テストのバイナリ対象集合で検証する。WATの処理はライブラリの対象外とする。

下記7仕様を機能ごとに4段階を通して実装する。公式テスト素材の固定・生成から実行・回帰比較までは、`conformance-runner`で一つの仕様・ツールとして扱う。Core 3.0は将来の別計画とする。この分割方針の承認と、各仕様のrequirements・design・tasks・実装の承認は区別する。文書の言語は日本語とし、`spec.json.language`は`ja`とする。

完成済みの`runtime-foundation`に続き、`host-linking`で関数呼出し・リソース生成・import/exportの実行・リンク基盤を整備する。その公開能力を使って`conformance-runner`のspectest・registerと初回baselineを成立させ、その後に各命令・初期化機能を追加する。基盤の承認済み要件・設計・タスクと完成状態は維持する。後続機能では同じ公式スイートによる回帰確認も完了条件に含める。

## discovery時点の現状（2026-09-06）

- `src/WasmSharp`は.NET 10の公開型とメソッドシグネチャの骨組み。`Decode`・`Validate`・`Instantiate`・`Invoke`は未実装。
- `tests/WasmSharp.Tests`は.NET 10・TUnit 1.66.10のプロジェクト定義のみで、テスト本体はない。`tools`は空。
- `thirdParties`には公式specとWABTのソースがある。specは3.0系で、初期Core 2.0のテスト集合としてそのまま扱えない。
- 同梱WABTの宣言版は1.0.41だが、公式の同名タグとfeature既定値が異なる。上流commitは未特定であり、版名だけでは固定済みとは言えない。
- 既存の正式specとsteeringはない。既存コメントと公開型の意図を確認し、メソッドシグネチャの骨組みを完成済み契約とは扱わない。

## 進め方の選択

- **採用**: 最小基盤に実行・リンク基盤を加えてから、素材生成・spectest・register・実行・回帰比較を一つの公式ツールで整備する。数値・制御、メモリ命令、テーブル・参照命令、SIMDはその後に追加する。規模は大。
- **理由**: 公式スイートのimportやmodule間共有を初期から利用し、各機能の実装時に仕様解釈と実行結果を確認できる。リソースの生成・共有とguest命令の意味論を分け、未実装と不具合を同じ固定集合で追跡する。
- **順序の別案**: ホスト連携を全命令・segment初期化の後まで遅らせると、importを使う公式ケースの検証も遅れる。共通の実行・リンク能力を先行し、data/element固有の処理は各機能へ置く。
- **検討した別案**: Core 2.0全体のDecode・Validateを先行し、その後Instantiate・Invokeを完成させる。段階内の作業はまとまるが実行結果による確認が遅くなるため採用しない。こちらも規模は大。
- **範囲の別案**: Core 1.0限定ではSIMD等が完了目標に入らず、Core 3.0から開始するとGC等の追加設計とWABTの変換対応不足を同時に扱う必要がある。今回はCore 2.0を選択した。

## 対象範囲

- **対象**: Core 2.0のバイナリ形式、型検証、インスタンス化、実行。スカラー数値、関数、構造化制御、複数値、globals、import/export、start、線形メモリ、data、テーブル、element、間接呼び出し、`funcref/externref`、bulk memory/table、sign-extension、non-trapping conversions、`v128`とSIMD。
- **対象**: 明示的なホスト関数・共有リソースの連携、および公開APIだけを利用する公式テスト用の`spectest`とランナー。
- **対象外**: ランタイムと自作ツールによるWAT・WASTの解析、WASI、Component Model、JavaScript/Web API、JIT/AOT、既存エンジンへの実行委譲。
- **初期対象外**: GC、型付き関数参照、Wasm例外処理、tail-call、memory64、multi-memory、extended-const、relaxed-SIMD、threads等、Core 2.0の外にある機能。3.0のdeterministic profileも初期の追加要件にしない。
- 性能の数値目標、NuGet公開、追加TFM・OSへの対応は今回決めていない。将来のためだけの抽象化や拡張口は設けない。

## 維持する10項目

1. **4段階を分離する**: `Decode → Validate → Instantiate → Invoke`を明示する。段階ごとの例外型で、入力の破損・検証不成立・リンク不成立・実行中のtrapを説明できるようにする。
2. **未実装を検証失敗にしない**: 対象仕様の機能が未実装の場合は`WasmUnsupportedFeatureException`で区別する。仕様違反の判定と実装状況を混同しない。
3. **明示APIのみ**: 値の受け渡しは`WasmValue`。`object`・`dynamic`を受ける汎用引数、CLR型からの暗黙変換、4段階を畳むローダー、delegateからの関数型推論を導入しない。ホスト関数型は明示宣言する。
4. **線形バイトコードで実行する**: フラットな配列と単一の`switch`実行ループを使う。分岐は線形化時に`(targetPc, stackHeight, keepCount)`へ落とし、入れ子オブジェクトの走査や外側フレームへの完了値の伝播で実行しない。
5. **検証と線形化を同一パスにする**: 型検査しながら実行コードを生成し、型スタックを分岐情報にも使う。検証済みモデルと実行モデルを別々に二重保持しない。Decodeの入力表現と検証後の実行表現の違いまで禁止するものではない。
6. **内部のtrapに.NET例外を使わない**: 実行ループは列挙型の結果を返し、ホスト境界にある共通の1箇所で`WasmTrapException`へ変換する。Instantiateからのstart実行にも同じ境界処理を適用する。
7. **命令情報の唯一の定義元を持つ**: opcode・名前・即値形状・スタック効果・検証規則・実行ハンドラを1テーブルに集める。SIMDも同じ定義元と実行ループを拡張する。テーブルと`switch`の同期方法は基盤の設計で決め、別々の手管理一覧を正本にしない。
8. **検証ツールは通常の利用者**: `spectest`をツール側で公開APIだけから構成する。ツール専用の裏口を追加せず、通常利用にも意味のある能力だけを公開する。
9. **WASTは自前で解析しない**: `wast2json`がWASTをJSONと個別モジュールへ変換する。自作ツールの実行処理が読み込むのはJSONと`.wasm`だけとし、素材同定のためのWAST/WATのhash計算と区別する。
10. **feature flagは初期の最終範囲に固定する**: Core 2.0内をON、範囲外をOFFとし、実装の進捗で変更しない。`--enable-all`を使わない。未実装機能を無効化して検証対象から隠さない。

## 仕様間で共有する契約

- `WasmModule`は静的定義、`WasmInstance`は実行時の実体という既存の区別を保つ。検証前のInstantiateを防ぐ契約、検証成功後の実行表現の所有権は基盤で決める。
- 基盤はCore 2.0の値・型・添字空間と、後続が使うデコード結果・命令情報・スタック・実行結果の共通契約を持つ。各機能の具体的なデコード、検証、初期化、実行はその機能の仕様が所有する。
- scalarとvector・参照をやり取りできる`WasmValue`の契約を基盤で決める。各命令の意味論は該当機能が持つ。参照の同一性を明示的に表し、汎用`object`引数で代用しない。
- `host-linking`は関数の引数・結果0個/複数・locals・直接call/return、return後の到達不能部分を含む関数本体の型検証、globalの生成・スカラー初期化・get/set、memory/tableの型・limits・生成・公開取得、4種のimport/exportと同一性を所有する。memory/tableのguest命令とdata/element初期化は各機能仕様が同じ実体へ追加し、リソース表現を複製しない。
- `host-linking`は通常利用に必要なimportの識別情報を公開する。依存の把握をinstance生成や無関係な未実装命令のDecode成功へ依存させず、取得できた情報と未確認範囲を区別する。ランナーはこの能力で失敗したregisterへの依存を特定し、独自のバイナリ解析やWAST解析で補わない。
- startの型検証・実行と共通のInstantiate順序は`host-linking`が所有する。data/element初期化と、共有リソースに対する初期化・startの複合挙動は各segmentの所有仕様が追加・検証する。未対応のsegmentを無視してstartを実行しない。
- `WasmInstance`の既存コメント「Exportsは持たせない」を守る。公開取得操作と共有リソースの扱いは通常の埋め込み利用を根拠に設計する。`GetGlobal`の値取得だけでmutable globalの共有を表現できると決めつけない。
- 関数呼出しのフレーム・引数と結果の受渡しは`host-linking`で基盤を拡張する。`numeric-control`の分岐・loopも同じスタック基準を使う。直接call・start・ホスト再入には初期から既存の実行コンテキストと深さ制限を適用し、CLRの再帰呼出しに依存してプロセスを落とさない。
- Instantiate中のstartやsegment初期化のtrapをリンク不成立へ変換しない。呼び出し契約違反、ホスト処理の例外、実装制限・資源枯渇も、Wasmの仕様trapと無差別に混同しない。具体的な型・reasonの割り当てはrequirements/designで確定する。
- 未実装に遭遇して検証を終えられない場合、`runtime_unsupported`は「有効性を証明済み」を意味しない。判定できなかった範囲も記録し、不正なバイナリを一括して未実装へ分類しない。Core 2.0外の命令についても、選定仕様の否定テストの期待値を勝手に変更しない。

## 公式検証の方針と完了条件

公式specのCore 2.0固定版から対象ファイルを選ぶ。採用版は[外部ソースの固定](../../thirdParties/README.md)に従い、`v2.0.0`、commit `05ca4182176763112561ae20153975c12bd689e4`の`test/core`で、SIMD配下も含む。`conformance-runner`で全入力の変換結果、manifestとhash、同じ条件での再現性を確認する。3.0系テストやproposal集合との混在を避ける。

仕様版、suite取得元・commit・対象path、WABTの取得元とソースhash、ビルド条件・実行ファイルhash、全featureの実効ON/OFF、CLI引数、入力・生成物hashを記録する。CLIの有効化・無効化オプションはWABTの版と既定値に依存するため、実測前のコマンドを確定値にしない。

`wast2json`はtext構文エラーのテスト等に`.wat`も生成する。ランナーの実行処理はJSONの`module_type=text`を対象外として記録し、そのファイルを開かない。これらを合格件数に含めない。バイナリのmalformed/invalid、unlinkable、Instantiate中のtrap、Invoke中のtrap、exhaustion、戻り値の型・個数・ビット列・NaN patternはそれぞれの意味で判定する。

- `passed`: 該当assertionが期待どおり成立、または必要なセットアップ・単独actionが正常に完了。
- `failed`: 判定した結果が期待と不一致。
- `runtime_unsupported`: ランタイムの未実装を観測。
- `runner_unsupported`: ランナーがcommandや期待値の比較に未対応。
- `runner_error`: 変換失敗、JSONの破損、素材の欠落などで検証処理が異常で成立しない。
- `out_of_scope`: テキスト形式など明示された対象外。
- `blocked`: 前提が成立せず、依存するcommandを実行できない。

前のmodule/register失敗で実行できない後続commandは`blocked`とし、依存先の失敗を理由付きで記録して合格扱いにしない。集計ではセットアップ・単独action・assertionの件数を区別する。

生成済み素材の実行は保存したmanifestと生成物だけで行い、元WASTやWABTを必要としない。baselineは利用者の環境に保存し、共有・共同レビューを必須にしない。実行結果の比較では、検証プロファイルと入力・生成物の一覧・hashが一致すれば、変換器の実行ファイルhashが異なっても同じ固定スイートとして比較し、変換器の差異は出典情報として残す。

素材生成では、入力と出力先の配置ディレクトリだけが変わっても生成物の内容とhashを維持する。変換baseline比較は、全対象の比較と結果の記録・出力が完了し、入力・生成物の一覧とhash、変換結果、生成条件が一致して`runner_error`が0件の場合だけ終了コード0とし、差異や比較未完了を含むそれ以外は非0とする。

`runtime-foundation`の完成状態を維持し、次に`host-linking`を公開APIの直接テストで検証する。この段階では完成済みランナーを前提にしない。`conformance-runner`の初期完了で、全入力の素材生成と再現性、spectestとregisterを使う実行・リンク経路、全commandの記録と初回baselineをまとめて公式検証する。

初回公式受入では、全体の`failed`と入力単位・command単位の`runner_error`を0件とし、最小基盤と実行・リンク基盤の対応範囲で前提が揃うケースを合格させる。import・register・ホスト関数・共有リソースを、ランナーや公開APIの不足を理由に未対応へ残さない。後続のguest命令・segment・参照/SIMD比較を必要とするケースは、理由・必要機能・所管を記録した未対応と、それに依存する`blocked`を残せる。Core 2.0全件合格はこの段階では要求しない。

`numeric-control`以降は、各機能の実装単位ごとに必要なランナー対応も加えて固定スイート全体をコマンドで実行する。実装中の絞り込み実行は可能だが、機能の完了確認では全体の実行結果と直前のbaselineとの差分を残す。TUnitの個別テストは補助として使い、公式JSONの期待値判定や全体の回帰確認を代替しない。WAST用の簡易パーサーは作らない。

- 追加機能の対象ケースのうち、必要なランタイム機能が揃ったものは合格を要求する。必要なJSON command・期待値比較・ホスト設定の不足を理由に検証を後回しにしない。
- 同じ固定スイートで以前`passed`だったケースが別の分類へ変わる、または結果から欠落する場合は、回帰としてNGとする。合格件数だけでなく、元ファイルとcommandを特定して比較する。
- 後続のランタイム機能を必要とするケースは、出典・未成立の理由・必要な機能と所管仕様を記録する。その機能が揃った段階で再検証し、対象集合やfeature flagから除外しない。
- ランナー側のcommandや期待値比較の未対応は`runner_unsupported`とし、JSONの破損や素材の欠落などの`runner_error`、ランタイムの`runtime_unsupported`へ置き換えない。前提commandの失敗による`blocked`も元の原因に結びつけて記録する。

通常実行は、全対象の結果記録・出力が完了して`failed`・`runner_error`が0件なら終了コード0とし、これらがある場合は非0とする。理由・所管仕様を記録した後続機能に伴う未対応と、それらを原因とする`blocked`が残るだけでは非0にしない。回帰比較は、以前の`passed`の別分類への変化・欠落や比較条件の不一致も非0とし、比較が成立して`failed`・`runner_error`・回帰がなければ0とする。最終判定は、下記のCore 2.0全体の完了条件をすべて満たす場合だけ0とし、それ以外は非0とする。

最後は同じランナーで全対象を実行し、固定スイートの`out_of_scope`を除く全ケースが`passed`であることを確認する。未処理・欠落・件数未確定を残さず、`failed`・`runtime_unsupported`・`runner_unsupported`・`runner_error`・`blocked`はすべて0件とする。全7仕様と各機能に伴うランナー拡張が揃う前にCore 2.0準拠の完成を宣言しない。

最後の機能を統合する担当が、全7仕様とランナー拡張を統合した同じコード状態で最終実行を行い、コード状態・固定profile・生成物と結果を対応づけて記録する。ホスト連携とSIMDを別々の状態で検証した結果を足し合わせて最終確認の代わりにしない。

公式テスト合格は固定した集合での証拠として扱い、未収録の動作まで証明したとは言わない。各仕様では該当するCore 2.0の規則と実装・検証の対応も確認する。テストを追加・修正した場合は先に警告・エラーのないビルドを確認してからコマンドで実行し、既存のC#・TUnit規則に従う。

## 分割と依存関係の意図

Decode・Validate・Instantiate・Invokeを別々の機能仕様にせず、機能ごとに4段階を通す。最小基盤の次に関数実行と外部要素の生成・リンクを`host-linking`へ集め、数値演算・構造化制御、memory/table命令とsegment、SIMDはそれぞれの仕様が追加する。公式適合検証は、素材生成から実行結果の確認までを一つの仕様・ツールで扱い、生成済み素材も再利用できる。

`conformance-runner`の初期仕様は`host-linking`を上流とし、spectest・register、スカラー引数と結果0個/複数、global取得、公開段階と失敗分類に基づくassertion判定を含める。素材生成とJSON処理は実行・リンク基盤と並行して作業できるが、初回公式受入は両方が揃ってから行う。後続の参照・SIMD期待値比較等は各機能で同じツールへ追加する。ランタイム内部への専用hookや別ランナーは作らない。

独立した仕様の並行作業は可能だが、共通の命令テーブル・モジュール解析・実行ループへの編集は衝突し得る。設計で共通契約を先に固め、実装時は同じファイルの並行編集を避けて統合する。依存関係は仕様作成・完了確認の前提を表し、不要な実装レイヤーや公開拡張口を要求するものではない。

## Specs (dependency order)

- [x] runtime-foundation -- 明示的な4段階APIと値・型・失敗分類、最小の線形実行基盤。 Dependencies: none
- [ ] host-linking -- 関数実行、global・memory・tableの生成と共有、import/export、ホストcallbackとstartを公開APIで扱う実行・リンク基盤。 Dependencies: runtime-foundation
- [ ] conformance-runner -- 固定公式素材の生成、spectest・register、公開API実行・判定、全commandの結果記録・回帰比較と初回baseline。 Dependencies: host-linking
- [ ] numeric-control -- スカラー数値命令と構造化制御を共通実行機構へ追加し、数値trap・再帰・複数値制御を公式検証する。 Dependencies: host-linking, conformance-runner
- [ ] linear-memory -- スカラーload/store、data segment、bulk memoryと初期化・実行のtrapを公式検証する。 Dependencies: numeric-control
- [ ] tables-references -- 参照命令、table操作、element segment、間接呼出しと初期化・実行のtrapを公式検証する。 Dependencies: numeric-control
- [ ] simd -- v128とCore 2.0 SIMDを共通命令テーブル・実行ループに実装し、lane期待値判定を加える。 Dependencies: linear-memory

作業順は、①完成済みの最小基盤、②実行・リンク基盤、③公式適合検証ツールと初回baseline、④数値・制御、⑤メモリとテーブル・参照、⑥SIMD。⑤の2仕様は並行可能で、SIMDはメモリが揃えば着手できる。②は③を完了前提にせず、③で②の公式統合受入も行う。④以降は機能の完了時に同じスイートで回帰確認する。

初期ランナーはspectest・registerとスカラー入出力、global取得、段階別の否定assertionを扱う。以後のツール拡張と統合確認は次の機能仕様で行う。

| 機能仕様 | 同時に加えるランナー対応・統合確認 |
| --- | --- |
| `numeric-control` | 数値演算・構造化制御・再帰の公式ケース。必要な期待値・診断対応の拡張は初期のscalar比較と段階別判定を再利用 |
| `linear-memory` | data初期化とstart・共有memoryの複合ケース。初期のassert_uninstantiable判定を再利用 |
| `tables-references` | 参照の引数・結果、null・同一性、element初期化とstart・共有tableの複合ケース |
| `simd` | v128の引数・結果、laneごとのビット列・NaN patternの判定 |

## 将来のCore 3.0

2.0の固定profile・corpus・結果baselineを残し、3.0の仕様と期待結果は別計画として追加する。版が変わると以前invalidだった入力が有効になる場合もあるため、3.0実行時に2.0の否定テストをそのまま適用できるとは約束しない。必要なら2.0の版別実行契約またはリリースを維持する方法を、その計画で決める。

GC・再帰型・型付き参照、例外処理、64bitアドレス等では型モデル・公開契約・実行機構の追加設計が必要になる。単一命令テーブルと線形実行の方針を保って拡張するが、無変更・破壊的変更なしの移行を保証しない。3.0用の未使用型や汎用plugin機構は今作らない。WABTのGC等の変換可否も、その時点で改めて確認する。

## 根拠

- ユーザー提示の10項目と2026-09-06のdiscovery回答。
- [用語集](../../CONTEXT.md)と、[明示的な4段階API](../../docs/adr/0001-explicit-staged-runtime-api.md)、[同一パスの線形インタープリタ](../../docs/adr/0002-single-pass-linear-interpreter.md)、[Core 2.0の固定適合検証](../../docs/adr/0003-core2-fixed-conformance-profile.md)、[trapの伝播方式](../../docs/adr/0004-trap-result-propagation.md)のADR。
- [調査ノート](../../docs/research/wasm-runtime-discovery.md)。公式仕様とWABTの出典、固定時の注意点を含む。
- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)、[Core 2.0公式テスト候補](https://github.com/WebAssembly/spec/tree/v2.0.0/test/core)、[wast2jsonのJSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)。
