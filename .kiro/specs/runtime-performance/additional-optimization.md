# 追加性能改善と未コミット変更レビュー（2026-10-27）

## 対象と採用判断

利用者の「パフォーマンス改善案について提案を募り、妥当なものについて検証し有益なものは採用せよ」に基づき、2名の独立した提案担当から候補を集めた。初回のClaudeレビュー対象はステージ済み187ファイル。妥当な指摘を作業ツリーへ修正し、測定ツールを固定してから追加候補を比較した。

次の2案を検証した。初回局所比較では両方を採用と判断したが、再レビューで比較基準を訂正し、最終的には両案とも今回は不採用とした。局所的な割当削減は確認できたが、必要な比較条件を満たしていないためであり、改善案そのものの機能不良やすべての時間悪化を立証したという判断ではない。追加最適化を戻した現在のruntime集合は、先に受け入れた`0ac1b7884daa13e42d294d99fec63b59462eb1bcc917fc8e2aaab7bee9472c87`と全112ファイルで一致する。測定・診断処理とテスト・文書のレビュー修正は保持した。

1. 呼出し結果の所有配列の再コピーを省く案。試作では先に`WasmResults.FromOwnedValues`へ受け渡しを集約し、動作を保った状態で関連423件のテストを確認した。その後、内部経路では既に所有している不変配列を保持するよう変更した。所有・寿命の問題は見つからなかったが、訂正後の局所比較で複数値ケースの時間のばらつきが基準を超えたため、WasmResults.csとExecutionBoundary.csを保存した変更前へ戻した。
2. `FunctionValidator`の局所builderから最終命令配列へ`DrainToImmutable()`で移す案。builderは関数内で完結し、その後参照されないため、新しい抽象化は不要と判断した。CountとCapacityが異なる経路ではコピーする既存APIの挙動を利用した。局所比較と全体比較で小688B・大96,256B/回の削減が再現したが、全15ケースのstrict比較が不成立となったため、保存した`ToImmutable()`の版へ戻した。

export辞書をmoduleへ移す案は、InstantiateからValidateへ割当と保持期間を移す影響を評価する必要があるため採用を見送った。結果オブジェクトのsingleton化や配列poolは導入していない。

## 局所性能比較

記録ディレクトリは[additional-optimization-20261027](../../../artifacts/runtime-performance/additional-optimization-20261027/)。変更前Aと候補Bの3ファイルを`source-a/`・`source-b/`に保存し、SHA-256で固定した。同じcwdでA1→B1→A2→B2→A3→B3の順に切り替え、各版で下記4ケースを測った。全24runが終了0。数値による選別や再測定は行っていない。

- SDK10.0.402、collectorと生成子の.NET10.0.13、CoreLib完全版を固定・観測。
- `RuntimeFrameworkVersion=10.0.13`、`RollForward=Disable`、`DOTNET_TieredCompilation=0`、`DOTNET_TieredPGO=0`。
- 子processはCPU14、前後のaffinityは16384。待機workerはCPU14・15を除外。電源planは既存の高パフォーマンス設定。
- 固定素材は`task-2.5-20261022T160359Z/accepted-assets/asset-manifest.json`、SHA-256は`b7250adbce11b01ab88e7c8cca60eba099da0770ca4470e6c7f63f37df573c8a`。
- 各ケースのpolicyは測定前に保存。既存daily-pc policyの時間許容幅等を保持し、割当削減目標のみ単一値40B/回、複数値100B/回、Decode+Validate各1%へ設定した。

|ケース|変更前B/回|変更後B/回|削減|時間の中央値ns/回（前→後）|
|---|---:|---:|---:|---:|
|invoke.single|328|272|56B、17.07%|117.716→114.206|
|invoke.multi|584|464|120B、20.55%|165.645→144.040|
|decode-validate.small|9,144|8,456|688B、7.52%|3,274.026→2,809.241|
|decode-validate.large|723,696|627,440|96,256B、13.30%|160,032.949→127,402.951|

各群3runの割当値は同一で、全4ケースが割当削減目標を達成した。当初のpolicyによる比較条件差は0、比較CLIは4件とも終了0、ケース判定は`Improved`。時間指標はすべて`Indeterminate`かつ`WithinAllowedIncrease=true`だったが、このpolicyは旧受入に限定された運用幅を流用していた。以下の数値と記録は保持するが、最終採用の根拠には使わない。時間短縮の確定とも扱わない。

この24runは4ケースに対する局所比較であり、全15ケースの性能受入やValidate単体の時間測定を表さない。後述の全15ケース比較と区別する。以前のmemory.copy-fillの例外受入は今回の判定へ使用していない。

