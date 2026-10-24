# 実装計画

各サブタスクは1〜3時間の作業単位とする。計測の待ち時間は別とし、手順と結果の保存を各技術作業の成果に含める。追加・変更したテストは、Releaseビルドの警告・エラー0を確認後、コマンドで必ず実行する。

基本は記載順に進める。`(P)`は依存タスク完了後に並列化できるものだけに付ける。タスク11・12は改善計画で2件目・3件目を選定した場合に実施し、選定しない場合は理由を改善計画へ残す。Validate単体、grow、バッファ再利用の検証も条件成立時だけ行い、初期15ケースや最適化方式を先に拡張しない。条件付きタスクの依存は、選定時には完了、非選定時には非該当理由の確認で成立する。

- [x] 1. 測定と契約テストの実行基盤を用意する
- [x] 1.1 測定用・テスト用プロジェクトを既存solutionへ統合する
  - 既存ツールの構成に合わせて.NET10の実行可能プロジェクトとTUnitのテストプロジェクトを追加し、ランタイムの公開APIだけを測定側から参照する。
  - BenchmarkDotNet0.15.8とTUnit1.66.16を固定し、小さい保存データfixtureを使えるテスト基盤を用意する。通常のテストへ素材コンパイラや性能測定を持ち込まない。
  - Windows x64で固定版測定器の最小の別process実行とexporter・割り当て観測の起動を確認し、試走として扱う。既存solutionを含むReleaseビルドが警告・エラー0で完了する。
  - _Boundary: 測定project・テストprojectの統合_
  - _Requirements: 4.1, 5.2, 8.1_

- [x] 1.2 固定素材を生成・検査するツールを用意する
  - 既存DataCount修正を含む固定WABTから、素材生成とバイナリ検査に使う2つの実行ファイルをReleaseで用意する。公式profileは既存ファイルを読み取る。
  - Clang/LLD21.1.8の実行ファイルを指定できるようにし、版・hash・ビルド条件を確認する。PATH上に存在することを前提にしない。
  - 最小WATとfreestanding Cの変換・固定Core2 profileでの検査を実行し、生成ツールの指定方法と再実行コマンドを測定ツールの準備手順へ残す。
  - 最終公式受入で使う固定manifest・直前baselineの存在とhash、先行受入記録をこの段階で確認し、必要な固定素材が利用可能な状態にする。
  - _Boundary: 素材生成script_
  - _Requirements: 1.3, 8.2, 8.4_

- [x] 1.3 素材・ケース・段階別観測の共通データを定義する
  - ケース識別子と版、素材・入力hash、測定区間、操作単位、仕事量、開始状態、固定期待値、比較グループを設計どおり表現する。
  - module・instance・Invokeの観測を区別し、数値の型とbit列、配列の長さとdigestを失わずに保持する。
  - 小さいfixtureの読込で定義を復元でき、入力・期待値をケースと測定メソッドの対応表へ重複保持しないことを確認する。
  - _Boundary: BenchmarkCase・CaseObservation・AssetManifest_
  - _Requirements: 1.2, 1.4, 2.6, 2.7, 3.1_

- [x] 1.4 実行・保存・比較で共有するデータ契約を定義する
  - run用途、予定ケース、ケース別成否、nullable指標と取得不能理由、process別検査、環境とsource同定を保存できる形にする。
  - 比較要求と結果、全ケースの両指標の許容幅、対象の削減目標、同等範囲、独立run数、ノイズ幅・反復間判断幅を表現し、絶対差と比率を区別する。
  - 小さい正常・失敗fixtureを往復保存して状態と数値が保たれることを確認し、後続の測定・保存・比較が使う契約を揃える。
  - _Boundary: RunManifest・ComparisonPolicy_
  - _Requirements: 3.2, 3.3, 4.1, 4.3, 5.1, 5.2, 6.3_

- [x] 2. 代表処理の素材と固定期待値を用意する
- [x] 2.1 短い呼び出しとループ・関数呼び出しの素材を作る
  - 結果0個・1個・複数個の呼び出しと、整数分岐・直接call、浮動小数点・host callback、間接callの素材を作る。
  - ループ回数4,096、入力、関数型、table内容、結果の型・順序・bit列を固定し、callbackに累積状態を持たせない。
  - 独立した参照計算または手計算で期待値を定め、2素材を変換・検査して公開API経由の結果が一致する。
  - _Boundary: 固定素材とケース_
  - _Depends: 1.2, 1.3_
  - _Requirements: 1.1, 1.2, 1.3, 2.6, 2.7, 3.1_

- [x] 2.2 memory転送とscalar・SIMD走査の素材を作る
  - 固定の入力領域と出力領域で64KiBのfillとcopyを各1回行い、前後で同じ状態となる素材を作る。
  - 同じ16,384個のu32入力をscalarとSIMDで走査し、仕事量と期待値2,088,960を対応付ける。
  - 2素材を変換・検査し、反復後の出力全体と走査結果が固定値に一致する。期待値はWasmの観測結果から生成しない。
  - _Boundary: 固定素材とケース_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.7, 3.1_

- [x] 2.3 C由来の整数行列kernelを固定する
  - 32×32のu32行列積を自作freestanding Cで生成し、固定seed・32bit剰余演算・出力全要素の上書きを使う。
  - 設計のコンパイル条件と必要exportを指定し、未解決symbolや不要なimportを許容せず、固定profileでバイナリを検査する。
  - 単純な独立参照計算で固定した出力digestに一致し、同じ処理を繰り返しても前回出力が入力へ混入しない。
  - _Boundary: 固定素材とケース_
  - _Requirements: 1.1, 1.2, 1.3, 2.7, 3.1_

- [x] 2.4 準備段階を代表する大きいmoduleを生成する
  - 256定義関数と64KiB dataを持つ素材を生成し、関数形状、data・element内容、import、memory・table・global、start結果を固定する。
  - startは自instanceだけを更新し、提供するhost関数は累積副作用を持たないようにする。
  - 変換・固定profile検査後、公開APIによるインスタンス化でリソース初期状態とstart結果を確認できる。
  - _Boundary: 素材生成script_
  - _Requirements: 1.1, 1.2, 1.3, 2.4, 2.7, 3.1_

- [x] 2.5 6素材・15ケースを新規出力とmanifestへ統合する
  - 生成処理を接続し、source・tool・profile・引数・binaryのhash、出典・固定版・利用条件・必要表示を生成記録へ保存する。
  - 6バイナリと15ケースの分類、入力、期待値、操作単位、開始状態を対応付け、採用binary hashを固定する。
  - 再生成では既存の素材集合を上書きせず、変更時はケース版と旧結果を区別する。生成から固定profile検査までを準備手順のコマンドで再現できる。
  - _Boundary: 素材生成script・AssetManifest・固定素材とケースの統合_
  - _Depends: 2.1, 2.2, 2.3, 2.4_
  - _Requirements: 1.1, 1.2, 1.3, 1.5, 8.4_

