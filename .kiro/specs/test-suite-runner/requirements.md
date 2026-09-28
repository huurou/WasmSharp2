# 要件文書

## はじめに

WasmSharp2の実装者が、固定したWebAssembly Core 2.0の公式スイートを独立したCLIで生成・実行し、仕様違反、実装不足、既存合格ケースの回帰を確認するための要件を定める。生成済み素材だけの実行にも対応し、spectest・registerを含む公式ケースの出典と結果を追跡できるようにする。

先行するruntime-foundationとhost-linkingは公開APIの直接テストによる受入まで完了している。本仕様は最初から公式期待診断を前方一致で判定し、固定スイートに現れる全値型の引数構築と結果比較を初期から扱う。初回受入では固定スイート全体を処理し、先行基盤の成立と実装上の不一致を区別して記録し、全commandの記録、初回baselineの保存と再実行・回帰比較を確認する。初期対応する公式ケースを実行・判定できるまでの修正は、ランタイム側の修正も含めて本仕様で行う。判定で得たfailedの解消は診断互換性も含めて後続のtest-suite-conformanceへ集約する。初期必須の実行経路を妨げず、原因をランタイム側と確認できたrunner_errorも、分類と非0終了を維持して後続へ引き継げる。初期ランナーの完成とランタイムの全件合格は区別する。

## 対象範囲と隣接仕様

| 区分 | 範囲 |
| --- | --- |
| 提供形態 | ランタイムから独立して起動するCLI。ソースを`tools/`配下に配置する。素材生成・実行・baseline保存・baseline比較・最終判定を個別のコマンドで提供する。 |
| 公式素材 | 固定specの`test/core`配下の全WAST。SIMDを含む。固定WABTによるJSON・wasm・wat生成、出典・生成条件の記録、変換結果を兼ねるmanifest、照合、再現性確認。 |
| 初期の実行・判定 | module、register、spectest、invoke/get、固定スイートに現れる全値型（i32・i64・f32・f64・v128・funcref・externref）の引数構築と結果比較、結果0個/1個/複数・global取得、段階別assertionと公式期待診断の前方一致、全commandの結果分類と回帰比較。 |
| ランタイム修正 | 初期必須の公式ケースを実行・判定するために必要な修正。registerに必要な、moduleが宣言しinstanceが公開するexport一覧を取得する公開APIの追加を含む。 |
| 後続仕様 | test-suite-conformanceはスイートで判明したランタイムの動作・値・状態・失敗分類・診断の不一致をまとめて修正し、実行結果に応じて修正項目を追記・見直す。numeric-controlは数値・構造化制御、linear-memoryはmemory命令・data初期化、tables-referencesはtable・element・参照命令、simdはSIMD命令をランタイムへ追加し、同じツールの既存の値比較と段階別判定で公式統合確認を行う。 |
| 隣接する公開契約 | 関数・global・memory・tableの生成と共有、import/export、start、import情報取得、Wasmの各処理段階と失敗分類は先行ランタイムの公開契約を使う。spectestの提供内容とJSON commandの状態・期待値判定は本ツールで扱う。 |
| 実行対象外 | `module_type=text`のmodule。生成されたwatは出典・素材照合の対象だが、実行処理では開かず、解析・実行しない。 |
| 全体の対象外 | WAST/WATの自作解析、Wasm演算・import型照合の再実装、ランタイム内部へのアクセス・専用hook、別エンジンへの実行委譲、所管仕様の記録、Core 3.0/proposal用profile、baseline共有サービス。 |

本書ではツールを「Test Suite Runner」と呼ぶ。要件と受入基準は「要件番号.項番」で参照する。公式ケースは変換済みJSONの1commandを指し、その他の用語は[用語集](../../../CONTEXT.md)に従う。詳細なコマンド名、JSONファイルの構造、内部構造、hash方式、非0の終了値は設計で定める。新たな性能目標や並行実行の保証は追加しない。公式ケースの公開APIによる実行は単一プロセス内で順に行い、入力ごとのプロセス隔離、タイムアウトによる強制終了、自動再起動、途中再開は設けない。ハングやプロセス異常終了からの継続は保証せず、中断を検知して記録可能な場合は要件10.7に従う。