再レビュー後、元の許容幅・同等範囲を戻し、各A群3runだけから既定式でNoise/RunVariationを導き直した。[local-strict-recheck](../../../artifacts/runtime-performance/additional-optimization-20261027/local-strict-recheck/)の比較終了値はsingle=0、multi=1、Decode+Validate小=0・大=0。multiの候補群の最大中央値偏差6.221%は再導出したRunVariation5.5%を超えたため、結果配列案を不採用とした。小・大の計時区間はDecodeとValidateだけで結果配列案を通らず、この2件の成功をFunctionValidator案の局所検証として使う。

全15ケースの最終比較は[strict-final-20261027-v2](../../../artifacts/runtime-performance/additional-optimization-20261027/strict-final-20261027-v2/)に保存した。元基準runtimeは`4966bd32750411fb132e6849c0f3b56992a2a5be382cdb3e84d07fdd14c8a751`、検証した候補は`0563688abda2884bffadba1a12c2034e95ab7340500e57319b5c035bf23c3b69`。候補の値を使わず、基準だけの校正3runでNoiseとRunVariationを固定し、その後に交互6runを測定した。元の許容増加幅・同等範囲・削減目標・必要3runは維持した。追加policyはDecode+Validate小大の割当削減目標1%だけを追加する。校正の独立再計算は全15ケースで一致した。

校正45ケースと最終90ケースの計135ケースはすべて終了0、前後検査成功、runtime・CPU・source一致を確認した。比較条件差は0だが、元目標と追加目標の両policyで比較CLIは終了1となった。全15割当指標と11時間指標の計26/30指標が許容内。残る時間指標は`invoke.empty`の基準群、`invoke.single`と`invoke.multi`の候補群でばらつき超過、`memory.copy-fill`で中央値18.776%増加・許容幅超過だった。小大の割当目標は達成したが、追加policyで時間短縮の確定とは扱わない。全体受入は不成立のまま保持し、旧運用幅・限定例外の適用、結果に合わせた再測定、memory.copy-fillの追加原因調査は行っていない。

初回の[strict-final-20261027](../../../artifacts/runtime-performance/additional-optimization-20261027/strict-final-20261027/)は測定開始前に停止した。Gitインデックスの管理情報の変化をステージ変更と誤って拒否したため、開始時の全1474エントリのmode/blob/stage/pathが一致することを確認し、照合方法だけを修正してv2を開始した。性能runは初回0件であり、結果を選別した再試行ではない。失敗記録も保持した。

局所比較の手順・事前policy・全24runは記録ディレクトリ直下の`README.md`、`Measure-Case.ps1`、`Run-Series.ps1`、`policies/`、各ケースの`A1`〜`B3`に保存した。旧集約値は[performance-summary.json](../../../artifacts/runtime-performance/additional-optimization-20261027/performance-summary.json)、最終判断と全15ケースの判定は[final-selection.json](../../../artifacts/runtime-performance/additional-optimization-20261027/final-selection.json)を参照する。両案を戻した後に現在のruntime集合と既受入版の一致を確認した。

## 初回Claude指摘の判断と修正

### runtime（21ファイル）

1. **Low・採用**。入口容量テストで外側のStageとRun引数が同じになり、段階の取り違えを検出できない。不要なStage設定2行を除き、外側のDecodeへの復元も検証した。

### measurements（87ファイル）

1. **Low・不採用**。準備段階で未書込のInput.Arraysを照合する問題は、固定5ケースがすべて空配列のため発生しない。未対応入力のための拡張は追加しない。
2. **Low・採用**。入力準備失敗がInstantiateと記録される。入力の取得・生成・書込前にInvokeへ切り替え、異常入力の回帰テストを追加した。
3. **Low・採用**。保存のI/O失敗が検査失敗へ変わり、開始状態や元例外を誤る。検査例外の境界を先に分離し、保存失敗をSavingへ分類、開始状態は保存成功後に反映、二次保存失敗でも元検査例外を保持する。I/O失敗4ケースで検証した。
4. **Low・採用**。manifest参照が`../`で配下を逸脱できる。path解決を分離した後、正規化後の配置を確認し、profile・cases・binary・source参照を検査する。
5. **Low・不採用**。15ケースとBenchmark/GlobalSetupの対応表をreflectionで検査する提案。現行の対応は一致し、対応表をなぞる追加テストは作らない。
6. **Low・不採用**。DecodeテストがCleanupのValidateへ依存するという指摘。最後のmoduleだけが検証され、先のmoduleは未検証である契約を確認する構成なので維持する。
7. **Low・採用**。Assets READMEの接続予定という説明を、接続済みの現状へ更新した。

### results（53ファイル）