- [x] 3. ケース読込と正しさの照合を成立させる
- [x] 3.1 固定素材と入力をケースへ対応付ける
  - 素材manifestから相対pathでバイト列を読み、hashを照合する。ケース定義を唯一の入力・期待値として使い、測定メソッドとの対応だけを別に保持する。
  - 明示型の引数、固定seedの配列、操作数・guest仕事量・状態方針を復元し、scalarとSIMDの共通入力を確認する。
  - 正常fixtureは読込でき、hash不一致・未知ケース・入力の食い違いは測定前に理由付きで拒否される。
  - _Boundary: AssetManifest・CaseCatalog・InputFactory_
  - _Depends: 2.5_
  - _Requirements: 1.2, 1.4, 1.5, 2.6, 2.7_

- [x] 3.2 公開操作の結果と反復後の状態を照合する
  - 各段階の観測から固定期待値を照合し、Decode後の追加操作やInstantiate後のstart確認も計時外で行えるようにする。
  - Invokeの結果個数・順序・bit列、copy/fill・行列出力全体、必要なmemory・table・global状態を確認する。ケース間で可変import実体を共有しない。
  - 全15ケースの事前照合と反復後照合が成立し、意図的な不一致はケース・段階・期待・観測を持つ失敗として検出される。
  - _Boundary: CaseVerifier_
  - _Requirements: 1.4, 2.4, 2.7, 3.1, 3.2_

- [x] 4. 測定設定と結果保存を実装する
- [x] 4.1 (P) 測定器の実行条件とexporterを固定する
  - .NET10・x64・Release・Throughput・1launch、割り当て観測、Full JSON・measurement CSVを設定し、電源planの自動変更とMemoryRandomizationを無効にする。
  - pilot・warmup・反復調整は測定器へ委ね、実効Job設定、実際の反復数、outlier方針、ActualとResultを記録可能にする。
  - 単一スレッド同期処理の試走で設定とraw出力を確認し、バッチ平均と個々のInvoke遅延を区別する。保存処理の変更とは独立して進める。
  - _Boundary: BenchmarkConfig_
  - _Depends: 1.1, 1.4, 3.2_
  - _Requirements: 2.1, 4.2, 5.3_

- [x] 4.2 (P) 新規runの保存と未完了読込を実装する
  - 予定ケースと実行中状態を新規出力へ保存し、当該run内のraw・検査・条件を対応付けて最後に完了を確定する。
  - 既存出力を拒否し、途中終了や保存失敗のrunは読込時にも未完了のままとする。性能基準値と公式baselineを分ける。
  - 小さいfixtureで上書き拒否、rawとケースの対応、未完了の保持を確認できる。測定設定の変更とは別の境界だけを扱う。
  - _Boundary: RunStore_
  - _Depends: 1.4, 3.2_
  - _Requirements: 3.3, 5.1, 5.4_

- [x] 4.3 実行環境と使用sourceを採取する
  - CPU・OS・architecture、SDK/runtime・測定器版、GC/JIT、電源と接続状態、コマンド、process構成を実行条件へ保存する。
  - HEAD、index/worktree差分、測定で使う未追跡source/project、submodule、素材・ケース・Job設定を記録し、ランタイムとharnessのsource集合を分けて同定する。
  - 使用source/project・assemblyの実行前後hashを照合し、途中変更を比較不成立にできる。無関係な未追跡ファイルは収集しない。
  - _Boundary: BenchmarkRun_
  - _Depends: 4.1, 4.2_
  - _Requirements: 5.1, 5.2, 5.3, 5.5, 5.6_

- [x] 5. 公開APIの測定区間とprocess別検査を接続する
- [x] 5.1 準備段階の5ケースを測定する
  - 小・大moduleのDecodeとDecode+Validateでは、事前読込バイト列から毎回新しいmoduleを生成する。
  - Instantiateでは検証済みmoduleから新しいinstanceを生成し、import接続、リソース生成、segment初期化、startまでを計時内に含める。
  - 最後の戻り値を保持し、後検査で追加操作や初期状態を照合する。5ケースの試走でI/O・生成・照合が計時外にあり、各1操作の区間を識別できる。
  - _Boundary: PreparationBenchmarks_
  - _Depends: 3.2, 4.1_
  - _Requirements: 2.1, 2.2, 2.4, 2.7, 3.1_

- [x] 5.2 定常Invokeの10ケースを測定する
  - module・instance・export取得・引数準備と事前照合を計時外へ置き、公開Invokeから結果取得・消費までを1操作として測定する。
  - 短いInvokeに反復前セットアップを追加せず、結果0個も同じ消費方法で扱う。guestループ回数を操作数に代用しない。
  - copy/fillの固定点、行列の出力上書き、読み取り専用入力を全測定段階で維持し、hot pathへ照合・JSON・hash・ログ・boxingを入れない。
  - 10ケースの試走で前後検査が通り、準備と結果消費の区間を確認できる。
  - _Boundary: InvokeBenchmarks_
  - _Depends: 3.2, 4.1_
  - _Requirements: 2.1, 2.5, 2.6, 2.7, 3.1, 5.3_

- [x] 5.3 親子processの実行設定と前後検査を統合する
  - 親が予定ケースとrun設定を保存し、専用環境変数で子processへ設定pathを渡す。子は計時外で設定を読む。
  - run・case・Job・processの対応を保って、前検査、実行開始、後検査、失敗を保存し、子が実際に読み込んだランタイムと測定assemblyのpath・hashを残す。
  - 正常終了、後検査失敗、中断の別process試走で、測定結果と対応する検査記録を突合できる。追加の診断processも取り違えない。
  - _Boundary: BenchmarkRun・測定クラス・RunStoreの統合_
  - _Depends: 4.2, 4.3, 5.1, 5.2_
  - _Requirements: 3.1, 3.2, 3.3, 5.1, 5.2, 5.6_

- [x] 6. 指標の採取と成功判定を実装する
- [x] 6.1 時間・割り当て・GCを同じケースへ保存する
  - 時間の単位・測定数・平均・中央値・標準偏差・99.9%信頼区間を、launch・iteration・mode/stage・操作数・経過時間のrawと対応付ける。
  - managed割り当て総量・操作数・丸め前bytes/op・表示値と、Gen0/1/2総回数・1,000操作当たりの値を保存する。
  - 時間と割り当てが別反復の観測であること、process全体のmanaged割り当てであること、表示0の丸め条件を記録する。
  - 固定report fixtureから両主指標とGCを復元でき、保持量・nativeメモリ・guestメモリ使用量やInvoke遅延分布への誤表示がない。
  - _Boundary: BenchmarkRun_
  - _Depends: 4.2, 5.3_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1_

