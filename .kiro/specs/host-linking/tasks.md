# 実装計画

各小タスクは1〜3時間を目安とし、原則は記載順に進める。並列候補は2.1〜2.3、および2.4と2.5だけとし、タスク1全体と明示した依存が完了してから実施する。共通型・fixture・プロジェクト設定はタスク1で先行整備し、並列中は各リソースと専用テストだけを変更する。共有箇所の変更が必要になった場合は直列化する。

各実装タスクで正負テストを追加・更新し、Releaseビルドの警告・エラー0を確認後、対象テストをコマンド実行する。TUnitのAAA、日本語名、メソッド別クラス、await付きassertionに従い、同期contextの観測はawait前に終える。各タスクのレビューと検証結果を末尾の実装記録へ追記する。後半の受入タスクは不足する公開経路を補い、成立済みの単体テストを重複追加しない。

## 基盤

- [x] 1. 既存の検証環境と共通入力を整える
- [x] 1.1 既存ランタイムと生成器の検証前提を確立する
  - .NET 10、固定Core 2.0 spec素材、既存のランタイム・生成器・両TUnit構成を確認し、必要な復元とビルド設定を整える。
  - 生成器をビルド時依存に保ち、既存の命令契約ソース取り込みを維持する。WABTや公式ランナーを前提にしない。
  - 既存の定数返却経路を含む両テストが、警告・エラー0のReleaseビルド後にコマンドで成功する。
  - _Boundary: WasmSharp.Tests, WasmSharp.Generators.Tests_
  - _Requirements: 1.6, 12.5_

- [x] 1.2 ホスト連携の正負バイナリを構築できる入力fixtureを用意する
  - 型、4種import/export、リソース、locals、start、任意の命令バイト列を組み合わせ、既存の定数専用fixtureを維持する。
  - 不正添字・長さ・UTF-8・途中破損を補正せず表現し、非seek・short read・I/O失敗は既存fixtureを再利用する。
  - 正負入力を決定的なバイト列で再現でき、WAT/WAST解析や内部実行hookを使わない。
  - _Boundary: WasmSharp.Tests_
  - _Requirements: 1.4, 12.1, 12.4_

- [x] 1.3 リソースと外部要素の共通型・生成診断を整える
  - limits、globalの値型と可変性、4種の外部要素を設計どおり記述できるようにする。
  - limits記述では不正な大小関係も保持し、ホスト生成の契約違反とバイナリのValidate失敗を分ける。ホスト単独の資源生成に位置情報を強制しない。
  - 型情報が不変に保持され、処理段階外の実装保持上限を位置なしで表せることをテストで確認する。
  - _Boundary: WasmLimits, WasmGlobalType, WasmExternalKind, WasmImplementationLimitException_
  - _Requirements: 4.1, 5.1, 6.1, 7.8, 10.8_

- [x] 1.4 添字付き命令の共通表現を生成テストへ統合する
  - 命令のuint添字を値即値と区別して保持し、デコード済み命令と実行命令へ一貫した表現を用意する。
  - 対象命令の即値・検証規則を記述する共通情報と、生成器テストの実ソース取り込み・コンパイル入力を同時に整える。handlerのない命令はまだ対応済みとして登録しない。
  - 添字の保持と既存定数命令の互換性を確認し、通常ビルドと生成器テストが新しい命令表現で成功する。
  - _Boundary: DecodedInstruction, Instruction, ImmediateKind, ValidationRule, StackEffectKind, WasmSharp.Generators.Tests_
  - _Requirements: 1.6, 3.1, 3.2_

## 共有リソース

- [x] 2. ホストから共有リソースを生成・操作できるようにする
- [x] 2.1 (P) globalの型・可変性・現在値を管理する
  - 7種の値型について初期値と更新値の型を照合し、ビット列と参照同一性を保持する。
  - immutable更新と型違いを区別し、拒否時は現在値を変更しない。
  - ホストからの生成・取得・更新の正負テストで、同じ実体の更新と失敗時不変を確認する。
  - _Boundary: WasmGlobal_
  - _Depends: 1.3_
  - _Requirements: 4.1, 4.5, 4.6_

- [x] 2.2 (P) memoryの割当と範囲コピーを実装する
  - 65,536バイト単位のゼロ初期化領域と現在ページ数・任意最大値・バイト長を保持する。
  - ページ境界を跨ぐ読み書きで全範囲を先に検査し、末尾の長さ0を許す。4GiBの長さを単一配列のint範囲へ縮めない。
  - 読み出しコピーが後の更新に追従せず、範囲外書込みが部分変更を残さないことを確認する。
  - _Boundary: WasmMemory_
  - _Depends: 1.3_
  - _Requirements: 5.1, 5.2, 5.5_

- [x] 2.3 (P) tableの割当と参照要素操作を実装する
  - funcref/externrefの型別nullで初期化し、現在要素数と任意最大値を保持する。
  - 位置と参照型を検査し、null・非nullの参照同一性を保つ。仕様上限と配列保持上限を区別する。
  - 正常な取得・設定と、範囲外・型違い・初期保持上限による拒否をテストで確認する。
  - _Boundary: WasmTable_
  - _Depends: 1.3_
  - _Requirements: 6.1, 6.2, 6.3, 6.6, 10.8_

- [x] 2.4 memoryを既存内容を保って増大する
  - 上限と加算を割当前に検査し、成功時だけ追加ページをゼロ初期化して確定する。
  - 増大量0、成功時・false時の元サイズ、宣言・仕様上限を扱い、実割当の例外は変換せず既存状態を維持する。
  - 成功・予測可能な失敗・増大量0のテストでサイズと内容を確認する。巨大割当や実OOMを通常テストの必須条件にしない。
  - _Boundary: WasmMemory_
  - _Depends: 2.2_
  - _Requirements: 5.3, 5.4, 10.8_

- [x] 2.5 (P) tableを指定参照で増大する
  - 追加領域を指定参照で初期化し、既存要素と参照同一性を保って確定する。
  - 宣言・仕様・配列保持上限はfalseで返し、実割当例外は変換しない。増大量0でも初期参照型を検査する。
  - 成功時・false時の元サイズ、型違い時の不変更、追加要素の同一性を確認する。
  - _Boundary: WasmTable_
  - _Depends: 2.3_
  - _Requirements: 6.4, 6.5, 6.6, 10.8_

## 関数実体と提供登録

- [x] 3. 明示型の関数と名前付き提供登録を用意する
- [x] 3.1 定義関数と両形式のホスト関数を区別して保持する
  - 定義関数の所属instanceとmodule全体の関数添字を保持し、定義配列の添字と区別する。
  - ホスト関数は明示関数型とどちらか一方のcallback形式を保持し、結果を所有済みの値集合にする。取得元instanceへ所属させない。
  - 両形式を生成して型を取得でき、不正な生成引数を拒否する。既存定義関数の定数呼出しを維持する。
  - _Boundary: WasmFunction, WasmHostModule_
  - _Depends: 1.3_
  - _Requirements: 2.10, 7.9, 8.1_

- [x] 3.2 4種の提供登録と原子的な重複拒否を実装する
  - 名前は完全一致とし、空文字列を許しnullを拒否する。種類をまたぐ同名itemを拒否する。
  - 提供元追加時に対応表をスナップショットし、名前の組が1件でも重複したら全件を追加しない。同じmodule名の非重複itemは追加できる。
  - 実体をコピーせず、追加後の定義変更が既存登録へ影響しないことと、引数なし提供元の互換性を確認する。
  - _Boundary: WasmImports, WasmHostModule_
  - _Depends: 2.1, 2.4, 2.5, 3.1_
  - _Requirements: 7.1, 7.13_

- [x] 3.3 関数実体を種類別の型へ分離する
  - WasmFunctionを外部継承できない公開抽象型とし、定義関数と2形式のホスト関数をinternal sealedの具体型へ分ける。種類固有の情報を基底型から除き、各callbackを非nullableで保持する。
  - ExecutionBoundaryは具体型で分岐し、Interpreter.RunとExecutionFrameはWasmDefinedFunctionだけを受け取る。WasmInstanceの生成時にはmodule全体添字と定義添字を明示する。
  - 既存の型取得・生成時null拒否・関数参照の同一性・定数Invoke・引数不一致・実行状態復元を維持する。ホストcallback実行とinstance指定Invokeはタスク10で接続し、今回も既存の未対応拒否を維持する。
  - 内部契約テストとfixtureを新しい型へ移し、警告・エラー0のReleaseビルド後にruntimeとgeneratorの両suiteを確認する。挙動変更のないリファクタリングとして機能フラグ・作為的なREDは追加しない。
  - _Boundary: WasmFunction, Execution/WasmDefinedFunction, Execution/WasmHostFunction, Execution/WasmInstanceHostFunction, WasmInstance, ExecutionBoundary, Interpreter, ExecutionFrame, WasmSharp.Testsの関連テストとfixture_
  - _Depends: 3.1, 3.2_
  - _Requirements: 2.2, 2.10, 7.9, 8.1_

## 静的定義

- [x] 4. 外部要素とstartを実行せずに読み取る
- [x] 4.1 バイナリ共通読取を既存Decodeへ統合する
  - ヘッダー、sectionの外枠・順序、型、import記述を共有reader上へまとめ、既存Decodeから使う。
  - 構文と意味論を分け、生の添字・limits・元位置を保持する。第二のバイナリパーサーを作らない。
  - 既存の符号化・UTF-8・Stream・失敗位置のテストが同じ分類で成功する。
  - _Boundary: ModuleBinaryFormat, ModuleDecoder_
  - _Depends: 1.2, 1.3_
  - _Requirements: 1.4, 1.6, 11.1_

- [x] 4.2 4種のimport/exportとmemory/table定義を読み取る
  - 関数・global・memory・tableのimport、全種類のexport、memory/table定義を静的moduleへ保持する。
  - import宣言順、各種類の生の添字、limits、元位置を保持し、対応済みsectionの旧Unsupported期待を更新する。
  - バイト列とStreamの正負入力で静的情報を保持し、破損をDecode失敗にできる。callbackも資源割当も行わない。
  - _Boundary: ModuleDecoder, WasmModule_
  - _Requirements: 1.1, 1.4, 7.7_

- [x] 4.3 global初期化式とstartを読み取り未対応segmentを区別する
  - globalの型・初期化式とstartの関数添字を保持し、スカラー定数とglobal取得の式を構文として読む。
  - data/element/data_countを無視せず、未対応機能・位置・未確認範囲を返す。既知の構文違反を未対応へ置き換えない。
  - 対象sectionの正例と破損例、未対応segmentを持つ入力で、実行を伴わない段階別失敗を確認する。
  - _Boundary: ModuleDecoder, WasmModule_
  - _Requirements: 1.1, 1.4, 1.5, 4.2, 9.1_

## 独立したimport調査

- [x] 5. 実行可否と独立して完全なimport情報を公開する
- [x] 5.1 全sectionを走査して完全な要求型一覧を作る
  - 共通readerでtype/importを完全に読み、宣言順の名前・種類・要求型を型付きで所有する。
  - 他payloadは解釈せずスキップするが、後続sectionの外枠・重複・終端まで確認し、成功時にも未確認範囲を保持する。
  - 完全取得とimportなしを返し、未対応本体やsegmentでも取得でき、後続破損では部分一覧を返さないことを内部テストで確認する。
  - _Boundary: ImportInspector, WasmImportInspection, WasmImportInfo_
  - _Depends: 4.1, 4.3_
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [x] 5.2 import調査の公開入口と失敗診断を統合する
  - バイト列とStreamから調査を呼べるようにし、module生成・検証済み化・登録・割当・実行を要求しない。
  - 構文破損、型未解決、未対応、実装制限を位置・未確認範囲・元診断付きで区別する。I/Oと実OOMは元の例外を伝播する。
  - 非seek・現在位置・非close、null/非readable拒否を公開操作で確認し、失敗結果に一覧を公開しない。
  - _Boundary: WasmModule, ImportInspector, WasmImportInspectionException_
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 12.4_

## 宣言の検証

- [x] 6. リンク前に型と添字の整合性を検証する
- [x] 6.1 外部要素の添字空間・export・limitsを検証する
  - 各種類でimportが定義に先行する添字空間を検査し、種類を跨ぐexport名重複を拒否する。
  - memory合計1個、最小/最大の関係と仕様上限を検証し、複数tableは許可する。
  - 範囲外添字・不正limitsはValidateで拒否し、成功時だけ全体の検証状態とexport索引を反映する。
  - _Boundary: ModuleValidator_
  - _Depends: 4.2, 4.3_
  - _Requirements: 1.2, 3.2, 3.6, 7.7, 7.8_

- [x] 6.2 global初期化式とstartの型を検証する
  - global初期化はスカラー定数またはimported immutable global取得とし、宣言型と1個の結果を照合する。
  - mutable/定義global参照を拒否し、importからのv128/参照値を許す。startは定義/importいずれも有効添字かつ引数・結果0個とする。
  - start検証の正負はimport関数を使って確認し、Validateがcallback/startを実行しないことを確かめる。結果0個の定義startが検証に成功する正例は9.2で確認する。
  - _Boundary: ModuleValidator_
  - _Requirements: 1.2, 4.2, 4.3, 9.1, 9.3_

## リンクと構築

- [ ] 7. importを照合して構築済み実体を名前で取得する
- [ ] 7.1 提供登録を全件照合してリンク診断を返す
  - Instantiate開始時の登録を確定し、必要なimportだけを宣言ごとに名前・種類・型で照合する。
  - 関数型列とglobal型/可変性、memory/tableの現在サイズ・最大値・参照型を照合し、不一致を変換や自動増大で補わない。
  - 不在・種類・型不一致の診断で宣言番号と名前・位置を確認し、同名importの個別照合と余分な提供itemの無影響をテストする。
  - _Boundary: ModuleInstantiator, WasmInstantiateException_
  - _Depends: 3.2, 6.2_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.12_