1. **High・採用**。Windows固定ドライブのfixtureがLinuxで完全修飾pathにならず比較を誤る。OSの一時ディレクトリに基づく絶対pathへ変更し、期待値も更新した。Linuxでの実行は下記の環境制約により未確認。
2. **Medium・採用**。collectorだけのruntime記録では子のroll forwardを見逃す。子のruntime/CoreLib完全版とMSBuild環境変数を保存し、欠落・同Job内混在を拒否、run間差を比較する。旧記録は補完せず、新比較器では比較不可とする。旧結果の再比較には復元した旧harnessを使う旨を文書化した。
3. **Low・不採用**。Jobの一覧設定が型名になる潜在問題。対象のArguments/NuGetReferencesは現行固定JobでDefault、CLIにも変更経路がないため一般化しない。
4. **Low・採用**。診断workloadが最新patchへroll forwardする。保存された完全版を`--fx-version`でworkloadとreaderへ渡す。実際の診断起動で一致を確認した。
5. **Low・採用**。git/dotnet起動失敗が未処理例外になる。`Win32Exception`を理由付きの終了2へ変換し、git不在の回帰テストを追加した。
6. **Low・採用**。Git差分をUTF-8文字列へ変換すると不正バイトを失う。コマンド起動・終了確認を先に分離し、差分を生バイト保存する。不正UTF-8バイトを含むindex・worktreeの復元を検証した。

### documents（26ファイル）

1. **Medium・採用**。観測後に合意したdaily-pc許容幅が要件・設計本文へ反映されていない。保存済み利用者判断に基づき、適用対象・変更値・観測後の変更であることを同期した。以前の厳しいpolicyと結果は保持した。
2. **Medium・採用**。古いNOT_VERIFIED/Blockedが現行状態に見える。日付付きの旧記録見出しとsuperseded表記で区別した。
3. **Medium・採用**。steeringの最新baseline・完了状態が古い。受入済みruntime-performanceのrun/baselineへ参照を更新した。
4. **Medium・採用**。受入時のruntime・JIT・CPU固定と比較手順が追跡文書にない。READMEへソース復元と固定条件・daily-pc比較手順を追加した。
5. **Low・不採用**。Ubuntu CI成功の確認提案。文書はworkflow構成の説明であり、実行成功を主張していない。未確認という制約を保持する。

付随して、直接callを通じる共有CallFunctionの容量検証とindirect固有検証の説明を分けた。diagnosis.md末尾の余分な空行を除いた。復元テストは複数のgitを検出すると呼出しに失敗したため、`Get-Command -TotalCount 1`で単一化した。

## 初回の機能検証（2案の試作時）

- `dotnet build WasmSharp2.slnx -c Release --warnaserror`:終了0、警告0・エラー0。候補B復元後のビルドも終了0。
- Releaseビルド済み4プロジェクトを`dotnet run --no-build -- --report-trx`で実行。WasmSharp.Tests4,799、Generators160、TestSuiteRunner702、Benchmarks349、計6,010件成功、失敗0・skip0。
- 固定公式147入力・53,907コマンド:52,830passed、1,077out_of_scope、failed/runner_error/blocked/未処理は0。受入済みbaselineとの条件差・追加・欠落・変更・退行0。`run`・`compare-run`・`verify`はすべて終了0。記録は[official](../../../artifacts/runtime-performance/additional-optimization-20261027/official/)。baselineを更新する操作は行っていない。
- `Restore-RunSource.Tests.ps1`:終了0、PASS。
- `Collect-Diagnostic.ps1`で既存の準備済み素材を使用し、invoke.singleを1秒起動。collect/read/top-methods終了0、保存された.NET10.0.13/CoreLib完全版と一致、readable=true、events_lost=0。性能判定には使用しない。
- PowerShell文書例14ブロックと測定scriptの構文解析にエラーなし。
- WSLのUbuntuは存在するが、PATHと標準配置先に.NET CLIが見つからず、Linuxテスト・GitHub Ubuntu CIは未実行。

## 再レビューの追加指摘（2026-10-27 5:40以降）

- results R1（Low・採用）: 診断runtimeがcollector由来のままで、子の版が異なると誤った版を固定する。選択責務を分離した後、保存checkをhash照合して子の完全版を採用する。全件が旧形式の場合だけcollector代用を明記し、部分欠落・混在は拒否する。
- results R2（Low・採用）: 同Job内の子runtime混在拒否に直接テストがない。2ケースで選択ケースの版は一致させ、別ケースだけの混在による条件不一致を検証した。
- results R3（Low・採用、Round4）: 旧形式の代用元environment.jsonのhashを照合していない。SDK/runtimeの環境読込を集約し、両形式で保存hashを確認する。環境改変を拒否する検証も追加した。
- documents R1（Medium・採用）: 過去の受入runtimeと現在候補の境界、全15ケースの最終比較が不足していた。全体比較を実施して不成立を記録し、追加2案を戻した。現在のruntimeと先の受入版は一致し、旧受入の範囲を維持する。
- documents R2（Medium・採用）: 今回限りのdaily-pc幅を自動再利用していた。元のpolicyの許容増加幅・同等範囲・目標を保持し、新harnessで元基準runtimeを測り直してNoiseとRunVariationだけを導出した。候補の値は幅の算出に使わず、不成立もそのまま採否へ反映した。
- documents R3（Low・採用）: 再レビュー状態を最終結果へ更新する。各群の終了コードと最終イベント・セッションIDを確認して反映する。
- documents N1（Low・採用、Round5）: acceptance.mdに現在のharness集合がなく、過去の検証節も現在の結果と混同しやすい。最終検証のharness全41ファイルの集合hashと一覧への参照を追記し、既存見出しを保持したまま2026-10-26の受入版の記録と明示した。独立検証の実行日は元の2026-10-27を保持する。runtime・harnessのコード変更はなく、source集合一致とリンク先・差分を確認し、同じビルド・テストは繰り返さない。