- [x] 6.2 測定失敗・取得不能・未完了を成功値から除外する
  - 予定report、build・validation、全process終了値とErrors、測定行、同processの前後検査、必須指標、保存完了のすべてを突合する。
  - 結果不一致・trap・例外、指標欠落、予定ケース欠落、中断を設計の状態と理由へ分類し、取得不能値を0で埋めない。
  - process終了の詳細が分からない場合は最後に確認した段階までを記録し、推測で補完しない。
  - 小さいreportと検査fixtureで、reportが存在しても非0終了・後検査失敗・GC欠落等が成功へ採用されない。
  - _Boundary: BenchmarkRun_
  - _Requirements: 3.2, 3.3, 5.1_

- [x] 7. 保存済み結果の比較を実装する
- [x] 7.1 内容と設定によって比較条件を照合する
  - 素材・入力・期待値・仕事量・区間・状態復元・harness・依存版・ビルド・Job・環境を照合し、ランタイム変更以外の差を列挙する。
  - run固有ID・時刻・保存path・process ID等を比較条件から除外し、実行ファイルや素材のpathは役割と相対pathへ対応付ける。実効GC/JITや意味のある引数は比較する。
  - assembly hashは使用物の同定に使い、build先やランタイム変更に伴う差だけでは比較不能にしない。
  - fixtureで条件差を検出し、配置先だけが違う同条件runは比較可能になる。比較中に再測定や基準値更新を行わない。
  - _Boundary: RunComparison_
  - _Depends: 1.4, 4.3, 6.2_
  - _Requirements: 1.5, 5.4, 5.5_

- [x] 7.2 事前policyで両指標と全ケースを判定する
  - 独立run群の中央値、個々の値・幅・信頼区間、絶対差・比率を示し、0基準の比率はnullとして絶対差で判定する。
  - 条件・正しさ・測定数・ばらつきの成立を先に確認し、悪化、改善、同等、判定不能の順序と理由を設計どおり評価する。
  - 時間が同等で割り当てのみ目標達成した場合も改善とし、非対象ケースの改善と選定変更の達成件数を区別する。
  - 許容内だが目標未達等の判定不能と、比較不足による完了阻害を区別する。全ケースの両指標、累積悪化、ノイズ幅、policy不足をfixtureで検証できる。
  - _Boundary: RunComparison・ComparisonPolicy_
  - _Requirements: 4.4, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8_

- [x] 8. 利用者の操作と標準検証へ統合する
- [x] 8.1 素材確認・測定・比較のCLIを接続する
  - 設計のcheck・run・compareとcase選択を接続し、新規出力先、用途、比較元、事前policyを指定して操作できるようにする。
  - 成功、保存できた測定失敗・受入不成立、引数・I/O・未完了の終了値を0・1・2に分ける。
  - 素材生成からcheck、別processの正常・後検査失敗Smoke、保存結果のcompareまでをWindows x64で実行し、失敗を性能値へ採用しないことを確認する。
  - 実行に使った準備・ケース選択・結果保存・比較コマンドをツールの操作手順へ反映する。
  - _Boundary: 測定CLI・CaseVerifier・BenchmarkRun・RunComparisonの統合_
  - _Depends: 2.5, 5.3, 6.2, 7.2_
  - _Requirements: 2.1, 3.2, 3.3, 5.1, 5.3, 5.4, 8.4_

- [x] 8.2 新しい契約テストを標準コマンドと通常CIへ組み込む
  - 既存3プロジェクトに限定された実行設定へ測定ツールの契約テストを追加し、標準手順と技術方針を同じ実行対象へ揃える。
  - 通常CIでは小さい保存データfixtureだけを使い、BDN起動、性能の合否、Clang/WABT生成を要求しない。
  - Releaseビルドの警告・エラー0の後、追加したテストをコマンドで全件実行し、既存テストの実行経路も保持されていることを確認する。
  - _Boundary: テストproject・通常CIの統合_
  - _Depends: 6.2, 7.2, 8.1_
  - _Requirements: 8.1, 8.4_

- [x] 9. 比較環境と基準値から改善計画を確定する
- [x] 9.1 基準revisionを復元して同条件で測定できるようにする
  - 保存したHEAD・差分・使用する未追跡source/projectから独立した比較用ディレクトリを復元し、source内容hashを照合する。
  - 同じ完全版SDK/runtime・harness・素材・入力・ビルド条件でビルドとSmokeを再現する。復元のために利用者の作業ツリーやGitの状態を変更しない。
  - 計時・検査・集計コードと設定・依存版を固定し、復元・ビルド・測定コマンドを操作手順へ残す。元の環境と異なるpathでも同条件として照合できる。
  - _Boundary: 改善・受入手順_
  - _Depends: 8.1, 8.2_
  - _Requirements: 5.2, 5.4, 5.5, 8.4_

- [x] 9.2 全15ケースの基準値を独立runで取得する
  - 固定した同版・入力・条件で3回以上独立測定し、時間のばらつきと丸め前割り当て量の安定性を確認する。
  - Smokeを基準値に混ぜず、各runの成否・環境・source・raw・前後検査と採用ID/hashを残す。
  - 全15ケースの比較前提が成立し、両指標の基準値と観測した幅を再集計できる。不成立なら原因を解消して新規出力へ取り直す。
  - _Boundary: 改善・受入手順_
  - _Requirements: 4.1, 5.1, 5.2, 5.4, 6.1_

- [x] 9.3 原因調査に使う診断ツールと専用出力を用意する
  - 基準値で調べる必要のある時間・割り当ての寄与に合わせて.NET診断ツールを選び、版と実行ファイル・引数を固定する。
  - 通常測定と別のDiagnostic系列および診断用コピーを用意し、必要なら保持観測用の採取環境も準備する。
  - 最小の診断採取を実行して結果を読めることを確認し、準備と再実行コマンドを操作手順へ残す。
  - _Boundary: 改善・受入手順_
  - _Requirements: 5.2, 5.6, 8.4_

- [x] 9.4 診断で時間と割り当ての原因を切り分ける
  - 準備したツールでprofileを取得し、通常測定とは別のDiagnostic系列へ保存する。
  - 時間と割り当て双方の寄与を調べ、観測した処理と代表ケースを対応付ける。
  - Validate単体やgrowが必要な場合だけ凍結harnessの診断用コピーへ追加する。未検証moduleまたは固定初期サイズinstanceをiteration外でバッチ作成し、各1回だけ操作する別Jobとする。
  - 追加時はInvocationCount・UnrollFactorを1にし、バッチ数と操作数を合わせる。検証済みmoduleの再Validate、平均値の差、上限到達後のgrow失敗を成功測定へ混ぜない。
  - 再実行可能なprofileと原因の対応が得られ、受入用15ケースとharnessが変わっていないことを確認する。
  - _Boundary: 改善・受入手順_
  - _Requirements: 2.3, 3.4, 5.6, 6.2_

