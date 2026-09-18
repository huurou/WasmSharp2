# 要件文書

## はじめに

WasmSharp2の埋め込み利用者が、引数と結果を持つ関数を呼び出し、明示型のホスト関数や共有リソースをWasmへ接続して、複数moduleを組み合わせるための実行・リンク基盤を定める。完成済みのruntime-foundationの公開4段階、値・型、失敗分類を拡張し、後続のconformance-runnerが通常の公開操作だけでspectest・registerとimport依存の追跡を構成できるようにする。

## 対象範囲と隣接仕様

| 区分 | 範囲 |
| --- | --- |
| 関数実行 | Core 2.0の関数型、引数、結果0個・1個・複数、locals、local.get/set/tee、直接call、return、drop、unreachable。既存のスカラー定数とendを組み合わせる。unreachableはInvokeとstartで実際のWasm trapを確認するため本仕様に含める。 |
| 値の受渡し | i32・i64・f32・f64・v128・funcref・externrefを引数、locals、結果、ホストとの受渡しに使う。値を運べることと、参照命令・SIMD命令の実行対応を区別する。 |
| global | 型・可変性・実体、ホストでの生成と取得・更新、スカラー定数とimported immutable global.getによる定義globalの初期化、global.get/set、共有。参照・v128固有の初期化命令は後続仕様で扱う。 |
| memory/table | 型・limits、生成・割当・公開取得・ホストからの内容アクセスと増大、import/exportによる同一実体の共有。guest命令やsegment初期化は含めない。 |
| 接続と初期化 | 4種の外部要素のimport/exportと再export、import情報の取得、instanceを受け取る形式と受け取らない形式のホストcallback、呼び出し時のinstance指定と同期再入、startの型検証・実行。 |
| 隣接仕様 | numeric-controlは数値演算・構造化制御と先行命令の組合せ、linear-memoryとtables-referencesはguest命令・data/element初期化、simdはSIMD命令を追加する。call_indirect・ref.*命令・宣言済み関数参照の検証はtables-referencesで扱う。 |
| 公式適合検証 | 本仕様は公開APIの直接テストで受け入れる。WAST/JSONの解釈、spectestの具体値、registerコマンド、baselineはconformance-runnerが扱い、その初回受入で本仕様の公式統合確認を行う。完成済みランナーを本仕様の完了前提にしない。 |
| 保証範囲 | 単一スレッドでの同期実行。検証済みmoduleの共有や独立instanceの利用を含む並行利用、別スレッド・非同期フローへの実行コンテキストの伝播は保証しない。 |
| 全体の対象外 | WAT/WASTの自作解析、WASI、Component Model、JavaScript/Web API、JIT/AOT、既存エンジンへの実行委譲、Core 2.0外の機能。性能数値目標や配布形態は追加しない。 |

完成済みruntime-foundationの受入範囲と承認状態を維持する。今回扱う機能と後続機能の分担は[ブリーフ](brief.md)と[ロードマップ](../../steering/roadmap.md)に従う。新しい公開操作のシグネチャ、型の構成、内部の実行方式、例外のreasonや診断情報の具体形式は設計で定める。

## 要件

### 要件1: 明示的な処理段階と対象バイナリ

**目的:** 埋め込み利用者として、ホスト連携を含むmoduleを段階ごとに処理し、実行前の失敗と実行中の失敗を区別したい。

#### 受入基準

1. When 本仕様の範囲だけで構成されたCore 2.0バイナリをバイト列または読み取り可能なストリームからDecodeした場合, the WasmSharp2 shall 関数・import/export・global・memory・table・startの定義を含むmodule定義を、インスタンスの生成や関数実行を伴わずに返し、後続段階で利用可能にする。
2. When 本仕様の範囲の定義をValidateし、Core 2.0の検証規則をすべて満たした場合, the WasmSharp2 shall 対象の同じmoduleを検証済みにし、ホストcallbackやstartを実行しない。
3. If 検証が未実施または成功していないmoduleにInstantiateを要求した場合, then the WasmSharp2 shall `InvalidOperationException`で拒否し、ホストcallbackやstartを実行しない。
4. If 本仕様で検査する範囲にバイナリの構文違反がある場合, then the WasmSharp2 shall 基盤の符号化・section・UTF-8規則に従って`WasmDecodeException`で拒否し、リンク不成立や未実装へ置き換えない。
5. If 未対応のdata/element segmentを含む未実装機能によりDecodeまたはValidateを完了できない場合, then the WasmSharp2 shall `WasmUnsupportedFeatureException`で中断段階・対象機能・未確認範囲を識別可能にし、そのmoduleをインスタンス化可能にせず、startを実行しない。
6. When 本仕様の対応範囲を拡張した状態で既存の最小定数返却経路を利用した場合, the WasmSharp2 shall runtime-foundationで成立した処理段階・値のビット列・失敗分類の契約を維持する。

