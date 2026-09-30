# test-suite-runnerの調査と設計判断

## 概要

- **対象機能:** test-suite-runner
- **設計調査の種類:** CLIを新設し、既存ランタイムと固定WABTへ接続するため、詳細な設計調査（full discovery）を行った。
- **主要な知見:** 公開APIへの追加はexport名・種類の一覧取得に絞れる。相対入力pathで固定WABTを起動すればJSONの書換えなしに配置rootを変えられる。全147生成JSONは10種・53,907commandで、初期必須の公式経路を具体的なcommand番号へ対応付けられる。
- 以下の「調査の前提」から「設計へ持ち越す調査・決定事項」までは設計前のギャップ分析を保持する。そこにある未承認状態・未実測事項・候補は当時の記録であり、今回の決定と確認結果は末尾の「設計調査」と「設計判断」に記録する。

## 調査の前提

- 調査日: 2026-09-27
- 対象: [requirements.md](requirements.md)の全14要件・111受入基準と、現在のワークツリーにある実装・固定依存
- [spec.json](spec.json)は`requirements-generated`、要件は未承認、文書言語は`ja` 本分析は要件承認や設計決定を代行しない。
- 要件・steering、ランタイム、固定外部依存を並行して読み取り専用で調査した。既存の未コミット変更を前提に分析し、本作業の成果物はこの文書とする。
- コード・既存テスト・固定上流ソースを静的に確認し、固定commit、入力件数、現存する変換器のhashを読み取った。今回、ビルド、テスト、素材の再生成、公式スイートの実行は行っていない。過去の受入記録を今回の実行結果として扱わない。

## 分析の要約

- `Decode → Validate → Instantiate → Invoke`、import情報取得、4種の共有実体、全7値型の保持は再利用できる。ランタイム全体を作り直す必要はない。
- 独立CLI、manifest、JSON commandの状態管理、期待値比較、結果保存、baseline比較は新設が必要 registerにはinstanceのexport一覧を取得する公開APIも必要である。
- 固定WABTの入力pathはJSONの`source_filename`へ入る。既存READMEの絶対pathによる変換例をそのまま正式生成処理へ移すと、配置root変更時の再現性を満たせない。
- 初期から6分類、全値型比較、公式診断の前方一致を扱う。既存ランタイムの日本語診断との不一致は観測して`failed`へ残し、判定基準を緩めない。
- 新しいCLIと必要な公開API拡張を組み合わせる案が有力 主な不確定要素は、初期必須の公式実行経路を成立させるためのランタイム修正量である。

## 現在の資産と責務

### ランタイムとテスト

| 資産 | 再利用できる能力・制約 |
| --- | --- |
| [WasmModule](../../../src/WasmSharp/WasmModule.cs) | Decode、Validate、Instantiateが独立する。`InspectImports`は完全なimport情報と未確認範囲を返すが、module全体の有効性を保証しない。 |
| [WasmInstance](../../../src/WasmSharp/WasmInstance.cs) | `GetFunction`、`GetGlobal`、`GetGlobalResource`、`GetMemory`、`GetTable`がある。名前を既知とすれば実体を取得できるが、export名・種類の公開一覧はない。 |
| [WasmHostModule](../../../src/WasmSharp/WasmHostModule.cs)、[WasmImports](../../../src/WasmSharp/WasmImports.cs) | 4種の実体を提供登録できる。実体はコピーしない。`WasmImports.Add`は同じmodule名とitem名の重複を拒否し、置換操作はない。 |
| [WasmValue](../../../src/WasmSharp/WasmValue.cs)、[WasmResults](../../../src/WasmSharp/WasmResults.cs) | i32・i64、f32・f64の生ビット、v128の上下64ビット、funcref・externref、結果0個・複数を表現できる。WABT JSONの読取や期待値判定は含まない。 |
| [ModuleInstantiator](../../../src/WasmSharp/Modules/ModuleInstantiator.cs)、[ExecutionBoundary](../../../src/WasmSharp/Execution/ExecutionBoundary.cs) | importの型・limits照合、実体共有、start、trapとexhaustionの公開例外化を所有する。ツールは公開操作を通じて利用し、内部処理を参照・再実装しない。 |
| [公開APIによる全値型の往復テスト](../../../tests/WasmSharp.Tests/WasmFunction_InvokeAcceptanceTests.cs)、[startのテスト](../../../tests/WasmSharp.Tests/WasmModule_InstantiateStartTests.cs) | 値・参照保持、共有リソース、失敗後の副作用等の既存テストがある。公式JSONの読取、診断比較、公式ケースの成立を確認するテストではない。 |

`tools/`にランナー実装はなく、[solution](../../../WasmSharp2.slnx)はランタイム・生成器とそれぞれのテストだけを含む。[現行CI](../../../.github/workflows/unit-tests.yml)もspecを取得してReleaseビルドと2つのTUnitプロジェクトを実行する構成で、公式生成・実行・baseline操作の工程はない。