- [x] 9.5 原因に対応する局所変更と変更前policyを確定する
  - 基準値とprofileから1〜3箇所だけを選び、対象ケース、所有境界、変更ファイル、必要な既存テスト、狙う指標を改善計画へ保存する。
  - 各変更は単一責務の局所変更とし、責務整理と最適化をそれぞれ1〜3時間で検証できる範囲へ限定する。候補一覧全体を変更対象にしない。
  - 最初の変更前に、元の基準revisionに対する全15ケースの両指標の許容幅・最終目標を固定する。各変更の比較元と削減目標・同等範囲・ノイズ幅・反復間判断幅を測定値から決める。
  - 再利用を選ぶ場合は容量上限と根拠、保持観測条件、参照寿命の確認方法を先に定める。計画外の所有境界や先行SIMD判断の変更が必要なら設計レビューへ戻る。
  - policyを比較ツールで読み込め、採用runと対象1〜3件、変更前の数値基準、直接テストが対応している。
  - _Boundary: 改善・受入手順_
  - _Requirements: 4.5, 6.2, 6.3, 7.1, 7.2, 7.3, 7.4, 7.5_

- [ ] 10. 第1の選定箇所を改善する
- [x] 10.1 第1の箇所の責務と所有を先に整理する
  - 改善計画で選んだ1つの境界だけを対象に、値・状態・資源の所有を既存責務へ揃える。既に満たす場合は理由を残し、不要な抽出を行わない。
  - 既存テストを優先し、変更で生じる所有・寿命・失敗の危険にだけ直接テストを補う。
  - 挙動を維持した状態でReleaseビルドと関連テストが通り、最適化へ進める。測定harnessは変更しない。
  - _Boundary: 選定ランタイム箇所（第1の改善）_
  - _Depends: 9.5_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [x] 10.2 第1の箇所へ測定に裏付けられた最適化を行う
  - 整理済みの所有境界内で、事前に狙った時間または割り当ての原因を減らす局所変更を行う。公開API・段階・診断・境界検査を維持する。
  - 対象に応じて結果とcallback引数の独立性、locals、関数とimportの同一性、所属instance、数値bit列、範囲と部分更新禁止、segment・start失敗前の副作用を保つ。
  - 再利用時は論理的コンテキストと物理bufferの寿命を分け、再入中の領域を貸し出さず、全終了経路で深さ・上限・Stage・使用数と不要参照を復元・除去する。
  - Releaseビルドと変更対象の直接テストが通る。生成器または埋込対象ソースに影響する場合は生成器テスト、SIMD変更時は新processの`DOTNET_EnableHWIntrinsic=0`でも関連テストを実行する。
  - _Boundary: 選定ランタイム箇所（第1の改善）_
  - _Requirements: 6.2, 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [ ] 10.3 第1の変更の性能と必要な参照寿命確認を統合する
  - 変更前後を同期間に交互に各3run以上測り、対象ケースと全15ケースの許容幅を事前policyで評価する。結果と両指標の差・ばらつきを保存する。
  - 再利用を追加した場合は別のRetention系列で、同じprocess数・入力・反復数・小→大→小の列による保持量とrootを比較し、必要最小限のWeakReferenceテストを実行する。
  - 時間が同等で割り当てだけ目標達成した場合も評価し、許容幅超過や比較不足は修正・再測定で解消する。成立した版だけを次の変更の比較元とする。
  - harness修正が必要なら候補受入を中止し、旧runtimeと修正版harnessの基準値を取得する。削減目標・許容幅・同等範囲は保持し、基準側に基づくノイズ幅等以外を後付けしない。
  - _Boundary: 選定ランタイム箇所・性能比較・保持観測の統合_
  - _Depends: 7.2, 9.1, 9.5, 10.2_
  - _Requirements: 4.5, 5.5, 5.6, 6.3, 6.4, 6.5, 6.7, 7.3, 7.4, 8.1_

- [x] 11. 選定した場合に第2の箇所を改善する
  - _非該当: 独立レビューで承認したoptimization-plan.mdは1箇所だけを選定したため、第2の改善を行う条件は成立しない。11.1〜11.3は非該当として確認済み。_
- [x] 11.1 第2の箇所の責務と所有を先に整理する
  - 2件目を選定した場合だけ、確定済みの単一境界で第1の変更後の状態を前提に責務を整理する。
  - タスク10.1と同じ公開契約・既存テスト優先の条件を適用し、必要な直接テストを補う。対象が既に原則を満たせば理由を残す。
  - 最適化前のReleaseビルドと関連テストが通り、第1の受入結果と第2の変更前policyが参照できる。
  - _Boundary: 選定ランタイム箇所（第2の改善）_
  - _Depends: 9.5, 10.3_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [x] 11.2 第2の箇所へ局所的な最適化を行う
  - 選定した原因だけを減らし、タスク10.2の所有・値・再入・失敗の制約を該当境界へ適用する。
  - 第1の変更へ無関係な拡張を加えず、凍結harnessと第2の事前policyを保持する。
  - Releaseビルド、関連直接テスト、変更に応じた生成器・SIMD設定別テストが通る。
  - _Boundary: 選定ランタイム箇所（第2の改善）_
  - _Requirements: 6.2, 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [x] 11.3 第2の変更の比較と必要な保持観測を統合する
  - 第1の受入版を比較元に、タスク10.3と同じ交互測定・両指標・全ケースの許容幅確認を行う。
  - 再利用を加えた場合の保持量・参照寿命を別系列で確認し、必要なテストを実行する。harness修正時も同じ基準値再取得規則を適用する。
  - 第2の事前policyによる判定が保存され、未解決の悪化・比較不足を残さず次の比較元を確定できる。
  - _Boundary: 選定ランタイム箇所・性能比較・保持観測の統合_
  - _Depends: 10.3, 11.2_
  - _Requirements: 4.5, 5.5, 5.6, 6.3, 6.4, 6.5, 6.7, 7.3, 7.4, 8.1_

- [x] 12. 選定した場合に第3の箇所を改善する
  - _非該当: 独立レビューで承認したoptimization-plan.mdは1箇所だけを選定したため、第3の改善を行う条件は成立しない。12.1〜12.3は非該当として確認済み。_
