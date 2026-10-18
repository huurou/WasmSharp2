# 実装タスク

## 実行上の前提

各実行単位は1〜3時間を目安とする。
担当名と具体的な変更先は[設計](design.md)のファイル構成計画に従う。
大項目1で既存の挙動を保って責務を整理し、その後に挙動を追加する。
新しい公開API、未完成instanceの公開、成功だけを返す仮handlerは作らない。

準備段階では命令・sectionの未対応報告を維持する。
6.1・6.2・6.4・7.1・7.2で対応済みへ接続し、それぞれ未対応を期待する既存テストと命令集合の期待値を更新する。
metadataからまだ呼べない検証分岐やhandlerは、接続タスクで公開経路から検証する。
テストを追加・変更したタスクは、警告・エラー0のReleaseビルドを確認後、関連テストをコマンドで実行する。
接続した機能単位で固定スイート全体のrunとbaselineとのcompare-runを行い、問題はランタイムで修正する。

並列候補は5.1と5.2のみとする。
大項目1〜4を完了してから着手し、共通fixture・命令宣言・validatorを編集しない。
同じ出力を使うビルド・テスト・公式runは並行実行しない。
それ以外は記載順と_Depends:_に従う。
baselineは最終受入まで保持する。

- [x] 1. 検証前提を確認し、既存処理の責務を整理する
- [x] 1.1 固定素材・比較元と既存テスト基盤を確認する
  - .NET 10、3つのテストプロジェクト、固定spec・WABT、公式素材と受入済みbaselineの存在・識別条件を確認する。
  - 現在の比較元はlinear-memoryの最終保存と対応manifestを使用し、2026-10-17の既知診断差を出典付きで識別できる状態にする。
  - 参照型・要素・2添字のバイナリを作る既存fixtureの補助を必要な範囲で整え、後続で共通fixtureを並列編集せずに済む状態にする。
  - Releaseビルドと既存テストの成立を確認し、素材・比較元・実行コマンドを受入用の成果物に記録する。
  - _Boundary: ConformanceVerification_
  - _Requirements: 9.1, 9.3, 9.5_

- [x] 1.2 公開契約を保ってテーブルの読み書きを内部操作へ集約する
  - 単一要素の範囲検査と取得・設定を内部操作へまとめ、公開操作の引数検査順と例外を維持する。
  - 参照型と同一性、境界違反時の未変更、増大前後の公開挙動を既存テストで確認する。
  - 公開APIから見た結果が変わらず、内部操作からは境界失敗を値で返せる状態にする。
  - _Boundary: WasmTable_
  - _Requirements: 4.1, 4.2, 4.6, 8.4_

- [x] 1.3 直接呼び出しの対象解決と共通実行を分ける
  - 関数実体を解決した後のフレーム・引数・結果・ホスト呼び出し処理を抽出し、直接callをその処理へ接続する。
  - 定義関数の所属instance、ホストへ渡す呼び出し元、深さ上限と同期再入を維持する。
  - 既存の直接call・複数結果・ホスト例外・exhaustion後の再呼び出しテストが通る状態にする。
  - _Boundary: FunctionDispatch_
  - _Requirements: 7.1, 7.5, 7.6, 7.7_

- [x] 1.4 関数実体をglobal評価より先に構築する内部経路へ整理する
  - インスタンス構築がimport済みglobalを受け取り、全関数実体を生成してから定義globalを評価する契約へ変更する。
  - import照合、table・memory割当、既存data初期化、startの順序と例外を維持する。
  - 既存の内部コンストラクター利用とfixtureを更新し、未完成instanceを公開する操作を追加しない。
  - global・関数同一性・linking・data・startの既存テストが通り、共有領域へ書く前にinstanceの構築が終わる状態にする。
  - _Boundary: InstanceConstruction_
  - _Requirements: 6.1, 6.5, 6.6, 6.7_

- [ ] 2. 命令と要素の静的な表現を準備する
- [x] 2.1 参照型即値と2添字を命令表現に追加する
  - 参照型即値と2個のu32添字を、分岐表・メモリ即値と混同しない形で保持する。
  - 対象の検証規則とスタック効果を追加し、設計の命令別対応を既存の属性と生成経路で表現できる状態にする。
  - 生成器の実契約埋込みとテスト用handler宣言を更新し、生成コードのコンパイルと既存命令の生成テストが通ることを確認する。
  - 対象命令の実際の宣言は統合タスクまで未対応のままにする。
  - _Boundary: InstructionDefinitions_
  - _Requirements: 1.1, 8.5, 9.5_