- [ ] 7.2 リンク済み実体と定義リソースをinstanceへ統合する
  - 全import照合後に4種の表を構築し、定義実体だけをinstanceごとに割当・初期化する。global式の値を型・ビット列・参照同一性を保って設定する。
  - 未検証のInstantiateを拒否し、既存の提供元spanと空入力も同じ構築へ接続する。保持上限と実割当失敗をリンク失敗へ変換しない。
  - startなし入力の構築と定義の独立性・import共有を公開操作で確認する。startの実行接続は11.2まで未対応として拒否し、黙って成功させない。
  - _Boundary: WasmModule, ModuleInstantiator, WasmInstance_
  - _Depends: 2.1, 2.4, 2.5, 3.3, 7.1_
  - _Requirements: 1.3, 4.2, 4.7, 5.1, 6.1, 7.7, 7.11, 9.4, 10.8_

- [ ] 7.3 名前による4種の取得と再exportを完成する
  - 同じ対象の別名・反復取得・再exportで関数とリソースの同一性を保つ。
  - globalの現在値取得を維持し、共有実体取得を追加する。名前不在・種類違いは呼出し契約違反として拒否する。
  - 定義関数の元instanceとmodule全体添字を保ち、ホスト関数を取得元へ固定せず、全種類の取得・同一性のテストを通す。
  - _Boundary: WasmInstance_
  - _Requirements: 2.10, 4.5, 7.9, 7.10, 7.11_

## 関数実行

- [ ] 8. 引数・locals・結果をフレームで管理して実行する
- [ ] 8.1 フレームの引数・localsと複数結果の受渡しを拡張する
  - 引数先頭とoperand先頭を分け、追加localsを型別ゼロ/nullで初期化する。
  - 終了時は宣言結果だけを順序どおり返し、localsと一時値を除く。必要量の加算・保持上限を区別する。
  - 内部実行テストで0/複数引数結果、7種の初期値、呼出し間の分離を確認し、既存定数経路も維持する。
  - _Boundary: Interpreter, WasmExecutionContext, ExecutionFrame, FunctionCode_
  - _Depends: 7.3_
  - _Requirements: 2.1, 2.3, 2.8, 2.9_

- [ ] 8.2 locals操作・値の破棄・unreachableを命令宣言と実行へ統合する
  - local取得・設定・値を残す設定、最上位valueの破棄を実装する。
  - unreachableは元関数添字と位置を持つ内部trap結果とし、後続命令を実行しない。
  - 各handlerと対応する命令宣言を同時に有効化し、生成器テスト入力も追随させる。内部実行ループのテストで値の順序と同一性、設定結果、後続未実行を確認する。
  - _Boundary: InstructionSet, Interpreter, WasmSharp.Generators.Tests_
  - _Depends: 1.4, 8.1_
  - _Requirements: 2.4, 2.7, 2.9, 10.1_

- [ ] 8.3 直接callとreturnの命令宣言と単一実行ループを統合する
  - 定義関数の直接callでcalleeフレームを追加し、引数・戻り先・結果を同じ実行ループで管理する。
  - importした定義関数の元instanceを使い、return後の命令を実行しない。guest再帰にCLR再帰や公開Invokeを使わない。
  - 各handlerと対応する命令宣言・生成器テスト入力を同時に接続する。内部実行ループのテストで入れ子locals・結果順序・元instance、小さい深さ上限と終了後の深さ解放を確認する。
  - _Boundary: InstructionSet, Interpreter, WasmExecutionContext, WasmSharp.Generators.Tests_
  - _Requirements: 2.5, 2.6, 2.8, 2.10, 10.3, 10.5, 10.6_

- [ ] 8.4 global命令の宣言と所属instanceの共有実体操作を統合する
  - 実行中の定義関数の所属instanceからglobalを解決し、現在値の取得とmutable値の更新を行う。
  - 検証済み命令へ重複した型検査を追加せず、型・ビット列・参照同一性を維持する。
  - 各handlerと対応する命令宣言・生成器テスト入力を同時に接続し、内部実行ループのテストでホストとの相互更新とimport定義関数の元globalを確認する。
  - _Boundary: InstructionSet, Interpreter, WasmSharp.Generators.Tests_
  - _Depends: 2.1, 7.3_
  - _Requirements: 2.10, 4.4, 4.5_

## 命令宣言と検証の統合

- [ ] 9. 新命令をDecode・型検証・生成実行へ統合する
- [ ] 9.1 添字即値のDecodeと生成経路全体を統合確認する
  - 先行整備した命令宣言と添字表現を使って即値を読み取り、元位置とuint添字をデコード済み命令へ保持する。
  - handler署名と通常ビルド生成を維持し、8.2〜8.4で追加した宣言・handler・生成器テスト入力の組合せを確認する。共有ファイルは直列編集する。
  - 対象命令のDecodeと生成コードのコンパイルを確認し、旧Unsupported負例を本来の構文失敗へ更新する。未終端call即値と後続命令の未対応を区別する。
  - _Boundary: ModuleDecoder, InstructionSet, WasmSharp.Generators.Tests_
  - _Depends: 1.4, 4.3, 8.2, 8.3, 8.4_
  - _Requirements: 1.1, 1.4, 1.5, 3.1_

- [ ] 9.2 引数・locals・call・globalの型検査と線形化を拡張する
  - 0/1/複数引数結果と追加locals、local/globalの添字・可変性、callの入出力とreturn/endの宣言結果を検証する。
  - 検証と線形化を同一パスで行い、圧縮localsを早期に巨大展開せず、合計と保持上限を区別する。
  - 型・個数・添字の正負テストを通し、旧Unsupported期待を更新する。引数・結果0個の定義startがValidateに成功する正例もここで確認し、全体失敗時に一部関数を実行可能にしない。
  - _Boundary: ModuleValidator_
  - _Depends: 8.1, 9.1_
  - _Requirements: 1.2, 2.1, 2.3, 3.1, 3.2, 3.3, 3.6, 9.1_

- [ ] 9.3 到達不能部分の型多相性を検証する
  - return/unreachable後は関数底でのpopだけにunknownを与え、明示的に積まれた具体型を維持する。
  - 到達不能でも添字・global可変性・既知型不一致・end余剰値を検査する。
  - 多相性で成立する正例を受理し、不正local、immutable更新、具体型不一致を含む負例を拒否する。
  - _Boundary: ModuleValidator_
  - _Requirements: 3.1, 3.3, 3.4, 3.5_

## ホスト呼出しと同期再入

- [ ] 10. ホスト境界と実行コンテキストを完成する
- [ ] 10.1 両形式のcallbackへ値を渡し結果を検査する
  - 共通の内部ホスト呼出しで、callback引数を呼出し専用コピーへ移し、指定形式だけにinstanceを渡す。
  - 結果のnull・型・個数を確認してからguestを継続し、結果の所有と元のホスト例外実体を維持する。
  - 内部呼出しの正負テストで引数順序・不正結果による後続未実行・例外同一性を確認する。
  - _Boundary: Interpreter_
  - _Depends: 3.3, 8.3, 9.3_
  - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6_

- [ ] 10.2 公開呼出しへ関数種別・instance指定・context選択を統合する
  - 値引数の不一致を実行前に拒否し、instance必須hostの省略/nullを拒否する。instanceなしhostと定義関数は追加instanceを無視する。
  - 単独host呼出しではcontextを作らず、定義関数入口では元instanceの上限を選ぶ。既存contextがあれば継承し、作った入口だけが解除する。
  - 両公開呼出し形式、定義関数へのnull/別instance、単独hostからの資源操作とWasm入口を公開テストで確認する。
  - _Boundary: WasmFunction, ExecutionBoundary_
  - _Depends: 10.1_
  - _Requirements: 2.2, 2.10, 8.9, 8.10, 8.11, 10.4, 10.9, 10.10, 10.11_

- [ ] 10.3 Wasmからのhost呼出しと同期再入の保存復元を統合する
  - guestからのhost callは直前の定義関数のinstanceを渡し、frameを追加せず深さ1段を消費してfinallyで戻す。
  - 同一/別instanceへの再入は外側contextを共有し、入口frame/value/depthまで復元して内側ループを終了する。
  - 再入中のstack拡張でもcallback引数と外側localsが変わらず、内側trapをホストが捕捉した後に外側を継続できる公開テストを通す。
  - _Boundary: Interpreter, WasmExecutionContext, ExecutionBoundary_
  - _Requirements: 2.8, 8.4, 8.7, 8.8, 10.3, 10.6, 10.7_

- [ ] 10.4 ホスト往復のstack余裕と失敗診断を統合する
  - 再入入口とcallback直前でCLR stack余裕を確認し、深さ上限とは別のexhaustion理由と未計測上限を返す。
  - runtime結果だけを共通境界で例外化し、Invokeの段階・元位置を保持する。ホストからの同型例外を再分類しない。
  - 内部結果境界でHostStackLimit・Limit=null・復元を確認し、公開の直接再帰/host再入でexhaustionと独立再実行を確認する。
  - _Boundary: Interpreter, ExecutionBoundary, ExecutionResult, WasmExhaustionException_
  - _Requirements: 10.2, 10.3, 10.5, 10.6, 10.7, 10.8_

## start統合

- [ ] 11. 構築済みinstanceでstartを実行する
- [ ] 11.1 start専用入口を共通実行境界へ統合する
  - 新規contextはstart所有instanceの上限とし、既存contextがあれば共有する。start自体の余分な深さを加えない。
  - 定義/import定義/hostのstartを同じ関数呼出しへ接続し、hostにはstart所有instance、import定義には元の資源環境を使う。
  - 内部境界テストでInstantiate段階のtrap/exhaustionと、再入で既に例外化したホスト例外の元実体維持を確認する。
  - _Boundary: ExecutionBoundary_
  - _Depends: 10.4_
  - _Requirements: 9.3, 9.5, 10.2, 10.3, 10.4, 10.10_

- [ ] 11.2 構築・export公開・start実行をInstantiateへ統合する
  - 全リンク・割当・初期化とexport取得可能化の後にstartを毎回1回実行し、成功した場合だけinstanceを返す。
  - リンク/割当失敗時はstartを実行せず、start中断時は完了済み副作用や保存済み参照を戻したり無効化したりしない。
  - start中callbackから定義memoryと関数を取得でき、startなし・各start形式・再Instantiateの公開正負テストを通す。
  - _Boundary: ModuleInstantiator, WasmInstance, ExecutionBoundary_
  - _Depends: 7.2, 7.3, 11.1_
  - _Requirements: 8.8, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8_

## 公開操作の受入

- [ ] 12. 公開経路の統合受入と基盤回帰を完了する
- [ ] 12.1 関数の値受渡しと命令の組合せを公開操作で確認する
  - 0/1/複数引数結果と7種の値を往復し、NaN/v128ビット列と参照同一性を確認する。
  - local/call/return/drop/globalを組み合わせ、呼出しごとのlocals分離、元instanceの資源、return/unreachable後の未実行を確認する。
  - 公開4段階を通る正負入力が期待結果・型拒否・実trapを示し、既存の最小定数経路も成功する。
  - _Boundary: WasmSharp.Tests_
  - _Depends: 9.3, 10.4, 11.2_
  - _Requirements: 1.6, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10, 4.4, 10.1, 12.1_

- [ ] 12.2 複数instanceの共有・再exportとリンク失敗を確認する
  - 4種の別名/再exportの同一性、定義の独立性、ホストとguestのglobal相互更新、memory/tableの共有更新・増大を確認する。
  - 増大後の現在サイズを使う型照合、同名importの個別照合、提供重複の全件拒否、不在・種類・型不一致を公開操作で区別する。
  - 読み出しコピーの保持と再入後の現在memoryへの書込みを確認し、取得元に関係なく共有先で更新を観測できる。
  - _Boundary: WasmSharp.Tests_
  - _Requirements: 4.5, 4.7, 5.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.9, 7.10, 7.11, 7.12, 7.13, 12.2_

- [ ] 12.3 callbackのinstance選択・寿命・例外を公開操作で確認する
  - 両形式の登録/実行、省略/null拒否、異なる明示instanceで同じhost実体を呼ぶ経路を確認する。
  - 別instanceの定義関数経由・再export host直接・host start・C#直接の4経路で渡すinstanceを確認する。
  - 再入によるstack拡張後も引数が安定し、返却元再利用後も結果が安定し、不正結果でguestを再開せず元例外実体を伝播する。
  - _Boundary: WasmSharp.Tests_
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 12.2, 12.6_

- [ ] 12.4 contextの上限選択と中断回復を公開操作で確認する
  - 単独hostからの資源操作だけ・B直接・AからBネスト・A終了後Bを、両callback形式で確認する。
  - 異なる上限を持つinstance間の再帰/再入、host startとimport定義startで、入口上限と資源環境を分離して確認する。
  - Invoke/startの実trap、両形式のhost再入exhaustion、内側例外捕捉後の継続、中断後の独立Invoke/Instantiateが成立する。
  - _Boundary: WasmSharp.Tests_
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.9, 10.10, 10.11, 12.3, 12.7_

- [ ] 12.5 start失敗後の保存参照と副作用を公開操作で確認する
  - start前の構築/接続/初期化済み状態から、callbackがinstance・関数・リソースを保存する。
  - trapとホスト例外による中断後に共有状態と保存参照を操作し、start完了や再実行を暗黙に要求しないことを確認する。
  - 失敗したInstantiateがinstanceを返さず、保存参照による後続呼出しと完了済み副作用が残るテストを通す。
  - _Boundary: WasmSharp.Tests_
  - _Requirements: 9.2, 9.4, 9.5, 9.6, 9.7, 9.8, 12.8_

- [ ] 12.6 import情報と未対応segmentの境界を公開操作で確認する
  - 完全取得、空一覧、未対応本体/segmentの読み飛ばしと未確認範囲、未解決型、途中/後続破損を区別する。
  - 同じ入力で情報取得成功とDecode未対応が両立し、調査成功を実行可能性へ昇格させない。
  - data/element/data_countは完全処理で未対応となりstartを一度も実行せず、調査失敗は部分一覧を返さない。
  - _Boundary: WasmSharp.Tests_
  - _Depends: 5.2, 11.2_
  - _Requirements: 1.5, 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 12.1, 12.4_