- [x] 12.1 第3の箇所の責務と所有を先に整理する
  - 3件目を選定した場合だけ、第2の受入版を前提に確定済みの単一境界で責務を整理する。
  - タスク10.1の条件で直接テストを対応付け、不要な抽出は行わず、最適化前の挙動を維持する。
  - Releaseビルドと関連テストが通り、第3の変更前policyと変更する所有境界が対応している。
  - _Boundary: 選定ランタイム箇所（第3の改善）_
  - _Depends: 9.5, 11.3_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [x] 12.2 第3の箇所へ局所的な最適化を行う
  - 選定した原因だけを減らし、タスク10.2の所有・意味論・失敗の制約を該当境界へ適用する。
  - 前2件の契約と凍結harnessを保ち、事前に定めた指標以外へ費用を移していないか確認する。
  - Releaseビルド、関連直接テスト、変更に応じた生成器・SIMD設定別テストが通る。
  - _Boundary: 選定ランタイム箇所（第3の改善）_
  - _Requirements: 6.2, 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [x] 12.3 第3の変更の比較と必要な保持観測を統合する
  - 第2の受入版と候補を交互に測定し、タスク10.3の比較・保持観測・harness修正時の規則を適用する。
  - 両指標と代表ケースの悪化を個別に確認し、不成立は修正と再測定で解消する。
  - 第3の事前policyによる判定と必要な保持観測が保存され、最終評価へ進める版を確定できる。
  - _Boundary: 選定ランタイム箇所・性能比較・保持観測の統合_
  - _Depends: 11.3, 12.2_
  - _Requirements: 4.5, 5.5, 5.6, 6.3, 6.4, 6.5, 6.7, 7.3, 7.4, 8.1_

- [ ] 13. 最終版の性能と公式適合検証を統合して受け入れる
- [ ] 13.1 元の基準revisionと最終版を全15ケースで比較する
  - 選定した全変更の個別評価後、元の基準revisionと最終revisionを交互に各3run以上測定し、最初の全体policyを適用する。
  - 全15ケースの時間・割り当てを改善・同等・悪化・判定不能と理由付きで示し、途中の比較では見えない累積悪化も確認する。
  - 全ケースの許容内が判断でき、完了を阻害する比較不足等がなく、選定した変更の少なくとも1件で事前の時間短縮または割り当て削減目標が再現する。
  - _Boundary: 改善・受入手順_
  - _Depends: 9.1, 9.5, 10.3, 11.3, 12.3_
  - _Requirements: 6.4, 6.5, 6.6, 6.7, 6.8_

- [ ] 13.2 最終版のビルドと公開契約の回帰を確認する
  - 最終版をReleaseで警告・エラー0にし、変更したテストと関連テストをコマンドで実行する。
  - 正常終了・trap・exhaustion・同じホスト例外実体の伝播後の独立呼び出し、同期再入、結果・引数・locals・import・所属instanceを、選定箇所に対応する既存ケースで確認する。
  - 数値・SIMDのbit列、memory/tableの部分更新禁止、先行segment・startの副作用、失敗前に保持された参照の契約を確認する。
  - 該当する生成器テストとSIMDの新process・`DOTNET_EnableHWIntrinsic=0`によるテストを含め、実行結果と未実施範囲を特定できる。
  - _Boundary: 選定ランタイム箇所・既存直接テストの統合_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1_

- [ ] 13.3 固定公式スイート全体の比較とverifyを行う
  - 固定manifestと直前の受入済みbaselineの存在・hash・受入記録を再確認し、既存公式CLIで全体runを新しい出力先へ保存する。
  - 同じ保存runへ比較とverifyを適用し、failed・runtime_unsupported・runner_error・blocked・以前passedの回帰と欠落を0件にする。
  - 期待値・診断照合・対象集合・feature設定を緩めず、比較前にbaselineを更新しない。既存out_of_scopeの意味は維持する。
  - 修正が必要なら新規出力で全体run・比較・verifyをやり直す。ランタイム修正後はタスク13.1・13.2も再実行し、性能と機能が同じ最終版で成立する。
  - _Boundary: 改善・受入手順・既存公式ランナーの統合_
  - _Depends: 13.1, 13.2_
  - _Requirements: 6.7, 6.8, 8.2, 8.3_

- [ ] 13.4 受入結果を保存して再実行経路を確認する
  - 性能と公式検証の両方が成立後、比較・verifyに使った同じ公式runを既存の保存操作で新規baselineへ保存し、旧baselineと過去の受入記録を保持する。
  - 性能のケース別判定と機能検証を分け、run ID/hash、コマンド、終了値、固定素材/profile、選定変更、再現した改善、未実施範囲を受入記録へ対応付ける。
  - 素材準備・対象選択と全体実行・保存・基準値比較・診断・公式回帰確認の操作手順が、今回使った成果物から再実行できることを確認する。
  - _Boundary: 改善・受入手順・既存公式ランナーの統合_
  - _Requirements: 5.4, 6.8, 8.2, 8.4_

## Implementation Notes

- 10.2: InterpreterContextの保持上限超過分岐だけでWasmFailureLocationを生成し、通常入口の事前生成を除いた。公開Invokeで深さ2の1call/1024callと返値42を確認する回帰を追加し、旧eager/OFFは追加57288bytes>4096bytesでRED（保存終了値2、旧報告1を訂正）、ON/フラグ除去後GREEN。容量8ケースは期待診断を維持してinstance Interfaceへ移行し、ThreadStaticの終了を同期helper内でawait前に完了する。独立レビューはRelease警告・エラー0、全4project5,969件・整形3C#成功、フラグ残存0・公開診断/容量算術/復元/所有維持でAPPROVED。runtime集合は`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`、harness/policy/固定基準版は不変。記録は`artifacts/runtime-performance/task-10.2-20261024T123953Z/`と`review-10.2-20261024T125100Z/`。全15ケース本測定の性能受入と公式全体検証は未実施。

- 10.1: `EnsureCapacity`へ診断元のDefinedFunctionを渡し、位置組立てをStage/容量を所有するInterpreterContextへ移した。本体2ファイルと内部Interfaceを使う18テスト/fixtureだけを変更し、成功時1個の生成・CalculateCapacity本体・診断期待値を維持した。Interface移行のCS1503×2のRED後、Release警告・エラー0、runtime4,798件、独立レビュー全4project5,968件・整形20ファイル成功、開始終了hash不一致0でAPPROVED。runtime集合は`7d101d93eb47efc791679fa99a1a2fb516d757c34feb056732abdd3f41d18e84`、harness/policy/固定基準版は不変。記録は`artifacts/runtime-performance/task-10.1-20261024T122154Z/`と`review-10.1-20261024T123123Z/`。遅延生成・性能本測定・公式全体検証は未実施。