前提は[ブリーフ](brief.md)、[ロードマップ](../../steering/roadmap.md)、[固定版と変換条件](../../../thirdParties/README.md)、[Core 2.0の固定適合検証](../../../docs/adr/0003-core2-fixed-conformance-profile.md)に従う。JSONの意味は[固定WABTのwast2json形式](../../../thirdParties/wabt/docs/wast2json.md)、spectestの具体値は[固定specの参照インタープリター](../../../thirdParties/WebAssembly-spec/interpreter/host/spectest.ml)を根拠とする。

## 要件

### 要件1: 個別のコマンドによる操作

**目的:** 実装者として、公式素材の準備、実行、baselineとの比較、最終判定を同じツールの個別のコマンドで行い、機能追加の前後を比較したい。

#### 受入基準

1. When 利用者が検証ツールを起動する場合, the Test Suite Runner shall ランタイムから独立したCLIとして、素材生成・実行・baseline保存・baseline比較・最終判定をそれぞれ独立したコマンドで提供し、複数の工程をまとめて実行する操作は設けない。
2. When 利用者が素材生成を要求した場合, the Test Suite Runner shall 固定公式入力の変換と生成素材の照合を行い、ランタイムで公式ケースを実行せずに、生成物とmanifestを保存する。
3. When 利用者が実行を要求した場合, the Test Suite Runner shall 保存したmanifestと生成物から公式ケースを実行・期待値判定し、結果の保存と集計を行う。元WAST、WABT、生成時の配置先は要求しない。
4. When 利用者がbaselineを保存または比較する場合, the Test Suite Runner shall baselineの保存とbaselineとの比較を別々の明示的なコマンドとして提供し、どちらも保存済みの結果JSONを入力として再生成・再実行せずに処理する。比較コマンドからbaselineを作成・更新しない。
5. When 利用者がbaselineとの比較を要求した場合, the Test Suite Runner shall 変換結果の再現性比較と実行結果の回帰比較を区別して実施し、比較結果を保存する。
6. When 利用者が最終判定を要求した場合, the Test Suite Runner shall 保存済みの1つの実行結果JSONを入力とし、再生成・再実行せずに要件13.5の条件で判定する。
7. While 公式ケースを実行・判定している間, the Test Suite Runner shall 通常の埋め込み利用者としてWasmSharp2の公開APIのみを用い、Wasmの意味論やimport型照合を独自に代行しない。

### 要件2: 公式入力・変換器・検証プロファイルの固定

**目的:** 実装者として、実装の進捗に左右されない同じ検証対象と変換条件を再利用したい。

#### 受入基準

1. When 初期の公式入力を確定する場合, the Test Suite Runner shall 採用specのCore 2.0固定commitに含まれる`test/core/**/*.wast`をSIMD配下も含めてすべて対象とし、WABT自身のtestsuiteや別版・proposalの入力を混在させない。
2. When 素材を生成する場合, the Test Suite Runner shall 採用specとWABTの取得元・commit、変換器実行ファイルのhash、検証プロファイル、変換引数をmanifestに記録する。変換器のビルド条件は実行ファイルのhashで代表させ、ソースは採用commitと入力WASTのhashで識別し、ソースツリー全体のhashは取らない。
3. When 変換条件を記録する場合, the Test Suite Runner shall 固定WABTが持つ全featureの既定値と実効ON/OFF、および変換結果に影響する実行条件を記録する。
4. While Core 2.0の固定スイートを生成・実行している間, the Test Suite Runner shall Core 2.0内の機能をON、範囲外をOFFに維持し、`--enable-all`と`--no-check`を使用しない。
5. If ランタイムに未実装機能が残っている場合, then the Test Suite Runner shall その理由で公式入力・期待値・featureを変更せず、同じ対象集合に対して未対応を記録する。
6. When 利用者が固定条件を変更する場合, the Test Suite Runner shall 専用の更新操作を設けず、変更後の再生成結果と変換baselineの比較で変更前後の条件と差分を記録し、baseline保存コマンドによる明示的な上書きで新しい固定条件を確定する。
7. While 素材の生成・照合・実行・比較を行っている間, the Test Suite Runner shall 公式入力、固定した外部ソース、LICENSE・NOTICEを変更しない。

### 要件3: 全入力の変換とmanifest

