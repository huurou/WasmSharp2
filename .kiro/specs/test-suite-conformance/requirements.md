# 要件文書

## はじめに

WasmSharp2の利用者が通常の公開APIからWebAssembly Core 2.0の期待どおりの動作・結果・失敗を得られるよう、公式テストスイートで判明した先行ランタイムの不一致を修正するための要件を定める。診断互換性に加え、Decode・Validate・Instantiate・Invokeの動作、値、状態、失敗分類の不一致を扱い、調査と再実行で修正対象を具体化する。

2026-10-01の初回公式受入では、全147入力・53,907commandを記録し、passedは1,547件、failedは944件だった。必須66ケースは全passed、入力単位・command単位のrunner_errorは0だった。944件はすべて公開診断の前方一致が不成立であり、処理段階・公開例外型の一致だけでは、参照実装と同じ原因を選んだとまでは確認できていない。これらは[引継ぎ文書](handoff.md)に記録された時点の観測であり、修正後の検証結果ではない。

本書の入力は[ブリーフ](brief.md)、[引継ぎ文書](handoff.md)、[診断不一致の全件一覧](diagnostic-groups.json)とする。用語は[CONTEXT.md](../../../CONTEXT.md)、仕様間の順序と完成目標は[ロードマップ](../../steering/roadmap.md)、公開診断の契約は[ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)に従う。受入基準は「要件番号.項番」で参照する。「適合検証担当者」は、本仕様の修正結果を確認し、受入を行う開発者を指す。

## 対象範囲と隣接仕様

| 区分 | 範囲と前提 |
| --- | --- |
| 対象となる動作 | `runtime-foundation`と`host-linking`が提供するバイナリ解析、型検証、関数実行、リンク、リソースの状態・共有、start、trap・exhaustionで判明した実装不具合 |
| 対象となる診断 | バイナリの`assert_malformed`・`assert_invalid`、`assert_unlinkable`、`assert_uninstantiable`、`assert_trap`、`assert_exhaustion`が観測する公開診断の先頭と原因選択 |
| 上流への期待 | `test-suite-runner`で実装済みの公開API実行、spectest・register、期待値比較、分類、記録、終了コード、baseline比較・保存を利用する。初期必須の公式ケースを実行・判定できるまでの修正は、ランタイム側も含めて同仕様の範囲とする。 |
| 本仕様に集約する不一致 | 公式判定で得たfailedと、初期必須の実行経路を妨げずランタイム側の原因が確認されたrunner_error。既知944件に限定せず、再実行で判明した先行機能の不一致を追加する。 |
| 後続仕様への期待 | `numeric-control`、`linear-memory`、`tables-references`、`simd`は、自身が追加する機能と診断を同じ判定基準で受け入れる。本仕様の完了をこれらの新規実装へ依存させない。 |
| 対象外となる実装 | ランナーの機能追加・判定変更、別ランナー、数値・構造化制御、memory/table命令、data/element初期化、参照、SIMDの先行実装 |
| 対象外となる互換性 | WAT・WASTの構文解析、Core 3.0や別版への診断互換性、補助説明を含む診断の全文一致、一般的なホスト例外やAPI誤用のメッセージ統一。`module_type=text`は従来どおりout_of_scopeとする。 |

要件2〜6のランタイム動作は先行機能の範囲に適用する。後続機能が前提となるケースは要件7.6で扱い、未実装機能を無視して先の処理を合格させない。参照する仕様と診断は[固定したCore 2.0公式素材](../../../thirdParties/README.md)と同じspec commitに限定し、別版の参照実装や期待値を混在させない。要件4〜6の個別診断条件は、要件3.4に従って失敗原因を選択した結果に適用する。内部構造、検査手順、修正箇所の分割は設計段階で定める。

本仕様の診断互換性は、固定公式ケースと固定版の参照実装から確認した診断選択の規則を、通常の入力と実行状態にも共通して適用する範囲とする。完了確認は固定スイート全体と、修正した規則の境界条件・複合不正を確認するテストで行う。任意入力に含まれるあらゆる不正の組合せについて、固定参照実装との完全互換を保証することは本仕様の完了条件に含めない。