- 9.5: `optimization-plan.md`で通常入口のWasmFailureLocation生成1件を選定し、10.1の内部Interface整理・10.2の失敗時生成へ分けた。第2/第3の改善と再利用は非選定、11/12は条件不成立で非該当。`comparison-policy.json`は全15ケース/30指標/独立3run、direct/indirect各180000bytes/op削減、時間幅は基準run間幅とCI、割当幅は観測安定性から固定した。全体/第1変更は同一policy（SHA-256`e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba`）を参照し、両対象の目標達成も個別確認する。独立レビューはRelease警告・エラー0、全4project5,968件、全policy値再計算一致、CLIの同run重複拒否とproduction数値比較30指標同等/許容内・対象2指標だけ目標未達、source/入力hash不変でAPPROVED。記録は`artifacts/runtime-performance/task-9.5-20261024T120137Z/`と`review-9.5-20261024T121336Z/`。実装・候補比較・改善達成は未実施。

- 9.4: 代表12ケースのDiagnosticで時間・割当stackとコードを対応付け、`diagnosis.md`へ候補・所有境界・既存テスト・限界を保存した。direct/indirectのWasmFailureLocation割当重みは99.660%/99.788%で、通常のguest callごとの生成へ対応。結果再コピー・検証builderも候補とし、選定/policyは未確定。元scalar抽出に後検査stack2本を見つけ、元データ保持で全ケース前後100msを除いた中央4.8秒へ統一し混入0を確認。UTC対応原因は未確認、PollGC/inliningと低sampleの限界も保持し、bytes/opや改善率へ換算しない。独立レビューはRelease警告・エラー0、全4project5,968件、12profile/204保存ファイルhash・独立再読込12/12一致、固定105ファイル・3箇所のsource集合・開始終了258ファイル不一致0でAPPROVED。記録は`artifacts/runtime-performance/task-9.4-20261024T113533Z/`と`review-9.4-20261024T114847Z/`。Validate単体/grow追加は不要、受入15ケース/harness・ツールは不変。

- 9.3: dotnet-trace10.0.750501とTraceEvent3.1.21を固定し、`Diagnostics/`の手順・templateで元repo外の診断コピーとDiagnostic出力を用意した。Programだけ既存caseのInitialize/反復/Cleanupへ置き換え、Wasm処理PIDから時間sampleとGCAllocationTickのstackを採取する。独立レビューはRelease警告・エラー0、全4project5,968件、5秒採取PID75672/4,489操作・前後検査成功、時間sample2,558/割当tick9,695のstack読込・EventsLost0、再読込一致・上書き拒否、固定105ファイル・元runtime/harness一致を確認しAPPROVED。準備記録は`artifacts/runtime-performance/task-9.3-20261024T111451Z/preparation-final/preparation.json`、診断コピーと再実行pathは同`evidence.md`、レビューは`review-9.3-20261024T112842Z/`。初回の未公開tool版導入失敗を保持し、公式feedの実在版へ固定した。sampleはCPU使用率や正確なbytes/opへ置換せず、原因調査・選定・保持観測・最適化は後続へ分ける。

- 9.2: 固定した9.1復元版・6素材・Measurement Jobで全15ケースを独立3run直列測定し、45/45ケースSucceeded。基準値・採用ID/hash・再集計経路は`baseline.md`、詳細は`artifacts/runtime-performance/task-9.2-20261024T104127Z/`。時間の幅/中央値は0.471〜7.887%、丸め前割当は11ケース一致、4ケースの微差も保持した。独立レビューはRelease警告・エラー0、全4project5,968件、保存380ファイルhash・BDN45ケース・raw3,616行・表15行・14,826条件値差0を確認しAPPROVED。記録は`review-9.2-20261024T110649Z/`。固定source150件・assemblyと元HEAD/index/refs不変。集計のreflection引数型不一致は手順scriptだけを修正して失敗ログを保持した。Smoke不採用、改善目標/policy・診断・最適化・最終公式検証・Ubuntuは未実施。

- 9.1: `Restore-RunSource.ps1`で保存HEAD・index/worktree差分・使用未追跡sourceを元repo外の独立cloneへ復元し、ファイル・集合hashと再帰submodule状態を照合する。C#・計時harness・依存版は変更せず、READMEへ復元・同版build・測定手順を追加した。元版と復元版のRelease警告・エラー0、全15check・別process Smoke成功、300比較条件の差0、元HEAD/refs/index/statusと旧保存runの不変を確認。独立レビューも全4project5,968件・復元契約成功、APPROVED。記録は`artifacts/runtime-performance/task-9.1-20261024T102137Z/`と`review-9.1-20261024T103507Z/`。継続用復元先は`task-9.1-20261024T102137Z/restored-path.txt`に保存し、runtime集合`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`・harness集合`bfdd7e94b71ecbf21c50669310920af24dd10616861790f5d668b926609c824d`を固定基点とする。Smokeは基準値に含めず、本測定・診断・最終公式検証・Ubuntuは未実施。

- 8.2: README・通常CI・技術方針を4テストprojectへ揃えた。前回の停止はレビューのread-only指定と検証生成物の書き込みの衝突で、再診断はCONFIG_GAP/RETRY_TASK。再開時にコード非編集と検証生成物の書き込み許可を区別し、必要な権限申請が承認された。Release警告・エラー0、設定31件、契約308件、独立レビュー全4project5,968件成功、失敗・スキップ0、APPROVED。検証前後の対象ファイルhash一致を確認。実装変更は`3ba5bff`、最新記録は`artifacts/runtime-performance/task-8.2-resume-20261024T101038Z/`と`review-8.2-resume-20261024T101515Z/`。Ubuntu・性能測定・公式全体検証は未実施。

- 8.1: ProgramからBenchmarkCliへ入出力責務を分離し、check/run/compareと0/1/2の終了値・新規保存を接続した。`--assets`を配置正規化へ対応付け、Smokeだけ3反復へ変更して有限CIを取得する。Measurement Jobは維持。Release警告・エラー0、契約308件、独立レビュー全4project5,968件成功、APPROVED。新規素材生成/全15check0・正常Smoke0・後検査失敗Smoke1・比較1/非採用/入力hash不変を確認。記録は`artifacts/runtime-performance/task-8.1-20261023T024900Z/`と`review-8.1-20261023T031105Z/`。親buildとBDN子buildのhash差・清掃後の観測path不在を区別する。採用素材は2.5のmanifestを維持。Ubuntuと基準値取得は未実行。

- 7.2: `RunComparison.Compare`は条件読込を共有し、run群/policyと指標集計を境界内のpartialへ分ける。中央値・個値・CI・差/比率、0基準、ノイズ/ばらつき、阻害/非阻害、全15ケースと累積悪化を判定し、IsAcceptedで全体受入を示す。内部要求は保存先参照をRunDirectoriesと明示し、JSON名は維持、報告IDはmanifest内IDとする。Release警告・エラー0、契約292件（Compare65件）、独立レビュー全4project5,952件成功、APPROVED。記録は`artifacts/runtime-performance/task-7.2-20261023T022053Z/`と`review-7.2-20261023T024038Z/`。保存fixtureだけを使用し、Ubuntu未実行。