配置は[structure.md](../../steering/structure.md)に従い、ツールを`tools/`、テストを`tests/<対象プロジェクト>.Tests/`へ置く。ランタイムからツールやWABTへの依存は追加しない。C#の命名・日本語テスト名・AAA・TUnitの既存規約を再利用し、技術レイヤー別のフォルダや将来用途の抽象化を増やさない。

### 固定依存

| 対象 | 今回確認した状態 |
| --- | --- |
| WebAssembly spec | HEADは`05ca4182176763112561ae20153975c12bd689e4` `test/core`の入力は147WAST、うちSIMD57件 |
| WABT | HEADは`03a00a1334e6121fb0cce4fccbd6bb109b68acaa` 固定版の[feature定義](../../../thirdParties/wabt/include/wabt/feature.def)は全21件、ON7件・OFF14件 |
| 変換器 | `artifacts/wabt-core2/Release/wast2json.exe`が存在し、SHA-256は`B0E1D0316A265F659566A0E3B6F1A00FEA9E315E3D25F2C3E0853E414D0BE245` [READMEの記録](../../../thirdParties/README.md)と一致する。 |
| 変換手順・既存素材 | READMEに全入力変換例があり、`artifacts/wast2json-official-examples`に形式確認用の素材がある。正式manifestやbaselineの代わりにはならない。 |

変換器の存在とhash一致は、今回のビルド成功・全件変換成功・配置rootを変えた再現性の証拠ではない。固定版の一次資料をローカルで読めるため、別版のWeb資料や最新の依存へ置き換える調査は行っていない。

## 要件と既存資産の対応

`Missing`は不足する実装、`Unknown`は実行・設計調査が必要な点、`Constraint`は維持すべき制約を表す。以下で全111項目を要件別に対応付ける。対応付けは受入基準の合格判定ではない。

| 受入基準 | 既存資産 | ギャップ |
| --- | --- | --- |
| 1.1～1.7 | .NET10の開発基盤、公開4段階API | **Missing:**生成・実行・baseline保存・比較・最終判定の独立コマンド **Constraint:**複数工程をまとめない。実行は保存済み素材のみ、保存・比較・最終判定は保存済みJSONのみを入力にする。 |
| 2.1～2.7 | 固定gitlink、README、WABTのfeature定義 | **Missing:**出典・全featureの既定値/実効値・実行ファイルhash・生成条件の記録 **Constraint:**全147入力を維持し、Core 2.0外はOFF、`--enable-all`/`--no-check`は禁止 公式入力・外部ソースを変更しない。 |
| 3.1～3.7 | READMEのPowerShell変換例 | **Missing:**正式manifest、相対path/hash/参照対応、失敗・未処理記録と独立入力の継続 既存例は初回失敗でthrowする。**Unknown:**同条件と配置root変更時の全生成物一致 |
| 4.1～4.7 | 固定WABTのJSONと素材形式 | **Missing:**生成時/実行時で対象を分ける照合、入力異常と件数未確定の記録 **Constraint:**実行時に元WASTを要求しない。watは照合でhashを取り、実行では開かず`out_of_scope`にする。 |
| 5.1～5.7 | 名前によるexport取得、4種の提供登録、実体共有 | **Missing:**公開export一覧、入力ごとの直近module・module識別子・登録名・失敗原因の管理 **Constraint:**否定assertionは名前対応を更新せず、実行済みの共有リソースの副作用は保持する。 |
| 6.1～6.8 | `InspectImports`、import名・種類・要求型と未確認範囲 | **Missing:**依存元commandと元の失敗の追跡、独立した後続処理の継続 **Constraint:**Decode/Validateを先行し、登録依存による`blocked`はInstantiate直前に判定する。未登録だけで`blocked`にしない。 |
| 7.1～7.5 | 明示型のhost関数、global/memory/table生成API | **Missing:**spectestの固定構成、入力ごとの初期化、commandに結び付くprint記録 **Constraint:**同じ入力では同じ実体を提供し、型・limits照合をInstantiateへ委ねる。 |
| 8.1～8.11 | `WasmValue`の全値型、Invoke、global現在値、複数結果 | **Missing:**WABT値の読取、ビット保持の引数構築、v128のlane処理、NaN pattern、参照同一性、個数・型・順序の比較 **Constraint:**値処理を後続命令仕様へ延期しない。 |
| 9.1～9.11 | 段階別公開例外、Reason/Location | **Missing:**assertion別の期待段階・失敗種類・診断比較 **Constraint:**`Message.StartsWith(expectedText, StringComparison.Ordinal)`を無加工で適用し、不一致を`failed`とする。 |
| 10.1～10.7 | 未実装・実装上限・Wasm失敗等の公開分類 | **Missing:**6分類、発生操作/原因、保存失敗・未処理の記録 **Constraint:**不明なJSON、ランナーcallback例外、公開契約外例外等は`runner_error` `runner_unsupported`は作らない。 |
| 11.1～11.8 | 一般のテスト結果保存はTRXのみ | **Missing:**相対入力path＋command順序の識別、期待/実際/段階/原因のJSON、出典との対応、重複のない集計 **Constraint:**入力単位エラーを架空のcommand件数へ加えない。 |
| 12.1～12.10 | baseline方針のみ | **Missing:**同形式での明示保存、完了確認、変換比較、実行比較、ケース単位の回帰と欠落の検出 **Constraint:**比較でbaselineを更新せず、実行ファイルhashだけの差は両比較とも出典差異にする。 |
| 13.1～13.7 | 既存build/testの終了コード | **Missing:**用途別の終了判定 **Constraint:**保存成功・実行成功・回帰なし・最終合格を区別する。診断不一致を含む`failed`や`runner_error`があれば実行と回帰比較は非0 |
| 14.1～14.9 | 先行基盤の公開API直接テスト、過去の変換記録 | **Missing:**実CLIによる全件処理、再現性、公式の初期必須経路、全command記録、初回baseline、再実行・比較 **Unknown:**初期必須経路を妨げるランタイム修正範囲 **Constraint:**初回ランナー受入と全8仕様統合後の最終合格を区別する。 |