**目的:** 実装者として、どの入力からどの素材が生成されたかと、変換の完了状況を漏れなく確認したい。

#### 受入基準

1. When 素材生成を開始した場合, the Test Suite Runner shall 固定対象の全入力を列挙し、それぞれをwast2jsonでJSONと個別moduleの素材へ変換する。
2. When 変換結果を保存する場合, the Test Suite Runner shall 元入力と生成されたJSON・wasm・watの対応、相対path、hash、入力ごとの変換状態、出典・生成条件と集計をmanifestに保持し、manifestを変換結果のJSONファイルとして保存する。
3. When JSONが個別moduleの素材を参照する場合, the Test Suite Runner shall JSONからの参照先とmanifest上の生成物を対応付け、参照先の欠落を検出可能にする。
4. If 一つの入力の変換が失敗した場合, then the Test Suite Runner shall その入力と変換器の診断をrunner_errorとして記録し、独立した残りの入力の変換を継続する。
5. If 変換を完了できなかった入力がある場合, then the Test Suite Runner shall 変換成功・変換失敗・未処理を区別し、存在する一部の生成物だけでその入力を変換成功と扱わない。
6. When 入力と出力先の配置rootだけを変更して同じ固定条件で再生成した場合, the Test Suite Runner shall 生成物の内容とhashを維持し、JSON内の`source_filename`やmodule参照にも配置rootによる差を生じさせない。
7. When 素材生成を完了した場合, the Test Suite Runner shall 全対象の変換状態と生成物一覧を出力し、変換成功をランタイムの適合結果として扱わない。

### 要件4: 素材の照合と入力異常

**目的:** 実装者として、素材の欠落・変更・破損をWasmの期待失敗と取り違えず、別の配置先でも同じ素材を使いたい。

#### 受入基準

1. When 生成工程で素材を照合する場合, the Test Suite Runner shall 元入力と生成物の一覧・path・hashをmanifestと照合する。
2. When 実行工程で素材を照合する場合, the Test Suite Runner shall 保存された生成物の一覧・path・hashとJSONからの参照をmanifestに照合し、生成時の元入力の存在確認を前提にしない。
3. If manifestと実際の素材に欠落・不一致があるか素材を読み取れない場合, then the Test Suite Runner shall 対象入力・素材と理由をrunner_errorとして記録し、期待されたmalformed・invalid・trapの成立に置き換えない。
4. If JSONが破損しているかcommandの判定に必要な構造・値が不正な場合, then the Test Suite Runner shall 読み取り可能な入力・commandの位置と異常理由をrunner_errorとして記録する。
5. When JSONのcommandをすべて列挙できない入力がある場合, the Test Suite Runner shall その入力のcommand件数を未確定として報告し、0件や推測のblocked件数で埋めない。
6. When `module_type=text`のcommandを処理する場合, the Test Suite Runner shall 実行処理では参照先watを開かずout_of_scopeとして記録し、素材照合で検出した入力異常とは分けて保持する。
7. When 保存したmanifestと生成物を相対配置を保って移動した場合, the Test Suite Runner shall 生成時の絶対pathに依存せず、同じ素材の照合と実行結果比較を可能にする。

### 要件5: command順序・module・registerの状態

**目的:** 実装者として、公式スクリプトの順序と共有状態を保って複数moduleの連携を検証したい。

#### 受入基準

1. When 一つの入力を実行する場合, the Test Suite Runner shall JSONの全commandを記載順に処理し、入力間でmodule・module識別子・登録状態・spectestの状態を分離する。
2. When 通常のmodule commandを処理する場合, the Test Suite Runner shall 公開APIのDecode・Validate・Instantiateを順に行い、成功したinstanceを後続のactionやregisterから利用可能にする。
3. When actionまたはregisterの対象moduleを選ぶ場合, the Test Suite Runner shall module識別子が指定されていれば対応するmoduleを、省略されていれば直近の通常module commandを参照する。
4. When registerが成功した場合, the Test Suite Runner shall 公開APIで取得した対象instanceの全exportを指定された登録名に対応付け、後続moduleからそのexportされた関数・global・memory・tableを同じ実体として利用可能にする。
5. While module識別子と登録名を解決している間, the Test Suite Runner shall 両者を区別し、同じ文字列であることだけを理由に一方を他方として扱わない。
6. When 否定assertionがmoduleを生成・処理した場合, the Test Suite Runner shall そのmoduleで直近module・module識別子・登録状態を更新せず、実行に伴って共有リソースへ生じた副作用はランタイムの公開契約どおり保持する。
7. If 通常module commandまたはregisterが成立しなかった場合, then the Test Suite Runner shall 失敗した対象への後続参照を以前の成功moduleへ代替せず、原因commandを識別できる状態を保持する。