- 7.1: `RunComparison.CompareConditions`はRunStore.Loadを再利用し、保存内容/設定の差を既存ConditionDifference/ComparisonReasonへ返す。runtimeコードと識別/配置差は除外し、runtime集合中のproject/共通build設定は照合する。ProjectPathは保存cwdから解決しconstructorのroot復元と共有する。Release警告・エラー0、契約227件（条件照合48件）、独立レビュー全4project5,887件成功、APPROVED。記録は`artifacts/runtime-performance/task-7.1-fix1-20261023T020834Z/`と`review-7.1-r2-20261023T021300Z/`。数値/policy判定は7.2、Ubuntu未実行。

- 6.2: `CaptureCase/EvaluateCase`へ観測収集と判定を分離し、report・全process・前後検査・必須指標・RunStoreの保存完了を突合する。観測値/rawは保持し、Failed/Incomplete/Unavailableを成功採用から除外する。Incompleteを含むrunは完了へ補完しない。Release警告・エラー0、契約179件、独立レビュー全4project5,839件成功、APPROVED。既存試走と小report/検査fixtureで主要境界を検証。記録は`artifacts/runtime-performance/task-6.2-20261023T012743Z/`と`review-6.2-20261023T014134Z/`。ExtraRun未知終了はnull、1反復Smokeの非有限CIはUnavailableのまま8.1へ引継ぐ。Ubuntu未実行。

- 6.1: `BenchmarkRun.SaveReports`へ保存を先に分離し、Result統計・Actual含むraw CSV・割当総量/操作数/丸め前/ToEven表示・GC全世代を同caseへ保存する。4.1の実出力89行・15Result反復を小fixtureへ採用し、元出力/統計の一致を確認。Release警告・エラー0、契約158件、独立レビュー全4project5,818件成功、APPROVED。記録は`artifacts/runtime-performance/task-6.1-20261023T010716Z/`と`review-6.1-20261023T012101Z/`。1反復SmokeのCI非有限は理由付きTime=nullとなるため6.2/8.1で考慮する。GC欠落/操作数0や複数Jobのreportも0へ補完しない。成功確定は6.2、Ubuntu未実行。

- 5.3: 親PrepareはIncompleteの予定runとJob別設定JSONを保存し、`WASMSHARP_BENCHMARK_RUN_CONFIG`で子へ渡す。子の`BenchmarkProcess`は計時外で前検査・実行開始・後検査・失敗をPID別snapshotへ追記し、実際ロードしたassemblyを記録する。Release警告・エラー0、契約151件、独立レビュー全4project5,811件成功、APPROVED。正常・後検査失敗（子0でもErrorsあり）・中断83・追加診断PIDを別process Smokeで照合。BDN0.15.8のExtraRunはExecuteResultsに含まれず、終了値はnullを維持してrawログと併記する。指標採取・成功確定は6.1/6.2へ引継ぐ。記録は`artifacts/runtime-performance/task-5.3-20261023T010600Z/`と`review-5.3-20261023T010021Z/`。Ubuntu未実行、Smokeは基準値未採用。

- 5.2: `InvokeBenchmarks`は10ケースの公開Invoke・専用field保持・事前生成Consumerによる参照消費を各1操作とし、InitializeとCleanupへ準備・照合を分離する。前検査と測定のinstance/importを共有せず、Cleanupで追加Invokeしない。Release警告・エラー0、契約144件、独立レビュー全4project5,804件成功、APPROVED。10ケースの別process Smokeは全子終了0・Errors空・raw294行・GC/割当・Cleanup対応成功。記録は`artifacts/runtime-performance/task-5.2-20261023T002153Z/`と`review-5.2-20261023T003239Z/`。ソースコピーの更新時刻保持による増分ビルド見落としに注意。Ubuntu未実行、Smokeは基準値未採用。

- 5.1: `PreparationBenchmarks`は公開操作と最後の戻り値保持だけを計時し、内部Initializeで前検査後に測定用状態を新規準備する。既定素材配置は`AppContext.BaseDirectory/Assets/asset-manifest.json`で、5.3のrun設定へ接続する。Release警告・エラー0、契約131件、独立レビュー全4project5,791件成功、APPROVED。5ケースの別process Smokeは各1操作・子終了0・Errors空・raw/GC/割当・Cleanup完了。記録は`artifacts/runtime-performance/task-5.1-20261023T000305Z/`と`review-5.1-20261023T001528Z/`。probeの継承Cleanupは属性重複を避ける。Ubuntu未実行、Smokeを基準値に混ぜない。

- 4.3: `BenchmarkRun`の採取APIはCollectorの観測と指定Job/runtimeconfigを区別し、使用sourceをruntime/harnessへ分けて前後hashを照合する。復元用Git差分は色・外部変換を抑止してa/b接頭辞を固定する。Release警告・エラー0、契約122件、独立レビュー全4project5,782件成功、APPROVED。採取のみの試走で141source・assembly・保存参照179件・素材6件のhash不一致0。記録は`task-4.3-fix2-20261022T235017Z/`と`review-4.3-r3-20261022T235438Z/`（`artifacts/runtime-performance/`配下）。内部Tiering/PGO/ReadyToRunは理由付きUnavailable、Ubuntu未実行。

- 4.2: `RunStore`は新規runをIncompleteで保存し、条件・raw・process検査とcase結果を対応付けて最後に完了を確定する。`RunSource.IndexDiff/WorktreeDiff/UntrackedFiles`も保存参照として存在/hashを照合し、未追跡コピーはrun直下からリポジトリ相対構造を保つ。`SourceSet.Files`とassemblyの観測pathはこの照合へ含めない。process検査は段階別snapshotを追記し、保存後のDTO一覧を所有する。Release警告・エラー0、契約113件、独立レビュー全4project5,773件成功、APPROVED。記録は`artifacts/runtime-performance/task-4.2-20261022T223812Z/`、`task-4.2-fix1-20261022T230001Z/`、`review-4.2-r2-20261022T230545Z/`。保存失敗テストのUnix分岐は未実行。

- 4.1: `BenchmarkConfig.Create`はMeasurement JobとMemoryDiagnoser・Full JSON・measurement CSVを固定し、pilot・warmup・反復調整はBDNへ委ねる。CSVの調整項目は`Default`、実際の操作数・反復数はrawに残る。試走はJSON/CSV全88行・Actual/Result各15行・子終了0・Errors空・GC/割当・電源plan維持を確認し、基準値には採用していない。Release警告・エラー0、独立レビュー全4project5,738件成功、APPROVED。記録は`artifacts/runtime-performance/task-4.1-20261022T222408Z/`と`artifacts/runtime-performance/review-4.1-20261022T223207Z/`。

