# タスク13.2機能回帰検証結果

候補ランタイムの機能回帰として、Releaseビルドは警告・エラー0、4つのTUnitプロジェクトは合計5981件成功・失敗0・スキップ0だった。新processの`DOTNET_EnableHWIntrinsic=0`では、SIMDの公開API直接テスト15メソッド・81件が成功し、失敗・スキップ0だった。この結果は性能改善の受入やfeatureGOを示さない。

## 対象と前提

- タスク: [tasks.mdの13.2](tasks.md)。Boundaryは選定ランタイム箇所・既存直接テストの統合。Depends指定はなく、10.3がBlockedでも候補版の機能回帰を独立して確認する。
- 要件: [requirements.md](requirements.md)の7.1、7.2、7.3、7.4、7.5、8.1。
- 設計: [design.md](design.md)の「責務境界」「性能改善と受入」「公開契約の回帰テスト」「ビルドと公式検証」「完了条件」。番号のない見出しへ架空の番号は付けない。
- HEAD: `6c96445425a4e29b62f6f991db4cacddc1535764`。唯一の選定変更は、`InterpreterContext.EnsureCapacity`の通常入口で`WasmFailureLocation`を生成せず、保持上限超過時だけ生成する変更。タスク10.1で責務を整理した後、10.2で適用している。
- 環境: Windows x64、SDK10.0.402、.NET10.0.13。完全版・OSは[environment.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/environment.json)と[dotnet-info.txt](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/dotnet-info.txt)に保存した。
- 実行cwd: `D:/source/repos/WasmSharp2`。コマンドは直列実行し、既存のREADMEと通常CIの4projectをそのまま対象にした。
- 今回はソース変更・振る舞い変更・新規テスト追加なし。REDとfeatureflagは非該当。再利用や保持bufferも追加しておらず、新WeakReferenceテスト・Retention観測は非該当。

記録先は[artifacts/runtime-performance/task-13.2-20261024T153250Z](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/)。実行前に[task-brief.md](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/task-brief.md)で受入基準・完了定義・設計制約・検証方式を確定した。

## 検証対象の内容

| 集合 | SHA-256 |
| --- | --- |
| 候補runtime | `0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87` |
| 現workingtreeのharness | `5b66c040c551df97c7964984140dc75f88ad91f2e8da7dfb02c3df36dc4af6f8` |
| 元comparison-policy.json | `e8dbcf4a4eadf015a6a60e0415652badf56038ba7b3742b54f24c495c03ebfba` |

runtime/harnessのファイル一覧は10.2の`final-check.json`から、hash計算法は既存`Verify-Source.ps1`から再利用した。4つのテストprojectはReleaseのMSBuild評価で使用ソース・埋込み資源・設定を採取した。`bin/obj`をsource集合へ含めない。[source-before.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/source-before.json)と[source-after.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/source-after.json)、[source-comparison.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/source-comparison.json)で前後のruntime・harness・4テストproject・policyのhash一致を確認した。HEAD・refs・全stage entry・cached diff・既存tracked worktree差分も一致した。増えたstatusは今回の新規regression-results.mdだけだった。

開始時には、タスク10.3の未レビュー・未コミット差分が既に存在した。対象は`optimization-plan.md`、`tasks.md`、未追跡`change-1-results.md`、測定ツールREADME、`BenchmarkProcess`、`ProcessCheck`、`RunConditions`、`RunComparison`と対応する5テスト/fixtureである。開始statusとindex/worktree差分を`source-before*`へ保存し、今回も保持した。4projectの成功はこのworkingtreeを対象とするが、harness修正の独立レビューや性能受入を代替しない。Gitのstage内容とrefsを前後比較し、indexのbytehashだけでstageの意味的な不変を判断しない。

## コマンドと結果

Releaseビルドを先に実行した。各テストには`--report-trx --results-directory <記録先>/trx/<ID>`を指定した。実際の引数配列・cwd・開始終了UTC・終了値は各IDのJSON、標準出力は各IDのlog、集計は[validation-summary.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/validation-summary.json)に保存した。