## 重要な統合上の差分

### 1. export一覧とregister

[WasmInstance.cs](../../../src/WasmSharp/WasmInstance.cs)の17行目の`exports_`はprivateで、元の`WasmModule.Exports`もinternalである。現在の公開APIだけでは、JSONがexportを列挙しない通常moduleから、registerに必要な全exportを取得できない。

設計候補は、instanceから名前・種類の不変な一覧を取得し、実体は既存の`GetFunction`等で取得する操作である。実体を型別に含む一覧を返す案も可能だが、公開契約は増える。いずれも内部indexの公開、バイナリの独自解析、ランナーへの`InternalsVisibleTo`追加を必要としない。

[structure.md](../../steering/structure.md)の29行目には「公開取得操作は名前による操作」「`WasmInstance`に`Exports`コレクションを追加しない」が残る。`GetExports()`等のメソッド形式ならproperty禁止とは区別できるが、名前一覧を取得する能力の追加自体は方針への追記が必要である。要件の明示した追加能力を満たす形を設計で決め、steeringを同期する。既存の名前取得APIは維持できるため、追加型の変更として収められる見込みである。

再registerは、ランナー側の登録名→現在の提供元を更新し、Instantiateごとに`WasmImports`を組み立てる案がある。同じ実体を`WasmHostModule.Define`へ渡せば、`WasmImports.Add`の重複拒否契約を変更せずに済む。失敗したmodule/registerについては原因commandを持つ状態を残し、古い成功先へ戻さない。

### 2. 配置rootに依存しない素材生成

[README](../../../thirdParties/README.md)の変換例は`$inputFile.FullName`を渡している。[wast2json.cc](../../../thirdParties/wabt/src/tools/wast2json.cc)の127行目は入力引数をwriterへ渡し、[binary-writer-spec.cc](../../../thirdParties/wabt/src/binary-writer-spec.cc)の453～455行目はそのまま`source_filename`へ出力する。絶対pathはWABTの必須条件ではなく、既存の呼び出し例の選択である。選択理由の記録は見つからなかった。

要件3.6に対する候補は、入力rootを作業ディレクトリとし、同じ表記の相対入力pathと固定の出力basenameを使う方法である。JSON内のmodule参照はbasenameとして出力されるため、JSONと素材の相対配置も一定にする。出典として記録する実際の配置情報と、比較する論理的な変換引数を区別する必要がある。生成後のJSON書換えを前提とせず、呼び出し条件で安定させる案を優先して検証する。

固定WABTはfeatureの既定ON側には`--disable-*`、OFF側には`--enable-*`を登録する（[feature.cc](../../../thirdParties/wabt/src/feature.cc)）。全featureへ対称なON/OFF引数を付ける方式は使えない。現在の固定profileは既定値と一致するため、全21featureを記録し、feature変更引数を渡さないREADMEの方針を再利用できる。`--debug-names`等の出力に影響する条件も固定・記録する。ビルド条件は要件2.2どおり実行ファイルhashで代表させ、詳細なビルド履歴の保存を追加必須にしない。

WABTはJSONを書いた後に個別moduleを書き出すため、途中失敗で一部ファイルだけが残り得る（`wast2json.cc`の131行目以降）。終了値、JSONの読取、参照素材の照合を合わせて入力ごとの変換完了を確定する必要がある。

### 3. JSON形式と全値型の比較

根拠は固定版の[wast2json形式](../../../thirdParties/wabt/docs/wast2json.md)と[binary-writer-spec.cc](../../../thirdParties/wabt/src/binary-writer-spec.cc)の168～324、367～390、418～449、508～618行目である。