- [ ] 12.7 全体ビルドと両テストsuiteで最終受入を確認する
  - Releaseビルドの警告・エラー0後、ランタイムと生成器の全suiteをコマンド実行し、変更した公開経路と基盤回帰を確認する。
  - 対象、コマンド、終了コード、passed/failed/skipped、未実施範囲を実装記録に残し、skipを成功へ加算しない。
  - 直接テストの成功を公式全件適合と表現せず、後続命令/segment、公式ランナー、実OOM・実CLR stack確認の実施有無を区別して受入結果を確定する。
  - _Boundary: WasmSharp.Tests, WasmSharp.Generators.Tests_
  - _Requirements: 1.6, 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 12.8_

## 実装記録（Implementation Notes）

タスク計画の確認: 12大タスク・41小タスク、受入基準99/99件の対応、依存関係・責務境界・実行前提を確認済み。Task Plan Review Gateと独立したTask-Graph Sanity ReviewはPASS。

実CLR stackの境界確認を行う場合は独立プロセスで実施し、通常suiteに巨大割当や実OOMを強制しない。

### 1.1 既存の検証環境（2026-09-18）

- 対象: .NET SDK 10.0.401、固定spec commit `05ca4182176763112561ae20153975c12bd689e4`、両TUnit 1.66.16、生成器のAnalyzer参照と実ソースEmbeddedResource。開始時の作業ツリーはクリーン。既存設定で成立し、プロジェクト設定の変更は不要。
- Task Brief: 既存の公開定数返却経路と生成器テストを維持し、警告・エラー0のReleaseビルド後に両suiteが成功すること（1.6、12.5）。環境確認のみで動作変更なしのためREDとfeature flagは対象外。
- `dotnet build WasmSharp2.slnx -c Release --warnaserror`: 終了0、警告0、エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.1-runtime`: 終了0、passed 441 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.1-generators`: 終了0、passed 27 / failed 0 / skipped 0。
- 未実施範囲: ホスト連携の実装、WABT・公式ランナー・公式suite、実OOM、実CLR stack。ライブラリのsmokeは既存の公開Decode → Validate → Instantiate → Invokeテストに含める。
- 整形ツール: `dotnet tool restore`と`--ignore-failed-sources`付きの再試行はSSL接続失敗で終了1。既存のNuGetキャッシュをソースとする一時設定を`artifacts/host-linking/offline-nuget.config`へ置き、`dotnet tool restore --configfile artifacts/host-linking/offline-nuget.config`は終了0。CSharpier 1.3.0とHusky 0.9.1を復元した。
- 独立レビュー: `kiro-review` APPROVED。上記buildを再実行して終了0・警告0・エラー0、両suiteを`TestResults/host-linking-1.1-review-runtime`と`TestResults/host-linking-1.1-review-generators`へ再出力し441/0/0と27/0/0を確認。`kiro-verify-completion`: TASK 1.1 VERIFIED。手動モードのためコミットなし。

### 1.2 バイナリ入力fixture