追加修正後のReleaseビルドは警告0・エラー0、Benchmarksの全350テストが成功・失敗0・skip0。診断runtime選択scriptは9ケースと新旧の保存run読込で成功した。runtime Round3、measurements Round3、results Round5は終了0・最終`type=result`・`is_error=false`・初回セッションID一致を確認し、採用した指摘の未解消と新規欠陥はない。documents Round5でR1〜R3、Round6で追加N1の解消を確認した。Round6も終了0・最終type=result・is_error=false・初回セッションID一致で、新しい欠陥はなかった。全4群の同一セッション再レビューが完了し、採用21指摘に未対応はない。

## 最終確認とレビューの状態

追加最適化を戻した最終版の検証記録は[final-validation-20261027](../../../artifacts/runtime-performance/additional-optimization-20261027/final-validation-20261027/)へ保存した。`dotnet build WasmSharp2.slnx -c Release --warnaserror`は終了0・警告0・エラー0。ビルド済み4テストprojectを`dotnet exec --fx-version 10.0.13`で実行し、WasmSharp.Tests4,799、Generators160、TestSuiteRunner702、Benchmarks350、計6,011件成功・失敗0・skip0をTRXで確認した。固定公式147入力・53,907コマンドは52,830passed・1,077out_of_scope、failed・runner_error・blocked・未処理0。受入済みbaselineとの条件差・追加・欠落・変更・退行0、`run`・`compare-run`・`verify`は終了0だった。既存baselineのhashも不変。実行引数・終了値は`commands.json`、集計は`summary.json`を参照する。Linuxは引き続き未実行。

初回Claudeレビューの全19指摘は採用14件・不採用5件、追加7指摘は採用とした。2名の独立エージェントによる性能候補・測定ツールの確認と、全15ケースの校正値の独立再計算も行ったが、Claude再レビューの代替とは扱わない。

初回は`You've hit your session limit · resets 5:40am (Asia/Tokyo)`で中断したため、利用者の依頼どおり2026-10-27の5:40に同じセッションを再開した。runtime Round3は完了した。documents Round4は終了1・`is_error=true`・`api_error=usage_limit_reached`となり、`You've hit your session limit · resets 10:40am (Asia/Tokyo)`が表示された。途中回答を完了扱いせず、同じ予約`wasmsharp2-claude-5-40`を2026-10-27の10:40へ更新した。10:40に同じセッションのRound5を再開し、終了0・最終result・is_error=false・セッション一致とR1〜R3の解消を確認した。同じセッションのRound6で追加N1も解消、新しい欠陥なしと確認した。再開予約wasmsharp2-claude-5-40はPAUSEDへ更新し停止した。最終回答と完了時のsource・ステージ照合はreview/final-state-20261027.jsonに保存した。

完了したセッション:

- runtime（Round3）:`105626a1-cfc7-4177-a3a5-ab1828906d5d`
- measurements（Round3）:`67193b73-97b8-4d78-9922-a4e743f885be`
- results（Round5）:`570f386b-7646-41ea-9b4b-b4c232ede2dc`
- documents（Round6）:`add1401c-94c2-42ff-a075-938f38022042`

初回・再開ログは`C:/Users/taihe/AppData/Local/Temp/wasmsharp-claude-review-6b73ba41c73b420e83665658e7c7aa33/`に保持している。回答・セッションID・依頼文と差分・起動scriptは[review](../../../artifacts/runtime-performance/additional-optimization-20261027/review/)にも保存した。

追加最適化の採否は両案不採用で確定した。ステージされた全1474エントリは開始時と一致し、HEADも維持している。インデックスファイル自体のhashは管理情報の変化により変わったため、ステージ内容の一致と区別する。`--cached --check`には開始時からのdiagnosis.md末尾空行が残るが、作業ツリーでは修正済みである。最終機能検証は[final-validation-20261027](../../../artifacts/runtime-performance/additional-optimization-20261027/final-validation-20261027/)へ保存した。