## 要件

### 要件1: 実行結果に基づく修正対象の追跡

**目的:** 開発者として、公式ケースの観測結果と原因を追跡し、必要なランタイム修正とその確認範囲を漏れなく定めたい。

#### 受入基準

1. When 公式スイートの不一致を本仕様の修正対象へ追加する場合, the 適合検証担当者 shall 元入力の相対pathとcommand indexに、期待結果、実際の結果、処理段階、公開診断を対応付けて記録する。
2. When 不一致の原因を調査する場合, the 適合検証担当者 shall 固定版の仕様規則・公式入力・参照実装を根拠に、判明した原因、必要な修正、確認方法を記録する。
3. If 再実行で先行機能の新たな動作・値・状態・失敗分類・診断の不一致が判明した場合, then the 適合検証担当者 shall 既知944件の一覧に含まれない不一致も本仕様の修正対象へ追加する。
4. If 調査した問題がランナー自体の不具合または後続機能の新規実装に属する場合, then the 適合検証担当者 shall 原因と該当仕様を記録してその仕様へ引き継ぎ、分類の付け替えによって本仕様の不一致を解消扱いにしない。
5. When 診断不一致の一覧を修正対象へ整理する場合, the 適合検証担当者 shall 102組の観測診断と調査で判明した根本原因を区別し、ケースから修正・確認結果を追跡できるようにする。

### 要件2: 公開動作と失敗分類の維持

**目的:** 利用者として、診断を含む修正後も、通常の公開APIからWasmの結果と失敗原因を区別して扱いたい。

#### 受入基準

1. When 先行機能で必要な前提が揃う公式ケースを公開APIで処理する場合, the WasmSharp2 shall Decode・Validate・Instantiate・Invokeの各段階で、既存ランナーの公式比較規則に従い、値の型・個数・数値表現・参照の同一性と、観測対象の状態を公式期待値に一致させる。
2. When importした関数・リソースを複数instanceから利用する場合, the WasmSharp2 shall 同じ実体の共有と共有状態の観測を維持し、moduleで定義したリソースはinstanceごとに独立させる。
3. If 入力の破損、検証不成立、リンク不成立、trapまたはexhaustionが生じた場合, then the WasmSharp2 shall 発生した処理段階に対応する既存の公開例外型と失敗分類を維持する。
4. If 未実装機能、ホストcallbackの例外、一般的な資源不足または実装上限により処理を継続できない場合, then the WasmSharp2 shall その失敗を期待されたWasmの破損・検証不成立・リンク不成立・trap・exhaustionへ読み替えない。
5. If ホストcallbackが例外を送出した場合, then the WasmSharp2 shall その例外の型と実体を保って呼び出し元へ伝播する。
6. If Validateが途中で失敗した場合, then the WasmSharp2 shall moduleを検証成功状態にせず、そのmoduleのInstantiateを許可しない。
7. When InspectImportsが成功した場合, the WasmSharp2 shall 完全なimport情報と未確認範囲を返す既存の契約を維持し、その成功をmodule全体の検証成功として扱わない。

### 要件3: 公開診断の前方一致と原因選択

**目的:** 利用者として、通常の公開例外から、固定公式期待診断と同じ失敗理由を確認したい。

#### 受入基準

1. When 対象の否定assertionが期待する失敗を公開APIから通知する場合, the WasmSharp2 shall 公開例外のMessageを対応する固定公式期待診断から始め、`Message.StartsWith(expectedText, StringComparison.Ordinal)`を成立させる。
2. When 期待診断に大文字小文字・空白・数値が含まれる場合, the WasmSharp2 shall それらを含む期待文字列を変更せず公開診断の先頭へ一致させる。
3. When 公開診断に日本語の説明・位置・添字・実際の値などの補助情報を付ける場合, the WasmSharp2 shall 公式期待診断の後ろへ付け、位置や独自の接頭辞をその前へ挿入しない。
4. If 同じ入力に複数の不正が含まれる場合, then the WasmSharp2 shall 固定公式ケースと固定版の参照実装から確認した診断選択の規則を通常の入力にも共通して適用し、対象の公式ケースでは参照実装と同じ不正を公開診断の原因に選ぶ。
5. When 公開例外にReason・Location・import識別情報・実行上限情報を伴う場合, the WasmSharp2 shall 選択した失敗原因と処理段階に一致する情報を保持し、診断文との矛盾を生じさせない。
6. The WasmSharp2 shall 通常の入力と実行状態から公開診断を決定し、公式JSON・期待文字列・ケースIDの参照やテスト専用の呼び出し経路を必要としない。