- Task Brief: 型、4種import/export、table/memory/global、locals、startと任意の命令をsection単位で組み合わせる。既知のバイト列との順序付き比較と公開定数実行で正例を確認し、生payload・宣言長・添字・終端を補正しない負例を確認する（1.4、12.1、12.4）。既存の定数fixtureとStreamを維持する。テスト基盤のみのためランタイムfeature flagは対象外。
- 変更: `HostLinkingModuleBinary.cs`と`HostLinkingModuleBinary_CreateTests.cs`。型・リソース・import/exportのsectionと生payloadを組み合わせ、uint LEB、UTF-8名、圧縮locals、任意命令を保持する。実行系・既存fixtureは変更しない。
- RED_PHASE_OUTPUT: 各テスト実行前の`dotnet build WasmSharp2.slnx -c Release --warnaserror`は終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/HostLinkingModuleBinary_CreateTests/*' --report-trx --results-directory TestResults/host-linking-1.2-red`は終了1、passed 0 / failed 1 / skipped 0（定数module期待値に対し空配列）。同コマンドの出力先`host-linking-1.2-red-resources`では終了1、passed 1 / failed 1 / skipped 0（import・リソース・startの欠落）。
- GREEN: 同focusedコマンドの出力先`host-linking-1.2-green-constant`は終了0、1/0/0、`host-linking-1.2-green-resources`は終了0、2/0/0。各実装前の失敗と実装後の成功を確認した。
- 最終確認: 対象2ファイルの`dotnet csharpier format`は終了0。Release `--warnaserror`ビルドは終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.2-runtime`は終了0、passed 448 / failed 0 / skipped 0。新規7件は既知バイト列、4種の構成、uint最大値と不正limits、UTF-8/長さ/途中破損、非seek・short readでの公開定数実行を確認。
- 未実施範囲: 新しいimport・リソース・start・localsの実行意味論は後続タスク。I/O失敗は既存ThrowingReadStreamと既存suiteを再利用。WAT/WAST解析、WABT、公式suite、実OOM・実CLR stackは対象外。
- 独立レビュー: `kiro-review` APPROVED。Release build終了0・警告0・エラー0、両suiteを`TestResults/host-linking-1.2-review-runtime`と`TestResults/host-linking-1.2-review-generators`へ再出力し448/0/0と27/0/0、各終了0。対象2ファイルの`dotnet csharpier check`と`git diff --check`も終了0。`kiro-verify-completion`: TASK 1.2 VERIFIED。

### 1.3 共通型・生成診断

- Task Brief: `WasmLimits`と`WasmGlobalType`を不変record、`WasmExternalKind`を4種のenumとして追加する。limitsの不正な大小関係も記述に残し、資源生成/Validateの検査をここへ移さない。保持上限例外の位置をnullableにし、基底`WasmException`も設計の変更一覧に従って注釈を整合させる。型情報の保持・コピー元の不変と、位置なしの原因/上限/元例外を確認する（4.1、5.1、6.1、7.8、10.8）。
- RED_PHASE_OUTPUT: 各段階の`dotnet build WasmSharp2.slnx -c Release --warnaserror`は終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmLimits_ConstructorTests/*' --report-trx --results-directory TestResults/host-linking-1.3-red-limits`はフラグOFFで終了1、0/3/0（min/max欠落）、ON後の出力先`host-linking-1.3-green-limits`は終了0、3/0/0。
- global型も同じコマンド形式でクラス`WasmGlobalType_ConstructorTests`を選択し、出力先`host-linking-1.3-red-globaltype`はOFFで終了1、0/2/0（値型/可変性欠落）、`host-linking-1.3-green-globaltype`はONで終了0、2/0/0。その後両フラグを除去し、標準のpositional recordへ整理した。
- 位置なし診断は既存の実行時動作であることを先に確認。クラス`WasmImplementationLimitException_ConstructorTests`のfocused run（出力先`TestResults/host-linking-1.3-location-baseline`）はnull抑制付きで終了0、3/0/0。今回の変更はnullable注釈と引数省略への整合で、動作変更を伴わない。最終テストでは抑制を使わずlocationを省略する。
- 最終確認: 変更8ファイルの`dotnet csharpier format`は終了0。Release `--warnaserror`ビルドは終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.3-runtime`は終了0、passed 454 / failed 0 / skipped 0（新規6件）。
- 未実施範囲: global/memory/tableの生成・操作と、バイナリ由来limitsのValidateは後続タスク。保持上限の実割当やOOMを強制しない。公式suiteと実CLR stackも未実施。
- 独立レビュー: `kiro-review` APPROVED。Release build終了0・警告0・エラー0、両suiteを`TestResults/host-linking-1.3-review-runtime`と`TestResults/host-linking-1.3-review-generators`へ再出力し454/0/0と27/0/0、各終了0。対象8ファイルのCSharpier check・diff checkも終了0。`kiro-verify-completion`: TASK 1.3 VERIFIED。

### 1.4 添字付き命令の共通表現

- Task Brief: デコード済み命令と実行命令へ、値即値と独立した`uint Index`を同じ形式で追加する。既存3引数構築は維持する。Index即値と9命令の検証/スタック効果を表すenum情報を準備し、生成器の合成宣言から実ソースをコンパイルしてhandlerへ値と添字を渡す。実ランタイムの対応済み宣言は変更せず、既存定数経路を維持する（1.6、3.1、3.2）。
- 変更: `DecodedInstruction`と`Instruction`に既定値0の第4引数とget-onlyのIndex、`ImmediateKind.Index`、9命令の`ValidationRule`/`StackEffectKind`を追加。生成器テストはDecodedInstructionもEmbeddedResourceから読み込み、従来のhandler署名を使う。付随文書としてREADMEの実ソース取り込み一覧だけを更新した。
- RED_PHASE_OUTPUT: `dotnet build WasmSharp2.slnx -c Release --warnaserror`は終了0・警告0・エラー0後、`dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/InstructionGenerator_InitializeTests/ホスト連携の命令情報を宣言する_実ソースの添字と値即値を独立してhandlerへ渡す' --report-trx --results-directory TestResults/host-linking-1.4-red`はフラグOFFで終了1、passed 0 / failed 9 / skipped 0。実ソースのコンパイルには成功し、添字の保持・handlerへの受渡しが失敗した。
- GREEN: フラグON後に同ビルドが終了0・警告0・エラー0、同focusedコマンドの出力先`host-linking-1.4-green`は終了0、9/0/0。添字uint最大値、値即値42、元位置、既存3引数構築のIndex=0を確認した。
- フラグ除去後: 変更8ソース/設定ファイルの`dotnet csharpier format`は終了0。Release `--warnaserror`ビルドは終了0・警告0・エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.4-runtime`: 終了0、passed 454 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-1.4-generators`: 終了0、passed 36 / failed 0 / skipped 0（新規9件）。
- 未実施範囲: 新命令のDecode・型検証・実行handlerはタスク8〜9。現在のInstructionSetは定数/endだけが対応済みの状態を維持し、既存の命令表テストで確認。公式suite、実OOM・実CLR stackは未実施。
- 独立レビュー: `kiro-review` APPROVED。型検証コメントの整理後、Release build終了0・警告0・エラー0。上記両suiteの出力先を`TestResults/host-linking-1.4-review-runtime`と`TestResults/host-linking-1.4-review-generators`として再実行し、454/0/0と36/0/0、各終了0。対象8ファイルのCSharpier check・diff checkも終了0。主担当が最新TRXの件数を再確認し、`kiro-verify-completion`: TASK 1.4 VERIFIED。

### タスク1の完了範囲

- 1.1〜1.4はそれぞれ独立レビューAPPROVED、完了検証VERIFIED。基盤の入力fixture・共通型・診断・命令表現を整備し、追加レビュー対応後のReleaseビルドは警告0・エラー0、ランタイム455件と生成器36件が成功（合計491、failed 0、skipped 0）。検証時だけのフラグは除去済み。
- 残り37小タスク（2以降）は未着手。ホスト連携機能全体のGO、公式Core 2.0適合、実OOM・実CLR stackの証明は今回の結果に含めない。手動モードのため`kiro-validate-impl host-linking`は自動実行せず、ステージング・コミットも行っていない。

### Claude Codeの追加レビューと対応（2026-09-18）

- ユーザーがAnthropicへの対象コード・仕様の送信を承認した後、Claude Code 2.1.274でタスク1の変更20ファイル（未追跡を含む）・差分・関連仕様とコードを読み取り専用レビューした。Read/Glob/Grepだけを許可し、編集・コマンド・Git変更・テスト実行を禁止。終了0、総合判定はAPPROVED、修正必須の指摘なし。レビュー後の20ファイルのSHA-256は開始前と一致し、Claudeによる変更なし。報告は`artifacts/host-linking/claude-review/review.md`。
- 指摘1（任意）を採用: fixtureの文字列名を符号化するとき、単独サロゲートを代替文字へ黙って置換しないよう、`UTF8Encoding(false, true)`を使用。不正UTF-16の文字列はEncoderFallbackExceptionで拒否し、不正UTF-8のバイト列は既存のraw Sectionから引き続き構築できる。
- 指摘2（任意）を採用: Globalsのtuple要素`Kind`を`ValueType`に変更し、import/exportのexternal kindとの違いを明確化。生成バイト列は変更しない。
- 指摘3（任意）を採用: 生成器テストのModuleContractsが実行契約側のWasmValueに依存するため、ExecutionContractsと同条件で読み込む旨をコメントへ追加。取り込み条件は変更しない。
- 指摘4〜6（情報）はコード変更不要: StackEffectKind/ValidationRuleの区別は既存PushI32/Constantテストが検証済み。WasmExternalKindはタスク1.3で指定された先行整備で、enumだけを写すテストは不要。位置なし診断が実行時の変更ではなくAPI形状の整合であることは既存記録と一致する。
- RED: `dotnet build WasmSharp2.slnx -c Release --warnaserror`は終了0、警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/HostLinkingModuleBinary_ExportsTests/*' --report-trx --results-directory TestResults/host-linking-claude-red`は終了1、passed 0 / failed 1 / skipped 0（EncoderFallbackExceptionを期待したが例外が発生しない）。
- 修正後: 対象3ファイルのCSharpier formatは終了0。Release `--warnaserror`ビルドは終了0、警告0・エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-claude-runtime`: 終了0、passed 455 / failed 0 / skipped 0。新規1件は単独high/lowサロゲートの拒否、既存fixtureテストは正常名とraw不正UTF-8の保持を確認。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-claude-generators`: 終了0、passed 36 / failed 0 / skipped 0。
- Claudeの判定はコード読解の証拠であり、上記build/testは主担当が別途実行した。追加修正はテスト基盤だけで、ランタイム本体・タスク2以降・公式suite・実OOM・実CLR stackは対象外。
- 修正部分の独立レビュー: `kiro-review` APPROVED、修正必須の指摘なし。`dotnet build WasmSharp2.slnx -c Release --warnaserror`を再実行し終了0・警告0・エラー0。上記両suiteの出力先を`TestResults/host-linking-claude-review-runtime`と`TestResults/host-linking-claude-review-generators`として再実行し、455/0/0と36/0/0、各終了0。対象3ファイルのCSharpier check・diff checkも終了0。主担当が最新TRXの件数を確認し、`kiro-verify-completion`: タスク1の追加レビュー対応VERIFIED。ステージング・コミットなし。

### タスク2の実行前提（2026-09-19）

- 開始時の作業ツリーはクリーン。前提1.1〜1.4と仕様の承認状態を確認。手動モードで2.1〜2.5を順に実装し、サブタスクごとに独立レビューと完了検証を行う。
- 検証対象の公開境界は設計のWasmGlobal、WasmMemory、WasmTable。ライブラリのsmokeは既存の公開Decode → Validate → Instantiate → Invokeテストに含める。guest命令・import/exportへの接続は後続タスク。
- 初回の標準Releaseビルドは終了0・警告0・エラー0。その後の復元でNU1301（NuGet SSL接続）と、サンドボックスのパッケージ保存先を参照するNETSDK1064が発生。`dotnet restore WasmSharp2.slnx --source C:/Users/taihe/.nuget/packages --packages C:/Users/taihe/.nuget/packages -p:NuGetAudit=false`は終了0。以後は`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`を使用し、成功確認後にテストを実行する。依存バージョン・リポジトリ設定は変更していない。

### 2.1 globalの生成と値の更新

- Task Brief: 7種の値型を保持し、mutable更新と取得済み値の保持、immutable・型違いの拒否時不変を公開コンストラクターとValueで確認する（4.1、4.5、4.6）。不正な型記述とnullを生成時に拒否する。
- RED_PHASE_OUTPUT: `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmGlobal_ConstructorTests/*' --report-trx --results-directory TestResults/host-linking-2.1-red-create`はフラグOFFで終了1、passed 0 / failed 1 / skipped 0。ON後の出力先`host-linking-2.1-green-create-retry`は終了0、1/0/0。先行する`green-create`はビルド失敗後に古いDLLで誤実行したもので、検証証拠から除外する。
- 契約検査追加前に、同形式でfilterを`/*/*/WasmGlobal_*Tests/*`、出力先を`host-linking-2.1-red-contract`として実行し終了1、3/6/0。検査実装・フラグ除去後の`host-linking-2.1-green`は終了0、9/0/0。各有効なテスト実行直前のReleaseビルドは終了0・警告0・エラー0。対象3ファイルのCSharpier formatは終了0。
- 未実施範囲: Wasm命令によるglobal操作、module間共有、公式suite、実OOM・実CLR stack。
- 独立レビュー: `kiro-review` APPROVED。Releaseビルドは終了0・警告0・エラー0、runtimeとgeneratorの全suiteは出力先`TestResults/host-linking-2.1-review-runtime`と`TestResults/host-linking-2.1-review-generators`で464/0/0と36/0/0、各終了0。対象3ファイルのCSharpier checkは終了0。主担当が最新TRXを確認し、`kiro-verify-completion`: TASK 2.1 VERIFIED。

### 2.2 memoryの生成と範囲コピー

- Task Brief: 65,536バイト単位のページ配列とulongのバイト長で生成し、Read/Writeの全範囲をコピー前に検査する。末尾ゼロ長、3ページを跨ぐコピー、入力・出力バッファの独立性、拒否時の転送先/memory不変を確認する（5.1、5.2、5.5）。4GiBを単一int長へ変換せず、巨大割当はテストしない。
- RED_PHASE_OUTPUT: 標準のruntime `dotnet run --no-build`に`--treenode-filter '/*/*/WasmMemory_ConstructorTests/*' --report-trx --results-directory TestResults/host-linking-2.2-red-create`を指定し、OFFで終了1、passed 2 / failed 1 / skipped 0。ON後の生成テスト3件は次の`red-copy`で全件成功。
- コピー実装前にfilterを`/*/*/WasmMemory_*Tests/*`として`host-linking-2.2-red-copy`へ実行し終了1、7/10/0。実装後の`host-linking-2.2-green-copy`は終了0、17/0/0。
- limits検査前のconstructor filter・出力先`host-linking-2.2-red-limits`は終了1、3/3/0。検査実装・フラグ除去後の全memory filter・`host-linking-2.2-green`は終了0、21/0/0。各テスト実行前のRelease `--no-restore --warnaserror`ビルドは終了0・警告0・エラー0。対象4ファイルのCSharpier formatは終了0。
- 未実施範囲: 増大は2.4、guest命令・import/exportへの接続は後続タスク。4GiBの実割当・実OOM・公式suite・実CLR stackは未実施。
- 独立レビュー: `kiro-review` APPROVED。Releaseビルドは終了0・警告0・エラー0。runtime/generatorの全suiteは`TestResults/host-linking-2.2-review-runtime`と`TestResults/host-linking-2.2-review-generators`で485/0/0と36/0/0、各終了0。対象4ファイルのCSharpier check・diff checkも終了0。主担当が最新TRXを確認し、`kiro-verify-completion`: TASK 2.2 VERIFIED。

### 2.3 tableの生成と参照要素操作

- Task Brief: FuncRef/ExternRefだけを許可し、型別null初期化、非null/null設定と参照同一性、位置・型違いの拒否時不変を公開APIで確認する（6.1、6.2、6.3、6.6、10.8）。uint最大値の宣言を受け入れ、初期要素数のArray.MaxLength超過は位置なしのWasmImplementationLimitExceptionとして割当前に拒否する。
- RED_PHASE_OUTPUT: `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmTable_ConstructorTests/*' --report-trx --results-directory TestResults/host-linking-2.3-red-create`はOFFで終了1、passed 1 / failed 10 / skipped 0。ON・生成実装後の`host-linking-2.3-green-create`は終了0、11/0/0。
- Get/Set検査実装前に同形式のfilterを`/*/*/WasmTable_*Tests/*`、出力先を`host-linking-2.3-red-elements`として実行し終了1、11/11/0。実装・フラグ除去後の`host-linking-2.3-green`は終了0、22/0/0。各テスト直前のRelease `--no-restore --warnaserror`ビルドは終了0・警告0・エラー0。対象4ファイルのCSharpier formatは終了0。
- 未実施範囲: 増大は2.5、guest命令・module間共有は後続タスク。巨大割当・実OOM・公式suite・実CLR stackは未実施。
- 独立レビュー: `kiro-review` APPROVED。Releaseビルドは終了0・警告0・エラー0。runtime/generatorの全suiteは`TestResults/host-linking-2.3-review-runtime`と`TestResults/host-linking-2.3-review-generators`で507/0/0と36/0/0、各終了0。対象4ファイルのCSharpier check・diff checkも終了0。主担当が最新TRXを確認し、`kiro-verify-completion`: TASK 2.3 VERIFIED。

### 2.4 memoryの増大

- Task Brief: ulong加算で宣言・仕様上限を割当前に確認し、追加ページの全割当後にだけページ表を差し替える。成功/false時の元サイズ、増大量0、既存内容・取得済みコピー・追加ゼロ領域・増大後の境界コピーを確認する（5.3、5.4、10.8）。実割当例外は捕捉せず既存表を保つ。
- RED_PHASE_OUTPUT: `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmMemory_TryGrowTests/*' --report-trx --results-directory TestResults/host-linking-2.4-red`はOFFで終了1、passed 3 / failed 4 / skipped 0。ON・実装後の`host-linking-2.4-green`は終了0、7/0/0。
- フラグ除去後にfilterを`/*/*/WasmMemory_*Tests/*`、出力先を`host-linking-2.4-final`として実行し終了0、28/0/0。各テスト前のRelease `--no-restore --warnaserror`ビルドは終了0・警告0・エラー0。対象2ファイルのCSharpier formatは終了0。
- 未実施範囲: 実OOM・4GiBの実割当は強制せず、失敗時の確定前不変更はコードレビューでも確認する。guest命令・module接続・公式suite・実CLR stackは未実施。
- 独立レビュー: `kiro-review` APPROVED。Releaseビルドは終了0・警告0・エラー0。runtime/generatorの全suiteは`TestResults/host-linking-2.4-review-runtime`と`TestResults/host-linking-2.4-review-generators`で514/0/0と36/0/0、各終了0。対象2ファイルのCSharpier check・diff checkも終了0。主担当が最新TRXを確認し、`kiro-verify-completion`: TASK 2.4 VERIFIED。

### 2.5 tableの増大

- Task Brief: 追加要素を指定されたnull/非null参照で初期化し、既存と追加の参照同一性を保持する。型検査はdelta=0や上限判定より先に行い、宣言・仕様・配列保持上限は割当前にfalseを返す。新配列の割当・コピー・初期化後だけ確定する（6.4、6.5、6.6、10.8）。
- RED_PHASE_OUTPUT: `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmTable_TryGrowTests/*' --report-trx --results-directory TestResults/host-linking-2.5-red`はOFFで終了1、passed 3 / failed 7 / skipped 0。ON・実装後の`host-linking-2.5-green`は終了0、10/0/0。
- フラグ除去後にfilterを`/*/*/WasmTable_*Tests/*`、出力先を`host-linking-2.5-final`として実行し終了0、32/0/0。各テスト前のRelease `--no-restore --warnaserror`ビルドは終了0・警告0・エラー0。対象2ファイルのCSharpier formatは終了0。
- 未実施範囲: 実OOMを強制せず、割当失敗時不変は確定順序と例外を捕捉しないコードでも確認する。guest命令・module接続・公式suite・実CLR stackは未実施。
- 独立レビュー: `kiro-review` APPROVED。主担当が最新TRXを確認し、`kiro-verify-completion`: TASK 2.5 VERIFIED。下記の最終状態に対するビルド・両suite・静的検査を独立レビュアーが実行した。

### タスク2の完了範囲

- 2.1〜2.5はそれぞれ独立レビューAPPROVED、完了検証VERIFIED。globalの生成・更新、memoryの生成・範囲コピー・増大、tableの生成・参照操作・増大を実装した。変更は本体3ファイル、専用テスト10ファイル、この実装記録。TDD用フラグはすべて除去済み。
- `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-2.5-review-runtime`: 終了0、passed 524 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-2.5-review-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計560件でskipを成功へ加算していない。
- 変更CS全13ファイルを明示した`dotnet csharpier check`と`git diff --check`は終了0。placeholder・一時フラグ・未実装例外の残存なし。既存の公開定数実行を含む回帰も成功。
- 残り32小タスク（3以降）は未着手。guest命令、import/export統合、公式Core 2.0適合、実OOM・4GiB実割当・実CLR stackの証明は含めない。手動モードのため`kiro-validate-impl host-linking`は自動実行せず、ステージング・コミットも行っていない。

### タスク2のClaude Codeレビューと修正（2026-09-19）

- 対象: レビュー開始時の未コミット17ファイル（すべてステージ済み、未ステージ・未追跡なし）。タスク2の本体・専用テスト・記録と、既存テスト3ファイルの変更を含む。claude-code-reviewスキルの明示実行に基づき、送信先Anthropicと対象資料・読み取り専用の範囲を伝え、Claude Code 2.1.274へ依頼した。
- 実行: `--print --safe-mode --tools Read,Glob,Grep --allowedTools Read,Glob,Grep --disallowedTools mcp__* --permission-mode dontAsk --strict-mcp-config --no-session-persistence --output-format stream-json --verbose`を使用。CLI終了0、最終resultはsuccess。17ファイルの確認一覧を含む最終回答を取得した。入力・出力はリポジトリ外の一時ディレクトリに保存。レビュー中の17ファイルのSHA-256、インデックス内容、HEADは開始時と一致し、Claudeによる編集なし。
- 指摘1を採用: 不変globalの生成でも`Value = initialValue`が更新用setterを呼び、InvalidOperationExceptionとなる。現在のコードで既存テスト2件の失敗を再現した。WasmGlobalに明示的な値フィールドを設け、生成時の型検査後は直接初期値を設定する。setterの可変性・型検査は維持し、生成時にsetterを経由しない理由をコメントに記載した。
- 指摘2は現在の検証証拠を更新する点を採用: 前回の実装完了時は値フィールドへの直接代入で、今回のレビュー開始時はsetter経由へ変更されていた。過去の検証記録は当時のコードの結果として保持し、今回の再現・修正後の結果を本節へ追記する。過去の成功件数を現行コードの証拠には流用していない。
- 指摘3は不採用: memoryの範囲外例外のParamNameをoffset/バッファ名に分ける提案は任意の診断改善。仕様は範囲全体の拒否と例外分類を要求しており、ParamNameの細分は要求していない。拒否時不変は満たしているため、追加引数や分岐は設けない。
- 指摘4は不採用: 空memoryのケースはサイズ・最大値・長さ0のRead成立を、範囲外offsetと空バッファのケースは長さ0でも拒否する境界を確認している。ゼロ初期化は非空2ページ、部分変更なしは非空バッファの別ケースで検証済み。空バッファに対するAllの自明な成立だけを根拠にテスト不足とは判断せず、重複テストは増やさない。
- 指摘5は情報として確認し変更なし: tableのuint上限は配列保持上限にも包含されるが、仕様上限と実装上限を明示しており挙動上の欠陥はない。
- Codexの追加確認: `WasmMemory_TryGrowTests.cs`の改行コードが混在し、CSharpier checkが終了1となることを再現。対象ファイルの整形のみ行い、既存のIDE0230抑制やテスト内容は維持した。
- 修正前: Releaseビルドは終了0・警告0・エラー0。下記suiteコマンドの出力先を`TestResults/host-linking-2-claude-before-runtime`と`TestResults/host-linking-2-claude-before-generators`として実行し、runtimeは終了1・passed 522 / failed 2 / skipped 0、generatorは終了0・36/0/0。不変globalの生成と更新拒否を検証する既存2テストが生成時に失敗したため、新たな重複テストは不要とした。
- 修正後: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`は終了0・警告0・エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-2-claude-after-runtime`: 終了0、passed 524 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-2-claude-after-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計560件成功。
- 対象C#全16ファイルのCSharpier checkは終了0。最新TRXと修正差分を確認し、`kiro-verify-completion`: タスク2のレビュー対応VERIFIED。Claudeの指摘は静的読解によるもので、上記build/testはCodexが別途実行した。
- 未実施範囲: タスク3以降、公式suite、実OOM・4GiB実割当・実CLR stack。修正は既存テストで再現・回復が確認できる局所変更のため、Claudeによる再レビューは行っていない。今回の修正と記録は未ステージで残し、開始時のステージ内容・HEADは維持する。

### タスク3の実行前提（2026-09-19）

- 開始時の作業ツリーはクリーン。仕様の承認、前提1.3・2.1・2.4・2.5の完了を確認し、手動モードで3.1、3.2を順に実装する。
- 検証境界はCreateHost・Type、Define・Add、および既存の公開定数Invoke。実行接続前のcallback保持と、リンク接続前の登録スナップショットは内部契約として確認する。テスト用の公開APIは追加しない。
- BUILD: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`。初回は終了0・警告0・エラー0。途中でサンドボックス内の成果物上書きがMSB3021/MSB3491で失敗したため、以後は同じビルドを通常権限で実行する。失敗後はテストを実行せず、再ビルド成功を確認してから進めた。
- TEST: CIと同じ両TUnitプロジェクトを`dotnet run --project <csproj> -c Release --no-build -- --report-trx --results-directory <出力先>`で実行する。SMOKEはruntime suite内の公開Decode → Validate → Instantiate → GetFunction → Invokeによる定数実行。依存パッケージ・生成器・プロジェクト設定は変更しない。

### 3.1 定義関数と両形式のホスト関数の保持

- Task Brief: 定義関数の元instanceとmodule全体のuint関数添字を保持し、定義・実行コードへは別の定義配列添字でアクセスする。両ホストcallback形式と明示型を生成時に保持し、type/callbackのnullをArgumentNullExceptionで拒否する（2.10、7.9、8.1）。callback戻り値は既存の所有済みWasmResultsへ変更する。
- RED_PHASE_OUTPUT: 各実行前の上記Releaseビルドは終了0・警告0・エラー0。runtimeの標準TESTに`--treenode-filter '/*/*/WasmFunction_CreateHostTests/*'`を加えた`TestResults/host-linking-3.1-red-host`はフラグOFFで終了1、passed 0 / failed 2 / skipped 0。ON・保持実装後の`host-linking-3.1-green-host`は終了0、2/0/0。
- null検査追加前の同filter、出力先`host-linking-3.1-red-null`は終了1、2/4/0。検査追加後、filterを`/*/*/(WasmFunction_CreateHostTests)|(WasmFunction_ConstructorTests)/*`にした`host-linking-3.1-red-index`は終了1、6/1/0（module全体添字を定義配列へ使ってIndexOutOfRangeException）。添字を分離後、filter `/*/*/WasmFunction_*Tests/*`の`host-linking-3.1-red-invoke`は終了1、30/2/0（未接続ホストInvokeが未対応例外でなくInvalidOperationException）。
- GREEN: 同filterの`host-linking-3.1-green`は終了0、32/0/0。一時フラグ除去後のReleaseビルドは終了0・警告0・エラー0、`host-linking-3.1-final`は終了0、32/0/0。変更CS5ファイルのCSharpier formatは終了0。
- 未実施範囲: import経由の取得・再exportはタスク7以降、ホストcallbackのInvoke/call実行接続はタスク10.1。現段階のホストInvokeはWasmUnsupportedFeatureExceptionで拒否し、callbackを実行しない。task10.1ではこの暫定拒否テストを実行契約の正負テストへ置き換える。公式suiteとfeature全体の完了検証は実施していない。
- 独立レビュー: `kiro-review` APPROVED、修正必須の指摘なし。Releaseビルドは終了0・警告0・エラー0。両suiteの標準TESTを`TestResults/host-linking-3.1-review-runtime`と`TestResults/host-linking-3.1-review-generators`へ出力し、533/0/0と36/0/0、各終了0。CSharpier check（対象CS5ファイル）と`git diff --check`も終了0。主担当が最新TRXを直接確認し、`kiro-verify-completion`: TASK 3.1 VERIFIED。

### 3.2 4種の提供登録と原子的な重複拒否

- Task Brief: WasmHostModuleの名前付きDefineとWasmImports.Addで、名前の完全一致・空名許可・null拒否・種類横断の重複拒否を実装する。提供元追加時に不変の対応表を保持し、実体はコピーしない。同じmodule名の非重複itemは合流し、重複があれば全件不追加とする（7.1、7.13）。内部の閉じたWasmExternalValue階層をWasmImports.csへ同居させ、object/castによる接続は使わない。
- RED_PHASE_OUTPUT: 各テスト実行直前の標準Releaseビルドは終了0・警告0・エラー0。runtime標準TESTに`--treenode-filter '/*/*/WasmHostModule_DefineTests/*'`を付けた`TestResults/host-linking-3.2-red-define`はフラグOFFで終了1、passed 0 / failed 1 / skipped 0。ON後の`host-linking-3.2-green-define`は終了0、1/0/0。
- filter `/*/*/WasmHostModule_*Tests/*`の`host-linking-3.2-red-contract`は終了1、8/9/0（生成時のnull名・4種のnull実体の検査不足、null名のParamName不一致）。契約検査実装後、filter `/*/*/(WasmHostModule_*Tests)|(WasmImports_AddTests)/*`の`host-linking-3.2-red-add`はAddフラグOFFで終了1、17/1/0（提供登録なし）。ON・登録実装後の`host-linking-3.2-green-add`は終了0、18/0/0。
- 同じ合成filterの`host-linking-3.2-red-merge`は終了1、22/4/0（同名moduleの非重複item合流と空提供元の再追加を拒否し、null提供元がNullReferenceException）。全件照合後に不変対応表を差し替える実装後の`host-linking-3.2-green`は終了0、26/0/0。元の提供元への後続Define、取得済みsnapshotへの後続Addの非干渉、同一実体・別種類の重複時不変も確認した。
- 一時フラグ除去後: 対象CS5ファイルの`dotnet csharpier format`は終了0。標準Releaseビルドは終了0・警告0・エラー0。同filter・出力先`TestResults/host-linking-3.2-final`は終了0、passed 26 / failed 0 / skipped 0。
- 未実施範囲: Instantiateのimport照合と登録snapshotの接続、再export、guestからの操作、callback実行・同期再入、start、公式suite。これらは後続タスクの範囲であり、今回の登録テスト成功をリンク・実行機能全体の完成とは扱わない。
- 独立レビュー: `kiro-review` APPROVED、修正必須の指摘なし。標準Releaseビルドは終了0・警告0・エラー0。対象CS5ファイルのCSharpier checkと`git diff --check`は終了0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3.2-review-runtime`: 終了0、passed 559 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3.2-review-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計595件成功。task3全体の追加はruntime35件。
- 主担当が両最新TRX・差分・レビュー判定を確認し、`kiro-verify-completion`: TASK 3.2および選択タスク3 VERIFIED。task3.1完了後の追加変更は提供登録とその専用テスト・記録に限定し、最終両suiteにはtask3.1も含む。タスク4以降は未着手。手動モードのため`kiro-validate-impl host-linking`は自動実行せず、ステージング・コミットも行わない。

### タスク3のClaude Codeレビューと修正（2026-09-19）

- 対象: 未コミット10ファイル（未ステージ4、未追跡6、ステージ済みなし）。本体はWasmFunction・WasmHostModule・WasmImports、テストはWasmFunctionのConstructor/CreateHost/Invoke、WasmHostModuleのConstructor/Define、WasmImportsのAdd、および本記録。送信先Anthropic、対象差分・内容・関連仕様とコード、読み取り専用レビューの範囲を提示し、ユーザーの明示許可を得た。
- 実行: Claude Code 2.1.274へ`--print --safe-mode --tools Read,Glob,Grep --allowedTools Read,Glob,Grep --disallowedTools mcp__* --permission-mode dontAsk --strict-mcp-config --no-session-persistence --output-format stream-json --verbose`で依頼。CLI終了0、最終resultはsuccessで全10ファイルの確認一覧を取得した。入出力はリポジトリ外の一時ディレクトリに保存。レビュー中の対象SHA-256、インデックス内容、HEADは開始時と一致し、Claudeによる編集なし。
- 指摘1（Medium）は不採用: DefineがImmutableDictionaryの値比較へ重複拒否を委ね、BCL既定の診断となる点と、将来ラッパーを値等価へ変更した場合の無言許容を問題視した。現在は閉じた参照等価のclassをDefineごとに新規生成し、同一実体・種類横断を含む重複拒否と登録不変を既存テストで確認できる。要件7.13・設計は拒否と不変を要求するが、重複時のメッセージやParamNameを規定していない。仮定の型変更に備える追加照合・診断専用テストは設けない。
- 指摘2（Low）は文書化案を採用: 2引数コンストラクターがmodule全体の添字と定義配列の添字を同一視し、将来importを接続した際に生成側の更新漏れを検知できないとの指摘。現在の生成経路はimportなしであり、別添字を保持する3引数版とそのテストもある。2引数版を削除せず、summaryとparamへ「両添字が一致する場合」の前提を明記した。importを含む生成経路の接続はタスク7.2で扱う。
- 指摘3（Low）は採用: WasmFunction.Instanceの長い1行によるCSharpier check失敗の予測を、対象CS全9ファイルのcheckで再現した（終了1、指摘は同ファイルのみ）。`dotnet csharpier format src/WasmSharp/WasmFunction.cs`は終了0で、getterを複数行へ整形した。過去の整形記録は保持し、今回の現行コードに対する検証結果を本節に記録する。
- 指摘4（Low）は採用: WasmHostModule.csのCRLFからLFへの変更で既存コメントも全置換差分となっていた。元のCRLFとUTF-8 BOMを維持する形へ戻し、意味のない差分を除去した。処理内容は変更していない。
- 指摘5（Low）は変更不要と判断: 空提供元をAddすると空のmodule項目が残り、将来のリンク診断を誤分類する可能性との指摘。空提供元の追加は許可され、要件7.2と設計はmodule/itemの解決不能を共通のMissingImportとして扱う。空のmodule項目を保持すること自体に違反はなく、先行returnは追加しない。実際のitem解決で判定する後続タスク7の責務を確認した。
- 修正前: Releaseビルドは終了0・警告0・エラー0。下記suiteコマンドの出力先を`TestResults/host-linking-3-claude-runtime`と`TestResults/host-linking-3-claude-generators`として実行し、runtimeは終了0・passed 559 / failed 0 / skipped 0、generatorは終了0・36/0/0。今回の修正はコメント・整形・改行に限るため、テストの追加・変更は行っていない。
- 修正後: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`は終了0・警告0・エラー0。その成功を確認してから下記両suiteを実行した。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3-claude-after-runtime`: 終了0、passed 559 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3-claude-after-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計595件成功、skipを成功へ加算していない。
- 上記本体3ファイルとテスト6ファイルを明示した`dotnet csharpier check`は終了0（Checked 9 files）。`git diff --check`と`git diff --cached --check`も終了0。最新TRX・修正差分と全5件の採否を確認し、`kiro-verify-completion`: タスク3のレビュー対応VERIFIED。Claudeは静的レビューのみを担当し、build/test/format確認はCodexが別途実行した。
- 未実施範囲: タスク4以降のimport照合・再export・guest操作・callback実行接続・同期再入・start、公式suite、feature全体の完了検証。挙動変更や未解決の疑義を伴わない局所修正のためClaudeによる再レビューは実施しない。修正と記録は未ステージで残し、開始時のインデックスとHEADを維持する。

### 3.3 関数実体の種類別分離（2026-09-19）

- 承認と範囲: ユーザーが公開抽象型と種類別の内部具体型への設計変更・レビュー・実装を明示依頼した。手動モードで追加タスク3.3だけを実施し、この改訂について設計・タスクの承認状態を維持する。開始時の作業ツリーはクリーン、HEADは`5bd2126f2c202af0dd2da22244397d64a67a834d`。要件とタスク4以降の実装範囲は変更しない。
- 設計レビュー: 履歴を引き継がない独立レビュアーがドラフトと現行コードを照合しGO。要件対応99/99（欠落・余分・重複0）、必須境界4節、構成要素18/18の具体パス、42小タスクの依存に欠落・循環なしを確認した。7.2と10.1は3.3へ依存する。追加のTypeテスト移動先は実装時に配置計画へ補記した。
- Task Brief: WasmFunctionの公開操作と関数の参照同一性を維持し、元instance・両添字・定義コードはWasmDefinedFunctionだけへ、各非nullable callbackはWasmHostFunction/WasmInstanceHostFunctionへ分離する。基底はprivate protectedコンストラクターを持つabstract class、具体型はinternal sealedとする。ExecutionBoundaryで型分岐し、Interpreter.RunとExecutionFrameを定義関数専用にする。共通の引数検証・定数Invoke・失敗後の実行状態復元を維持する。
- 基準確認: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`は終了0・警告0・エラー0。続く`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmFunction_*Tests/*' --report-trx --results-directory TestResults/host-linking-3.3-baseline`は終了0、passed 32 / failed 0 / skipped 0。
- RED_PHASE_OUTPUT: 非挙動変更のリファクタリングのため対象外。機能フラグや作為的な失敗テストは追加せず、既存のConstructor/TypeテストをExecution配下のWasmDefinedFunctionへ移し、CreateHostと内部fixture・frame入力を更新した。移行途中の初回ビルドは旧Typeテストの基底固有メンバー参照でCS1061の4エラーとなり、テストは実行しなかった。定義関数型へ移した後の同Releaseビルドは終了0・警告0・エラー0。
- 実装後の対象検証: `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/(WasmFunction_*Tests)|(WasmDefinedFunction_*Tests)|(Interpreter_*Tests)|(ExecutionBoundary_*Tests)|(WasmExecutionContext_*Tests)/*' --report-trx --results-directory TestResults/host-linking-3.3-focused`は終了0、passed 67 / failed 0 / skipped 0。変更CS全17ファイルの`dotnet csharpier check`と`git diff --check`は終了0。
- 独立実装レビュー: 履歴を引き継がない別のレビュアーが`kiro-review`に従って現行差分と未追跡5ファイルを直接確認しAPPROVED。修正必須の指摘なし。本体8ファイル・関連テスト/fixture9ファイル・仕様4文書の境界内であり、残存placeholder・秘密情報パターン・新規依存・公開Invokeへの内部再入なし。下記ビルドと全suiteをレビュアーが独立実行した。
- `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。成功を確認してから両suiteを実行。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3.3-review-runtime`: 終了0、passed 559 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-3.3-review-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計595件成功、skipを成功へ加算していない。
- 変更CS17ファイルのCSharpier check、`git diff --check`、`git diff --cached --check`は各終了0。主担当も両TRXの件数と現行差分を直接確認し、`kiro-verify-completion`: TASK 3.3 VERIFIED。検証後の追加編集は本完了記録だけで、CS内容は同一。ステージング・コミットは行わず、開始時のインデックスとHEADを維持した。
- 未実施範囲: host callback実行・instance付きInvoke・import/reexport接続・start・公式suite・feature全体の完了検証。既存のホストInvoke未対応拒否はExecutionBoundaryへ移し、callbackを実行せずcontextも開始しない。タスク10の実行接続を今回の型分離へ混ぜない。

### 4.1 バイナリ共通読取（2026-09-19）

- Task Brief: 既存WasmBinaryReader上のヘッダー、sectionの外枠・順序、型、import記述をModuleBinaryFormatへ集約する。生のuint型添字・limits・入力位置を保ち、構文違反だけをDecode失敗とする（1.4、1.6、11.1）。完全Decodeへのimport保持の接続は4.2、公開import調査は5で行う。
- 変更: ModuleDecoderの既存共通読取を移動し、4種のimportを型別のModuleImportとして読む。TableDefinition/MemoryDefinitionは共通import型記述として整備した。既存のWasmBinaryReader、符号化・UTF-8・Stream契約を再利用する。
- RED_PHASE_OUTPUT: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`終了0・警告0・エラー0後、`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/ModuleBinaryFormat_ReadImportsTests/*' --report-trx --results-directory TestResults/host-linking-4.1-red`は終了1、passed 0 / failed 11 / skipped 0。importフラグOFFのNotSupportedExceptionにより、正例と構文破損診断が未成立であることを確認。
- GREEN: フラグONと実装後、同ビルド成功を確認し、同filter・出力先`TestResults/host-linking-4.1-green`は終了0、11/0/0。一時フラグ除去・対象CS6ファイルのCSharpier format後もReleaseビルド終了0・警告0・エラー0。
- 独立レビュー: `kiro-review` APPROVED。上記Releaseビルド終了0・警告0・エラー0後、標準の両suiteを`--report-trx --results-directory TestResults/host-linking-4.1-review-runtime`（generator側は`host-linking-4.1-review-generators`）で実行。runtime終了0、passed 570 / failed 0 / skipped 0、generator終了0、36/0/0。
- 対象CS6ファイルのCSharpier check、`git -c core.excludesFile= diff --check`は終了0。主担当が最新TRXとレビュー判定を確認し、`kiro-verify-completion`: TASK 4.1 VERIFIED。意味論検証・import接続・実行・公開調査・公式suiteは未実施。

### 4.2 外部要素とmemory/table定義のDecode（2026-09-19）

- Task Brief: 4種import/exportとtable/memory定義を、宣言順・種類・生のuint添字・limits・元位置とともに不変な静的moduleへ保持する。バイト列/非seek・short-read Streamで同じ構文判定を行い、callbackもリソース割当も行わない（1.1、1.4、7.7）。
- 変更: ModuleDecoderから共通import型読取を接続し、WasmModuleへ定義配列をコピー保持。FunctionExportをModuleExportへ置き換え、既存Validatorとテストの添字参照だけを機械的に移行した。関数本体の診断はimport関数数を加えたmodule全体の添字を用いる。
- 段階境界: 新しい定義が旧Validatorで無視されて検証成功しないよう、WasmModule.Validate入口で未対応の定義をValidate段階のUnsupportedとして拒否する。意味論検証・資源割当・リンクは実装せず、タスク6でこの制限を対応する検証へ置き換える。
- RED_PHASE_OUTPUT: 初回ビルドのnullable診断1件を解消後、`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmModule_DecodeTests/*外部要素*|/*/*/WasmModule_DecodeTests/Import関数*|/*/*/WasmModule_DecodeTests/読取対応済み*' --report-trx --results-directory TestResults/host-linking-4.2-red`は終了1、passed 41 / failed 20 / skipped 0。filterはクラス全体61件へ展開され、新規20件がフラグOFF・旧Unsupportedで失敗した。
- GREEN: フラグONと読取・保持・段階拒否の実装後、上記Releaseビルド成功後にfilter `/*/*/(WasmModule_*Tests)|(ModuleDecoder_*Tests)/*`、出力先`TestResults/host-linking-4.2-green`で終了0、165/0/0。一時フラグ除去、定義配列のコピー保持と空section後の破損確認を追加した最終同filter（`TestResults/host-linking-4.2-final`）は終了0、168/0/0。各テスト前のReleaseビルドは終了0・警告0・エラー0。

