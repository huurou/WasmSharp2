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

- [ ] 3. 明示型の関数と名前付き提供登録を用意する
- [ ] 3.1 定義関数と両形式のホスト関数を区別して保持する
  - 定義関数の所属instanceとmodule全体の関数添字を保持し、定義配列の添字と区別する。
  - ホスト関数は明示関数型とどちらか一方のcallback形式を保持し、結果を所有済みの値集合にする。取得元instanceへ所属させない。
  - 両形式を生成して型を取得でき、不正な生成引数を拒否する。既存定義関数の定数呼出しを維持する。
  - _Boundary: WasmFunction, WasmHostModule_
  - _Depends: 1.3_
  - _Requirements: 2.10, 7.9, 8.1_

- [ ] 3.2 4種の提供登録と原子的な重複拒否を実装する
  - 名前は完全一致とし、空文字列を許しnullを拒否する。種類をまたぐ同名itemを拒否する。
  - 提供元追加時に対応表をスナップショットし、名前の組が1件でも重複したら全件を追加しない。同じmodule名の非重複itemは追加できる。
  - 実体をコピーせず、追加後の定義変更が既存登録へ影響しないことと、引数なし提供元の互換性を確認する。
  - _Boundary: WasmImports, WasmHostModule_
  - _Depends: 2.1, 2.4, 2.5, 3.1_
  - _Requirements: 7.1, 7.13_

## 静的定義

- [ ] 4. 外部要素とstartを実行せずに読み取る
- [ ] 4.1 バイナリ共通読取を既存Decodeへ統合する
  - ヘッダー、sectionの外枠・順序、型、import記述を共有reader上へまとめ、既存Decodeから使う。
  - 構文と意味論を分け、生の添字・limits・元位置を保持する。第二のバイナリパーサーを作らない。
  - 既存の符号化・UTF-8・Stream・失敗位置のテストが同じ分類で成功する。
  - _Boundary: ModuleBinaryFormat, ModuleDecoder_
  - _Depends: 1.2, 1.3_
  - _Requirements: 1.4, 1.6, 11.1_

- [ ] 4.2 4種のimport/exportとmemory/table定義を読み取る
  - 関数・global・memory・tableのimport、全種類のexport、memory/table定義を静的moduleへ保持する。
  - import宣言順、各種類の生の添字、limits、元位置を保持し、対応済みsectionの旧Unsupported期待を更新する。
  - バイト列とStreamの正負入力で静的情報を保持し、破損をDecode失敗にできる。callbackも資源割当も行わない。
  - _Boundary: ModuleDecoder, WasmModule_
  - _Requirements: 1.1, 1.4, 7.7_

- [ ] 4.3 global初期化式とstartを読み取り未対応segmentを区別する
  - globalの型・初期化式とstartの関数添字を保持し、スカラー定数とglobal取得の式を構文として読む。
  - data/element/data_countを無視せず、未対応機能・位置・未確認範囲を返す。既知の構文違反を未対応へ置き換えない。
  - 対象sectionの正例と破損例、未対応segmentを持つ入力で、実行を伴わない段階別失敗を確認する。
  - _Boundary: ModuleDecoder, WasmModule_
  - _Requirements: 1.1, 1.4, 1.5, 4.2, 9.1_

## 独立したimport調査

- [ ] 5. 実行可否と独立して完全なimport情報を公開する
- [ ] 5.1 全sectionを走査して完全な要求型一覧を作る
  - 共通readerでtype/importを完全に読み、宣言順の名前・種類・要求型を型付きで所有する。
  - 他payloadは解釈せずスキップするが、後続sectionの外枠・重複・終端まで確認し、成功時にも未確認範囲を保持する。
  - 完全取得とimportなしを返し、未対応本体やsegmentでも取得でき、後続破損では部分一覧を返さないことを内部テストで確認する。
  - _Boundary: ImportInspector, WasmImportInspection, WasmImportInfo_
  - _Depends: 4.1, 4.3_
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [ ] 5.2 import調査の公開入口と失敗診断を統合する
  - バイト列とStreamから調査を呼べるようにし、module生成・検証済み化・登録・割当・実行を要求しない。
  - 構文破損、型未解決、未対応、実装制限を位置・未確認範囲・元診断付きで区別する。I/Oと実OOMは元の例外を伝播する。
  - 非seek・現在位置・非close、null/非readable拒否を公開操作で確認し、失敗結果に一覧を公開しない。
  - _Boundary: WasmModule, ImportInspector, WasmImportInspectionException_
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 12.4_

## 宣言の検証

- [ ] 6. リンク前に型と添字の整合性を検証する
- [ ] 6.1 外部要素の添字空間・export・limitsを検証する
  - 各種類でimportが定義に先行する添字空間を検査し、種類を跨ぐexport名重複を拒否する。
  - memory合計1個、最小/最大の関係と仕様上限を検証し、複数tableは許可する。
  - 範囲外添字・不正limitsはValidateで拒否し、成功時だけ全体の検証状態とexport索引を反映する。
  - _Boundary: ModuleValidator_
  - _Depends: 4.2, 4.3_
  - _Requirements: 1.2, 3.2, 3.6, 7.7, 7.8_

- [ ] 6.2 global初期化式とstartの型を検証する
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
  - _Depends: 2.1, 2.4, 2.5, 3.1, 7.1_
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
  - _Depends: 3.1, 8.3, 9.3_
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