### 要件2: 関数の引数・locals・結果と直接呼び出し

**目的:** 埋め込み利用者として、定義関数とimport関数を同じ関数型の契約で呼び出し、宣言どおりの値を受け取りたい。

#### 受入基準

1. When 関数型に一致する引数で関数をInvokeした場合, the WasmSharp2 shall 引数と結果の個数0個・1個・複数に対応し、宣言順の型と値を保持して結果を返す。
2. If Invokeの引数の個数または順序上の型が関数型に一致しない場合, then the WasmSharp2 shall 関数本体やホストcallbackを実行する前に`ArgumentException`系の例外で拒否する。
3. When 定義関数の呼び出しを開始した場合, the WasmSharp2 shall 引数に対応するlocalsへ渡された値を設定し、追加localsを数値・v128では型に応じたゼロ、参照では型に応じたnullで初期化する。
4. When local.get・local.set・local.teeを実行した場合, the WasmSharp2 shall 指定したlocalの取得・更新をCore 2.0の規則に従って行い、local.teeでは設定した値を後続の計算でも利用可能にする。
5. When 直接callで定義関数・import関数・ホスト関数を呼び出した場合, the WasmSharp2 shall 対象の関数型に従って引数を渡し、正常終了時に結果を呼び出し元へ宣言順で渡す。
6. When returnを実行した場合, the WasmSharp2 shall 現在の関数の宣言結果を呼び出し元へ返してその他の一時値を破棄し、その関数内でreturnに続く命令を実行しない。
7. When dropを実行した場合, the WasmSharp2 shall 直前の1個のvalueを取り除き、残る値の順序と内容を変えない。
8. When 同じ関数を繰り返し呼び出すか同期的にネストして呼び出す場合, the WasmSharp2 shall 呼び出しごとの引数・locals・結果を分離し、別の呼び出しによるlocalsの更新で外側または後続の呼び出しのlocalsを変更しない。
9. When 引数・locals・結果として数値・v128・参照のvalueを受け渡した場合, the WasmSharp2 shall 暗黙変換せずにビット列または参照先の同一性を保持する。
10. When 別instanceからimportした定義関数を呼び出した場合, the WasmSharp2 shall その関数が所属する元のinstanceの関数・global・memory・tableへの参照を使って実行し、import先の状態へ置き換えない。

### 要件3: 関数本体と宣言の検証

**目的:** 埋め込み利用者として、引数や直接callを使う関数の型・添字の誤りを実行前に検出したい。

#### 受入基準

1. When 本仕様の命令だけからなる関数をValidateした場合, the WasmSharp2 shall 引数・locals・結果、各命令の入力と出力、関数終端の値の型と個数をCore 2.0に従って検証する。
2. If 検証対象の型・関数・local・global・memory・tableの添字が該当する添字空間の範囲外である場合, then the WasmSharp2 shall `WasmValidateException`で拒否する。
3. If 到達可能な命令の入力値が不足するか、local.set/tee・直接call・return・global.setの型または結果の個数が適合しない場合, then the WasmSharp2 shall `WasmValidateException`で拒否する。
4. When returnまたはunreachableの後に到達不能な命令列がある場合, the WasmSharp2 shall Core 2.0の型スタックの多相性に従って検証し、到達不能であることだけを理由に不正としない。
5. If 到達不能な命令列に不正な添字、immutable globalへのglobal.set、または多相性を考慮しても成立しない型の組合せがある場合, then the WasmSharp2 shall `WasmValidateException`で拒否し、到達不能を理由に検査を省略しない。
6. If 検証が途中で失敗した場合, then the WasmSharp2 shall 一部の関数だけを検証済みの実行対象として公開しない。