- 独立レビュー: `kiro-review` APPROVED。Releaseビルド終了0・警告0・エラー0後、標準両suiteを`TestResults/host-linking-4.2-review-runtime`/`host-linking-4.2-review-generators`へ実行。runtime終了0、passed 590 / failed 0 / skipped 0、generator終了0、36/0/0。
- 変更CS14ファイルのCSharpier check、差分の空白検査は終了0。主担当が最新TRXとレビュー判定を確認し、`kiro-verify-completion`: TASK 4.2 VERIFIED。global初期化式/startは4.3、意味論検証・import調査・リンク・実行・公式suiteは後続タスクの範囲。

### 4.3 global初期化式・startと未対応segment（2026-09-19）

- Task Brief: 承認済みの公開WasmModule.Decode（byte列/Stream）を検証境界とする。global型とスカラー定数/global.getを含む初期化式、startの生の関数添字を元位置付きで保持する（1.1、1.4、1.5、4.2、9.1）。初期化式は実行せず、型整合・global参照制約・startの型検証はタスク6へ分離する。data/element/data_countは従来のUnsupportedと未確認範囲を維持し、先に確定したsection長・順序等の破損をDecode失敗として扱う。
- 実装: GlobalDefinitionは型・位置・所有済み命令列、StartDefinitionは生のuint関数添字・位置を保持する。既存の命令読取を式終端で戻る形に共用し、関数本体の余剰バイト検査は呼出元へ移した。global.getの添字読取は初期化式だけに限定し、関数本体のhandler・命令宣言は後続タスクまで変更しない。
- RED_PHASE_OUTPUT（global）: Release `--no-restore --warnaserror`ビルド終了0・警告0・エラー0後、標準runtimeコマンドへfilter `/*/*/WasmModule_DecodeTests/Global初期化式の定数とglobal取得*`と出力先`TestResults/host-linking-4.3-red-globals`を指定。フラグOFFで終了1、passed 0 / failed 2 / skipped 0（section.global未対応）。ON・実装後、ビルド成功と同filterの`host-linking-4.3-green-globals`で終了0、2/0/0。
- RED_PHASE_OUTPUT（start）: 同Releaseビルド成功後、filter `/*/*/WasmModule_DecodeTests/Startの生の関数添字*`、出力先`TestResults/host-linking-4.3-red-start`はフラグOFFで終了1、0/2/0（section.start未対応）。ON・実装後、ビルド成功と同filterの`host-linking-4.3-green-start`で終了0、2/0/0。
- 境界・負例: 型と結果数の意味検証をDecodeへ混ぜず、式の欠落終端・不正LEB・不正値型/可変性・未割当opcode・平坦elseをDecode失敗とする。startの重複・順序・長さと後続segment外枠の破損、参照/SIMD初期化命令と3種segmentのUnsupported/未確認範囲を確認した。global/startは後続の意味検証が揃うまでValidateで拒否し、Instantiate可能にしない。
- フラグ除去後: 変更CS18ファイルのCSharpier format終了0。同Releaseビルド終了0・警告0・エラー0後、filter `/*/*/(WasmModule_*Tests)|(ModuleDecoder_*Tests)/*`、出力先`TestResults/host-linking-4.3-final`は終了0、passed 199 / failed 0 / skipped 0。その後のCS変更はswitchのcase順整理のみで、独立レビュー時に再ビルド・全suiteを実行する。
- 独立レビュー: 履歴を引き継がないレビュアーによる`kiro-review`はAPPROVED。修正必須の指摘なし。下記の最新ビルド・両suiteは4.1〜4.3全体の同じコード状態に対して実行した。
- `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-4.3-review-runtime`: 終了0、passed 621 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-4.3-review-generators`: 終了0、passed 36 / failed 0 / skipped 0。合計657件成功。skipを成功へ加算していない。
- libraryのsmokeに相当する既存の公開`Decode → Validate → Instantiate → GetFunction → Invoke`定数経路は、上記runtime suite内のWasmFunction_InvokeTestsで確認。変更CS18ファイルのCSharpier checkと通常/cachedの`git diff --check`は終了0。
- 主担当も両最新TRX、構造化レビュー判定、変更CS18ファイルが検証時点から不変であることを確認し、`kiro-verify-completion`: TASK 4.3および選択タスク4 VERIFIED。4.1〜4.3の各レビュー完了後にチェックを更新した。開始時の作業ツリーはクリーン。ステージング・コミットは行っていない。
- 未実施範囲: タスク5以降の公開import調査、型・添字・初期化式・startの意味検証、リンク・資源割当、guest命令の実行接続、callback/start実行、公式suite、feature全体の完了検証。手動モードのため`kiro-validate-impl host-linking`は自動実行しない。新しい定義を含むmoduleのValidate未対応拒否は、タスク6の対応する検証へ置き換える。

### Definition型のフォルダ整理（2026-09-19）

- ユーザーの依頼によりGlobalDefinition・MemoryDefinition・TableDefinition・StartDefinitionを`src/WasmSharp/Modules/Definitions/`へ移動し、名前空間と参照側のusing、design.mdの対応パスを更新した。処理の変更はなく、ユーザーが分離した`Modules/Imports/`とステージ済み変更を維持した。
- 最終`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。
- ビルド成功後の`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-definitions-folder-final`: 終了0、passed 621 / failed 0 / skipped 0。
- 対象CS10ファイルの`dotnet csharpier check`は終了0。初回に検出したimport型2ファイルの改行差分と、ModuleBinaryFormatの長いエラー行の整形を解消した。テストの追加・挙動変更、生成器suiteの再実行、ステージング・コミットは行っていない。