### 要件6: 処理順・前提commandへの依存と継続実行

**目的:** 実装者として、実行できないケースの原因を特定し、無関係な公式ケースの結果まで失わずに確認したい。

#### 受入基準

1. When commandを処理する場合, the Test Suite Runner shall 処理の順に進め、最初に処理を継続できなくなった箇所でそのcommandを分類する。
2. When assertion内を含むmoduleを処理する場合, the Test Suite Runner shall 登録状態に依存しないDecode・Validateを先に行ってその結果で分類し、登録依存の不成立によるblockedはInstantiateへ進む時点でだけ判定する。
3. When 後続moduleの登録依存を確認する場合, the Test Suite Runner shall 公開APIで得たimport情報とその時点の登録状態から実際の依存を特定し、WASTやバイナリの独自解析で補わない。
4. If 対象moduleまたは依存するregisterが既知のcommand失敗によって利用できない場合, then the Test Suite Runner shall その結果を必要とする後続commandをblockedとし、直接の原因commandと元の失敗を追跡可能にする。
5. If 失敗したcommandと依存関係のない後続moduleまたは別入力がある場合, then the Test Suite Runner shall それらの処理を継続し、一律にblockedへ分類しない。
6. If moduleが要求する登録名が一度も登録されておらず既知の失敗commandへの依存もない場合, then the Test Suite Runner shall 単なる登録名の不在をblockedにせず、公開Instantiateによるリンク不成立の判定対象として扱う。
7. When import情報を取得できた場合, the Test Suite Runner shall その成功をmodule全体のDecode・Validate・実行成功の証明として扱わず、assertionが要求する公開段階で判定する。
8. If import情報を取得できない場合, then the Test Suite Runner shall 取得操作の実際の失敗と未確認範囲を記録し、importなしや架空の依存を推定せず、その失敗だけで期待malformed・invalidの成立を代用しない。

### 要件7: spectestの固定環境

**目的:** 実装者として、公式テストが要求する同じホスト環境を初回から利用し、importと共有リソースを検証したい。

spectestの提供内容は次のとおりとする。ページ数はCore 2.0のメモリページを単位とする。

| 名前 | 型・初期値 |
| --- | --- |
| `print` | 関数`() -> ()` |
| `print_i32` | 関数`(i32) -> ()` |
| `print_i64` | 関数`(i64) -> ()` |
| `print_f32` | 関数`(f32) -> ()` |
| `print_f64` | 関数`(f64) -> ()` |
| `print_i32_f32` | 関数`(i32, f32) -> ()` |
| `print_f64_f64` | 関数`(f64, f64) -> ()` |
| `global_i32` | immutableのi32、666 |
| `global_i64` | immutableのi64、666 |
| `global_f32` | immutableのf32、666.6をf32に丸めた値 |
| `global_f64` | immutableのf64、666.6をf64に丸めた値 |
| `table` | funcref、初期10要素・最大20要素、全要素null |
| `memory` | 初期1ページ・最大2ページ、全バイト0 |

#### 受入基準

1. When 入力の実行環境を用意する場合, the Test Suite Runner shall 上表の明示型のホスト関数7個、数値global4個、tableとmemoryを公開APIで生成し、import用の`spectest`として提供する。
2. When spectestのprint系関数が呼ばれた場合, the Test Suite Runner shall 結果0個で正常復帰し、関数名と引数の型・ビット列を呼び出しが起きたcommandの詳細結果に記録する。標準出力へは出力しない。
3. When 同じ入力内の複数moduleが同じspectestの外部要素をimportした場合, the Test Suite Runner shall 同じ関数・リソースの実体を提供し、共有memory/tableに生じた変更を後続の利用でも保持する。
4. When 別の入力の処理を開始した場合, the Test Suite Runner shall 上表の初期状態のspectestを提供し、以前の入力で行われた変更を持ち込まない。
5. If import名・外部要素の種類・型・limitsが提供内容に適合しない場合, then the Test Suite Runner shall テストの要求に合わせてspectestを作り替えず、ランタイムの公開Instantiateでリンクの成否を判定する。