### 要件4: globalの生成・初期化・共有

**目的:** 埋め込み利用者として、型と可変性が明示されたglobalを生成し、ホストと複数moduleから同じ状態を参照したい。

#### 受入基準

1. When ホストが値の型・可変性・その型の初期値を指定してglobalを生成した場合, the WasmSharp2 shall Core 2.0の各値型を保持するglobal実体を生成し、型・可変性・現在値を取得可能にする。
2. When 定義globalを初期化した場合, the WasmSharp2 shall 対応するスカラー定数またはimported immutable global.getによる値を、宣言型とビット列または参照先の同一性を保って設定する。
3. If 定義globalの初期化式の型が宣言型と一致しないか、global.getがmutable globalまたは同moduleの定義globalを参照する場合, then the WasmSharp2 shall Validateで`WasmValidateException`を通知する。
4. When global.getまたは型が一致するmutable globalへのglobal.setを実行した場合, the WasmSharp2 shall そのglobal実体の現在値の取得または更新を行う。
5. When ホストまたはWasmがmutable globalへ型の一致する値を設定した場合, the WasmSharp2 shall globalの生成元を問わず、同じglobal実体を共有するすべての参照先から、ホストを含めて更新後の値を取得可能にする。
6. If ホストが型の異なる値を設定するかimmutable globalを更新しようとした場合, then the WasmSharp2 shall 値を変更せずに呼び出しの契約違反として拒否する。
7. When 同じmoduleから複数のinstanceを生成した場合, the WasmSharp2 shall 定義globalはinstanceごとに独立させ、同じ外部globalをimportした部分では同一実体を共有する。

### 要件5: memoryの生成とホスト操作

**目的:** 埋め込み利用者として、memoryをホストで生成・操作し、複数moduleへ同じ記憶領域を接続したい。

#### 受入基準

1. When 有効な最小ページ数と任意の最大ページ数に従ってmemoryをホストで生成するかmoduleの定義から割り当てた場合, the WasmSharp2 shall 1ページを65,536バイトとして最小サイズの領域をゼロ初期化する。
2. When ホストがmemoryを取得した場合, the WasmSharp2 shall 現在のページ数と最大ページ数の有無・値を確認し、範囲を指定して現在の領域とホスト側のバッファの間でバイトを読み書きできるようにする。
3. When memoryの増大が成功した場合, the WasmSharp2 shall 指定ページ数だけ現在サイズを増やし、既存のバイトを保持して追加領域をゼロ初期化する。
4. If memoryの増大が最大ページ数または実装上の資源制限により失敗した場合, then the WasmSharp2 shall サイズと内容を変更せず、失敗をWasmの仕様trapと区別できるようにする。
5. If ホストがmemoryの範囲外を読み書きしようとした場合, then the WasmSharp2 shall 範囲外アクセスを呼び出しの契約違反として拒否する。
6. When 同じmemoryを複数のinstanceやホストで共有する場合, the WasmSharp2 shall バイトの更新と成功した増大を、同じmemory実体の現在の内容とサイズとして取得可能にする。

### 要件6: tableの生成とホスト操作

**目的:** 埋め込み利用者として、funcrefまたはexternrefを保持するtableを生成・操作し、参照の同一性を保って共有したい。

#### 受入基準

1. When 有効な参照型と最小要素数・任意の最大要素数に従ってtableをホストで生成するかmoduleの定義から割り当てた場合, the WasmSharp2 shall 最小要素数のtableをその参照型のnullで初期化する。
2. When ホストがtableを取得した場合, the WasmSharp2 shall 要素の参照型、現在の要素数、最大要素数の有無・値を確認し、有効な位置の参照を取得・設定できるようにする。
3. When ホストがtableへ型の一致するnullまたは非null参照を設定した場合, the WasmSharp2 shall 参照の種類と参照先の同一性を保持する。
4. When 型の一致する初期参照を指定したtableの増大が成功した場合, the WasmSharp2 shall 既存の要素を保持して指定要素数だけ増やし、追加要素を指定された参照で初期化する。
5. If tableの増大が最大要素数または実装上の資源制限により失敗した場合, then the WasmSharp2 shall 要素数と内容を変更せず、失敗をWasmの仕様trapと区別できるようにする。
6. If ホストがtableの範囲外へアクセスするか、要素型と異なる参照を設定・増大用に指定した場合, then the WasmSharp2 shall tableを変更せずに呼び出しの契約違反として拒否する。
7. When 同じtableを複数のinstanceやホストで共有する場合, the WasmSharp2 shall 要素の更新と成功した増大を、同じtable実体の現在の内容と要素数として取得可能にする。