| ID | コマンド | 終了値 | 成功 | 失敗 | スキップ |
| --- | --- | --- | --- | --- | --- |
| 01-build | `dotnet build WasmSharp2.slnx -c Release --warnaserror` | 0 | 警告0・エラー0 | — | — |
| 02-runtime | `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory <記録先>/trx/02-runtime` | 0 | 4799 | 0 | 0 |
| 03-generators | `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory <記録先>/trx/03-generators` | 0 | 160 | 0 | 0 |
| 04-runner | `dotnet run --project tests/WasmSharp.TestSuiteRunner.Tests/WasmSharp.TestSuiteRunner.Tests.csproj -c Release --no-build -- --report-trx --results-directory <記録先>/trx/04-runner` | 0 | 702 | 0 | 0 |
| 05-benchmarks | `dotnet run --project tests/WasmSharp.Benchmarks.Tests/WasmSharp.Benchmarks.Tests.csproj -c Release --no-build -- --report-trx --results-directory <記録先>/trx/05-benchmarks` | 0 | 320 | 0 | 0 |
| 06-simd-no-intrinsics | 新processに`DOTNET_EnableHWIntrinsic=0`を設定し、02-runtimeと同じprojectへ`--treenode-filter "/*/*/WasmFunction_InvokeTests/(V128*)\|(SIMD*)"`を追加（記録先は`trx/06-simd-no-intrinsics`） | 0 | 81 | 0 | 0 |

表のSIMDフィルタ中の縦線はMarkdown表のためにエスケープしている。実際の引数は`/*/*/WasmFunction_InvokeTests/(V128*)|(SIMD*)`。[06-simd-no-intrinsics.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/06-simd-no-intrinsics.json)に渡した文字列を保存した。TUnitへ`--filter`やsource pathを渡していない。partial classの5つのSIMDファイルにある15メソッドとTRX内の15メソッドが一致し、パラメーター展開後は81件だった。[simd-selection.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/simd-selection.json)に実名を保存した。この設定はJITの確認であり、全CPU命令の無効化や他CPUの検証を意味しない。

生成器は160件全てを実行し、数値・SIMD・memory・参照の宣言/handler整合、生成dispatch、失敗原因/位置の伝播を含む。測定ツール320件は小さいfixtureを使う契約テストで、BDNの本測定は起動していない。

公開APIの起動確認は、02-runtimeに含まれる既存の`Decode→Validate→Instantiate→GetFunction→Invoke`直接テスト（定数結果42、数値、SIMD、host連携等）で行った。同じ公開経路の独立smokeを重複して追加しない。

## 公開契約と既存テストの対応

以下は02-runtimeのTRXで全件Passedを確認した代表ケースである。件数はパラメーター展開後。各メソッドのsource path/行番号、class、test IDと件数は[contract-coverage.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/contract-coverage.json)に保存した。公開APIテストの多くは`WasmFunction_InvokeTests`または`WasmModule_InstantiateTests`のpartial classであり、ファイル名をclass名として扱わない。