### 要件8: actionと値の比較

**目的:** 実装者として、関数の呼び出し結果とglobal値を、固定スイートに現れる全値型について型・個数・ビット列・参照まで正確に判定したい。

#### 受入基準

1. When invoke actionを処理する場合, the Test Suite Runner shall 対象instanceのexportされた関数を公開APIで取得し、JSONが指定する順序・型・値の引数で呼び出す。
2. When get actionを処理する場合, the Test Suite Runner shall 対象instanceの指定されたexportからglobalの現在値を公開APIで取得する。
3. When i32・i64・f32・f64のJSON値を引数として使用する場合, the Test Suite Runner shall 固定WABTの文字列表現が示す型とビット列を保持し、符号付き整数の解釈や浮動小数点への数値変換によって値を変更しない。
4. When v128のJSON値を引数として使用する場合, the Test Suite Runner shall lane型と各laneの値から、lane順序と各laneのビット列を保った128ビット値を構築する。
5. When funcrefまたはexternrefのJSON値を引数として使用する場合, the Test Suite Runner shall `null`をその型のnull参照とし、externrefの非null値には同じ入力内で番号ごとに同一のホスト値を割り当てる。
6. When assert_returnを判定する場合, the Test Suite Runner shall 結果0個・1個・複数に対応し、期待値と実際の値の個数・順序・型を比較する。
7. When 数値の期待値またはv128の各laneの期待値が具体的な値で指定されている場合, the Test Suite Runner shall ビット列の一致で比較し、浮動小数点の正負の0と明示されたNaN payloadを区別する。
8. When f32・f64の期待値、またはf32・f64のlaneの期待値が`nan:canonical`である場合, the Test Suite Runner shall 符号を問わず、その型のcanonical NaNに一致するビット列だけを成立とする。
9. When f32・f64の期待値、またはf32・f64のlaneの期待値が`nan:arithmetic`である場合, the Test Suite Runner shall 符号を問わず、指数部がすべて1かつ仮数部の最上位ビットが1のNaNだけを成立とする。
10. When 参照の期待値を判定する場合, the Test Suite Runner shall `null`の期待値は同じ型のnull参照だけを成立とし、externrefの非null期待値は同じ番号に割り当てたホスト値と同一の参照だけを成立とする。
11. When 単独actionが正常に完了した場合, the Test Suite Runner shall assertionとは区別してpassedとし、取得した結果を記録する。

### 要件9: 公開段階に対応したassertion

**目的:** 実装者として、構文・型検証・リンク・実行のどの期待結果が成立したかを、公式期待診断も含めて区別したい。

#### 受入基準

1. When binaryのassert_malformedを判定する場合, the Test Suite Runner shall 公開Decodeがバイナリの構文不成立を報告した場合だけ、その期待失敗を成立とする。
2. When binaryのassert_invalidを判定する場合, the Test Suite Runner shall Decode成功後の公開Validateが型・構造の検証不成立を報告した場合だけ、その期待失敗を成立とする。
3. When assert_unlinkableを判定する場合, the Test Suite Runner shall Decode・Validate成功後の公開Instantiateがimportの不足または不適合によるリンク不成立を報告した場合だけ、その期待失敗を成立とする。
4. When assert_uninstantiableを判定する場合, the Test Suite Runner shall Decode・Validateとリンクが成功し、Instantiate中のstartまたはsegment初期化でWasm trapが発生した場合だけ、その期待失敗を成立とする。
5. When action形式のassert_trapを判定する場合, the Test Suite Runner shall 対象actionの実行で公開APIがWasm trapを報告した場合だけ、その期待失敗を成立とする。
6. When assert_exhaustionを判定する場合, the Test Suite Runner shall 対象actionの実行で公開APIがexhaustionを報告した場合だけ、その期待失敗を成立とし、通常のtrapやOOMで代用しない。
7. If 判定を実施できたが正常終了・結果値・公開段階・失敗種類・診断が期待と異なる場合, then the Test Suite Runner shall failedとし、期待した結果と実際の観測結果を記録する。
8. When 要件9.1～9.6の否定assertionを判定する場合, the Test Suite Runner shall 各項の段階・例外型・失敗分類に加え、公開例外のMessageに対する`actualException.Message.StartsWith(expectedText, StringComparison.Ordinal)`の成立を合格条件とする。expectedTextはJSONのtextをそのまま使用する。
9. If 必要な命令・segmentがランタイムで未実装のため判定を完了できない場合, then the Test Suite Runner shall runtime_unsupportedとし、未実装を期待されたinvalid・リンク不成立・trap・exhaustionの成立に置き換えない。
10. While 期待診断を照合している間, the Test Suite Runner shall 期待文字列と実際のMessageを加工せず、大文字小文字・空白・数値を保持した前方一致を一律に使い、正規化・別名への置換・Reasonだけの一致による代替・ケース別除外・照合を無効にする合格モードを設けない。
11. When 否定assertionの診断を記録する場合, the Test Suite Runner shall 期待文字列、実際のMessage、例外型、発生段階と取得可能なReason・Locationを記録し、前方一致が不成立の場合は、診断の修正を後続仕様で行う場合もfailedとして扱う。

