# WasmSharp2ロードマップ

## 概要

C#でWebAssemblyバイナリをデコード・検証・インスタンス化・実行するライブラリを一から実装する。初期の完了目標は**WebAssembly Core 2.0のバイナリ・検証・実行への準拠**とし、公式テストのバイナリ対象集合で検証する。WATの処理はライブラリの対象外とする。

2026-09-06のdiscoveryで、ユーザーは複数仕様への分割、初期Core 2.0、下記8仕様を機能ごとに4段階を通して実装する進め方を承認した。Core 3.0は将来の別計画とする。この承認はdiscoveryの方針承認であり、後続のrequirements・design・tasksや実装の承認ではない。文書の言語は日本語とし、後続で生成する`spec.json.language`は`ja`とする。

## 現状

- `src/WasmSharp`は.NET 10の公開型とメソッドシグネチャの骨組み。`Decode`・`Validate`・`Instantiate`・`Invoke`は未実装。
- `tests/WasmSharp.Tests`は.NET 10・TUnit 1.66.10のプロジェクト定義のみで、テスト本体はない。`tools`は空。
- `thirdParties`には公式specとWABTのソースがある。specは3.0系で、初期Core 2.0のテスト集合としてそのまま扱えない。
- 同梱WABTの宣言版は1.0.41だが、公式の同名タグとfeature既定値が異なる。上流commitは未特定であり、版名だけでは固定済みとは言えない。
- 既存の正式specとsteeringはない。既存コメントと公開型の意図を確認し、メソッドシグネチャの骨組みを完成済み契約とは扱わない。

## 進め方の選択

- **採用**: 小さな4段階の実行基盤を作り、数値・制御、メモリ、参照、ホスト連携、SIMDを縦に追加する。公式テスト素材の固定・生成は基盤と並行して整備する。規模は大。
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

③以降の機能は固定済み公式素材を選んで検証する。全ランナーが完成するまではTUnitから公開APIへ生成済み`.wasm`を渡し、対象と期待値の出典を記録する。WAST用の簡易パーサーは作らない。最後にランナーで全対象を実行し、対象内の`failed`・`unsupported`・`tool_error`・依存による未実行が0であることを確認する。ランナーとSIMDの両方が揃う前にCore 2.0準拠の完成を宣言しない。

公式テスト合格は固定した集合での証拠として扱い、未収録の動作まで証明したとは言わない。各仕様では該当するCore 2.0の規則と実装・検証の対応も確認する。テストを追加・修正した場合は先に警告・エラーのないビルドを確認してからコマンドで実行し、既存のC#・TUnit規則に従う。

## 分割と依存関係の意図

Decode・Validate・Instantiate・Invokeを別々の機能仕様にせず、機能ごとに4段階を通す。共通の機械だけ基盤にまとめ、メモリ・テーブル・ホスト連携・SIMDの意味論をそれぞれの仕様に集める。テスト素材の生成とランタイム利用者としてのランナーは、依存する入力と責務が違うため分ける。

独立した仕様の並行作業は可能だが、共通の命令テーブル・モジュール解析・実行ループへの編集は衝突し得る。設計で共通契約を先に固め、実装時は同じファイルの並行編集を避けて統合する。依存関係は仕様作成・完了確認の前提を表し、不要な実装レイヤーや公開拡張口を要求するものではない。

## Specs (dependency order)

- [ ] wasm-runtime-foundation -- 明示的な4段階APIと値・型・失敗分類、最小の線形実行基盤。 Dependencies: none
- [ ] wasm-test-corpus -- Core 2.0公式テスト・WABT・実効featureを固定し、JSONとモジュール素材を生成する。 Dependencies: none
- [ ] wasm-numeric-control -- スカラー数値、関数、構造化制御、複数値、globalsを4段階で実装する。 Dependencies: wasm-runtime-foundation, wasm-test-corpus
- [ ] wasm-linear-memory -- 線形メモリ、data segment、load/store、bulk memoryを実装する。 Dependencies: wasm-numeric-control
- [ ] wasm-tables-references -- テーブル、参照値、element segment、間接呼び出し、bulk tableを実装する。 Dependencies: wasm-numeric-control
- [ ] wasm-host-linking -- 明示型のホスト関数、import/exportの共有、リンクとstartを実装する。 Dependencies: wasm-linear-memory, wasm-tables-references
- [ ] wasm-simd -- v128とCore 2.0 SIMDを共通命令テーブル・実行ループに実装する。 Dependencies: wasm-linear-memory
- [ ] wasm-conformance-runner -- 公開APIだけでspectestとJSONコマンドを実行し、公式適合結果を分類・集計する。 Dependencies: wasm-test-corpus, wasm-host-linking

仕様を作る波は、①基盤と素材、②数値・制御、③メモリとテーブル・参照、④ホスト連携とSIMD、⑤ランナー。ランナー自身はSIMD命令を実装せず、基盤の`WasmValue`でv128の入出力を扱う。SIMD未完成時はその実行を`unsupported`として扱い、全体の完成条件は別途全8仕様を要求する。

## 将来のCore 3.0

2.0の固定profile・corpus・結果baselineを残し、3.0の仕様と期待結果は別計画として追加する。版が変わると以前invalidだった入力が有効になる場合もあるため、3.0実行時に2.0の否定テストをそのまま適用できるとは約束しない。必要なら2.0の版別実行契約またはリリースを維持する方法を、その計画で決める。

GC・再帰型・型付き参照、例外処理、64bitアドレス等では型モデル・公開契約・実行機構の追加設計が必要になる。単一命令テーブルと線形実行の方針を保って拡張するが、無変更・破壊的変更なしの移行を保証しない。3.0用の未使用型や汎用plugin機構は今作らない。WABTのGC等の変換可否も、その時点で改めて確認する。

## 根拠

- ユーザー提示の10項目と2026-09-06のdiscovery回答。
- [用語集](../../CONTEXT.md)と、[明示的な4段階API](../../docs/adr/0001-explicit-staged-runtime-api.md)、[同一パスの線形インタープリタ](../../docs/adr/0002-single-pass-linear-interpreter.md)、[Core 2.0の固定適合検証](../../docs/adr/0003-core2-fixed-conformance-profile.md)、[trapの伝播方式](../../docs/adr/0004-trap-result-propagation.md)のADR。
- [調査ノート](../../docs/research/wasm-runtime-discovery.md)。公式仕様とWABTの出典、固定時の注意点を含む。
- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)、[Core 2.0公式テスト候補](https://github.com/WebAssembly/spec/tree/v2.0.0/test/core)、[wast2jsonのJSON仕様](https://github.com/WebAssembly/wabt/blob/main/docs/wast2json.md)。