memoryのホスト向け内容アクセスは範囲指定の読み書きとし、内部領域を直接参照するSpan等の借用ビューは公開しない。読み出したバイトはコピーであり、その後のmemoryの更新・増大によって変化しない。同期再入をまたぐ場合も、各読み書きはその操作時点のmemoryへアクセスする。tableは要素単位で参照を取得・設定し、取得した参照の同一性を保つ。判断の根拠は[ADR 0009](../../../docs/adr/0009-range-based-host-memory-access.md)に従う。

### 要件7: importの名前解決・型照合とexportの同一性

**目的:** 埋め込み利用者として、ホストと既存instanceから得た外部要素を別moduleへ接続し、接続の誤りと共有状態を確認したい。

#### 受入基準

1. When ホスト関数または既存の関数・global・memory・tableをimportの提供元として登録した場合, the WasmSharp2 shall importのmodule名・item名を完全一致で解決し、外部要素をコピーせずに接続する。
2. If 必要な提供元・itemがないか、外部要素の種類が一致しない場合, then the WasmSharp2 shall Instantiateをリンク不成立として拒否し、不成立となったimportを識別可能にする。
3. When 関数またはglobalをimportへ接続する場合, the WasmSharp2 shall 関数は引数型列と結果型列、globalは値型と可変性がそれぞれ完全一致する場合に型適合と判定する。
4. When memoryまたはtableをimportへ接続する場合, the WasmSharp2 shall 現在サイズが要求最小値以上であり、要求最大値がある場合は提供側にも最大値があって要求最大値以下であり、tableでは参照型も一致する場合に型適合と判定する。
5. If 要件7.3または7.4の型適合条件を満たさない場合, then the WasmSharp2 shall Instantiateをリンク不成立として拒否し、値変換・複製・自動増大で不一致を補わない。
6. When moduleが同じmodule名・item名のimportを複数宣言している場合, the WasmSharp2 shall 各宣言について型を照合し、名前の重複だけを理由に拒否しない。
7. When importと定義を併用するmoduleを検証・インスタンス化する場合, the WasmSharp2 shall 各外部要素の種類でimportが定義より先行する添字空間を使い、対応する実体へ参照を解決する。
8. If 異なる種類を含めてexport名が重複するか、memoryのimportと定義の合計が1個を超えるか、memory/tableのlimitsがCore 2.0の制約を満たさない場合, then the WasmSharp2 shall Validateで`WasmValidateException`を通知する。
9. When instanceから種類とexport名を指定して外部要素を取得する場合, the WasmSharp2 shall 対応する関数またはリソース実体を取得可能にし、同じ対象の別名export・繰り返し取得・importした対象の再exportで同一性を保持し、取得元instanceをホスト関数の実体へ固定しない。
10. If 指定名が存在しないか指定種類のexportではない場合, then the WasmSharp2 shall `ArgumentException`系の例外で取得を拒否し、Wasmのtrapや未実装とは区別する。
11. When 同じmoduleから複数のinstanceを生成した場合, the WasmSharp2 shall 定義関数・memory・tableをinstanceごとに独立させ、importした同じ外部要素だけを共有する。
12. When moduleが参照しない余分な提供元やitemが登録されている場合, the WasmSharp2 shall それらを照合・実行せずに当該moduleのインスタンス化を行う。
13. If ホストが同じ提供登録の集合に同じmodule名・item名の組を重複して登録しようとした場合, then the WasmSharp2 shall 登録時に呼び出しの契約違反として拒否し、既存の対応付けを変更しない。

limitsの検証は最小値と最大値の関係、memoryの上限65,536ページ、tableの32ビット要素数を含む。memory/tableのimport適合は生成時の初期サイズではなく、増大後を含む現在サイズを用いる。tableの複数定義・importをmemoryの1個制限で拒否しない。instanceの取得操作は名前に基づくものとし、Exportsコレクションを追加しない。

