# 要件定義書

## はじめに

tables-referencesは、Core 2.0の参照値、テーブル命令、要素セグメント、間接呼び出しを既存のDecode・Validate・Instantiate・Invokeで扱う。
利用者は、ホストとWasmが共有する参照とテーブルの同一性を保ちながら、結果と失敗段階を公開APIで確認できる。

## 対象範囲と隣接仕様

- 対象はfuncref・externref、ref.null/ref.func/ref.is_null、参照を使う変数・引数・結果・typed select、table命令、elem.drop、call_indirect、Core 2.0の全要素セグメント形式である。
- **アクティブ要素セグメント**はインスタンス化時に指定テーブルを初期化する。**パッシブ要素セグメント**はtable.initの供給元として保持する。**宣言的要素セグメント**は関数参照の宣言に使用し、実行時にコピーできる要素を残さない。
- host-linkingの公開値・関数・テーブル・global・import/export契約と、numeric-controlの構造化制御を引き継ぐ。参照に固有の検証と実行を追加し、公開ホスト操作の契約は維持する。
- 現行checkoutにあるlinear-memoryのデータ初期化と統合し、要素初期化、データ初期化、startの順序を確認する。データ命令そのものの意味論は変更しない。
- 型付き関数参照、call_ref、GC、再帰型、exnref、table64、WASI、並行実行、非同期実行、SIMD実装は対象外とする。
- 公式ランナーの参照比較、spectest、register、診断判定を再利用する。対象集合、期待値、結果分類を本機能の進捗に合わせて変更しない。

## 要件

### Requirement 1: バイナリの解釈

**目的:** 利用者として、参照とテーブルを使うCore 2.0バイナリを読み込み、構文不正と後続の型検証失敗を区別したい。

#### 受入基準

1. When 対象の参照命令・テーブル命令・elem.drop・call_indirectを含むバイナリをDecodeする, the WasmSharp2 shall 各命令とその即値をCore 2.0の符号化に従って読み取る。
2. When 空またはflagsが0から7の要素セクションをDecodeする, the WasmSharp2 shall mode、参照型、関数添字列または初期化式列、必要なテーブル添字とオフセット式を読み取る。
3. If 対象構文のLEB128、参照型、elemkind、flags、式の終端、宣言長またはセクション構成が不正である, then the WasmSharp2 shall Decodeの失敗として入力位置付きで拒否する。
4. When 構文上有効だが添字・型・定数式規則に違反する対象バイナリをDecodeする, the WasmSharp2 shall その静的な不正の判定をValidateで行える状態にする。
5. If 対象外の既存未実装機能に遭遇する, then the WasmSharp2 shall 既存の未対応分類と未確認範囲を保ち、入力全体の有効性や実行成功を報告しない。

### Requirement 2: 参照とテーブルの静的検証

**目的:** 利用者として、不正な参照操作を実行前に検出し、検証済みモジュールを安心してインスタンス化したい。

#### 受入基準

1. When 対象命令をValidateする, the WasmSharp2 shall 関数・型・テーブル・要素セグメントの添字と、必要なoperandの型・個数・順序を検証する。
2. When ref.funcをValidateする, the WasmSharp2 shall 関数の存在とCore 2.0の宣言済み関数参照集合への所属を検証し、関数本体内やstartだけからの参照を宣言として扱わない。
3. When 要素セグメントをValidateする, the WasmSharp2 shall 各初期化式の結果が要素型に一致し、アクティブの場合は対象テーブルの要素型とi32のオフセット式が一致することを検証する。
4. When globalまたは要素の初期化式をValidateする, the WasmSharp2 shall ref.null、ref.func、型の適合するimportされたimmutable globalのglobal.getをCore 2.0の定数式規則に従って扱い、定義globalやmutable globalへの参照、結果数・結果型の不正を拒否する。
5. When table.copy・table.init・call_indirectをValidateする, the WasmSharp2 shall コピー元とコピー先の参照型の一致、または間接呼び出し先テーブルがfuncrefであることと型添字の存在を検証する。
6. While 型スタックが到達不能なコードを検証している, the WasmSharp2 shall 既存のスタック多相性を保ち、対象命令の静的添字・宣言・型注釈の検証を省略しない。
7. If 対象の検証が失敗する, then the WasmSharp2 shall 検証成功状態を確定せず、Instantiateによるリソース変更やstart実行を許可しない。

### Requirement 3: 参照値の実行

**目的:** 利用者として、参照の種類、null、同一性を保って関数とホストの間で値を受け渡したい。

#### 受入基準