| 要件 | 確認した契約 | 既存class.method | 成功件数 |
| --- | --- | --- | --- |
| 7.1 | Decodeの境界と診断位置 | `WasmModule_DecodeTests.制御終端やelseや型即値が不正_両入力で診断先頭と元の関数位置を通知する` | 6 |
| 7.1 | Validateの全体成功と失敗の境界 | `WasmModule_ValidateTests.後半関数の結果型が不正_再試行しても検証失敗しインスタンス化できない` | 1 |
| 7.1 | Instantiateのリンク・保持上限・startの順序 | `WasmModule_InstantiateTests.巨大table定義とstartを持つ_全リンク成功後だけ保持上限を返してstartを実行しない` | 2 |
| 7.1、7.4 | 選定箇所の保持上限診断・外側状態復元 | `InterpreterContext_EnsureCapacityTests.必要数の加算がintの上限を超える_位置付き保持上限を伝播して外側の状態を保つ` | 1 |
| 7.1、7.4 | 公開Invoke入口の保持上限診断とcontext解除 | `ExecutionBoundary_InvokeTests.関数入口で保持上限を超える_再分類せず伝播し現在のコンテキストを解除する` | 1 |
| 7.1、7.4 | 内部call入口の保持上限診断と外側復元 | `Interpreter_CallTests.呼出し先の必要容量が保持上限を超える_入口の処理段階と呼出し先の位置で実装上限とし外側を復元する` | 1 |
| 7.1、7.3 | 選定変更の結果を保つ直接call回帰 | `WasmFunction_InvokeTests.同じ深さの直接call回数を増やす_割当が回数に比例せず結果を保つ` | 1 |
| 7.2 | 数値NaNpayload・符号付きゼロのbit列 | `WasmFunction_InvokeTests.公開経路でf32とf64のselectを呼ぶ_選択したNaNpayloadと符号付き0の生bitを保つ` | 4 |
| 7.2 | SIMD浮動小数点のbit・丸め・変換 | `WasmFunction_InvokeTests.SIMD浮動接続のbitと丸めと変換を実行する_両入力の公開4段階でlaneの結果を保持する` | 20 |
| 7.2 | SIMD整数の固定幅・境界値 | `WasmFunction_InvokeTests.SIMD整数接続の境界値を実行する_両入力の公開4段階で固定幅の結果を返す` | 10 |
| 7.2 | SIMDのlane・引数型と全bit列 | `WasmFunction_InvokeTests.SIMD接続の各命令が関数にある_両入力の公開4段階で引数型と結果の全bitを保持する` | 14 |
| 7.3、7.4 | 正常終了後の独立呼び出しと返却済み結果 | `WasmFunction_InvokeTests.定数関数を反復し別の関数も呼ぶ_先に取得した結果と反復結果を保持する` | 1 |
| 7.3、7.4 | 同期再入の正常/trap後のcallback引数・外側locals・context | `WasmFunction_InvokeTests.Hostから同期再入してstackを拡張する_正常終了と捕捉したtrapの後も引数と外側localsを保つ` | 8 |
| 7.3 | 各callのlocals初期化と外側値 | `WasmFunction_InvokeTests.制御値を同じ呼出し元から連続callする_内側分岐後も外側値とlocalsを保ち呼出し先localsを初期化する` | 2 |
| 7.3 | import関数実体と所属instance | `WasmFunction_InvokeTests.数値条件の制御からimport定義関数と再exportしたhostを呼ぶ_同じ関数実体と所属instanceを保つ` | 1 |
| 7.3、7.4 | 別policyの独立呼び出しとWasm入口毎の解除 | `WasmFunction_InvokeTests.単独hostからWasmを順に呼ぶ_各入口の上限を選びホストへ戻るたびにcontextを解除する` | 2 |
| 7.3、7.4 | trap/exhaustion/同一host例外後の独立呼び出し・上限・共有import・locals | `WasmFunction_InvokeTests.制御内でtrapまたは同期再入またはhost例外に至る_最外の上限と例外実体を保ち次の呼出しを妨げない` | 6 |
| 7.4 | host再入exhaustion後の独立実行 | `WasmFunction_InvokeTests.Hostから再帰的に再入する_深さ上限で中断し独立して再実行できる` | 4 |
| 7.4 | host由来Wasm例外の型・同じ実体・位置・段階と次の実行 | `WasmFunction_InvokeTests.Hostがランタイムと同型の例外を投げる_位置と段階を含め元実体を伝播し次の実行を妨げない` | 2 |
| 7.2、7.3、7.4 | V128の同期再入後の結果・locals・上限とhost例外実体 | `WasmFunction_InvokeTests.V128のhost同期再入が成功または失敗する_外側値とlocalsと上限を復元し返却済み結果を保つ` | 3 |
| 7.2、7.5 | memoryの全範囲事前検査と部分更新禁止 | `WasmFunction_InvokeTests.メモリ一括操作の全範囲を符号なしで検査する_空範囲の境界を認め不正なら変更前に元位置でtrapする` | 20 |
| 7.2、7.5 | tableの全範囲事前検査と部分更新禁止 | `WasmFunction_InvokeTests.一括操作の符号なし範囲を検査する_末尾の0長だけ成功し範囲外は部分更新せず位置付きtrapになる` | 20 |
| 7.5 | data失敗時の先行segmentだけの副作用と後続/start中断 | `WasmModule_InstantiateTests.アクティブデータが現在メモリの範囲を超える_先行書き込みだけ残して後続とStartを中断する` | 5 |
| 7.5 | element失敗時の先行segmentだけの副作用と後続/data/start中断 | `WasmModule_InstantiateTests.後続activeの範囲が不正_先行転送だけ残し後続elementとdataとStartを中断する` | 5 |
| 7.5 | startのtrap/host例外/exhaustion後も保持したinstance・関数・resourceを使用可能 | `WasmModule_InstantiateTests.Startが副作用と参照保存の後に失敗する_instanceを返さず保存参照の操作と完了済み更新を維持する` | 3 |
| 7.2、7.5 | SIMD store失敗時の部分更新禁止・先行更新・保存参照と次の実行 | `WasmFunction_InvokeTests.SIMDメモリ接続のstoreがInvokeかstartでtrapする_段階と元位置と先行更新と保存参照と次の呼出しを保つ` | 2 |

