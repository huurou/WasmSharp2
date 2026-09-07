# WasmSharp2ロードマップ

## 概要

C#でWebAssemblyバイナリをデコード・検証・インスタンス化・実行するライブラリを一から実装する。初期の完了目標は**WebAssembly Core 2.0のバイナリ・検証・実行への準拠**とし、公式テストのバイナリ対象集合で検証する。WATの処理はライブラリの対象外とする。

2026-09-06のdiscoveryで、ユーザーは複数仕様への分割、初期Core 2.0、下記8仕様を機能ごとに4段階を通して実装する進め方を承認した。Core 3.0は将来の別計画とする。この承認はdiscoveryの方針承認であり、後続のrequirements・design・tasksや実装の承認ではない。文書の言語は日本語とし、後続で生成する`spec.json.language`は`ja`とする。

2026-09-07の順序見直しでは、進行中の`wasm-runtime-foundation`を先頭に保ち、公式素材、ランナーの初期基盤、各実行機能の順に進める。基盤の承認済み要件・設計・タスクは維持する。後続機能では必要なランナー対応と公式スイートによる回帰確認も完了条件に含める。

## discovery時点の現状（2026-09-06）

- `src/WasmSharp`は.NET 10の公開型とメソッドシグネチャの骨組み。`Decode`・`Validate`・`Instantiate`・`Invoke`は未実装。
- `tests/WasmSharp.Tests`は.NET 10・TUnit 1.66.10のプロジェクト定義のみで、テスト本体はない。`tools`は空。
- `thirdParties`には公式specとWABTのソースがある。specは3.0系で、初期Core 2.0のテスト集合としてそのまま扱えない。
- 同梱WABTの宣言版は1.0.41だが、公式の同名タグとfeature既定値が異なる。上流commitは未特定であり、版名だけでは固定済みとは言えない。
- 既存の正式specとsteeringはない。既存コメントと公開型の意図を確認し、メソッドシグネチャの骨組みを完成済み契約とは扱わない。

## 進め方の選択

- **採用**: 小さな4段階の実行基盤を完成させ、公式テスト素材の固定・生成とランナーの初期基盤を整備してから、数値・制御、メモリ、参照、ホスト連携、SIMDを縦に追加する。各機能の実装に合わせて同じランナーを拡張し、固定スイートを継続実行する。規模は大。
- **理由**: 各機能の仕様解釈と実行結果を早く対応づけられ、未実装と不具合の差分を固定したテスト集合上で追える。
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
9. **WASTは自前で読まない**: `wast2json`がWASTをJSONと個別モジュールへ変換する。自作ツールが読み込むのはJSONと`.wasm`だけとする。
10. **feature flagは初期の最終範囲に固定する**: Core 2.0内をON、範囲外をOFFとし、実装の進捗で変更しない。`--enable-all`を使わない。未実装機能を無効化して検証対象から隠さない。

## 仕様間で共有する契約

- `WasmModule`は静的定義、`WasmInstance`は実行時の実体という既存の区別を保つ。検証前のInstantiateを防ぐ契約、検証成功後の実行表現の所有権は基盤で決める。
- 基盤はCore 2.0の値・型・添字空間と、後続が使うデコード結果・命令情報・スタック・実行結果の共通契約を持つ。各機能の具体的なデコード、検証、初期化、実行はその機能の仕様が所有する。
- scalarとvector・参照をやり取りできる`WasmValue`の契約を基盤で決める。各命令の意味論は該当機能が持つ。参照の同一性を明示的に表し、汎用`object`引数で代用しない。
- メモリ・テーブル・globalを定義して利用する機能は各機能仕様が所有する。ホスト連携はそれらのリソースをimport/exportで結び、同一性を保つ責務を持つ。guestメモリ命令やtable命令をホスト連携側に再実装しない。
- `WasmInstance`の既存コメント「Exportsは持たせない」を守る。公開取得操作と共有リソースの扱いは通常の埋め込み利用を根拠に設計する。`GetGlobal`の値取得だけでmutable globalの共有を表現できると決めつけない。
- 分岐のstack heightの基準、loop引数とblock結果の保持数、関数呼び出し時のスタック基準は基盤と数値・制御で一致させる。CLRの再帰呼び出しに依存してテストプロセスを落とす構成を避ける。
- Instantiate中のstartやsegment初期化のtrapをリンク不成立へ変換しない。呼び出し契約違反、ホスト処理の例外、実装制限・資源枯渇も、Wasmの仕様trapと無差別に混同しない。具体的な型・reasonの割り当てはrequirements/designで確定する。
- 未実装に遭遇して検証を終えられない場合、`unsupported`は「有効性を証明済み」を意味しない。判定できなかった範囲も記録し、不正なバイナリを一括して未実装へ分類しない。Core 2.0外の命令についても、選定仕様の否定テストの期待値を勝手に変更しない。