- [ ] 2.2 要素のmodeと初期化式を不変の静的定義で保持する
  - active・passive・declarativeを明示し、activeだけが対象tableとoffset式を持つ定義を追加する。
  - 参照型、初期化式列、入力位置を所有し、moduleに宣言順で保持する。
  - 不正なmodeとtargetの組合せを作らず、元の入力コレクションの変更が定義へ波及しないことを確認する。
  - この時点ではsection9の公開Decodeを未対応のまま維持する。
  - _Boundary: ModuleDecoder_
  - _Requirements: 1.2, 1.4, 6.1_

- [ ] 2.3 table系trapの公開診断と位置を完成させる
  - 既存のtable範囲外・未初期化要素・間接call型不一致の原因を公開文言へ対応付ける。
  - call_indirect範囲外だけ内部診断種別で区別し、境界違反・null要素のselectorを構造化した添字として結果へ保持する。既存原因enumを維持し、添字なしでもtable系は設計表の先頭文字列を使う。表にない既存原因の文言は変更しない。
  - Instantiate/Invoke、section9/10、関数添字・入力位置を保持し、生成器の実契約埋込みも成立させる。
  - 診断変換のテストで各期待prefixと位置を確認する。uninitialized element 2と、selectorが0xffffffffの場合のundefined element -1を含め、固定参照実装と同じ符号付き10進表記を確認する。
  - _Boundary: ExecutionBoundary_
  - _Depends: 2.1_
  - _Requirements: 7.2, 7.3, 7.4, 8.1, 8.2, 8.3_

- [ ] 3. 宣言集合・定数式・命令の検証を追加する
- [ ] 3.1 参照の検証に必要な添字型と宣言集合を構築する
  - module検証内でtable型・element型・関数総数・宣言済み関数添字を一度構築する。
  - 関数本体とstartを除く全定義から宣言を集め、global式・関数export・全modeのelementと、関数本体/start/importだけの否定例を確認する。
  - 同じ集合を後続の定数式と関数検証へ渡せる状態にし、検証成功時だけコードを確定する契約を維持する。
  - _Boundary: ReferenceValidation_
  - _Depends: 2.2_
  - _Requirements: 2.1, 2.2, 2.7_

- [ ] 3.2 参照定数式の契約を追加し、検証・構築の利用箇所へ統合する
  - 参照null、宣言済み関数参照、import immutable globalの適格性と期待型を既存の定数式規則へ追加する。
  - 評価には構築済みの全関数実体を渡し、同じ関数を返す。既存の数値・data offset評価を維持する。
  - 定数性の走査後にref.funcの存在・宣言と命令の型を検証し、最後に結果数・型を確認する。未知ref.funcに非定数命令が続く場合のconstant expression requiredを確認し、global.getの未知添字・mutable制約は定数性の走査中に検査する。
  - 呼び出し側へ検証contextと関数実体を渡す変更を統合し、既存の数値・dataの定数式テストと参照評価のテストを通す。新しい命令宣言を必要とする参照式の検証テストは6.1で実行する。
  - _Boundary: ConstantInitializer, ReferenceValidation, InstanceConstruction_
  - _Depends: 1.4, 3.1_
  - _Requirements: 2.4, 3.1, 3.2, 6.1, 8.1_

- [ ] 3.3 参照命令とtyped selectを既存の型スタックへ追加する
  - 関数参照の存在・宣言、null判定の参照型、typed selectの型数と候補型・条件型を検証する。
  - 到達不能のUnknownを扱いつつ、静的添字や宣言の検査を省略しない。
  - 参照typed selectの制限を解除できる分岐を準備し、untyped参照禁止とv128未対応を維持する。
  - 既存の制御・selectの検証を維持したビルドが通り、6.1の統合で正常・否定経路を検証できる状態にする。
  - _Boundary: ReferenceValidation_
  - _Depends: 2.1, 3.1_
  - _Requirements: 2.1, 2.2, 2.6, 3.4, 3.5, 3.6_

- [ ] 3.4 要素とテーブル命令の添字・参照型を検証する
  - 全modeの要素式、activeのtable型とi32 offsetを検証し、passive/declarativeにはtableの存在を要求しない。
  - table命令のoperandと型を検証し、copy元先とinit元先の参照型を照合する。
  - table.initはtable→elem→型一致→operandの順で検査し、要素の診断カテゴリをelem segmentとする。tableとelemがともに未知の公式ケースでunknown tableを優先することを7.1の公開経路で確認する。
  - active/declarativeに対するinit/dropを型検証だけで拒否せず、実行時の寿命へ委ねる。
  - 検証失敗時にmoduleを成功状態にせず、既存data・globalの検証を維持できる状態にする。
  - _Boundary: ReferenceValidation_
  - _Depends: 3.2, 3.3_
  - _Requirements: 2.1, 2.3, 2.5, 2.6, 2.7, 5.6_