提供登録の重複拒否は、要件7.6のmodule側の同名import宣言とは区別する。要件7.12は、登録済みの未参照提供元をInstantiateで照合・実行しないという契約である。暗黙の後勝ちによる差し替えは行わない。

### 要件8: 明示型ホストcallbackと値の寿命

**目的:** 埋め込み利用者として、ホスト処理を宣言した関数型で実行し、同期再入や例外があっても値を安全に受け渡したい。

#### 受入基準

1. When ホスト関数を提供する場合, the WasmSharp2 shall 利用者に関数型とcallbackの明示指定を求め、第1引数にinstanceを受け取る形式とinstanceを受け取らない形式を登録時に明示的に区別し、delegateからのWasm関数型の推論や汎用object・dynamic引数の暗黙変換を行わない。
2. When ホスト関数が呼び出された場合, the WasmSharp2 shall 宣言された個数・順序・型の引数をcallbackへ渡し、その戻り値を同じ関数型の結果として受け取る。
3. If callbackが返した結果の個数または順序上の型が宣言と一致しない場合, then the WasmSharp2 shall 呼び出しの契約違反として拒否し、その結果を使ってWasmの後続命令を実行しない。
4. While callbackが同期的に実行されている間, the WasmSharp2 shall 再入先で別の関数が実行されても、そのcallbackに渡した引数の型・値・順序を維持する。
5. When ホストから受け取った結果が呼び出し結果として公開された場合, the WasmSharp2 shall 後続のInvokeやcallback側の返却元領域の再利用によって、返却済みの結果コレクションの要素を変更しない。
6. If ホストcallbackが例外を投げた場合, then the WasmSharp2 shall その例外の型と実体を維持して伝播し、ラップした例外・ランタイムのtrap結果・リンク不成立へ置き換えない。
7. When callbackが同じスレッドで同期的にWasm関数を再呼び出した場合, the WasmSharp2 shall 同じinstanceまたは別instanceへの再入を可能にし、内側の終了後に外側の呼び出しを継続できるようにする。
8. When Wasmからinstanceを受け取る形式のホストcallbackを呼び出した場合, the WasmSharp2 shall 呼び出し元のWasm instanceを第1引数に渡し、startの実行中にも定義memoryを含む割当・初期化済みのexportへアクセス可能にする。
9. When C#からホスト関数をInvokeする場合, the WasmSharp2 shall 呼び出し時に対象instanceを明示可能にし、instanceを受け取る形式では指定された実体をcallbackへ渡し、関数の同一性や別の呼び出しで渡すinstanceを変更しない。
10. If instanceを受け取る形式のホスト関数に対してinstanceを省略するかnullを指定した場合, then the WasmSharp2 shall callbackの実行前に呼び出しの契約違反として拒否し、取得元instanceや進行中の呼び出し元で暗黙に補わない。
11. When instanceを受け取らない形式のホスト関数を呼び出した場合, the WasmSharp2 shall instanceをcallbackへ渡さず、instanceの省略・nullを理由に実行を拒否しない。

callback引数は同期実行中の利用を保証する。実行後も値を保持するための所有方法と具体的な引数・戻り値形式は設計で明示する。参照先オブジェクトや共有リソースの可変状態まで不変にする要求ではない。ホスト自身がWasmTrapException等を投げた場合も元の例外を伝播するため、例外型だけで発生元を必ず識別できるとは保証しない。

instanceを受け取る形式には対象のWasmInstance自体を渡す。このInstance引数はホスト向けの追加情報であり、Wasmの関数型や値引数には含めない。関数の取得経路から対象を固定するラッパーは作らない。C#からの呼び出しは、例えば`h.Invoke(a, arguments)`のようにinstanceを明示できる契約とし、具体的なシグネチャと登録操作は設計で定める。定義Wasm関数は要件2.10に従って元の所属instanceで実行する。

| 呼び出し経路 | instanceを受け取るcallbackへ渡す対象 |
| --- | --- |
| B → Aで定義したWasm関数F → H | A |
| BのWasmコード → Aから再exportされたH | B |
| AのstartとしてHを実行 | A |
| C#からHをInvoke | C#側が呼び出し時に明示したinstance。省略・nullは要件8.10に従って拒否 |