## 公式検証の方針と完了条件

公式specのCore 2.0固定版から対象ファイルを選ぶ。候補は`v2.0.0`、commit `05ca4182176763112561ae20153975c12bd689e4`の`test/core`で、SIMD配下も含む。確定は`wasm-test-corpus`で仕様保存版との対応と変換結果を確認して行う。同梱3.0系テストやproposal集合との混在を避ける。

仕様版、suite取得元・commit・対象path、WABTの取得元とソースhash、ビルド条件・実行ファイルhash、全featureの実効ON/OFF、CLI引数、入力・生成物hashを記録する。CLIの有効化・無効化オプションはWABTの版と既定値に依存するため、実測前のコマンドを確定値にしない。

`wast2json`はtext構文エラーのテスト等に`.wat`も生成する。ランナーはJSONの`module_type=text`を対象外として記録し、そのファイルを開かない。これらを合格件数に含めない。バイナリのmalformed/invalid、unlinkable、Instantiate中のtrap、Invoke中のtrap、exhaustion、戻り値の型・個数・ビット列・NaN patternはそれぞれの意味で判定する。

- `passed`: 該当assertionまたは必要なセットアップが期待どおり成立。
- `failed`: 実装済み経路の結果が期待と不一致。
- `unsupported`: ランタイムの未実装を観測。
- `out_of_scope`: テキスト形式など明示された対象外。
- `tool_error`: 変換不能、JSON未対応、実行準備の故障など、ランタイムの意味論を検証できていない。

前のmodule/register失敗で実行できない後続commandも、依存先の失敗を理由付きで記録して合格扱いにしない。集計ではセットアップcommandとassertionの件数を区別する。

`wasm-runtime-foundation`は既存の完了条件で先に完成させる。その後、`wasm-test-corpus`と`wasm-conformance-runner`の初期基盤を整備し、基盤の実装状態で固定スイート全体を処理した最初の結果baselineを保存する。この時点でCore 2.0全件合格は要求しない。

`wasm-numeric-control`以降は、各機能の実装単位ごとに必要なランナー対応も加えて固定スイート全体をコマンドで実行する。実装中の絞り込み実行は可能だが、機能の完了確認では全体の実行結果と直前のbaselineとの差分を残す。TUnitの個別テストは補助として使い、公式JSONの期待値判定や全体の回帰確認を代替しない。WAST用の簡易パーサーは作らない。

- 追加機能の対象ケースのうち、必要なランタイム機能が揃ったものは合格を要求する。必要なJSON command・期待値比較・ホスト設定の不足を理由に検証を後回しにしない。
- 以前`passed`だったケースが他の結果や未実行へ変わっていないことを確認する。合格件数だけでなく、元ファイルとcommandを特定して比較する。
- 後続のランタイム機能を必要とするケースは、出典・未成立の理由・必要な機能と所管仕様を記録する。その機能が揃った段階で再検証し、対象集合やfeature flagから除外しない。
- ランナー側のJSON未対応や期待値比較の不足は`tool_error`とし、ランタイムの`unsupported`へ置き換えない。前提commandの失敗による未実行も元の原因に結びつけて記録する。

最後は同じランナーで全対象を実行し、対象内の`failed`・`unsupported`・`tool_error`・依存による未実行が0であることを確認する。全8仕様と各機能に伴うランナー拡張が揃う前にCore 2.0準拠の完成を宣言しない。

最後の機能を統合する担当が、全8仕様とランナー拡張を統合した同じコード状態で最終実行を行い、コード状態・固定profile・生成物と結果を対応づけて記録する。ホスト連携とSIMDを別々の状態で検証した結果を足し合わせて最終確認の代わりにしない。

公式テスト合格は固定した集合での証拠として扱い、未収録の動作まで証明したとは言わない。各仕様では該当するCore 2.0の規則と実装・検証の対応も確認する。テストを追加・修正した場合は先に警告・エラーのないビルドを確認してからコマンドで実行し、既存のC#・TUnit規則に従う。

## 分割と依存関係の意図

Decode・Validate・Instantiate・Invokeを別々の機能仕様にせず、機能ごとに4段階を通す。共通の機械だけ基盤にまとめ、メモリ・テーブル・ホスト連携・SIMDの意味論をそれぞれの仕様に集める。テスト素材の生成とランタイム利用者としてのランナーは、依存する入力と責務が違うため分ける。

`wasm-conformance-runner`の初期仕様は、基盤の公開APIで実行できる公式ケースと、全commandの結果記録・回帰比較までを完成させる。spectestや後続機能の公開APIを待たない。各機能に必要なランナー拡張は、その機能仕様の受入作業として同じツール内へ追加する。JSON処理・期待値比較・spectestの配置はツール側に保ち、ランタイム内部への専用hookや別ランナーを作らない。