### 要件10: 結果の6分類と異常の扱い

**目的:** 実装者として、不一致・未対応・検証処理の異常・前提不成立を取り違えずに次の作業を判断したい。

#### 受入基準

1. When 処理したcommandの結果を確定する場合, the Test Suite Runner shall passed・failed・runtime_unsupported・runner_error・out_of_scope・blockedのうち一つを割り当てる。
2. When 通常moduleやregisterのセットアップが正常に完了したかassertionの期待が成立した場合, the Test Suite Runner shall そのcommandをpassedとして記録する。
3. If 公開APIでランタイムの未実装を観測した場合, then the Test Suite Runner shall runtime_unsupportedとし、公開APIが報告した未実装機能・中断段階・未確認範囲を記録して、入力全体の有効性を証明したと扱わない。
4. If 固定スイートの前提にないcommand種別・値の形式・構造がJSONに現れた場合, then the Test Suite Runner shall runner_errorとして位置と内容を記録し、ランタイムの未実装や期待失敗の成立へ置き換えない。
5. If ランタイムの実装上限、捕捉可能なOOM、ランナーが提供したホスト関数の例外、API利用の誤り、またはランタイムの公開契約にない例外により期待値判定を完了できない場合, then the Test Suite Runner shall runner_errorとして実際の例外型・原因と発生段階を記録し、期待失敗の成立、failed、runtime_unsupportedに置き換えない。
6. When runner_errorを記録する場合, the Test Suite Runner shall 素材変換・照合・JSON処理・公開API呼び出しなどの発生操作と原因を区別できる診断を残す。
7. If 処理中断や結果保存の失敗によって全対象の記録・出力を完了できない場合, then the Test Suite Runner shall 操作を正常完了と扱わず、保存できた範囲で未処理・未確定・出力失敗を明示する。

### 要件11: ケース識別・結果保存・集計

**目的:** 実装者として、すべての公式ケースの結果と原因を元の入力へ戻って調べ、件数の増減を正しく比較したい。

#### 受入基準

1. When commandを記録する場合, the Test Suite Runner shall 各commandを1件の公式ケースとし、元入力の相対pathとJSON内のcommand順序をケース識別に使用し、元入力の行番号とcommandの種類も保存する。
2. When 同じ行に複数のcommandがある場合, the Test Suite Runner shall それぞれを異なるケースとして保持し、行番号だけで上書き・統合しない。
3. When 実行結果を保存する場合, the Test Suite Runner shall 各ケースの分類、期待結果、実際の結果、処理段階、診断、runtime_unsupportedの未実装機能、blockedの原因を、該当する範囲で記録する。
4. When 実行結果の出典を保存する場合, the Test Suite Runner shall 検証プロファイル、入力・生成物の一覧とhash、変換器の出典を結果へ対応付け、比較成立の条件を確認できるようにする。
5. When 結果を集計する場合, the Test Suite Runner shall セットアップ・単独action・assertionごとに6分類の件数を示し、列挙できた全commandを重複なく一度だけ数える。
6. When 入力単位の変換・照合・JSON異常を集計する場合, the Test Suite Runner shall 入力単位のrunner_errorをcommand単位の件数とは分け、同じ入力異常から架空のcommandやblockedを追加しない。
7. When 操作結果を報告する場合, the Test Suite Runner shall 対象入力数、処理済み・未処理・件数未確定の入力、列挙済みcommand数、分類別集計と詳細結果の保存先を確認可能にする。
8. When 変換結果・実行結果・比較結果を保存する場合, the Test Suite Runner shall 集計とケースごとの詳細結果を、各結果に該当する範囲でJSONファイルとして保存する。