- i32/i64とf32/f64の具体値は符号なし10進の文字列である。f32/f64は数値ではなく生ビットを表す。整数は幅を保って解釈し、浮動小数点数への数値変換を介さず`FromF32Bits`/`FromF64Bits`を使える。
- v128は`lane_type`と文字列配列で表される。i8/i16/i32/i64/f32/f64のlane数は16/8/4/2/4/2 上下64ビットへの配置と、期待lane型による実値の切り出しはツール側の新規処理になる。
- canonical/arithmetic NaNは期待値のパターンであり、具体ビットの引数と分ける。符号を無視した判定、quiet bit、正負の0、明示NaN payloadは要件8のまま扱う。
- externrefは入力ごとに番号→同じホストobjectの対応を持つ。参照の判定には参照同一性を使う。funcref/externrefのnullは型も照合する。
- 単独action、assert_trap、assert_exhaustionの`expected`には`value`がなく型だけの要素も出力される。assert_returnの値付き期待値と同じ必須項目にはできない。
- 通常moduleは`module_type`を持たず、元のテキストmoduleもwasmへ変換される。Quotedの素材が`module_type=text`とwatになるため、元WASTの記法や拡張子だけで対象外を決めない。

固定全JSONの最終的な形式一覧は未確認である。固定WASTの参照例では番号付きexternrefと両型のnullを確認したが、これだけで全funcref表現の棚卸しが完了したとは扱わない。

### 4. 状態・失敗分類・診断

ランナーが所有する状態は、入力ごとの直近module、module識別子、登録名、spectest、externref対応、原因commandである。名前対応の成功/失敗と、実体に生じた副作用を別に扱う。否定assertionは直近module等を更新しないが、start等による共有実体の変更を巻き戻さない。

`InspectImports`は既知の登録失敗への実際の依存を見つけるために使う。Decode/Validateの結果を先に確定し、Instantiateへ進む時点で依存を判定する。取得自体の例外・未確認範囲は記録し、部分情報から「importなし」や架空の依存を補わない。

[ExecutionBoundary.cs](../../../src/WasmSharp/Execution/ExecutionBoundary.cs)の116～129行目はtrap/exhaustionに日本語の共通Messageを付けている。リンク失敗も[ModuleInstantiator.cs](../../../src/WasmSharp/Modules/ModuleInstantiator.cs)で日本語診断を作るため、公式期待診断との前方一致は成立しないケースが見込まれる。これは静的に分かる不一致候補であり、全件のfailed数は未測定である。ランナーは比較結果をそのまま保存し、[ADR0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)に従って修正を引き継ぐ。

例外型だけの分類にも注意が必要である。ホストcallback由来例外は型と実体を保って伝播する（[既存テスト](../../../tests/WasmSharp.Tests/WasmFunction_InvokeExhaustionTests.cs)）。spectestのprint記録処理等で発生した例外は、たとえWasm例外と同型でも要件10.5の`runner_error`である。ランナーが提供したcallbackの失敗と、公開段階で観測したWasmの失敗を区別する方法を設計する。

### 5. 完了状態・baseline・終了コード

入力単位の異常、列挙できたcommandの分類、未処理、件数未確定、結果保存失敗は別の事実として保持する必要がある。判定・baseline保存・集計が同じ完了情報を使えるJSON構造を設計する。読めないJSONを0件としたり、入力エラーから推測で`blocked`を生成したりしない。

変換baselineと実行結果baselineは、それぞれmanifestと実行結果を同じ形式で明示保存する。完成した結果なら不一致を含む初回baselineも保存できる。一方、回帰比較は以前passedだったケースの退行・欠落を特定し、不一致や比較未完了を「回帰なし」としない。実行ファイルhashだけが変わる場合は、変換比較・実行比較の双方で出典差異として扱う。

初回ランナー受入でfailedを後続へ残せることと、実行/回帰比較の終了コード0は別である。既知のfailedが残れば、回帰がなくても要件13.4により比較は非0となる。初回受入の確認では、保存済み結果の内容、比較の完了、回帰の有無、終了理由を区別して記録する。

## 初回公式受入と後続修正の境界

要件14.2～14.3により、初期必須の公式ケースを実行・判定する経路が不足すれば、ランタイム側も本仕様で修正する。ランナー自体と素材に起因する`runner_error`は0件を必要とする。観測した`failed`と、初期必須経路を妨げずランタイム側の原因と確認できた`runner_error`は、ケース・期待・実際・原因を記録して[test-suite-conformance](../test-suite-conformance/brief.md)へ引き継げる。分類と非0終了は維持する。

固定公式ケースには、初期能力と後続命令が同じmoduleへ混在する例がある。