独立した仕様の並行作業は可能だが、共通の命令テーブル・モジュール解析・実行ループへの編集は衝突し得る。設計で共通契約を先に固め、実装時は同じファイルの並行編集を避けて統合する。依存関係は仕様作成・完了確認の前提を表し、不要な実装レイヤーや公開拡張口を要求するものではない。

## Specs (dependency order)

- [ ] wasm-runtime-foundation -- 明示的な4段階APIと値・型・失敗分類、最小の線形実行基盤。 Dependencies: none
- [ ] wasm-test-corpus -- Core 2.0公式テスト・WABT・実効featureを固定し、JSONとモジュール素材を生成する。 Dependencies: wasm-runtime-foundation
- [ ] wasm-conformance-runner -- 基盤対応範囲のJSON実行と、固定スイート全体の結果分類・集計・回帰比較を行う初期ランナーを整備する。 Dependencies: wasm-runtime-foundation, wasm-test-corpus
- [ ] wasm-numeric-control -- スカラー数値、関数、構造化制御、複数値、globalsを4段階で実装し、対応するランナー機能と公式回帰確認を加える。 Dependencies: wasm-runtime-foundation, wasm-conformance-runner
- [ ] wasm-linear-memory -- 線形メモリ、data segment、load/store、bulk memoryと初期化trapの公式検証を実装する。 Dependencies: wasm-numeric-control
- [ ] wasm-tables-references -- テーブル、参照値、element segment、間接呼び出し、bulk tableと参照の公式期待値判定を実装する。 Dependencies: wasm-numeric-control
- [ ] wasm-host-linking -- 明示型のホスト関数、import/exportの共有、リンクとstartを実装し、ツール側のspectestとregisterを完成させる。 Dependencies: wasm-linear-memory, wasm-tables-references
- [ ] wasm-simd -- v128とCore 2.0 SIMDを共通命令テーブル・実行ループに実装し、ランナーのlane期待値判定を加える。 Dependencies: wasm-linear-memory

作業順は、①進行中の基盤、②公式素材、③初期ランナーとbaseline、④数値・制御、⑤メモリとテーブル・参照、⑥ホスト連携とSIMD。素材から基盤への依存は、基盤を最初に完成させる作業順の指定であり、素材生成ツールがランタイムを参照する要求ではない。⑤の2仕様は並行可能。⑥のホスト連携は⑤の両方を必要とし、SIMDはメモリが揃えば着手できる。各機能の完了時に同じスイートで回帰確認し、最後に全体の合格条件を確認する。

初期ランナー完了後に加えるツール側の対応は次の機能仕様で実装・検証する。これらを初期ランナーの完了前提へ戻して循環依存を作らない。

| 機能仕様 | 同時に加えるランナー対応 |
| --- | --- |
| `wasm-numeric-control` | 引数・複数戻り値、global取得、数値結果・NaN、trap・exhaustionの判定 |
| `wasm-linear-memory` | data初期化時のtrapを含むインスタンス化失敗の判定 |
| `wasm-tables-references` | 参照の引数・結果、null・同一性、element初期化trapの判定 |
| `wasm-host-linking` | spectest、module/registerと共有状態、リンク不成立・start・importを含む統合確認 |
| `wasm-simd` | v128の引数・結果、laneごとのビット列・NaN patternの判定 |

## 将来のCore 3.0

2.0の固定profile・corpus・結果baselineを残し、3.0の仕様と期待結果は別計画として追加する。版が変わると以前invalidだった入力が有効になる場合もあるため、3.0実行時に2.0の否定テストをそのまま適用できるとは約束しない。必要なら2.0の版別実行契約またはリリースを維持する方法を、その計画で決める。

GC・再帰型・型付き参照、例外処理、64bitアドレス等では型モデル・公開契約・実行機構の追加設計が必要になる。単一命令テーブルと線形実行の方針を保って拡張するが、無変更・破壊的変更なしの移行を保証しない。3.0用の未使用型や汎用plugin機構は今作らない。WABTのGC等の変換可否も、その時点で改めて確認する。

## 根拠

- ユーザー提示の10項目と2026-09-06のdiscovery回答。
- [用語集](../../CONTEXT.md)と、[明示的な4段階API](../../docs/adr/0001-explicit-staged-runtime-api.md)、[同一パスの線形インタープリタ](../../docs/adr/0002-single-pass-linear-interpreter.md)、[Core 2.0の固定適合検証](../../docs/adr/0003-core2-fixed-conformance-profile.md)、[trapの伝播方式](../../docs/adr/0004-trap-result-propagation.md)のADR。
- [調査ノート](../../docs/research/wasm-runtime-discovery.md)。公式仕様とWABTの出典、固定時の注意点を含む。
- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)、[Core 2.0公式テスト候補](https://github.com/WebAssembly/spec/tree/v2.0.0/test/core)、[wast2jsonのJSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)。