1. When ref.nullを実行する, the WasmSharp2 shall 指定したfuncrefまたはexternrefの型を持つnull参照を返す。
2. When ref.funcを実行する, the WasmSharp2 shall 現在の定義関数の所属インスタンスで解決した同じ関数実体をfuncrefとして返す。
3. When ref.is_nullを実行する, the WasmSharp2 shall funcref・externrefのnullにはi32の1を、非nullにはi32の0を返す。
4. When 参照値をlocals・globals・引数・結果・構造化制御で受け渡す, the WasmSharp2 shall 参照型と同一性を保ち、追加localの初期値にはその型のnullを使用する。
5. When 参照型を1個指定したtyped selectを実行する, the WasmSharp2 shall i32条件に従って選んだ同型の候補をそのまま返す。
6. If 型注釈のないselectに参照型を使うか、typed selectの型数・候補型・条件型が不正である, then the WasmSharp2 shall Validateで拒否する。

### Requirement 4: テーブルの基本操作

**目的:** 利用者として、複数のテーブルとホストが共有する実体をguest命令から読み書きし、増大の成否を判断したい。

#### 受入基準

1. When table.getまたはtable.setを実行する, the WasmSharp2 shall 指定したテーブルと符号なしi32添字の要素を読み書きし、参照の型と同一性を保つ。
2. If table.getまたはtable.setの要素添字が範囲外である, then the WasmSharp2 shall テーブルを変更せずtrapとして終了する。
3. When table.sizeを実行する, the WasmSharp2 shall 指定したテーブルの現在の要素数をi32のビット列として返す。
4. When table.growが成功する, the WasmSharp2 shall 増大前の要素数を返し、既存要素を保ったまま追加領域を指定参照で初期化する。
5. If table.growで指定した増分が宣言上限・保持上限を超えるか、増大に必要な領域を確保できない, then the WasmSharp2 shall 要素数と内容を変更せずi32の-1を返す。
6. When 同じテーブルをホストまたは複数のインスタンスが共有する, the WasmSharp2 shall guest命令と公開ホスト操作の更新を同じ実体に反映し、増分0の増大も元の要素数を返す。

### Requirement 5: テーブルの一括操作と要素の破棄

**目的:** 利用者として、テーブルのコピー・充填・初期化と要素セグメントの寿命をCore 2.0どおりに扱いたい。

#### 受入基準

1. When table.copyを実行する, the WasmSharp2 shall 指定した同型テーブル間で参照をコピーし、同じ実体の範囲が重なる場合もコピー開始前の参照列に相当する結果を残す。
2. When table.fillを実行する, the WasmSharp2 shall 指定範囲の各要素を同じ参照値で埋める。
3. When table.initを実行する, the WasmSharp2 shall 指定した要素セグメントの範囲を指定テーブルへコピーし、供給元の要素を保持する。
4. If table.copy・table.fill・table.initのコピー元または変更先の範囲が不正である, then the WasmSharp2 shall その命令による部分更新を行わずtrapとして終了する。
5. When 一括操作の長さが0である, the WasmSharp2 shall 各開始位置が対応する長さ以下なら成功し、長さを超える開始位置にはtrapを返す。
6. When elem.dropを実行する, the WasmSharp2 shall そのインスタンスの指定セグメントを長さ0として扱い、繰り返しの破棄は成功させ、他のインスタンスや転送済みの参照を変更しない。

### Requirement 6: 要素の初期化と副作用

**目的:** 利用者として、共有テーブル、データ初期化、startを含むインスタンス化の順序と失敗後の状態を予測したい。

#### 受入基準

1. When モジュールをInstantiateする, the WasmSharp2 shall 関数参照をglobalと要素の初期化式で解決できる状態を用意し、インスタンスごとに要素セグメントの値と寿命を保持する。
2. When アクティブ要素セグメントを初期化する, the WasmSharp2 shall 宣言順にオフセットを評価して対象テーブルへ転送し、成功したセグメントの内容を破棄する。
3. When パッシブまたは宣言的要素セグメントをインスタンス化する, the WasmSharp2 shall パッシブの内容をtable.init用に保持し、宣言的セグメントはテーブルを変更せず内容を破棄する。
4. If アクティブ要素セグメントの転送先範囲が不正である, then the WasmSharp2 shall 当該セグメントの部分転送を行わずInstantiateのtrapとして終了し、後続の要素初期化・データ初期化・startを実行しない。
5. When 要素とデータとstartを持つモジュールをInstantiateする, the WasmSharp2 shall 要素初期化、データ初期化、startの順に実行し、startから初期化後のテーブルとメモリを参照できるようにする。
6. If 要素初期化・データ初期化・startが失敗する, then the WasmSharp2 shall それ以前に完了した共有リソースの変更を戻さず、外部に保持された関数・インスタンス・リソースの参照を自動的に無効化しない。
7. If importの解決または型照合が成立しない, then the WasmSharp2 shall 共有テーブルへの要素転送とstartを行わず、既存のリンク不成立として報告する。