- [ ] 3.5 間接呼び出しの型・table・引数列を検証する
  - table存在→type存在→funcref制約→operandの順で検証し、selectorと引数の型・個数・順序を既存スタックへ接続する。7.2ではtableとtypeがともに未知の場合と、externref tableに未知typeを指定した場合の診断順も確認する。
  - 複数結果を既存の型スタックへ積み、到達不能時も静的添字とtable型の検査を維持する。
  - 7.2の統合で未知type/table、externref table、不足・不一致operandを拒否できる分岐を完成させる。
  - _Boundary: ReferenceValidation_
  - _Depends: 2.1, 3.1_
  - _Requirements: 2.1, 2.5, 2.6, 7.1, 7.4_

- [ ] 4. 領域操作と要素の寿命を実装する
- [ ] 4.1 テーブルの範囲転送・コピー・充填を追加する
  - 全範囲を拡張整数で検査してから変更し、境界違反時に部分更新を残さない。
  - 同じ実体を異なるtable添字で使う場合も、重複範囲を安全にコピーする。
  - 前後の重複、末尾、0長、最大u32、失敗時の未変更を内部操作のテストで確認する。
  - _Boundary: WasmTable_
  - _Depends: 1.2_
  - _Requirements: 5.1, 5.2, 5.4, 5.5_

- [ ] 4.2 インスタンスごとの要素列・転送・破棄を追加する
  - 評価済み参照列を所有し、転送時は供給元を保持したまま対象tableへ書く。
  - 破棄で保持列を空にし、反復破棄、破棄後の0長初期化、別instanceと転送済み参照への非影響を確認する。
  - 供給元と変更先の境界違反でtableが変わらず、参照の同一性が保たれるテストが通る状態にする。
  - _Boundary: ElementInstance_
  - _Depends: 2.2, 4.1_
  - _Requirements: 5.3, 5.4, 5.5, 5.6, 6.1, 6.3_

- [ ] 4.3 評価済み要素の実体表をインスタンス構築へ統合する
  - 完成した関数表とglobal表を使って全modeの要素式を評価し、宣言順の要素実体表をinstanceへ接続する。
  - この段階では共有tableへの転送とmodeによる破棄を行わず、既存のdata・start経路を維持する。
  - 同じmoduleから別instanceを作った場合の要素実体の分離と関数参照の同一性を内部構築テストで確認する。
  - 後続handlerが現在のinstanceから要素実体へ到達でき、既存の構築テストが通る状態にする。
  - _Boundary: InstanceConstruction, ConstantInitializer, ElementInstance_
  - _Depends: 1.4, 2.2, 3.2, 4.2_
  - _Requirements: 5.6, 6.1, 6.3_

- [ ] 5. 参照・テーブル・間接呼び出しの実行処理を準備する
- [ ] 5.1 (P) 参照値を生成・判定するhandlerを追加する
  - 関数参照は現在の定義関数の所属instanceから取得し、nullは参照型別に扱う。
  - null判定はfuncref・externrefの両方でi32の0または1を返す。
  - 共通fixtureを変更せず、直接handlerを確認するテストまたは6.1で使う専用fixtureを用意し、既存実行系のビルドを維持する。
  - _Boundary: ReferenceExecution_
  - _Depends: 1.4, 2.1, 3.3_
  - _Requirements: 3.1, 3.2, 3.3_

- [ ] 5.2 (P) テーブルの取得・設定・サイズ・増大handlerを追加する
  - 現在のinstanceの指定tableに対し、i32添字・増分を符号なしの値として扱う。
  - 増大成功時は旧サイズを返し、上限失敗と当該割当のOOMだけを-1へ変換する。
  - 増分0、共有実体の更新、上限失敗時の未変更を確認し、公開ホスト操作の例外契約を維持する。
  - 共通fixture・metadata・validatorを変更せずに実行処理を準備する。
  - _Boundary: TableExecution_
  - _Depends: 1.2, 2.1, 3.4_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 8.4_