### 要件4: バイナリ解析の診断互換性

**目的:** 利用者として、不正なバイナリのどの問題がDecodeの失敗原因になったかを、固定公式期待診断と一致する形で把握したい。

#### 受入基準

1. If バイナリ内の名前に不正なUTF-8符号化が含まれる場合, then the WasmSharp2 shall Decodeの失敗として`malformed UTF-8 encoding`から始まる診断を通知する。
2. If 整数の符号化が許容する表現長または値の範囲に違反する場合, then the WasmSharp2 shall 固定版の入力条件に応じて`integer representation too long`と`integer too large`を区別したDecode診断を通知する。
3. If ヘッダーのmagicまたはversionが不正、あるいはヘッダー途中で入力が終わる場合, then the WasmSharp2 shall 不正の内容に応じて`magic header not detected`、`unknown binary version`、`unexpected end`を選択する。
4. If section・import kind・mutability・limits・長さ・local数・function/code件数の符号化またはバイナリ構造が不正な場合, then the WasmSharp2 shall 対応する固定公式ケースの期待診断から始まるDecode診断を通知する。
5. If 式の終端やsectionの境界が不正な場合, then the WasmSharp2 shall 入力位置と後続バイトに応じて固定版の参照実装と同じ診断を選び、`END opcode expected`、`unexpected end of section or function`、`section size mismatch`を一律の終端エラーへまとめない。
6. When 破損または途中で終わるバイナリを処理する場合, the WasmSharp2 shall 入力とsectionの範囲検査を維持し、診断互換性のために範囲外の読取りや不正な長さの受入を許可しない。

### 要件5: 型・添字・構造の検証互換性

**目的:** 利用者として、デコードできたmoduleの型や構造が不正な理由を、Validateの公開診断で把握したい。

#### 受入基準

1. If 命令の入力値・関数の結果・初期化式の結果が必要な型または個数を満たさない場合, then the WasmSharp2 shall 対応する固定公式ケースの`type mismatch`から始まるValidate診断を通知する。
2. If function・global・local・memory・table・typeの添字が参照先を持たない場合, then the WasmSharp2 shall 対応する種類の`unknown`診断を通知し、期待診断に添字の数値がある場合はその数値も一致させる。
3. If export名が重複している場合, then the WasmSharp2 shall `duplicate export name`から始まるValidate診断を通知する。
4. If 定数式の制約、globalの可変性、startの関数型に違反する場合, then the WasmSharp2 shall 対応する`constant expression required`、`global is immutable`、`start function`のValidate診断を通知する。
5. If リソースの最小値と最大値の関係、memoryのページ上限またはCore 2.0で許されるmemory数に違反する場合, then the WasmSharp2 shall 対応する固定公式ケースのlimits・memoryに関するValidate診断を通知する。

### 要件6: リンクと実行時の診断互換性

**目的:** 利用者として、importの接続失敗と、start・関数実行中の失敗を、段階と原因を保った公開診断で区別したい。

#### 受入基準

1. If 必要なimportが提供されていない場合, then the WasmSharp2 shall `unknown import`から始まる診断を持つWasmInstantiateExceptionを通知し、ReasonをMissingImportとして維持する。
2. If 提供された外部要素の種類または型がimport宣言の要求に適合しない場合, then the WasmSharp2 shall `incompatible import type`から始まる診断を持つWasmInstantiateExceptionを通知し、種類の不一致と型の不一致をKindMismatchとTypeMismatchで区別する。
3. If startの実行でunreachableに到達した場合, then the WasmSharp2 shall `unreachable`から始まる診断を持つWasmTrapExceptionを通知し、Instantiate段階とUnreachableのReasonを保持する。
4. If InvokeまたはstartでWasmのtrapまたはexhaustionが生じた場合, then the WasmSharp2 shall 原因に対応する固定公式期待診断を公開し、InvokeとInstantiateの発生段階およびtrapとexhaustionの例外型を区別する。
5. If startの失敗前にホストがinstance・関数・リソースへの参照を取得した場合, then the WasmSharp2 shall それらの参照と失敗前に生じたリソース変更を、既存の公開契約に従って維持する。