| 静的に確認した候補 | 設計・受入への示唆 |
| --- | --- |
| [imports.wast](../../../thirdParties/WebAssembly-spec/test/core/imports.wast)の26～83行目のspectest複合module | elem、数値変換、i32.add、call_indirectを含む。現行[ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs)はelement/data等を未対応として中断するため、このmoduleだけで初期受入を構成できるとは限らない。 |
| [linking.wast](../../../thirdParties/WebAssembly-spec/test/core/linking.wast)の134行目以降のtable、314行目以降のmemory | elem/dataとguest命令を含む。前提moduleの不成立と後続register/importの依存を記録する例になる。 |
| imports.wastの3～21、383～402、495～503行目 | 補助module/register、table・memoryの単純importが初期経路の候補になる。 |
| linking.wastの3～20、39～81、291～300行目 | 関数、共有global、tableのexport/register/importを確認する候補になる。 |

上記はWASTを調査資料として静的に読んだ結果であり、実行ツールにWAST解析を追加する提案ではない。生成JSONのcommand識別へ対応付け、実CLIで要件14.3の各経路を確認する必要がある。大きなmoduleが未対応で止まることだけを理由に、後続仕様の全命令を前倒しする判断はしない。v128・参照の値処理と診断判定は、要件14.8～14.9に従ってランナーのテストでも初回に確認する。

## 実装アプローチの比較

| 案 | 内容と利点 | 制約・評価 |
| --- | --- | --- |
| A:既存処理の拡張 | READMEの変換例と既存TUnitを拡張する。既存の検証習慣を再利用しやすい。 | 既存ランナーがないため、純粋な拡張だけでは独立CLI・正式manifest・保存済みJSON操作を満たせない。ランタイムに素材管理を組み込むと責務が混ざる。単独の完成案にはならない。 |
| B:新設のみ | `tools/`にツールを新設し、現行公開APIだけへ依存する。ランタイムへの影響を抑えられる。 | ランタイム無変更では全exportの取得ができず、register要件を満たせない。独自バイナリ解析や内部アクセスによる補完は対象外 公開API追加と組み合わせる必要がある。 |
| C:新設と既存拡張の組合せ | CLIの素材生成・照合、スクリプト実行・比較、結果・baseline処理を新設し、ランタイムの既存能力を再利用する。export一覧と初期必須経路の修正だけをランタイムへ追加する。 | 要件と依存方向に最も合う。公開API変更とツールの受入を対応付ける計画が必要 設計上の有力候補とし、ここでは確定しない。 |

案Cでは次の2構成が実現可能である。

| 構成 | 利点 | 負担 |
| --- | --- | --- |
| 単一CLIプロジェクト内で責務を分ける | プロジェクト数が少なく、現在の単一入口に合う。処理は内部型に分けて直接テストできる。 | コマンド入口に変換・状態管理・比較処理を集中させない構成が必要 |
| 処理ライブラリと薄いCLIを分ける | 処理の公開契約とCLI入出力をプロジェクト単位で分離できる。 | 別アセンブリの契約と参照管理が増える。現要件は複数の呼び出し元を要求せず、テスト容易性だけで必須にはならない。 |

現時点では単一CLIを第一候補とし、素材、スクリプト、比較等の責務ごとに処理と関連データを同居させる。汎用plugin機構、別エンジン用の交換口、独自Wasm演算、入力ごとのプロセス隔離は追加しない。生成時だけWABTと公式入力を必要とし、実行・保存・比較・最終判定をそれらへ依存させない。

段階的に進める場合は、保存形式とコマンド境界、素材生成・照合、export公開契約、スクリプト状態と全値型/診断比較、結果/baseline/終了判定、公式全体受入の順で依存を整理できる。これは設計への提案であり、承認済みタスクではない。

## 工数とリスク

- **工数:XL（2週間以上の規模区分）** 新CLIだけでなく、再現可能な素材管理、状態と依存の処理、全値型比較、永続結果、用途別判定、実コマンドでの公式受入を整備するため 暫定的な相対見積りであり、日程の確約ではない。
- **リスク:High** 固定形式と既存公開APIは確認できるが、初期必須の公式ケースで必要になるランタイム修正量と全JSONの形式分布は未確定 誤分類や不完全な結果をbaselineとして保存すると、後続機能の回帰判定へ影響する。
- 診断の全件互換修正と後続命令の追加は、それぞれの仕様へ分ける。本見積りへCore 2.0全体の完成を含めない。

## 設計へ持ち越す調査・決定事項