- [ ] 5.3 テーブルの一括操作と要素破棄handlerを追加する
  - copy・fill・initのoperandと即値を設計の順で解決し、内部の領域操作へ渡す。
  - 失敗結果をtable範囲trapとして返し、要素破棄は現在のinstanceの状態だけへ反映する。
  - 内部操作を重複実装せず、0長・破棄後・同一実体の重複を7.1の公開経路で確認できる状態にする。
  - _Boundary: TableExecution_
  - _Depends: 4.1, 4.3, 5.2_
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [ ] 5.4 間接呼び出しの対象を解決して共通callへ渡す
  - selectorを取得し、table境界、null、引数型列・結果型列の順に検査する。
  - 型オブジェクトや型添字の同一性ではなく、構造が等しい関数型を許可する。
  - 成功時は同じ関数実体を共通callへ渡し、失敗時は関数実行・深さ増加・ホストcallbackを行わない。境界違反とnull要素の結果にはselectorを保持して公開境界へ渡す。
  - 異なるinstanceの定義関数とホスト関数を既存の呼び出し契約で扱える状態にする。
  - _Boundary: FunctionDispatch_
  - _Depends: 1.3, 1.2, 2.1, 2.3, 3.5_
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

- [ ] 6. 参照・基本table操作・要素初期化を公開4段階へ統合する
- [ ] 6.1 参照命令・参照定数式・typed selectを統合する
  - 参照即値のDecodeと命令宣言を、完成した検証・実行処理へ接続する。
  - globalのref.funcを関数先行構築へ接続し、参照のlocals・引数・結果・構造化制御・typed selectを公開APIで往復する。
  - 未宣言参照、typed selectの型数・型不一致、通常selectの参照禁止、定数式の制約を公開Validateから確認する。
  - 対象の未対応期待を更新し、関連TUnitと公式全体run・比較で参照機能の成立と回帰なしを確認する。
  - _Boundary: InstructionDefinitions, ModuleDecoder, ReferenceValidation, ConstantInitializer, InstanceConstruction, ReferenceExecution_
  - _Depends: 3.2, 3.3, 5.1, 2.3_
  - _Requirements: 1.1, 1.3, 1.4, 2.2, 2.4, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 6.1, 8.5_

- [ ] 6.2 基本テーブル命令を統合する
  - get/set/size/growの添字Decode、命令宣言、検証とhandlerを接続する。
  - 複数table、funcref・externref、共有実体、uint添字、増大失敗を公開APIで確認する。
  - 境界trapとAPI誤用・ホスト例外の区別を確認し、関連TUnitと公式全体run・比較を通す。
  - _Boundary: InstructionDefinitions, ModuleDecoder, ReferenceValidation, TableExecution, WasmTable_
  - _Depends: 3.4, 5.2, 2.3, 6.1_
  - _Requirements: 1.1, 2.1, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 8.1, 8.4, 8.5_

- [ ] 6.3 全要素形式のDecodeを追加する
  - flags0〜7を静的定義へ変換し、旧形式の関数添字列を元の入力位置付きの参照式へ正規化する。
  - 空section、elemkind、暗黙funcref、明示reftype、table添字、offset式を読み取る。
  - 不正flags・参照型・LEB128・長さ・終端をDecodeで拒否し、添字・型の不正はValidateへ渡す。
  - 内部読み取りテストを通し、section9全体の公開対応切り替えは初期化が揃う6.4で行う。
  - _Boundary: ModuleDecoder_
  - _Depends: 2.2, 6.1_
  - _Requirements: 1.2, 1.3, 1.4_

- [ ] 6.4 要素の検証・構築・初期化を既存dataとstartへ統合する
  - 4.3で構築した全要素実体に対し、宣言順にactive転送・成功後drop、declarative drop、passive保持を行う。
  - 要素処理後に既存data初期化とstartを実行し、section9を公開4段階で対応済みにする。
  - 不正な要素型・offset・table、初期化境界、失敗した転送の未変更と先行成功分の保持を公開APIで確認する。
  - link不成立時の未変更とelement→data→startの順序を確認し、関連TUnitと公式全体run・比較を通す。
  - _Boundary: ModuleDecoder, ReferenceValidation, ConstantInitializer, InstanceConstruction, ElementInstance, ExecutionBoundary_
  - _Depends: 3.4, 4.3, 2.3, 6.3_
  - _Requirements: 1.2, 2.3, 2.4, 2.7, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 8.1, 8.2, 8.5_