### 要件7: 固定スイートによる完了確認

**目的:** 適合検証担当者として、診断の不一致が解消し、先行機能で実行可能な公式ケースが合格したことを全体結果で判断したい。

#### 受入基準

1. When 本仕様の完了確認を行う場合, the 適合検証担当者 shall 既存ランナーと固定profileで全147入力・53,907commandを実行し、全対象の記録・出力が完了した結果を残す。
2. When 完了確認の結果を評価する場合, the 適合検証担当者 shall ケースの欠落・重複・未処理・件数未確定がないことを確認する。
3. When 既知944件の修正を受け入れる場合, the 適合検証担当者 shall 全944件がそれぞれ診断照合込みのpassedへ変わったことをケース識別ごとに確認し、failedの件数減少だけで解消と判断しない。
4. When 本仕様を完了と判断する場合, the 適合検証担当者 shall 先行機能で前提が揃う全ケースのpassedと、全体のfailedおよび入力単位・command単位のrunner_errorが0件であることを確認する。
5. When 公式判定を用いて修正を確認する場合, the 適合検証担当者 shall 公式入力・期待値・feature設定、段階・例外型・失敗分類・診断の判定条件を維持し、期待値の正規化、ケース別除外、ランナーによるメッセージの付け替えを行わない。
6. If 完了時にruntime_unsupportedまたはblockedが残る場合, then the 適合検証担当者 shall 後続機能の新規実装が必要なケースとその依存ケースだけであることを、ランタイムが報告した未実装機能と原因commandまで追跡して確認する。
7. When 本仕様の受入結果を報告する場合, the 適合検証担当者 shall 既存のrunとcompare-runの終了0を確認し、未対応とそのblockedが残る間のverifyの非0をCore 2.0全体の未完了として区別する。
8. When 修正結果の根拠をまとめる場合, the 適合検証担当者 shall 対象のCore 2.0規則・診断選択と検証結果の対応、および修正した診断選択規則の境界条件・複合不正を確認するテスト結果を残し、個別テストだけで公式スイート全体の受入を代替しない。

### 要件8: 回帰比較とbaseline更新

**目的:** 開発者として、既存の合格を失わずに不一致を修正し、問題を解消した結果を次の比較基準として引き継ぎたい。

#### 受入基準

1. When 最初の修正結果を比較する場合, the 適合検証担当者 shall 引継ぎ資料で特定した保存済み全体結果を比較元として使用し、取得できない場合は引継ぎ手順に従って旧revisionの比較元を再作成して、条件と照合結果を記録する。
2. When 修正後の全体結果を現baselineと比較する場合, the 適合検証担当者 shall 固定profileと入力・生成物が一致する条件で、元入力とcommandごとの変更前後の結果および回帰を確認する。
3. If 以前passedだったケースが別分類へ変わるか結果から欠落した場合, then the 適合検証担当者 shall 回帰として受入を不成立にし、初回の1,547passedと必須66ケースを含む既存合格を維持する。
4. If 不一致・runner_error・回帰・比較未完了が残る場合, then the 適合検証担当者 shall 必要な修正と全体再実行・現baselineとの比較を繰り返し、問題が残る結果で現baselineを上書きしない。
5. When 全体実行と現baselineとの比較で本仕様の完了条件を満たした場合, the 適合検証担当者 shall 保存済みの結果JSONを既存の保存コマンドによる別操作で現baselineへ明示的に上書きし、その結果と比較記録を後続仕様へ引き継ぐ。
6. When baseline保存の成功を確認する場合, the 適合検証担当者 shall 保存の成功と公式結果の合格を区別し、診断不一致の一覧や修正後の結果を修正前の全体baselineの代わりにしない。