### Requirement 7: 間接呼び出し

**目的:** 利用者として、テーブルで選んだ定義関数またはホスト関数を、直接呼び出しと同じ値・状態・資源上限の契約で実行したい。

#### 受入基準

1. When call_indirectの要素添字が有効で、非nullの関数参照の型が指定型と一致する, the WasmSharp2 shall 引数・結果の個数と順序を保って関数を呼び出す。
2. If call_indirectの要素添字が範囲外である, then the WasmSharp2 shall 呼び出し先を実行せず、未定義要素を示す診断付きのtrapを返す。
3. If call_indirectで選んだ要素がnullである, then the WasmSharp2 shall 未初期化要素を原因とするtrapを返す。
4. If call_indirectで選んだ関数の引数型列または結果型列が指定型と異なる, then the WasmSharp2 shall 関数型不一致のtrapを返し、同じ構造の型は別の型添字や別モジュールであっても一致と扱う。
5. When call_indirectで別インスタンスの定義関数を呼び出す, the WasmSharp2 shall その関数が元から所属するインスタンスのglobal・table・memoryを使用する。
6. When call_indirectでインスタンス情報を受け取るホスト関数を呼び出す, the WasmSharp2 shall 呼び出し元の定義関数が所属するインスタンスを渡し、ホスト例外を既存契約どおり伝播する。
7. When 間接呼び出しが再帰・ホストcallback・同期再入を含む, the WasmSharp2 shall 直接呼び出しと同じ実行コンテキストの上限を共有し、終了・trap・exhaustion・ホスト例外の後に呼び出し前の実行状態を復元する。

### Requirement 8: 失敗の分類と診断

**目的:** 利用者として、参照とテーブルの失敗を公開例外の種類・原因・位置から判断したい。

#### 受入基準

1. When 対象処理が失敗する, the WasmSharp2 shall Decode不正、Validate不成立、リンク不成立、trap、exhaustion、実装上限、未対応を既存の公開分類で区別する。
2. When 要素初期化または命令実行がtrapを返す, the WasmSharp2 shall InstantiateまたはInvokeの段階と、原因・バイト位置・該当する関数添字またはセクションIDを公開診断へ引き継ぐ。
3. When 固定した公式ケースが対象処理の否定assertionを評価する, the WasmSharp2 shall 既存ランナーによる段階・例外型・原因・期待診断の前方一致判定を満たす。
4. When 公開ホスト操作やホストcallbackが例外を返す, the WasmSharp2 shall APIの誤用またはホスト例外をguest命令のtrapや増大失敗へ置き換えない。
5. When 本機能の対応を完了する, the WasmSharp2 shall 対象の命令・要素セクション・参照typed selectを未対応のまま残さず、対象外機能の未対応報告は維持する。

### Requirement 9: 固定公式スイートによる受入

**目的:** 開発者として、対象機能の実装と既存機能の回帰を同じ公式スイートで判断したい。

#### 受入基準

1. When 本機能の完了を判定する, the WasmSharp2 shall 現在の固定Core 2.0プロファイルと公式素材を用い、全入力・全commandの実行結果を記録する。
2. When 対象ケースの前提となる機能が揃っている, the WasmSharp2 shall 既存ランナーの公開API経路と判定を使い、要素・参照・複数テーブル・間接呼び出し・初期化失敗を含むケースのpassedを要求する。
3. When 完了時の結果を直前の受入済みbaselineと比較する, the WasmSharp2 shall failed・runner_error・以前passedだったケースの退行または欠落が0件であることを要求する。
4. If 後続のSIMD等の未実装機能が対象ケースの実行を妨げる, then the WasmSharp2 shall 出典・報告された未実装機能・blockedの起因を残し、本機能の未対応を後続機能待ちとして扱わない。
5. When 公式ケースだけでは対象規則の境界を確認できない, the WasmSharp2 shall 補助テストを規則に対応付け、テスト変更後は警告・エラー0のビルドを確認してコマンドで実行する。
6. When 対象ケースと全体の回帰確認を完了する, the WasmSharp2 shall 比較済みの同じ結果を明示的にbaselineへ保存し、機能の受入とCore 2.0全体の完成を区別する。