- [ ] 7. 一括操作と間接呼び出しを統合する
- [ ] 7.1 一括table命令とelem.dropを公開経路へ接続する
  - 2添字のDecodeと宣言を接続し、table.initのelem→table、copyのdst→srcの即値順を確認する。
  - 全modeとdrop後の0長・非0長、反復drop、別instanceの分離、import同一実体の重複copyを公開APIで確認する。
  - 型不一致と範囲違反を所定段階で拒否し、失敗した命令の部分書込みを防ぐ。
  - 対応命令の期待を更新し、関連TUnitと公式全体run・比較を通す。
  - _Boundary: InstructionDefinitions, ModuleDecoder, ReferenceValidation, TableExecution, ElementInstance, WasmTable_
  - _Depends: 3.4, 5.3, 6.4_
  - _Requirements: 1.1, 1.3, 2.1, 2.5, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 8.3, 8.5_

- [ ] 7.2 間接callと失敗後に残る関数参照を統合する
  - type→tableのDecode、命令宣言、静的検証、対象選択と共通呼び出しを接続する。
  - 別moduleの同型関数・複数結果・元instanceのリソース、呼び出し元を受け取るホスト関数を公開APIで確認する。
  - 境界・null・型不一致のtrapと再帰・同期再入・例外後の状態復元を確認する。
  - 要素またはdataの初期化失敗後に共有tableへ残った関数を呼び、宣言的要素の破棄順と保存済み参照の有効性を確認する。
  - 関連TUnitと公式全体run・比較を通し、対象の間接call未対応を解消する。
  - _Boundary: InstructionDefinitions, ModuleDecoder, ReferenceValidation, FunctionDispatch, InstanceConstruction, ExecutionBoundary_
  - _Depends: 3.5, 5.4, 2.3, 6.4, 7.1_
  - _Requirements: 1.1, 2.5, 5.6, 6.4, 6.6, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 8.2, 8.3, 8.5_

- [ ] 8. 固定スイートと規則の対応で最終受入を確認する
- [ ] 8.1 対象規則の境界と既存公開契約の回帰を確認する
  - 55受入基準を実装・固定仕様・公式ケースまたは補助テストへ対応付け、未確認の規則だけを補う。
  - 対象の未対応期待が残っておらず、SIMD等の後続未対応と公開ホスト契約を維持していることを確認する。
  - Releaseビルドの警告・エラー0の後、3プロジェクトのテストをコマンドで実行する。
  - 実際のOOMを再現していない範囲と、上限失敗試験・捕捉範囲レビューを区別した結果を残す。
  - _Boundary: ConformanceVerification_
  - _Depends: 6.1, 6.2, 6.4, 7.1, 7.2_
  - _Requirements: 1.5, 8.1, 8.3, 8.4, 8.5, 9.5_

- [ ] 8.2 全公式commandを実行し、受入済みbaselineと比較する
  - 固定manifestから全入力・全commandを実行し、新しい出力先へ結果を保存する。
  - run完了後に同じ保存結果を比較し、failed・runner_error・回帰・欠落が0件であることを確認する。
  - 既知の診断補足差と新規差をケース単位で分け、新規差を原因別に切り分ける。修正は対応する実装・統合タスクへ戻し、修正と関連テストの完了後に8.2の全体実行・比較をやり直す。
  - 公式素材・期待値・分類・比較基準を変更せず、全体結果と比較が成立した状態にする。
  - _Boundary: ConformanceVerification_
  - _Depends: 1.1, 8.1_
  - _Requirements: 9.1, 9.3_

- [ ] 8.3 対象ケースの成立と残る未対応の起因を照合する
  - 前提が揃った参照・table・element・間接call・初期化失敗のケースを、8.2の同じ結果から確認する。
  - 対象機能に由来するruntime_unsupportedとblockedが残らず、後続機能待ちは出典と報告された未対応機能へ追跡できることを確認する。
  - 要件・規則・実装・テストと公式結果の対応を照合し、結果の合算で受入を代替しない。
  - 55受入基準と固定スイートの対象ケースを判断できる受入成果物を残す。
  - _Boundary: ConformanceVerification_
  - _Depends: 8.2_
  - _Requirements: 9.2, 9.4, 9.5_

- [ ] 8.4 比較済みの同じ結果を次機能のbaselineへ明示保存する
  - 8.2・8.3で受入済みとなった結果JSONだけを既存baseline-saveへ渡す。
  - 保存結果が元の全command・分類・素材識別を保持し、次の機能が対応manifestと組にして使える状態を確認する。
  - 機能受入とCore 2.0全体の完成を区別し、残る後続機能待ちを保持した保存結果を残す。
  - _Boundary: ConformanceVerification_
  - _Depends: 8.3_
  - _Requirements: 9.6_