Instance引数は対象instanceへのアクセス情報であり、Instantiateの成功を証明せず、実行ポリシーの選択にも使わない。callbackの形式と呼び出し深さの管理は要件10に従って分離する。判断の根拠は[ADR 0010](../../../docs/adr/0010-host-function-instance-context.md)に従う。

### 要件9: startとインスタンス化の成否

**目的:** 埋め込み利用者として、必要な接続と初期化が済んだ状態でstartを実行し、成功と中断の意味を判断したい。

#### 受入基準

1. If startが存在しない関数を指すか、その関数型が引数0個・結果0個でない場合, then the WasmSharp2 shall Validateで`WasmValidateException`を通知する。
2. When startを持つ検証済みmoduleをInstantiateした場合, the WasmSharp2 shall instanceの構築、必要なimportの照合・接続、定義リソースの割当・対象範囲の初期化を完了してからstartを1回実行し、正常終了後にinstanceを返す。
3. When startがimport関数を指す場合, the WasmSharp2 shall 定義関数と同じstartの型条件で検証し、接続された関数をstartとして実行する。
4. If リンク不成立またはリソースの割当失敗によってインスタンス化を完了できない場合, then the WasmSharp2 shall startを実行せず、成功したinstanceを返さない。
5. If startの実行がtrap・exhaustion・ホスト例外で中断した場合, then the WasmSharp2 shall 成功したinstanceを返さず、要件10の分類で失敗を利用者へ伝える。
6. When startを持つ同じmoduleから複数回Instantiateする場合, the WasmSharp2 shall 各回のインスタンス化でstartを実行し、過去の成功を理由に省略しない。
7. If startが共有リソースまたはホスト状態を更新した後に中断した場合, then the WasmSharp2 shall 中断前に完了した外部状態の更新をロールバックせず、同じ共有先から観測可能な状態として保持する。
8. If startが失敗する前にホストがinstance・関数・リソースの参照を保存していた場合, then the WasmSharp2 shall それらを自動的に無効化せず、保存済みの参照から引き続き操作可能にする。

start失敗時はInstantiateが例外で終了し、startによる初期化の完了を保証しない。保存された参照を使って処理を続けるかはホスト側が判断する。callbackへinstanceを渡せることは、ランタイムとして必要な構築・接続・リソース初期化が済んでいることを意味するが、Instantiateが成功したことは意味しない（[ADR 0011](../../../docs/adr/0011-retain-references-after-start-failure.md)）。

data/element初期化そのものと、それらを含むstart失敗時の複合挙動はlinear-memory・tables-referencesの受入で追加する。未対応segmentのため処理を続行できないmoduleは要件1.5に従ってDecodeまたはValidateで中断し、segmentを省略した成功例として扱わない。

### 要件10: trap・失敗分類と同期呼び出しの上限

**目的:** 埋め込み利用者として、trap、深さ上限、ホスト例外、利用契約違反を区別し、中断後も独立した呼び出しを行いたい。

#### 受入基準