| 項目 | Research Needed／決める内容 |
| --- | --- |
| exportの公開契約 | 名前・種類の記述情報か実体を含む返却型か、一覧の所有と順序、既存Get系APIとの関係を決める。structure.mdの名前取得方針との整合を取る。 |
| 初期必須の公式経路 | 14.3のホスト関数・共有global・memory/table・registerを具体的な生成JSONのcommandへ対応付ける。実行を通じて本仕様で必要な修正と後続へ渡せる不一致を判別する。 |
| 固定JSONの全形式 | 全147入力の生成JSONを読み、command種別、値付き/型のみexpected、lane型、参照形式の分布を確認する。固定WABTの`WriteActionResultType`には複数結果を区切るカンマが見当たらないため、該当形式が固定スイートに出るか確認する。現時点で変換不能や阻害要因とは断定しない。 |
| 配置root変更と生成条件 | 安定した相対入力path・出力名・区切り文字を定め、同条件再生成と配置rootだけを変えた生成の一覧/hashを実測する。絶対配置情報を比較条件へ混入させない。 |
| JSONとhashの契約 | manifest/実行結果/比較結果の構造、hash方式、path基準、command順序の基点、詳細と集計・全対象の完了情報の対応を決める。参照の欠落や部分出力を識別する。 |
| command状態と例外の発生元 | 直近module・識別子・再registerの更新単位、失敗状態と原因の連鎖、InspectImports失敗、spectest callback由来例外の扱いを決める。共有リソースの副作用を維持する。 |
| CLIと検証への組込み | コマンド名、引数、非0終了値、結果保存先を決める。ランナーテストをsolutionへ追加し、必要な公式生成・実行手順と既存CIの接続範囲を整理する。既知failedが残る初回受入を終了0へ変更しない。 |

詳細な試作・全件実行は設計および実装の検証へ引き継ぐ。次の段階は要件承認を確認したうえでの`$kiro-spec-design test-suite-runner`である。

## 設計調査（2026-09-27）

### 調査範囲と参照したスキル

- ユーザーの`kiro-spec-design test-suite-runner -y`に従い、現行要件を承認対象として設計を作成する。既存の未コミット変更を前提とし、古いconformance-runnerの7分類へ戻さない。
- `kiro-spec-design`に従い、各機能が担う範囲を明確にしてから調査結果を設計へまとめ、レビューを行った。`codebase-design`は、呼出し側が知る必要のある操作を絞り、用途のない汎用adapterを作らない判断に使用した。
- `csharp-conventions`は不変内部モデル/永続DTOの分離、機能ごとの配置、既存TUnitと命名規則へ使用した。domain.md、CONTEXT、核心steering、ADR0001/0003/0012と隣接契約を参照した。
- 公開APIと固定WABTの出力形式を別々のサブエージェントで調べた。両者は調査のみを行い、結果の取りまとめと文書の編集は主担当が行った。

### 公開APIとregisterの接点

**根拠:** [WasmInstance](../../../src/WasmSharp/WasmInstance.cs)、[WasmImportInfo](../../../src/WasmSharp/WasmImportInfo.cs)、[WasmHostModule](../../../src/WasmSharp/WasmHostModule.cs)、[WasmImports](../../../src/WasmSharp/WasmImports.cs)、[WasmValue](../../../src/WasmSharp/WasmValue.cs)

- exportの内部一覧はあるが公開列挙はない。追加は`ImmutableArray<WasmExportInfo> GetExports()`と名前・Kindを持つrecordに限定する。実体は既存Get系APIを使う。
- 現行の外部値wrapperはinternalであり、公開`WasmExternalValue`型は存在しない。wrapperを公開する必要はない。globalのregisterには値を返すGetGlobalではなくGetGlobalResourceを使う。
- WasmImportsは重複を拒否するため、登録名→現在の提供元をツールが保持し、Instantiateごとに再構成する。再registerは登録名全体の置換とする。
- InspectImportsの失敗とWasmの4段階は別の観測である。操作名を`InspectImports`として保存し、Location.Stageを上書きしない。UnsupportedFeature以外の取得失敗もそのReasonを残す。
- 値の全7型とv128の上下64bit、f32/f64のBits、参照の構築/取得は既存公開APIで足りる。ランナー専用hookは不要

**設計への反映:** GetExportsの追加と既存の名前による実体取得を組み合わせる。structure.mdは実装時に同期する。ランタイムの実体/型照合は再実装しない。

### 固定JSONの全形式と生成実験

**根拠:** 固定WABTの[JSON writer](../../../thirdParties/wabt/src/binary-writer-spec.cc)、[wast2json入口](../../../thirdParties/wabt/src/tools/wast2json.cc)、[feature.def](../../../thirdParties/wabt/include/wabt/feature.def)、[binary writer option](../../../thirdParties/wabt/include/wabt/binary-writer.h)

設計の不確定事項を解消するため、既存の固定exeを使い全147入力を2回変換した。2回目は入力WASTを相対配置を保って一時ディレクトリへコピーし、出力rootも変更した。入力をcore rootからの`/`区切り相対pathで渡し、作業ディレクトリをcore rootとした。上流ソースは変更していない。