## Claude Codeレビュー対応（2026-10-18）

対象は本仕様の要件・設計・調査・タスク・spec.jsonの5ファイルである。
製品コードとテストは変更しておらず、実装タスクは未着手のままとする。

| 初回指摘 | 判定と対応 |
| --- | --- |
| 1：未初期化要素の診断に添字がない | 採用。bulk.wastのuninitialized element 2に合わせ、間接callのselectorを結果へ保持し、公開境界で符号付き10進表記を付ける設計と確認項目を追加した。 |
| 2：即値順と静的検査順、要素診断名が曖昧 | 採用。格納順は維持し、table.initはtable→elem、call_indirectはtable→type→funcrefの検査順と、elem segmentという診断名を明記した。複合不正の確認を統合タスクへ対応付けた。 |
| 3：定数式のref.func存在検査が非定数命令より先になる | 採用。固定valid.mlに合わせ、定数性の走査後にref.funcの存在・宣言と型を確認する順序を明記した。global.getの適格性検査は先行する走査に残す。 |
| 4：責務整理直後にも公式全体runと比較を追加する | 不採用。1.2〜1.4は既存の公開契約・call・再入・linking・data・startのテストで挙動維持を確認し、6.1以降の統合単位で全体runと比較を行う。検証自体の欠落ではなく、切り分けのためだけに同じ全体検証を追加しない。 |
| 5：EARSのシステム名が既存仕様と異なる | 採用。55受入基準のシステム名をWasmSharp2へ統一した。受入内容と要件IDは変更していない。 |

初回と同じClaude Codeセッションの再レビューでは、指摘1・2・3・5の解消と指摘4の不採用が妥当と判定された。
再レビュー1回目の指摘1（Low）は、検証コマンドが一時領域のスクリプトを参照し、後日再実行できなくなる点である。
これを採用し、文書検証スクリプト、開始時のインデックス一覧、比較用の空ファイルをartifacts配下へ保存した。
再レビュー2回目の指摘1（Low）は、再実行で保存済み結果を上書きする点と、レビュー開始時のインデックス比較の有効範囲が不明な点である。
これを採用し、検証結果はUTC時刻を含む別名へ毎回新規作成する。-OutputPathで指定する場合も既存ファイルは上書きしない。
インデックス比較は今回のレビュー開始時点を基準とするため、後日ステージングやコミットを行った後は不一致となる。この結果を文書自体の不正とは扱わない。
artifacts保存後の初回実行（2026-10-18T01:42:46Z）は、生成前のdocument-check.jsonへのリンク1件を検出して終了1だった。初回JSONは後続の実行で上書きされており、元ファイルは残っていない。
結果JSONの生成後に再実行した記録が、保存済みのdocument-check.json（01:43:10Z、終了0）である。このファイルを以後の再実行で更新しない。

付随修正として、research.mdの並列候補を正式タスクの5.1・5.2へ合わせ、requirements.md末尾の余分な空行を除去した。
再レビューの補足に従い、添字なしでもtable系trapには設計表の先頭文字列を使用し、表にない既存原因の文言だけを維持することも明確にした。

文書の機械確認では55受入基準・29実行単位・並列候補2件を照合し、要件IDの未対応・未知ID、不正な依存、ローカルリンク切れ、JSON不正は0件だった。
確認コマンドは次のとおりである。

```powershell
pwsh -NoProfile -File artifacts/tables-references/claude-review-20261018-433517ce/check-documents.ps1
git -c core.excludesFile= diff --check
git -c core.excludesFile= diff --cached --check
```

文書確認スクリプトと追跡ファイルの空白検査は終了0だった。
[検証スクリプトと比較入力](../../../artifacts/tables-references/claude-review-20261018-433517ce/)と[文書検証結果](../../../artifacts/tables-references/claude-review-20261018-433517ce/document-check.json)に、再実行に必要なファイルと結果を保持する。
スクリプト内では未追跡5ファイルも空ファイルとのgit diff --no-index --checkで検査し、空白エラーがないことを確認した。この比較の終了1は新規内容との差分を示す。
開始時のgit ls-files --stageと照合し、インデックスは変更されていない。
manifestと比較元baselineのSHA-256はresearch.mdの記録と一致した。
文書だけの変更のためビルド・テスト・公式スイートは実行しておらず、テストの成功・失敗・スキップ件数は未計測である。