1. When unreachableを実行した場合, the WasmSharp2 shall Wasmのtrapとしてその実行を中断し、同じ呼び出し内で後続命令を実行しない。
2. If ランタイムがInvokeまたはstartの実行中にWasmのtrapを検出した場合, then the WasmSharp2 shall `WasmTrapException`で利用者へ伝え、InvokeとInstantiateのどちらの公開操作が中断したかを識別可能にする。
3. When 直接call・別instanceの関数へのcall・start・ホストcallbackからの同期再入が同じWasm実行コンテキストに属する場合, the WasmSharp2 shall callbackがinstanceを受け取る形式かどうかにかかわらず、ネスト中の呼び出し深さを共有し、再入のたびに深さを初期化しない。
4. When Wasm定義関数への入口またはstartの実行で新しいWasm実行コンテキストを開いた場合, the WasmSharp2 shall 定義関数の所属instanceまたはstartを実行するinstanceの実行ポリシーによる上限を終了まで使い、内側のinstanceやホストcallbackへ渡したinstanceの上限へ切り替えない。
5. If 呼び出し深さが実行コンテキストの上限を超える場合, then the WasmSharp2 shall `WasmExhaustionException`で中断し、Wasmの仕様trapやCLRのスタック枯渇によるプロセス終了へ置き換えない。
6. When ネストした呼び出しが正常終了または失敗によって終了した場合, the WasmSharp2 shall その呼び出しの深さを解放し、終了済みの呼び出しを以後の深さへ累積しない。
7. When 最外側の実行が正常終了・trap・exhaustion・ホスト例外によって終了した場合, the WasmSharp2 shall その実行のコンテキストを後続の独立したInvokeまたはInstantiateへ持ち越さない。
8. If リソース生成・ホスト操作・callbackの結果に呼び出し契約違反、実装制限または資源枯渇が生じた場合, then the WasmSharp2 shall Wasmの仕様trapおよびリンク不成立と区別して利用者へ伝える。
9. When 進行中のWasm実行コンテキストがない状態でC#からホスト関数を直接Invokeした場合, the WasmSharp2 shall callbackの形式とinstance指定の有無にかかわらずWasm実行コンテキストを作成せず、Instance引数で指定したinstanceの実行ポリシーを適用しない。
10. When 要件10.9のホストcallbackからWasm定義関数またはstartへ入る場合, the WasmSharp2 shall その入口で実行コンテキストを開始し、終了時に解除する。既存のWasm実行からの同期再入では、そのコンテキストを引き継ぐ。
11. When 要件10.10で開始したWasm実行が終了してホストcallbackへ戻り、その後に別のWasm実行へ入る場合, the WasmSharp2 shall 後の入口で新しい実行コンテキストを開始し、その入口のinstanceの上限を使い、先に終了した実行の上限を引き継がない。

既存コンテキスト内では両形式のホスト関数も呼び出し深さへ数える。startがホスト関数を指す場合も、startを持つinstanceでコンテキストを開始する。管理する深さ上限はWasm実行コンテキスト内の関数呼び出しを対象とし、任意のホストコード自身のCLR再帰を制限する契約ではない。

次の例は、既存コンテキストがない状態でC#からHを呼び、必要に応じてAをInstance引数に渡した場合を示す。Wasm実行へ入らないホスト側のリソース操作ではコンテキストを開始しない。

| Hから行う処理 | 実行コンテキストと上限 |
| --- | --- |
| Aのmemoryを読み書きするだけ | 開始しない |
| BのWasm定義関数を呼ぶ | Bへの入口で開始し、Bの上限を使う |
| AのWasm定義関数を呼び、その実行中にBも呼ぶ | Aへの入口で開始し、BでもAの上限を引き継ぐ |
| AのWasm実行が終了してHへ戻り、その後Bを呼ぶ | Aの終了時に解除し、Bへの入口で別のコンテキストをBの上限で開始する |

公開例外の基本分類はruntime-foundationを維持する。構文違反はWasmDecodeException、検証不成立はWasmValidateException、リンク不成立はWasmInstantiateException、未実装はWasmUnsupportedFeatureExceptionとする。APIの不正な引数はArgumentException系、利用状態の不正はInvalidOperationExceptionを基準とする。これらで未確定の細分や具体的な例外型は設計で定め、ホストが投げた例外には要件8.6を適用する。

### 要件11: 実行に依存しないimport情報の取得

**目的:** 埋め込み利用者と公式ランナーの作成者として、moduleを実行できない場合も外部依存を調べ、確認済み情報と取得失敗を区別したい。

#### 受入基準

1. When バイナリに含まれるimport情報を公開操作で取得した場合, the WasmSharp2 shall 宣言順にmodule名・item名・外部要素の種類・要求型を取得可能にする。
2. When 型とimportの必要情報を取得でき、無関係な関数本体に未実装命令がある場合, the WasmSharp2 shall module全体のDecode・Validate・Instantiateの成功を要求せずに、そのimport情報を取得可能にする。
3. When import情報を取得する場合, the WasmSharp2 shall ホストの提供元の登録、リソース割当、callbackまたはstartの実行を要求しない。
4. If 破損または未対応箇所によりimport情報を完全には確定できない場合, then the WasmSharp2 shall 情報取得全体を失敗とし、失敗理由と未確認範囲を識別可能にする。一部だけ読めたimport一覧は公開せず、依存がないという確定結果へ置き換えない。
5. When import情報が取得できた場合, the WasmSharp2 shall その取得成功をmodule全体の構文・型の有効性や実行可能性の証明として扱わない。
6. When importがないことを必要な範囲の検査で確認した場合, the WasmSharp2 shall importなしという確定結果を、情報取得の失敗または未確認とは区別して返す。