### 要件12: baselineの保存と比較

**目的:** 実装者として、素材の再現性とランタイムの回帰を別々に確認し、以前の合格を失ったケースを特定したい。

#### 受入基準

1. When 利用者がbaseline保存を要求した場合, the Test Suite Runner shall 指定された保存済みのmanifestまたは実行結果JSONを、同じ形式のまま変換baselineまたは実行結果baselineとして利用者の環境へ保存する。同じ保存先に既存baselineがあれば上書きし、旧baselineの別途保存、リポジトリでの管理、共有サービスの利用を必須にしない。
2. If 指定された結果JSONを読み取れないか、未処理・件数未確定・出力失敗が残っている場合, then the Test Suite Runner shall baselineを保存せずに理由を報告する。failed・runtime_unsupported・runner_error・blockedを含んでいても、全対象の処理と記録が完了していれば保存する。
3. When baselineと比較する場合, the Test Suite Runner shall 比較元のbaselineを自動置換せず、比較結果を別に保存する。
4. When 変換baselineと再生成結果を比較する場合, the Test Suite Runner shall 全対象の入力・生成物の一覧とhash、変換状態、specとWABTの採用commit、profile、引数などの生成条件を比較する。変換器実行ファイルのhashだけが異なる場合は出典差異として記録し、比較対象の不一致として扱わない。
5. If 変換比較で差異または未比較の対象がある場合, then the Test Suite Runner shall 対象入力・生成物・条件と差異または未比較の理由を報告し、同条件での再生成一致と扱わない。
6. When 実行結果baselineと比較する場合, the Test Suite Runner shall 検証プロファイルと入力・生成物の一覧・hashが一致することを比較成立の条件とする。
7. If 実行結果比較で変換器実行ファイルのhashだけが異なり、比較成立の条件は一致している場合, then the Test Suite Runner shall 比較を継続し、変換器の違いを出典差異として記録する。
8. When 実行結果比較が成立する場合, the Test Suite Runner shall ケース識別ごとに結果を対応付け、以前passedだったケースの別分類への変化と結果からの欠落を回帰として報告する。
9. If 比較成立の条件が一致しないかbaselineまたは比較結果を読み取り・保存できない場合, then the Test Suite Runner shall 比較が成立・完了しなかった理由を報告し、回帰なしと扱わない。
10. When 実行結果の差分を報告する場合, the Test Suite Runner shall 件数差だけでなく、変化した元入力・commandと変更前後の分類・期待・実際の結果・診断、回帰の有無を確認可能にする。

### 要件13: 用途別の終了コード

**目的:** 実装者として、素材生成、通常の開発、再現性確認、回帰確認、最終判定、baseline保存をコマンドの終了コードで使い分けたい。

#### 受入基準