- 3.2: `CaseVerifier.Verify`は段階別観測を照合し、Invoke後の状態を修復しない。Instantiateの初期資源とstartは追加Invoke前に確認し、固定入力・全出力digest・数値bit列・tableの関数実体を照合する。固定素材の提供登録は`CreateImports`で毎回新規生成する。フラグOFFのRED、除去後のRelease警告・エラー0、契約テスト76件、独立レビューの全4project5,736件と15ケースの独立probe230件成功、APPROVED。検証記録は`artifacts/runtime-performance/task-3.2-20261023T040000Z/`と`artifacts/runtime-performance/review-3.2-20261022T221824Z/`。

- 1.2: Clang21.1.8のwasm targetでは`--ld-path`が未使用になるため、`-c`でobjectを生成して明示pathの`wasm-ld`を直接起動する。固定WABTのReleaseツールとLLVM21.1.8は`artifacts/runtime-performance/task-1.2-20261022T162000Z/wabt/Release/`と`artifacts/runtime-performance/tools/clang+llvm-21.1.8-x86_64-pc-windows-msvc/bin/`へ配置した。最小WAT/CのCore2検査、版・hash、公式比較元を確認済み。独立レビューAPPROVEDと検証記録は`artifacts/runtime-performance/task-1.2-review-20261022T141156Z-146a2afe/`に保存した。
- 1.1: BDNはリポジトリ内の同名`.csproj`を探索するため、ソース控えはzip等で保持する。試走の親process終了0だけでは成功とせず、子process終了値・実行件数・raw出力を確認する。Release警告・エラー0、全4テストprojectの5,662件成功、独立レビューAPPROVED。試走と検証記録は`artifacts/runtime-performance/task-1.1-20261022-impl/`と`artifacts/runtime-performance/task-1.1-review-20261022-231400/`に保存した。
- 1.3: ケースと素材のJSON契約は`CaseJson.Options`でsnake_case、文字列enum、必須引数とnullable注釈の検査を共有する。入力生成・hash照合・Verifierは後続タスクで実装する。Release警告・エラー0、契約テスト16件成功、独立レビューAPPROVED。検証記録は`artifacts/runtime-performance/task-1.3-20261022T142133Z/`と`artifacts/runtime-performance/task-1.3-review-20261022T234100-7b9e6c/`に保存した。
- 1.4: run・process検査・指標・環境/source・比較policyの保存契約を定義した。null指標には取得不能理由を持たせ、絶対差と比率を別の型で保存する。Release警告・エラー0、契約テスト22件成功、独立レビューAPPROVED。検証記録は`artifacts/runtime-performance/task-1.4-20261022T151500Z/`と`artifacts/runtime-performance/task-1.4-review-20261022T161100Z/`に保存した。
- 2.1: `CaseInput`を`CaseJson.Options`でコンパクトJSONへ直列化したUTF-8列のSHA-256を入力hashとする。2素材・6ケースを固定し、期待値は手計算と閉形式で照合した。Release警告・エラー0、契約テスト25件成功。独立レビューでも新規出力へのCore2検査と6ケース各8回の公開Invoke照合が成功しAPPROVED。検証記録は`artifacts/runtime-performance/task-2.1-20261022T145210Z/`と`artifacts/runtime-performance/task-2.1-review-20261022T150306Z/`に保存した。
- 2.2: memoryの固定点反復に加え、別instanceで両出力を破壊してから1回のInvokeで回復することを確認した。scalar/SIMDは引数・配列・仕事量・期待値を共有するが、入力hashは関数export名を含むため異なる。Release警告・エラー0、契約テスト28件成功。独立レビューでも2素材のCore2検査と3ケース各8回の全byte・digest照合が成功しAPPROVED。記録は`artifacts/runtime-performance/task-2.2-20261022T150925Z/`と`artifacts/runtime-performance/task-2.2-review-20261022T152013Z/`に保存した。
- 2.3: `SeededU32`はxorshift32（左13・右17・左5、更新後に格納）とし、左右seedを`0x12345678`・`0x9e3779b9`へ固定した。Clang/LLD21.1.8の明示生成、import0とCore2検査、独立BigInteger計算、公開Invoke9回の全入力保持・全出力一致・破壊出力回復を確認した。Release警告・エラー0、契約テスト30件成功、独立レビューAPPROVED。記録は`artifacts/runtime-performance/task-2.3-20261022T152628Z/`と`artifacts/runtime-performance/task-2.3-review-20261022T153519Z/`に保存した。
- 2.4: large素材は255加算関数とstartの計256定義、64KiB data、固定tableとglobalを生成する。フラグOFFの失敗、ONと除去後の成功を確認した。Release警告・エラー0、契約テスト32件、公開インスタンス化24回と先行instance変更後の独立性21回が成功、独立レビューAPPROVED。記録は`artifacts/runtime-performance/task-2.4-20261022T154151Z/`と`artifacts/runtime-performance/task-2.4-review-20261022T155635Z/`。測定ツールREADMEの旧「2バイナリ・toolchain-smoke」表記は非阻害の指摘として2.5の全生成手順更新へ引き継ぐ。
- 2.5: 6素材・15ケースの統合生成を接続し、既存13ケースと採用hashを保持した。manifestは出力基準の相対参照、source revisionは基点HEADと内容hashを分ける。引数hashはSystem.Text.Json既定の順序付き文字列配列のコンパクトUTF-8 JSONから求める。PowerShell7.5以上が必要。旧README表記を解消し、Release警告・エラー0、契約テスト34件、全生成・DTO読込・公開API・既存出力とhash不一致の拒否を独立確認してAPPROVED。記録は`artifacts/runtime-performance/task-2.5-20261022T160359Z/`と`artifacts/runtime-performance/review-2.5-20261023T010000Z/`。
- 3.1: 保存DTOを維持して`AssetManifest.Load`から`LoadedAssets`へ読込み、profile・cases・binary・入力のhashを照合する。`CaseCatalog`はメソッド名の対応だけを固定し、`InputFactory`は型とbit列・little-endian配列を復元する。生成ツールは読込に要求しない。フラグOFFの新規20件失敗、ONと除去後54件成功、Release警告・エラー0。6素材15ケースの読込とmatrix全出力digest、比較条件・重複の拒否を独立確認してAPPROVED。記録は`artifacts/runtime-performance/task-3.1-20261022T162754Z/`と`artifacts/runtime-performance/review-3.1-20261023T021500Z/`。メソッド名と測定クラスの接続は後続タスクで行う。