一覧は必要なimport情報を完全に取得できた場合だけ返し、成功した空一覧をimportなしという確定結果とする。具体的な情報取得操作と失敗診断の形式は設計で定める。ランナーはこの能力から登録依存を特定し、取得できない情報を独自のバイナリ解析やWAST解析で補わない。

### 要件12: 公開操作による受入確認

**目的:** 利用者と実装者として、ホスト連携が公開操作で成立する証拠を得て、後続機能や公式全件適合の完成と混同しないようにしたい。

#### 受入基準

1. When 本仕様の受入確認を行う場合, the WasmSharp2の受入検証 shall 公開操作と正負のバイナリを用い、関数の引数・結果・locals・call/return、global、4種のimport/export、ホストcallback、start、および未対応segmentによる未実装分類とstartの未実行を確認する。
2. When 共有とホストcallbackの受入確認を行う場合, the WasmSharp2の受入検証 shall 型不一致、提供登録の重複拒否、同一実体の再exportと複数instanceでの共有、memory/tableのホストからの更新・増大、memoryの読み出しコピーと現在の領域の区別、callbackの値の寿命・例外・同期再入、およびstart中のcallbackからの定義memoryの取得を確認する。
3. When trapと実行上限の受入確認を行う場合, the WasmSharp2の受入検証 shall unreachableによるInvokeとstartの実trap、直接再帰とホスト再入によるexhaustion、中断後の独立した実行を公開操作で確認する。
4. When import情報取得の受入確認を行う場合, the WasmSharp2の受入検証 shall 完全取得、importなし、未実装関数本体との独立性、破損・未確認による取得不成立を区別できること、および途中で破損した一覧を部分結果として公開しないことを確認する。
5. When 本仕様の検証結果を報告する場合, the WasmSharp2の受入検証 shall 実行した対象と未対応・対象外・未検証の範囲を区別し、本仕様の直接テスト成功を公式スイート全体の合格やCore 2.0準拠と表現しない。
6. When ホスト関数の呼び出し契約を確認する場合, the WasmSharp2の受入検証 shall 両callback形式の登録と実行、instance必須形式での省略・nullの実行前拒否、instanceなし形式の単独実行、同一ホスト関数へ異なるinstanceを明示する呼び出し、および要件8の各経路で渡されるinstanceを確認する。
7. When ホスト関数と実行コンテキストの関係を確認する場合, the WasmSharp2の受入検証 shall instanceの指定・省略を含む単独ホスト呼び出しからWasmへ入る時点での上限選択、要件10の表に示すリソース操作だけ・Bへの直接呼び出し・A経由のネスト・A終了後のB呼び出し、両callback形式から既存コンテキストへ再入したときの深さ共有、およびホスト関数をstartとする場合の上限を確認する。
8. When start失敗後の扱いを確認する場合, the WasmSharp2の受入検証 shall start前の構築・接続・リソース初期化、start中に保存したinstance・関数・リソースの失敗後の操作、および完了済み副作用の保持を確認し、startによる初期化完了を前提としない。

## 仕様上の根拠

- [WebAssembly Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf): 関数と変数命令、unreachable、型とlimitsの検証、外部型の照合、インスタンス化、ホスト関数、リソースの生成・増大。
- [固定した公式仕様の取得元と版](../../../thirdParties/README.md): 本仕様の規則はCore 2.0を基準とし、後の仕様版のGC・型付き参照・memory64等を混在させない。
- [用語集](../../../CONTEXT.md)、[明示的な4段階API](../../../docs/adr/0001-explicit-staged-runtime-api.md)、[ホスト例外の伝播](../../../docs/adr/0007-propagate-host-exceptions.md)、[実行ポリシーとコンテキスト](../../../docs/adr/0008-instance-options-and-execution-context.md): 既存の公開契約を引き継ぐ。