### デフォルト引数の監査と必須化（2026-09-19）

- ユーザーの依頼により本体コードのデフォルト引数と呼出箇所を確認し、8箇所19引数のデフォルト値を削除した。対象はModuleExportのkind、WasmModule内部コンストラクターのimports/tables/memories/globals/start、DecodedInstructionとInstructionのindex、ReadInstructionsのisInitializer、WasmValue私有コンストラクターの3引数、ExecutionResult私有コンストラクターの6引数とSuccessのvalues。
- ModuleExport・WasmModule・命令型・読取モードは、追加情報の指定漏れをデフォルト値で隠さず呼出側で明示する。ModuleValidatorでInstructionへ変換する際も、暗黙の0にせずDecodedInstruction.Indexを引き継ぐ。WasmValueとExecutionResultの生成メソッドは、保持する値と使用しない欄を全て指定する。
- 残した省略には意味がある。WasmLimits.Maximumは最大値なし、Instantiate.optionsは既定の実行設定、WasmBinaryReaderは入力先頭・現在の文脈/位置、診断情報と原因例外は該当情報なしを表す。default(ExecutionResult)が空結果の成功を表す契約、ImmutableArrayのdefault正規化、値即値を持たない命令のdefaultも維持した。テストfixtureの省略値は通常のテスト条件や不正バイナリ用の上書きに使われているため維持した。
- 既存テストと生成器の動的コンパイル用コードで呼出引数を明示し、旧3引数コンストラクターの互換性だけを確認する不要なassertionを除去した。新しい挙動やテストは追加していないため、REDは対象外。
- `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。ビルド成功後に下記両suiteを実行した。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/default-arguments-audit-runtime`: 終了0、passed 621 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/default-arguments-audit-generators`: 終了0、passed 36 / failed 0 / skipped 0。両suite合計657件成功。
- 変更CS20ファイルの`dotnet csharpier check`: 終了0。未実施範囲は後続タスクの実装・公式suite・feature全体の完了検証。ユーザーのステージ済み変更とフォルダ分割を維持し、ステージング・コミットは行っていない。
- 独立レビューはAPPROVED、修正必須の指摘なし。8箇所19引数と関連テスト12ファイル、残した省略の根拠、最新TRXの621件/36件成功を直接確認した。通常/cachedの`git diff --check`は各終了0。主担当の`kiro-verify-completion`: 今回のデフォルト引数削除と既存動作の維持はVERIFIED。ビルド・テスト後の追加編集は本記録のみ。

### Claude Codeによる未コミット変更レビュー（2026-09-19）

- 対象は開始時点のステージ済み41ファイル（タスク4.1〜4.3、型のフォルダ分割、デフォルト引数削除）。未ステージ・未追跡ファイルは0。Claude Code 2.1.274を既存設定のモデルで、safe-mode・Read/Glob/Grepのみ・dontAsk・strict-mcp-config・no-session-persistenceにより実行。Anthropicへの送信対象と読み取り専用の範囲を事前に通知した。CLI終了0、最終resultは成功、41ファイル全件確認済みとの回答。レビュー中の対象ファイルとインデックスのSHA-256は開始時点から不変だった。
- 所見1（Medium、採用）: 初期化式限定のglobal.get読取が関数本体へ漏れても検知できるテストがなかった。既存の未対応命令テストに`2380`を追加し、関数本体では不完全な添字を読まずglobal.getのUnsupported・位置・未確認範囲を返すことを確認した。現在の実装は正しく、境界の回帰検知を補う修正。
- 所見2（Low、今回の修正としては不採用）: import解禁後にDecodeとValidateの診断用関数indexがずれる可能性。現在はValidate入口でimportを拒否するため到達不能。添字空間の統合はタスク6の範囲であり、今回その処理を先行実装しない。タスク6で拒否を外す際は診断位置のFunctionIndexもimportを含むmodule全体の添字に揃える。
- 所見3（Low、採用）: memory/table importの宣言位置とは別に保持する型記述位置のテストがなかった。既存入力を数えてmemory型35・table型49バイトを確定し、既存テストにassertionを追加した。
- 所見4（Low、採用）: ReadValueTypesは同じクラスのReadTypesからしか呼ばれず、移動時にprivateからinternalへ不要に拡大していた。privateへ戻した。
- 所見5（Low、不採用）: WasmSharp.csprojのProjectReferenceを複数行に戻す提案。属性値もビルド動作も変わらず、ユーザーによる既存の整形変更なので維持した。
- 所見6（Low、採用）: design.mdに移動前のModuleImport.csパスが残っていた。Imports配下へ修正し、分離した4つのimport型の配置と責務を追記した。
- 所見7（Low、採用）: 新規record型8個の引数説明が近接する既存型と揃っておらず、import宣言位置と型記述位置の区別も説明されていなかった。8型にparamコメントを追加した。ModuleBinaryFormatの全メソッドへのコメント追加は要求されておらず行っていない。
- 初回CSharpier checkでFunctionImport/GlobalImportの混在改行を検出し、当該2ファイルのformatで解消。最終の変更CS11ファイルの`dotnet csharpier check`は終了0。
- 最終`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`: 終了0、警告0、エラー0。ビルド成功後に両suiteを実行した。
- `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/claude-review-host-linking-4-runtime`: 終了0、passed 622 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/claude-review-host-linking-4-generators`: 終了0、passed 36 / failed 0 / skipped 0。合計658件成功、skipを成功へ加算していない。
- 未実施範囲: Claude自身によるビルド・テスト、Core 2.0一次資料との網羅照合、公式suite、タスク5以降の実装・feature全体の完了検証。修正は局所的なテスト・可視性・文書補完で疑義が残らないためClaudeへの再送は行わず、Codexが差分と上記検証結果を確認した。レビュー依頼文・元回答はリポジトリ外の一時フォルダに保存した。
- 最終の通常/cachedの`git diff --check`は各終了0。開始時と最終のインデックスSHA-256も一致し、ステージ済み内容・ブランチ・履歴は変更していない。採用5件の修正は未ステージの作業ツリーに残した。