7.1の公開4段階・失敗分類/位置/診断、7.2の数値/SIMD bit列と参照同一性、7.3の結果・引数・locals・import/所属instance、7.4の正常/trap/exhaustion/同じホスト例外実体の終了後と同期再入、7.5の部分更新禁止・先行segment/start副作用・保存参照に対応する既存ケースが成功した。8.1は先行したReleaseビルド警告・エラー0と全4projectのコマンド実行で確認した。新しい公開APIや意味論を追加していない。

## 記録処理の補足と限界

検証コマンド6件は全て終了0だった。記録用source採取の初回はPowerShellの空配列扱い、集計の初回は環境変数復元値の違いにより、それぞれ終了1となった。製品テストの失敗として扱わず、[artifact-notes.md](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/artifact-notes.md)に原因と補正を残した。

当初の`finally`は.NETへnullを渡した結果、各コマンドを動かしたshell内で環境変数の未設定nullが空文字になった。元JSONの`hw_intrinsic_restored`は書き換えていない。その個別shellは終了済みで、後続の新規shellでは未設定を確認した。user/machineの環境設定は変更していない。再利用用スクリプトは未設定ならEnv:から削除する処理へ補正し、[environment-restoration.json](../../../artifacts/runtime-performance/task-13.2-20261024T153250Z/environment-restoration.json)で同じ復元処理だけをprobeした（before=null/存在false、temporary=0、after=null/存在false）。この補正でruntimeやテストsourceは変わっておらず、テストを重複実行していない。

## 未実施範囲と再実行条件

- 10.3は、旧runtime固定ゲートの割当総量1680bytes差の再出によりBlockedのまま。元policy・数値目標・許容幅を保持し、新版policy・候補の再測定・性能受入を実施していない。
- 13.1の最終性能比較、13.3の固定公式素材全体run・同じ結果へのcompare-run/verify、13.4の受入baseline保存は実施していない。通常のランナーテスト702件成功は固定公式スイート全体の合格を示さない。
- Ubuntu/他OS/architecture、非同期/並行実行、他CPUでの確認は実施していない。
- 本記録の機能回帰成功は、採取した候補runtime/harness/test source集合のWindows x64/.NET10環境に限定する。今後runtimeが変更された場合は13.2を再実行し、公式回帰に伴うruntime修正があれば13.1の性能比較も同じ最終状態でやり直す。
- 独立レビュー、tasks.mdの更新とGit操作は親の担当であり、本記録だけでタスクや仕様全体の完了を宣言しない。