| 確認項目 | 今回の実測 |
| --- | --- |
| spec/WABT | 固定HEADはそれぞれ`05ca4182176763112561ae20153975c12bd689e4`、`03a00a1334e6121fb0cce4fccbd6bb109b68acaa` |
| 使用exe | 既存`artifacts/wabt-core2/Release/wast2json.exe` SHA-256=`b0e1d0316a265f659566a0e3b6f1a00fea9e315e3d25f2c3e0853e414d0be245` 今回は再ビルドしていない。 |
| 変換 | 両配置とも147/147成功 JSON147、wasm4,597、wat1,077の計5,821生成物 |
| 配置変更 | 生成物の相対一覧と全SHA-256が一致 source_filenameとmodule参照を含めJSON内容の書換えなし |
| JSON読取 | 147/147成功、合計53,907command |
| 懸念していた複数型のみexpected | 出現0件 writerのカンマ出力に関する静的懸念は、現在の固定スイートの阻害要因ではない。 |
| 固定集合外の形式 | 非nullfuncref値、either、assert_exception、exnrefは出現しない。 |

| command種別 | 件数 |
| --- | ---: |
| module | 1,600 |
| register | 19 |
| action | 155 |
| assert_return | 45,636 |
| assert_trap | 2,408 |
| assert_exhaustion | 15 |
| assert_malformed | 1,813 |
| assert_invalid | 2,144 |
| assert_unlinkable | 83 |
| assert_uninstantiable | 34 |

`module_type=text`はassert_malformed内の1,077件 通常moduleにはmodule_typeがなく、binaryとして扱う。action/assert_trap/assert_exhaustionのexpectedは型だけの配列で、v128もlane_typeを持たない。assert_returnの値付き期待値と別モデルにする。

v128はi8/i16/i32/i64/f32/f64の全lane形式が現れる。funcrefは引数null6件、期待null7件、型だけexpected2件 externrefはnullまたは番号と型だけexpectedが現れる。WABTの一般形式の非nullfuncref数値は関数indexではなく非nullパターンの符号化であるが、今回の固定集合にはなく実装を追加しない。

試験出力と集計は一時領域`C:/Users/taihe/AppData/Local/Temp/wasmsharp-runner-design-20260927-142230/`の`summary.json`、`inventory.json`、`a/`、`b/`に保存した。正式manifest、公式run結果、永続baselineではない。一時出力が失われても再調査できるよう条件と件数を本節へ記録する。素材の変換・JSON読取はWasmSharpによる実行成功を意味しない。

**設計への反映:** 入力は相対pathで指定し、出力ファイル名とprofileを固定する。feature引数なし、検証有効、canonical LEB有効、relocatable無効、debug names無効を記録する。固定WABTではON7/OFF14が既定値と同じである。各機能にenable/disableの両方の引数があるとは限らないため、使用できる引数を固定ソースで確認する。

### 初期必須の公式経路

固定WASTの調査に加え、上記生成JSONの0始まりcommand番号へ対応付けた。WasmSharpでの実行は未実施である。

| 観点 | ケース |
| --- | --- |
| spectest関数呼出し | `imports.wast#6～7`、`start.wast#15,#16,#17` 後続の数値演算・segmentが不要な呼出しを含む。 |
| 関数register・再export | `linking.wast#0～6` |
| 共有mutable global | `linking.wast#11～28` #24の更新後に両instanceから値を確認する。 |
| spectest数値global | `imports.wast#41～45` |
| tableのregister/import | `imports.wast#0,#1`から`#82～93` |
| memoryのregister/import | `imports.wast#0,#1`から`#127～129` |
| spectest table/memory | `imports.wast#94～101,#130～135` |

固定参照[run.ml](../../../thirdParties/WebAssembly-spec/interpreter/script/run.ml)ではregisterは登録名全体を置換する。通常module成功時は識別子と直近moduleを更新し、否定moduleは更新しない。失敗後に古い成功へ戻さず原因を保持する点は、要件5.7の継続実行用の契約である。

参照[spectest.ml](../../../thirdParties/WebAssembly-spec/interpreter/host/spectest.ml)はmemory/tableを共有し、関数/globalをlookup時に生成する構成だが、本仕様は要件7.3に従い全外部要素を入力ごとに1回生成して共有する。固定値・型・limitsは参照と合わせる。

### 標準機能の採用調査

- [System.Text.JsonのDOM](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/use-dom): 標準の読取専用DOMとreaderを使用できる。外部JSONのcommand境界の読取と、型付きモデルへの変換を分ける。
- [ProcessStartInfo.ArgumentList](https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.argumentlist?view=net-10.0): 引数をリストとして渡せる。空白・引用符を含むpathをシェル文字列へ連結しない。
- `.NET 10`とTUnitは既存プロジェクトを再利用する。単純な6コマンドに追加CLI frameworkを導入する必要はなく、WAST変換には自作parserでなく固定WABTを採用する。

## アーキテクチャ案の評価

| 案 | 利点 | 制約 | 判断 |
| --- | --- | --- | --- |
| READMEスクリプトと既存TUnitだけを拡張 | 既存資産を使える | 保存済み素材だけの独立実行やbaselineコマンドを一貫して提供できない | 不採用 |
| 独立CLI内に機能別モジュール | 依存方向を保ち、追加project・公開契約を絞れる | 入口へ状態や比較を集中させない構造が必要 | 採用 |
| 別処理ライブラリ＋CLI＋交換可能engine | 呼出し元を増やしやすい | 現要件に第2の呼出し元/engineがなく、公開契約と抽象化が増える | 不採用 |