### 5.1 完全なimport情報の内部取得（2026-09-19）

- Task Brief: 共通ModuleBinaryFormatで全sectionの外枠とtype/importを読み、宣言順の型付き情報を全走査成功後だけ返す。承認済みの内部ImportInspector.Inspectをテスト境界とし、公開入口と診断変換は5.2で接続する（11.1〜11.6、design「import情報取得」）。前提4.1・4.3は完了済み。開始時の作業ツリーはclean。
- 実装: WasmImportInfoの閉じたrecord階層とWasmImportInspectionを追加し、入力バッファと独立したImmutableArrayを返す。custom名はUTF-8検査し、他payloadは解釈せずDecode未確認範囲を記録する。limitsの意味検証、module生成、資源割当、実行は行わない。
- RED_PHASE_OUTPUT: `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`終了0・警告0・エラー0後、`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/ImportInspector_InspectTests/*' --report-trx --results-directory TestResults/host-linking-5.1-red-types`は終了1、passed 0 / failed 1 / skipped 0（フラグOFFによるNotSupportedException）。
- 型取得実装後、初回green-typesは配列assertionが参照比較だったため失敗。順序を確認するSequenceEqualへ修正。skipのテストを追加した`host-linking-5.1-red-skip`は終了1、1/1/0（未解釈payloadのRequireEnd失敗）。skip実装後の`host-linking-5.1-green-skip`は終了0、2/0/0。いずれも先にReleaseビルド成功を確認した。
- 一時フラグ除去・負例追加・4 CSファイル整形後、同Releaseビルド終了0・警告0・エラー0。同focusedコマンドの出力先`TestResults/host-linking-5.1-final`は終了0、passed 17 / failed 0 / skipped 0。完全取得・空一覧・全種類の未解釈payload・custom名・後続破損・型未解決・余剰バイトを確認した。
- 独立レビュー初回はREJECTED: 空の非custom payloadにDecode未確認範囲が欠落する1件を採用。空code payloadのテストを追加し、Releaseビルド成功後のfilter `/*/*/ImportInspector_InspectTests/空のcodePayload*`・出力先`TestResults/host-linking-5.1-red-empty`は終了1、0/1/0。ゼロ幅Decode範囲を残すよう修正し、custom名後の空データは従来どおり範囲追加不要とした。再ビルド終了0・警告0・エラー0後、全対象filterの`host-linking-5.1-green-empty`は終了0、18/0/0。
- 独立再レビューはAPPROVED。上記Releaseビルド終了0・警告0・エラー0後、標準runtimeコマンドの出力先`TestResults/host-linking-5.1-rereview-runtime`は終了0、passed 640 / failed 0 / skipped 0。`dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-5.1-rereview-generators`は終了0、36/0/0。4 CSのCSharpier checkと差分空白検査も終了0。主担当が最新TRXと修正後のコード・判定を確認し、`kiro-verify-completion`: TASK 5.1 VERIFIED。公開入口と失敗診断、Stream契約は5.2の範囲。ステージング・コミットは行っていない。

### 5.2 import調査の公開入口と診断（2026-09-19）

- Task Brief: 公開WasmModule.InspectImportsのbyte列/Streamを検証境界とする。成功は型付き完全一覧と未確認範囲を返し、破損・型未解決・未対応・実装上限は専用例外に分類する。Decode/Validate/Instantiate、提供登録、資源割当、callback/start実行へ接続しない（11.1〜11.6、12.4、design「import情報取得」）。
- 実装: 公開2 overload、WasmImportInspectionExceptionとreason enum、既存reader診断の変換を追加。元の例外と位置、既に読み飛ばしたpayload、失敗以降と全体のValidate未実施範囲を保持する。Streamは現在位置からshort readに対応して読み、seek/Lengthを使用せず入力を閉じない。null/非readableは引数例外、I/O・OutOfMemoryExceptionは変換しない。通常Decodeの実装は変更していない。
- RED_PHASE_OUTPUT: 各テスト前の`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror`は終了0・警告0・エラー0。標準runtimeコマンドへfilter `/*/*/WasmModule_InspectImportsTests/*`、出力先`TestResults/host-linking-5.2-red-public`を指定し、公開入口フラグOFFで終了1、passed 0 / failed 2 / skipped 0。ON・接続後の`host-linking-5.2-green-public`は終了0、2/0/0。
- 診断のRED: filter `/*/*/WasmModule_InspectImportsTests/読み飛ばしたpayloadの後が破損*`、出力先`TestResults/host-linking-5.2-red-diagnostics`は終了1、0/2/0（元のWasmDecodeExceptionのまま）。変換後、公開クラス全体の`host-linking-5.2-green-diagnostics`は終了0、4/0/0。型未解決のfilter `/*/*/WasmModule_InspectImportsTests/関数型が存在しない*`、出力先`TestResults/host-linking-5.2-red-unresolved`は終了1、0/4/0（MalformedBinary分類）。修正後の公開クラス全体`host-linking-5.2-green-unresolved`は終了0、8/0/0。
- フラグ削除後、byte列/Streamの未対応本体・3種segmentとの独立性、全4種import、同名関数importの異なる型添字、部分import後のUTF-8破損、後続破損、非seek/short read/現在位置/非close、引数例外、I/Oと模擬OOM例外の同一性を確認した。
- ビルド中に生成物への一時的なアクセス拒否（MSB3491/MSB3021）が2回あった。初回は同一コマンド再実行で成功。2回目は`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers`で終了0・警告0・エラー0。原因は断定せず、プロセス停止や権限変更は行っていない。
- 最新ビルド成功後の`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/(WasmModule_InspectImportsTests)|(ImportInspector_InspectTests)/*' --report-trx --results-directory TestResults/host-linking-5.2-final-expanded`は終了0、passed 48 / failed 0 / skipped 0。
- 実行しない検証: 巨大入力/コレクションの実割当と実OOM。現行の共有type/import readerはCore 2.0の型を全て読めるため、UnsupportedFeature変換は該当する実入力がなくコード確認とする。Core 2.0外の型を未対応扱いへ変えない。Streamの保持上限時は読取済み範囲を示し、説明に入力終端が未確認であることを残す。後続の意味検証・リンク・guest/callback/start実行・公式suite・feature全体の完了判定は今回の範囲外。
- 履歴を引き継がない独立レビュアーによる`kiro-review`: TASK 5.2およびタスク5全体統合はAPPROVED、必須修正なし。最新`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers`は終了0、警告0、エラー0。
- ビルド成功後の`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-5.2-review-runtime`: 終了0、passed 670 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-5.2-review-generators`: 終了0、passed 36 / failed 0 / skipped 0。合計706件成功。libraryのsmokeに相当する既存の公開定数実行経路もruntime suite内で確認した。
- 対象7 CSの`dotnet csharpier check`、通常/cachedの`git -c core.excludesFile= diff --check`は終了0。主担当が最新の両TRX、構造化APPROVED判定、対象7 CSのSHA-256がレビュー前後で一致することを確認し、`kiro-verify-completion`: TASK 5.2および選択タスク5 VERIFIED。5.1/5.2それぞれの独立レビュー後に完了チェックを更新した。
- ステージング・コミット・spec承認状態の変更は行っていない。手動モードのため`kiro-validate-impl host-linking`は自動実行せず、タスク6以降とfeature全体の検証は別途実施する。

### Claude Codeによるタスク5レビューの実行中断（2026-09-19）

- 対象は未コミット8ファイル（ステージ済み8ファイルと、そのうち3ファイルの未ステージのドキュメントコメント補完）。未追跡は0。送信先Anthropicと必要なコード・関連仕様の範囲を事前に説明し、Claude Code 2.1.274をsafe-mode、Read/Glob/Grepのみ、dontAsk、strict-mcp-config、no-session-persistenceで実行した。
- `claude auth status`はログイン済み・firstPartyを返したが、実レビューCLIは終了1。最終resultはis_error=true、api_error_status=401で、`OAuth access token has expired. Re-authenticate to continue.`が原因。レビュー結果・指摘は取得できておらず、レビュー完了とは扱わない。
- 実行前後の対象8ファイルとGitインデックスのSHA-256は一致。コード変更・ステージング・コミット・追加のビルド/テストは行っていない。上記の実装検証結果とは別の、外部レビュー未完了の記録である。再認証後に同じ範囲でレビューを再実行する必要がある。

### Claude Codeによるタスク5レビューと指摘対応（2026-09-19）

- ユーザーの再認証後、同じ未コミット8ファイルの最終作業ツリー（ドキュメントコメント補完と中断記録を含む）を再レビューした。Claude Code 2.1.274を既存モデル設定、safe-mode、Read/Glob/Grepのみ、dontAsk、strict-mcp-config、no-session-persistenceで実行。CLI終了0、最終resultはis_error=false、対象8ファイル全件確認済み。機能不具合・仕様退行の指摘はなく、Lowの所見3件を取得した。レビュー中の対象8ファイルとインデックスのSHA-256は開始時点から不変だった。
- 所見1（Low、採用）: 公開WasmImportInspectionExceptionへdefaultの未確認範囲を渡す経路が既存の公開入口テストでは確認されていなかった。既存の例外テスト規約に合わせてConstructorTestsを追加し、default/空配列の2件で、列挙可能な空配列への正規化とReason・Feature・Location・Message・InnerExceptionの保持を確認した。本体の現在の実装は正しく、公開コンストラクター契約の回帰検知を補う対応。
- 所見2（Low、採用）: I/O例外テストで使用するThrowingReadStream.CanReadが常にtrueで、入力を閉じないというassertionが破棄を検知できなかった。当該テスト内のMemoryStream派生をFailingReadStream(Exception)へ統一し、I/O・模擬OOMの両経路で、例外の同一性とDispose状態を反映するCanReadを確認するよう修正した。共有fixtureと本体は変更していない。
- 所見3（Low、不採用）: Location/ByteOffsetがnullの例外を捕捉すると診断変換が失敗するという将来の仮定。現在の調査で呼ぶWasmBinaryReader.Error/ReadNameとModuleBinaryFormat.ReadCountは必ずreader.Locationで位置を付ける。Stream.Readはこのcatchの外で実行され、利用者由来の位置なし例外もここへ入らない。UnsupportedFeatureを投げる実入力経路も現行type/import readerにはない。現時点の不具合ではないため、仮定だけのnullフォールバックは追加しない。
- 本体の挙動変更はなく、既存契約のテスト補完だけなのでRED用の本体変更は行っていない。今回編集したテスト2ファイルの`dotnet csharpier format`と`dotnet csharpier check`は各終了0。既存のドキュメントコメント補完を維持し、テストへXMLコメントは追加していない。
- `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers`: 終了0、警告0、エラー0。
- ビルド成功後の`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/claude-review-host-linking-5-runtime`: 終了0、passed 672 / failed 0 / skipped 0。新規コンストラクターテスト2件と既存のI/O・模擬OOMテストを含む。主担当が最新TRXの結果と対象コードを直接確認した。
- 未実施範囲: 生成器suiteの再実行（生成器・本体の変更なし）、Claude自身によるビルド・テスト、巨大入力/実OOM、UnsupportedFeatureの実入力、Core 2.0一次資料との網羅照合、公式suite、タスク6以降とfeature全体の完了検証。局所的なテスト補完で疑義は解消したためClaudeへの再送は行っていない。レビュー依頼文・元回答はリポジトリ外の一時フォルダに保存した。
- 通常/cachedの`git -c core.excludesFile= diff --check`は各終了0。開始時と最終のインデックスSHA-256は一致し、ステージ済み内容・ブランチ・履歴を変更していない。今回の修正はテスト2ファイルと本記録のみで、未ステージ・未追跡の作業ツリーに残した。主担当の`kiro-verify-completion`: タスク5の外部レビュー、全3所見の判定、採用2件のテスト補完と関連検証はVERIFIED。

### 6.1 外部要素の宣言検証（2026-09-19）

- Task Brief: 4種のimport先行の添字空間、関数型参照、種類を跨ぐexport名重複、memory合計1個、memory/tableのlimitsをModuleValidatorで検証する。型・添字・limits不正は位置付きWasmValidateException、複数tableと仕様上限の宣言は割当なしで受理する（1.2、3.2、3.6、7.7、7.8、design「デコード・検証・静的情報」）。
- 境界補足: 成功状態とexport索引の反映を確認するため、WasmModule.Validateへの最小接続と既存Decodeテストの旧Unsupported期待の更新を含む。関数export索引だけを作り、非関数を混入させず、コードと一緒に全体成功時だけ確定する。未実装のリンク・資源構築を黙って無視しないよう、その拒否はInstantiate段階へ移した。global初期化式/startのValidate拒否は6.2まで維持する。WasmInstanceの構築・取得は変更していない。
- RED_PHASE_OUTPUT: 各実行前の `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers` は終了0、警告0、エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/ModuleValidator_ValidateTests/各種類のimportと定義をexportする*' --report-trx --results-directory TestResults/host-linking-6.1-red-indices` は一時フラグOFFで終了1、passed 0 / failed 1 / skipped 0（importを含むexport添字を旧検証が拒否）。
- 負例追加後、filter `/*/*/ModuleValidator_ValidateTests/*`、出力先 `TestResults/host-linking-6.1-red-declarations` は終了1、25/23/0。型参照、limits、memory個数の未検査と関数添字の診断ずれを検出した。ON・実装後の `host-linking-6.1-green-declarations` は終了0、48/0/0。
- 公開接続前、filter `/*/*/WasmModule_ValidateTests/(外部要素の検証に成功*)|(資源定義の検証に成功*)|(Import後の後半関数が検証失敗*)`、出力先 `TestResults/host-linking-6.1-red-public` は終了1、0/5/0（旧Validate入口のUnsupported）。接続・一時フラグ削除後のReleaseビルドは終了0、警告0、エラー0。filter `/*/*/(ModuleValidator_ValidateTests)|(WasmModule_ValidateTests)|(WasmModule_DecodeTests)/*`、出力先 `TestResults/host-linking-6.1-final` は終了0、165/0/0。
- 開始時の作業ツリーは変更なし。検証は宣言と既存定数実行の範囲であり、実割当・リンク・start実行・公式suite・feature全体は未実施。独立レビューと完了判定はこの記録後に実施する。
- 履歴を引き継がない独立レビュアーの `kiro-review`: TASK 6.1 APPROVED、必須修正なし。上記Releaseビルド終了0・警告0・エラー0の後、標準runtimeコマンドの出力先 `TestResults/host-linking-6.1-review-runtime` は終了0、passed 702 / failed 0 / skipped 0。生成器の標準コマンド `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-6.1-review-generators` は終了0、36/0/0。対象7 CSのCSharpier check、git差分空白検査は終了0。
- 主担当が両最新TRX、変更差分、構造化APPROVED判定を直接確認し、`kiro-verify-completion`: TASK 6.1 VERIFIED。コードはレビュー中に変更せず、確認後に6.1のみ完了チェックを更新した。ステージング・コミットは行っていない。