1. When 素材生成を終了する場合, the Test Suite Runner shall 全対象の変換・照合・記録・出力が完了してrunner_errorと未処理が0件の場合だけ終了コード0を返し、それ以外は非0を返す。
2. When 実行を終了する場合, the Test Suite Runner shall 全対象の記録・出力が完了し、failedと入力単位・command単位のrunner_errorが0件の場合だけ終了コード0を返し、それ以外は非0を返す。runtime_unsupported・out_of_scopeと、runtime_unsupportedを原因とするblockedの残存だけでは非0にしない。
3. When 変換baseline比較を終了する場合, the Test Suite Runner shall 全対象の比較・記録・出力が完了し、要件12.4の比較対象が一致し、runner_errorが0件の場合だけ終了コード0を返し、それ以外は非0を返す。
4. When 実行結果の回帰比較を終了する場合, the Test Suite Runner shall 比較が成立し、全対象の比較・記録・出力が完了してfailed・入力単位/command単位のrunner_error・回帰が0件の場合だけ終了コード0を返し、それ以外は非0を返す。
5. When 最終判定を終了する場合, the Test Suite Runner shall 入力した実行結果JSONで全固定対象の処理・記録・出力が完了し、未処理・欠落・件数未確定・入力単位/command単位のrunner_errorがなく、out_of_scopeを除く全commandがpassedの場合だけ終了コード0を返す。
6. When 最終判定が要件13.5の条件を満たさない場合, the Test Suite Runner shall 非0を返し、残る不成立・未対応・前提不成立・入力異常・未完了の内容を報告する。
7. When baseline保存コマンドを終了する場合, the Test Suite Runner shall 指定した結果JSONからbaselineの保存が完了した場合だけ終了コード0を返し、要件12.2により保存しなかった場合を含むそれ以外は非0を返す。保存の成功をスイートの合格や回帰なしの判定として扱わない。

### 要件14: 初回公式受入と後続機能への継続利用

**目的:** 実装者として、先行基盤の成立と実装修正が必要な問題を公式ケースで把握し、残る対応を同じ判定基準の回帰検証で進めたい。

#### 受入基準

1. When 初回受入を行う場合, the Test Suite Runner shall 固定スイートの全入力を実コマンドで変換・照合し、同条件の再生成と配置rootだけを変えた再生成で素材の一致を確認可能にする。
2. When 初回受入の固定スイート全体を実行する場合, the Test Suite Runner shall 先行基盤で必要な前提が揃うケースを実行・判定し、ランナー自体と素材に起因する入力単位・command単位のrunner_errorを0件にする。初期必須の公式実行経路を妨げる問題は、ランタイム側の問題も本仕様で修正し、独自テストだけを代わりの受入証拠にしない。判定で得たfailedと、初期必須の実行経路を妨げず原因をランタイム側と確認できたrunner_errorは、ケース・期待・実際・原因を記録してtest-suite-conformanceへ引き継げる。これらの分類と非0終了は維持し、必要な前提が成立しない後続commandは原因付きのblockedとして残す。
3. When spectest・registerの初期対応を受け入れる場合, the Test Suite Runner shall ホスト関数、共有global、memory/tableのimport/export、register後の別moduleによる利用を実際の固定公式ケースで確認可能にし、ランナーや公開APIの不足を理由に初期範囲から先送りしない。
4. When 初回実行を完了した場合, the Test Suite Runner shall 全commandの結果記録、変換baselineと実行結果baselineの初回保存、再実行と回帰比較を実コマンドで確認可能にする。
5. If 初回受入時に後続の命令・segmentが必要なケースが成立しない場合, then the Test Suite Runner shall ランタイムが報告した未実装機能と依存元を記録して残し、初回受入でCore 2.0全件合格を要求したり宣言したりしない。
6. When 後続仕様が機能を追加した場合, the Test Suite Runner shall 同じ固定スイート全体の実行結果を現baselineと比較し、必要な前提が揃った対象ケースのpassedと回帰の有無を確認可能にする。問題があれば修正・再実行・比較を繰り返し、問題が解消した結果JSONを保存コマンドで現baselineへ上書きする運用を提供する。
7. When 全仕様を統合した最終確認を行う場合, the Test Suite Runner shall 全8仕様が揃った状態で固定スイート全体を1回実行して得た結果JSONだけを最終判定の入力とし、別の実行で得た部分的な合格を合算して最終判定の代わりにしない。
8. When 初回受入でv128と参照の値処理を確認する場合, the Test Suite Runner shall 引数構築と要件8.4・8.5・8.7～8.10の比較をランナーのテストで確認可能にし、対応する命令が未実装のため公式ケースで動作しないことを理由に、この値処理を後続仕様へ先送りしない。
9. When 初回受入で診断判定の動作を確認する場合, the Test Suite Runner shall 前方一致・不一致・補助説明付きメッセージを扱うテストで判定と記録を確認し、診断不一致が残る実行・回帰比較では要件13どおり非0を返す。ランナーの受入を理由に既知不一致をpassedや未対応へ変更しない。