## 設計判断

### 共通化するのは観測と比較の契約

- **課題:** 同じ値・段階を複数のcommandで判定する。
- **採用:** invoke/getの値処理をValueCodec/ValueMatcherへ、公開操作の観測をCommandObservationへ、判定をAssertionJudgeへ集める。引数と期待値、観測と結果分類を別の型にする。
- **理由:** scalarとv128 laneでNaN判定を共通化でき、診断の厳密さを一箇所で維持できる。
- **棄却:** commandごとの独立した値比較、ランタイムの演算の再実装、将来engine向け汎用interface

### 一覧は記述情報、実体は名前で取得

- **課題:** registerに全exportが必要だが、既存方針は名前による実体取得
- **採用:** GetExportsは名前とKindだけを宣言順で返す。既存Get系で実体を取得する。
- **理由:** 内部indexや型別wrapperの公開なしに通常利用へ意味のある機能を追加できる。
- **影響:** 実装時にstructure.mdと公開APIのテストを同期する。新しいExports propertyは不要

### 固定profileと、保存済みJSONだけで比較できる結果

- **課題:** run/比較/verifyで元WASTや元配置を要求せず、入力集合の欠落を検出する。
- **採用:** 固定入力一覧/hashと全feature条件をprofileに保持する。manifestには全対象の変換状態、RunReportには素材出典のスナップショットを保持する。
- **理由:** 部分生成物だけを全体として扱わず、比較対象条件を保存JSONのみで確認できる。
- **影響:** profileの変更は明示的な再生成・変換比較・baseline保存で確定する。日時や絶対pathを同一性へ含めない。

### 結果の完了と合格を別々に判定

- **課題:** 初回はfailedを記録しつつbaselineを保存する必要がある。
- **採用:** 入力処理、command列挙、未処理、件数未確定、出力成功を分類と別に持つ。比較元がpassedだったケースの分類変更/欠落を回帰にする。
- **理由:** 完了した失敗結果を保存できる一方、未確定・部分出力をbaselineや最終合格へ混入させない。
- **影響:** 既知failedが残るrun/compare-runは非0のまま 初回受入に合格することとコマンド終了0を同一視しない。

### 単純化と実装順序

- 標準BCL、1CLIプロジェクト、機能別のinternal具象型を使う。処理用別ライブラリ、DIコンテナ、汎用plugin、外部DB、常駐監視は不要
- 保存契約とCLI境界、固定素材と照合、export公開契約、command状態と値/診断判定、baseline/完了判定、全体公式受入の順に実装可能な境界を設ける。
- 公開export追加と素材処理は独立して着手できる。統合前にReportStoreのschemaとScriptCommandの型を共有契約として確定する。

## 設計上のリスクと確認方法

- **公式初期経路のランタイム問題:** 全体run内の具体commandを確認し、経路を妨げる問題は本仕様で修正する。判定できたfailedの解消は後続へ渡す。
- **日本語診断と公式textの不一致:** 正規化せずfailedとして保存する。前方一致・不一致・補助説明のテストで判定自体を検証する。
- **比較の誤成立:** 全入力/素材の同一性とケース一意性・記録完了を別々に検査する。欠落したpassedは回帰と未完了の両方で記録する。
- **捕捉不能な中断:** 要件どおりプロセス隔離・強制タイムアウト・再開を作らない。保存可能な中断だけ部分記録し、正常完了を主張しない。
- **外部形式の将来変更:** 固定JSONで未出現の形式を推測実装しない。WABT/spec更新時に全体変換と形式棚卸しを再実施する。

## 設計レビューゲート

- 機械確認: 全111受入基準をtraceabilityへ111件対応付け、欠落・余分・重複は0 責務境界、境界外、許可依存、再検証の契機、具体的ファイル計画を確認した。主要モジュールのファイル漏れ、プレースホルダー、文書内のローカルリンク切れも0
- 独立したレビューで、未知commandの集計先と、JSONを読めることと全件の記録が完了していることを分ける必要が指摘された。前者はcategory=nullの「種類未確定command」としてrunner_error件数を全体へ加える。後者は読取可能な不完全結果をCompareRunへ渡し、以前passedだったケースの欠落を回帰と未完了の両方で記録するよう修正した。
- 1回の修正後、同じレビュー担当が変更箇所とその影響を再確認し、最終GOと判定した。要件の見直しや追加修正が必要な指摘は残っていない。機械確認も再実行し成功した。
- このGOは設計のゲートである。CLI実装、.NETビルド/TUnit、WasmSharpによる公式実行、正式baselineの保存・比較、Core 2.0全件合格の証拠ではない。