### 6.2 global初期化式とstartの型検証（2026-09-19）

- Task Brief: 公開Validateでglobal初期化式を評価せず検証し、結果1個と宣言型の一致を要求する。global.getはimported immutableに限定し、7種の値型を許す。startはimport先行の関数添字を解決し、引数・結果0個を要求する。失敗時はコード/export索引を反映せず、Validateでcallback/startを実行しない（1.2、4.2、4.3、9.1、9.3、design「デコード・検証・静的情報」）。
- 実装: ModuleValidatorでglobal式とstartを関数本体より先に検証する。定数判定は既存InstructionSetを用い、式の評価・資源割当・リンクを追加しない。WasmModuleの旧Validate拒否を除去し、構築未対応のInstantiate拒否だけを残した。旧Decodeテストは生情報保持を維持し、意味不正の期待をWasmValidateExceptionへ更新した。
- RED_PHASE_OUTPUT: 各テスト実行前の `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers` は終了0・警告0・エラー0。標準runtimeコマンドにfilter `/*/*/WasmModule_ValidateTests/Globalのスカラー定数が宣言型と一致*`、出力先 `TestResults/host-linking-6.2-red-scalars` を指定し、フラグOFFで終了1、passed 0 / failed 4 / skipped 0。結果数・型不正のfilter `/*/*/WasmModule_ValidateTests/Global式の結果の個数や型が不一致*`、`host-linking-6.2-red-global-shape` は終了1、0/3/0（いずれも旧入口のUnsupported）。ON・実装後のfilter `/*/*/WasmModule_ValidateTests/Global*`、`host-linking-6.2-green-scalars` は終了0、7/0/0。
- global.getのRED: filter `/*/*/WasmModule_ValidateTests/(Imported*)|(Global取得*)`、`host-linking-6.2-red-global-get` は終了1、5/10/0（有効な取得を拒否、型不一致も命令位置での拒否）。取得の型解決実装後のfilter `/*/*/WasmModule_ValidateTests/(Global*)|(Imported*)`、`host-linking-6.2-green-globals` は終了0、22/0/0。mutable、定義globalの先行参照・自己参照、範囲外、数値/v128/参照の型不一致を含む。
- startのRED: filter `/*/*/WasmModule_ValidateTests/(Importのstart*)|(Startが*)|(定義start*)`、`host-linking-6.2-red-start` は終了1、0/12/0（旧start拒否）。型と添字検証・公開接続後の `host-linking-6.2-green-start` は終了0、12/0/0。byte列/非seek Streamで混在importの関数添字と型添字を区別し、Validate/re-Validateでcallbackが0回、Instantiateの未対応拒否でも0回であることを確認した。
- 一時フラグを除去・対象6 CSを整形後のReleaseビルドは終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/(ModuleValidator_ValidateTests)|(WasmModule_ValidateTests)|(WasmModule_DecodeTests)/*' --report-trx --results-directory TestResults/host-linking-6.2-final` は終了0、passed 199 / failed 0 / skipped 0。git差分空白検査も終了0。
- 未実施範囲: global実評価、リンク・資源割当、start/callback実行、公式suite、feature全体の検証。結果0個の定義startの公開Validate成功は計画どおり9.2へ残し、今回も既存のfunction.results未対応を確認した。独立レビューと完了判定はこの記録後に実施する。ステージング・コミットは行っていない。
- 履歴を引き継がない別の独立レビュアーの `kiro-review`: TASK 6.2およびタスク6全体統合はAPPROVED、必須修正なし。上記Releaseビルドは終了0・警告0・エラー0。`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-6.2-review-runtime` は終了0、passed 736 / failed 0 / skipped 0。
- `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/host-linking-6.2-review-generators` は終了0、passed 36 / failed 0 / skipped 0。合計772件成功。libraryのsmokeとしてbyte列・非seek Streamの公開Decode→Validate→Instantiate→GetFunction→Invokeを通す既存定数テスト16件も最新runtime TRXで成功を確認した。
- 対象11 CSの `dotnet csharpier check`、通常/cachedの `git -c core.excludesFile= diff --check` は終了0。主担当が両最新TRX、構造化APPROVED判定、6.2の主要4 CSのSHA-256がレビュー前後で一致することを確認した。`kiro-verify-completion`: TASK 6.2および選択タスク6 VERIFIED。各サブタスクの独立承認後に完了チェックを更新し、タスク6全体も完了とした。
- ステージング・コミット・承認metadata変更は行っていない。手動モードのため `kiro-validate-impl host-linking` は自動実行せず、タスク7以降とfeature全体の検証は別途実施する。
- 命名補足（2026-09-19）: ValidateFunctionの `index` を `definitionIndex`（定義配列の添字）、`functionIndex` を `moduleFunctionIndex`（importを含むmodule全体の関数添字）へ変更し、呼出し側のループ変数も揃えた。名前だけの変更で、処理・テストは変更していない。対象CSのCSharpier formatと `dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers` は終了0、警告0・エラー0。テストは再実行していない。

### タスク6 Claude Codeレビューと指摘対応（2026-09-19）

- 対象: 開始時のステージ済み12ファイル（タスク6.1/6.2、本体2ファイル、テスト9ファイル、本記録）。未ステージ・未追跡はなし。明示指定された`claude-code-review`に従い、Anthropicへ対象差分と関連コード・仕様を読み取り専用で送信した。Claude Code 2.1.278を`--safe-mode --tools Read,Glob,Grep --allowedTools Read,Glob,Grep --disallowedTools mcp__* --permission-mode dontAsk --strict-mcp-config --no-session-persistence`で実行し、CLI終了0、最終resultはsuccess、is_error=false、権限拒否なし。全12ファイルの確認を含む元回答と依頼文はリポジトリ外の一時フォルダに保存した。
- 所見1（Claude: Medium、採用はコメント補足のみ）: `FunctionExportIndices`がimportを含むmodule全体の関数添字である一方、コメントから定義添字との違いが読めない。現行の公開Instantiateはimportを拒否するため、指摘された誤った関数取得・範囲外アクセスは現在到達不能。内部契約の明確化としてWasmModuleのコメントに添字空間を明記した。消費側のリンク対応は計画どおり後続タスクに残した。
- 所見2（Low、採用）: Instantiateへ移したimport未対応の診断名`section.import`を既存テストが検査していない。`WasmModule_ValidateExternalsTests.cs`の既存正例へFeatureのassertionを追加し、byte列・非seek Streamの両経路で段階・診断名・未確認範囲を確認する。新規テストケースや本体の分岐は追加していない。
- 所見3（Low・情報、修正不要）: `RequireSupportedInstantiation`のstart分岐は、importがあればimport拒否が先行し、定義startは結果0個の実行形がValidateで未対応となるため、現時点では到達不能。6.2の記録と9.2の予定に一致するため変更しない。
- 所見4（Low、不採用）: 空の`GlobalDefinition.Initializer`では長さ検査前の末尾参照が例外になるとの指摘。公開Decodeは終端を読んだ場合だけ命令列を返し、終端欠落はWasmDecodeExceptionで拒否する。内部のコピー用コンストラクターへ不正な列を直接渡すケースはデコード済み定義の不変条件に反し、公開APIの検証契約ではない。防御的検査は追加せず、実際の不正な式に対する既存の終端位置診断も維持した。
- 所見5（Low・情報、修正不要）: 初期化式の使用不能命令を拒否するelse分岐は現行Decoderから到達不能。未対応命令はDecodeで停止し、対応済み命令は定数またはglobal.getとして検証することを確認した。指摘自身も防御として妥当としており変更しない。番号外のlimits定数抽出案も任意の可読性提案であり、今回は採用しない。
- 並行変更: レビュー中に別操作でインデックスが更新され、ModuleValidatorの改行が統一され、FunctionImport.cs/GlobalImport.csの改行変更が追加された。開始時blobとの正規化比較および`--ignore-space-at-eol`の差分で、いずれも処理変更がないことを主担当が確認して保持した。最初の対象11 CSのCSharpier checkはModuleValidatorの改行混在で終了1だったが、別操作後の単独checkは終了0であり、主担当による整形修正は不要だった。追加2ファイルはClaudeの初回差分対象外で、主担当が改行のみと確認した。
- 修正後の`dotnet build WasmSharp2.slnx -c Release --no-restore --warnaserror --disable-build-servers`: 終了0、警告0、エラー0。
- ビルド成功後の`dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/claude-review-host-linking-6-final-runtime`: 終了0、passed 736 / failed 0 / skipped 0。所見2のassertionを含む既存2経路も成功した。
- 同ビルド後の`dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build -- --report-trx --results-directory TestResults/claude-review-host-linking-6-final-generators`: 終了0、passed 36 / failed 0 / skipped 0。主担当が両最新TRXの計772件成功を直接確認した。
- 現在の対象13 CSに対する`dotnet csharpier check`、通常/cachedの`git -c core.excludesFile= diff --check`: 各終了0。並行変更確認後・修正直前のインデックスSHA-256と最終値は一致し、HEADも開始時から不変。主担当はステージング・コミット・Git状態変更を実行せず、コメント・assertion・本記録だけを未ステージの作業ツリーに残した。
- 未実施範囲: Claude自身によるビルド・テスト・整形実行、公式WASTケースとの個別突合と公式suite実行、タスク7以降、feature全体の受入。一次仕様は固定Core 2.0のvalid/types.rst・modules.rst・instructions.rstの関連規則を照合した。修正はコメントと既存assertionだけで疑義が残らないためClaudeへの再送は行っていない。
- `kiro-verify-completion`: TASK VERIFIED。今回の外部レビュー、全5所見の採否、採用2件の対応、最新ビルド・両suite・整形確認を完了した。feature全体のGOや後続タスクの完了は主張しない。
