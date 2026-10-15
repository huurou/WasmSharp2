# 要件文書

## はじめに

WasmSharp2の利用者が、WebAssembly Core 2.0の線形メモリを使うバイナリを、既存のDecode・Validate・Instantiate・Invokeを通して扱えるようにする。
本書は[ブリーフ](brief.md)と[ロードマップ](../../steering/roadmap.md)に基づき、受入基準を定める。
受入基準は「要件番号.項番」で参照する。

## 対象範囲と隣接仕様

| 区分 | 範囲 |
| --- | --- |
| スカラーロード命令 | i32.load、i64.load、f32.load、f64.load、i32.load8_s、i32.load8_u、i32.load16_s、i32.load16_u、i64.load8_s、i64.load8_u、i64.load16_s、i64.load16_u、i64.load32_s、i64.load32_u |
| スカラーストア命令 | i32.store、i64.store、f32.store、f64.store、i32.store8、i32.store16、i64.store8、i64.store16、i64.store32 |
| その他の命令 | memory.size、memory.grow、memory.init、data.drop、memory.copy、memory.fill |
| データセグメント | アクティブデータセグメントとパッシブデータセグメント、データ数セクション、オフセットを指定する定数式、データ初期化と開始関数（start）の実行順序 |
| 既存契約 | host-linkingが提供するメモリ型・サイズの上限と下限・生成・インポート時の型照合・共有・公開操作、numeric-controlが提供する数値演算と制御、既存ランナーが提供するspectest・register・処理段階ごとのアサーションを利用する。 |
| 隣接仕様 | テーブル・要素セグメント・参照命令はtables-references、ベクトル値を読み書きするメモリ命令はsimdで扱う。複数の仕様にまたがるケースに未対応の機能が含まれる場合、その結果を本仕様の成功とは扱わない。 |
| 対象外 | 既存のリソース基盤とランナーの再実装、memory64、multi-memory、スレッド間で共有するメモリ（shared memory/threads）、SIMD、WASI、WAT/WASTの自作解析、OS固有の最適化、性能の数値保証 |

Core 2.0に従い、1つのモジュールが持てるメモリは最大1つとし、32ビットアドレスを対象とする。
本仕様でいうメモリの共有とは、通常のWasmMemoryを複数のインスタンスとホストが同じ実体として参照することである。
スレッド用の共有メモリ（shared memory）は対象に含めない。

**アクティブデータセグメント（active）**：インスタンス化時に指定したメモリへ書き込む初期データ。

**パッシブデータセグメント（passive）**：memory.initで必要なときにコピーできるように保持するデータ。

**実行結果baseline**：同じ公式ケースを再実行したときの比較基準として保存した結果。

## 参照するWasm仕様

固定したコミットは`05ca4182176763112561ae20153975c12bd689e4`である。
以下の本文・参照実装・公式テストはすべて[同じCore 2.0固定版](https://github.com/WebAssembly/spec/tree/05ca4182176763112561ae20153975c12bd689e4)を使用する。

| ID | 参照箇所 | 確認対象 |
| --- | --- | --- |
| S1 | [binary/instructions.rst](../../../thirdParties/WebAssembly-spec/document/core/binary/instructions.rst)のMemory Instructions、[binary/modules.rst](../../../thirdParties/WebAssembly-spec/document/core/binary/modules.rst)のData Section・Data Count Section・Modules | オペコード、メモリ命令の即値（memarg）、予約バイト、データセグメントのフラグ、構文で定められた個数と順序の制約 |
| S2 | [valid/instructions.rst](../../../thirdParties/WebAssembly-spec/document/core/valid/instructions.rst)のMemory Instructions・Constant Expressions、[valid/modules.rst](../../../thirdParties/WebAssembly-spec/document/core/valid/modules.rst)のData Segments・Modules | 型、アラインメント、インデックス、定数式 |
| S3 | [exec/instructions.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/instructions.rst)のMemory Instructions、[exec/numerics.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/numerics.rst)のByte Sequences | メモリ命令の実行、バイト列とビット列、範囲外アクセスによるトラップ |
| S4 | [exec/modules.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/modules.rst)のGrowing Memories・Allocation・Instantiation、[exec/runtime.rst](../../../thirdParties/WebAssembly-spec/document/core/exec/runtime.rst)のMemory Instances・Data Instances | メモリの拡張、インスタンスごとのデータセグメントの状態、初期化の順序 |
| S5 | [test/core](../../../thirdParties/WebAssembly-spec/test/core)、[interpreter/binary/decode.ml](../../../thirdParties/WebAssembly-spec/interpreter/binary/decode.ml)、[interpreter/valid/valid.ml](../../../thirdParties/WebAssembly-spec/interpreter/valid/valid.ml)、[interpreter/exec/eval.ml](../../../thirdParties/WebAssembly-spec/interpreter/exec/eval.ml) | 公式テストの期待結果と診断の選び方 |

診断の前方一致は、Wasmの意味論に加えて[ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)で定めた既存の契約である。
固定する素材と検証条件は[thirdParties/README.md](../../../thirdParties/README.md)に従う。

## 要件

### 要件1: バイナリのデコード

**目的:** 利用者として、メモリ命令とデータセグメントを含むバイナリを読み込み、構文違反を型検証の不成立と区別したい。

**根拠:** S1、S5

#### 受入基準

1. When 対象のスカラーメモリ命令をデコードする, the WasmSharp2 shall 命令と、u32で表されたアラインメントの指数およびオフセットを、元のバイト位置とともに保持する。
2. When memory.size・memory.grow・memory.init・memory.copy・memory.fill・data.dropをデコードする, the WasmSharp2 shall Core 2.0で定められた即値と、値が0x00に固定された予約バイトを解釈し、予約バイトの値が不正な場合や即値が途中で途切れている場合はDecodeの失敗として報告する。
3. When データセクションをデコードする, the WasmSharp2 shall フラグ値0をメモリインデックス0を暗黙に指定するアクティブデータセグメント、1をパッシブデータセグメント、2をメモリインデックスを明示するアクティブデータセグメントとして読み取り、初期バイト列と、必要な場合はオフセット式を保持する。
4. If データセグメントのフラグ・LEB128・セクションの長さ・式の終端・セクションの順序または重複が構文規則に違反する, then the WasmSharp2 shall Decodeの失敗として報告する。
5. If データ数セクションが示す個数がデータセグメント数と一致しないか、コード内でデータセグメントのインデックスを使用しているにもかかわらずデータ数セクションが存在しない, then the WasmSharp2 shall Decodeの失敗として報告する。
6. When データ数セクションの有無と値が構文規則を満たす, the WasmSharp2 shall データセクションの省略、データセグメントを1つも含まないデータセクション、指定が必須ではなくても個数が整合するデータ数セクションを受理する。
7. When 入力バッファまたはStreamからデコードした後に呼び出し元が元の入力を変更する, the WasmSharp2 shall 保持したデータセグメントの内容と命令の意味を変更しない。

### 要件2: 静的検証

**目的:** 利用者として、実行前にメモリとデータセグメントの参照、型、アラインメント、定数式の違反を識別したい。

**根拠:** S2、S5

#### 受入基準

1. When メモリを参照する対象命令を検証する, the WasmSharp2 shall インデックス0のメモリが存在することを要求し、存在しない場合はValidateの失敗として報告する。
2. When ロード命令またはストア命令を検証する, the WasmSharp2 shall アドレスの型がi32であり、読み書きする値の型が命令で指定された数値型であることを検証する。
   ロード命令はその型の値を結果として返し、ストア命令は結果を返さないことを保証する。
3. If ロード命令またはストア命令で指定されたアラインメント（2^alignバイト）が、その命令で読み書きするバイト数を超える, then the WasmSharp2 shall Validateの失敗として報告する。
   ここでalignは、即値に含まれるアラインメントの指数を指す。
4. When memory.size・memory.grow・memory.init・memory.copy・memory.fillを検証する, the WasmSharp2 shall memory.sizeは引数を取らずi32の値を返すこと、memory.growはi32の引数を1つ取りi32の値を返すこと、memory.init・memory.copy・memory.fillはそれぞれi32の引数を3つ取り結果を返さないことを検証する。
5. When memory.initまたはdata.dropを検証する, the WasmSharp2 shall 指定したインデックスのデータセグメントが存在することを検証し、アクティブとパッシブのどちらのデータセグメントも参照可能とする。
6. When メモリを持たないモジュールがパッシブデータセグメントとdata.dropのみを使用する, the WasmSharp2 shall メモリがないことを理由に拒否せず、data.dropが引数を取らず結果も返さないことを検証する。
7. When アクティブデータセグメントを検証する, the WasmSharp2 shall 指定したインデックスのメモリが存在し、オフセット式がCore 2.0で許可された定数式によってi32の値を1つ生成することを要求する。
   global.getによる参照は、インポートした不変のグローバル変数に限定する。
8. While 到達不能な命令を検証している, the WasmSharp2 shall 既存のスタック多相性を適用しつつ、メモリとデータセグメントの参照およびアラインメントの違反を検出し、全体の検証が成功した場合に限って検証成功状態を確定する。

### 要件3: スカラーロード命令とストア命令

**目的:** 利用者として、境界付近や非整列アドレスでも規定どおりのバイト列を読み書きしたい。

**根拠:** S3

#### 受入基準

1. When ロード命令またはストア命令を実行する, the WasmSharp2 shall i32のアドレスを符号なし整数として解釈し、オフセットとの和を32ビットに折り返さずに実効アドレスを求める。
2. When 通常のロード命令またはストア命令を実行する, the WasmSharp2 shall リトルエンディアンで数値のビット列を読み書きする。
3. When 結果の整数幅より狭い幅を読み込む符号付きまたは符号なしのロード命令を実行する, the WasmSharp2 shall 読み込んだ幅から結果の整数幅へ、それぞれ符号拡張またはゼロ拡張する。
4. When 値の下位ビットだけを書き込むストア命令を実行する, the WasmSharp2 shall 値の下位8・16・32ビットのうち、命令が指定する幅だけを書き込む。
5. When 浮動小数点数のロード命令またはストア命令を実行する, the WasmSharp2 shall NaNのペイロードと符号や正負のゼロを含め、元のビット列を保持する。
6. When 範囲内の非整列アクセスまたはページ境界をまたぐアクセスを実行する, the WasmSharp2 shall アラインメントの指定値に実効アドレスが整列していないことを理由にトラップせず、指定した全バイトを読み書きする。
7. If 実効アドレスを起点とするアクセス範囲が現在のメモリ領域を超える, then the WasmSharp2 shall 範囲外アクセスによるトラップを報告し、失敗したストア命令によってメモリの内容を部分的に変更しない。

### 要件4: memory.sizeとmemory.grow

**目的:** 利用者として、複数のインスタンスやホストが共有するメモリの現在のサイズを取得し、拡張の成功と失敗を戻り値で区別したい。

**根拠:** S3、S4、既存のメモリ公開契約

#### 受入基準

1. When memory.sizeを実行する, the WasmSharp2 shall 65,536バイトを1ページとする現在のページ数をi32で返す。
2. When i32を符号なし整数として解釈したページ数だけmemory.growによる拡張に成功する, the WasmSharp2 shall 拡張前のページ数をi32で返し、既存の内容を保持して追加領域をゼロで初期化する。
3. If 要求した拡張がメモリ実体の最大ページ数（インポートしたメモリでは提供元の宣言値）または65,536ページの上限を超えるか、追加領域を確保できない, then the WasmSharp2 shall Wasm命令のmemory.growからi32の-1を返し、現在のメモリのサイズと内容を維持する。
4. When 追加するページ数を0としてmemory.growを実行する, the WasmSharp2 shall 現在のページ数を返し、メモリのサイズと内容を維持する。
5. When 同じメモリを参照するホストまたは別のインスタンスが拡張や書き込みを完了する, the WasmSharp2 shall その後に実行するメモリ命令から、更新後のサイズと内容を観測可能にする。

### 要件5: データセグメントの初期化と寿命

**目的:** 利用者として、ホストや他のインスタンスと共有するメモリへ初期データを配置し、初期化の順序と失敗後の状態を判断したい。

**根拠:** S2、S4、開始関数の失敗時に適用される既存契約

#### 受入基準

1. When 同じモジュールから複数のインスタンスを生成する, the WasmSharp2 shall データセグメントの破棄状態をインスタンスごとに独立させる。
2. When パッシブデータセグメントを含むモジュールをインスタンス化する, the WasmSharp2 shall その内容を後続のmemory.initで使えるように保持し、インスタンス化時にはメモリへ書き込まない。
3. When アクティブデータセグメントを含むモジュールをインスタンス化する, the WasmSharp2 shall データセグメントの宣言順にオフセットを評価して対象メモリへ内容の全体を書き込み、書き込みが成功したデータセグメントを破棄する。
4. When 複数のアクティブデータセグメントが重なる範囲へ書き込む, the WasmSharp2 shall 後に宣言されたデータセグメントの内容をその範囲に残す。
5. When すべてのアクティブデータセグメントの初期化が成功する, the WasmSharp2 shall その後で開始関数を実行し、開始関数から初期化済みのメモリと破棄済みのアクティブデータセグメントを観測可能にする。
6. If アクティブデータセグメントの書き込み範囲がメモリ領域を超える, then the WasmSharp2 shall Instantiate段階のトラップで中断し、失敗したデータセグメントの内容を書き込まず、後続のデータセグメントの初期化と開始関数の実行を行わない。
7. If 後続のデータセグメントの初期化または開始関数の実行が失敗する, then the WasmSharp2 shall 共有するメモリに対してそれ以前に完了した変更を取り消さず、開始関数の実行中にホストが保持したインスタンス・関数・リソースの参照を無効化しない。
8. When アクティブデータセグメントのオフセットが現在のメモリのバイト数と等しく、データセグメントが空である, the WasmSharp2 shall 初期化を成功させる。
   オフセットが現在のメモリのバイト数を超える場合は、空のデータセグメントでもトラップとする。

### 要件6: メモリの一括操作とdata.drop

**目的:** 利用者として、データセグメントからの転送、メモリ領域間のコピー、メモリ領域を指定値で埋める操作、データセグメントの破棄を仕様どおりに実行したい。

**根拠:** S3、S4

#### 受入基準

1. When memory.initを実行する, the WasmSharp2 shall 指定されたデータセグメントのコピー元の位置から、指定されたバイト数をメモリ内のコピー先の位置へコピーし、コピー後もデータセグメントの内容を保持する。
2. When data.dropを実行する, the WasmSharp2 shall 対象インスタンスのデータセグメントを空として扱い、同じデータセグメントを繰り返し破棄しても成功させる。
3. When 破棄済みのデータセグメントに対してmemory.initを実行する, the WasmSharp2 shall コピー元の位置とコピーするバイト数がどちらも0であり、コピー先の位置が範囲内である場合に限って成功させ、それ以外はトラップとする。
4. When memory.copyを実行する, the WasmSharp2 shall 同じメモリ内のコピー元からコピー先へ指定されたバイト数をコピーする。
   コピー元とコピー先が重なる場合は、その前後関係にかかわらず、コピー開始前のコピー元の内容をコピー先へ書き込む。
5. When memory.fillを実行する, the WasmSharp2 shall 指定範囲の各バイトに、指定した値の下位8ビットを書き込む。
6. When メモリの一括操作でアドレスと処理するバイト数を解釈する, the WasmSharp2 shall i32を符号なし整数として扱い、加算の折り返しを起こさずに、各命令が読み書きする全範囲を検査する。
7. If memory.init・memory.copy・memory.fillのいずれかの命令で、処理に必要な範囲が対象のメモリまたはデータセグメントに収まらない, then the WasmSharp2 shall コピーまたは指定値の書き込みを始める前にトラップとし、その命令ではメモリを変更しない。
8. When メモリの一括操作で処理するバイト数が0である, the WasmSharp2 shall 各開始位置が、それぞれが参照する領域のバイト数以下の場合に限って成功させ、領域のバイト数を超える開始位置はトラップとする。

### 要件7: 公開契約と失敗の識別

**目的:** WasmSharp2を組み込む利用者として、既存APIから共有される状態と処理が失敗した段階を、一貫した方法で扱いたい。

**根拠:** 先行仕様、S5、ADR 0004・0009・0011・0012

#### 受入基準

1. When モジュール内で定義したメモリまたはインポートしたメモリに、対象命令とデータセグメントの初期化を適用する, the WasmSharp2 shall 同じ実行規則を適用し、エクスポート・再エクスポートと、ホスト向けの公開読み書き操作から、同じメモリの実体への変更を観測可能にする。
2. When メモリ命令が関数内または開始関数内でトラップする, the WasmSharp2 shall WasmTrapExceptionに原因・元の命令位置・関数インデックスと、InvokeまたはInstantiateの処理段階を保持する。
   データセグメントの初期化時に発生したトラップには、Instantiateの処理段階とデータセグメントのバイト位置を保持する。
3. When 構文違反・検証不成立・リンク不成立・トラップ・実行時の資源上限・実装上の制限・未実装が発生する, the WasmSharp2 shall 既存の失敗分類を維持し、memory.growが返す失敗値やホストAPIでの範囲指定の誤りを、それらの失敗分類と混同しない。
4. When メモリ操作を挟んで、ホストコールバック、同期的な再入、再エクスポートした定義関数の呼び出しが発生する, the WasmSharp2 shall 関数が所属するインスタンスのメモリとデータセグメントを使用し、既存のコンテキストの復元、呼び出しの深さ制限、ホスト側で発生した例外の伝播を維持する。
5. If 後続仕様で扱う未実装の命令や要素セグメントに遭遇して処理を完了できない, then the WasmSharp2 shall 未対応の原因と未確認の範囲を報告し、それらを無視してモジュールや後続処理を成功させない。

### 要件8: 公式適合検証と回帰確認

**目的:** 適合検証担当者として、本仕様の達成と後続機能の実装を待つ状態を区別し、既存の合格を維持したことを確認したい。

**根拠:** S5、ロードマップの公式検証方針

#### 受入基準

1. When 本仕様の機能を追加して受入を確認する, the 適合検証担当者 shall 固定したCore 2.0公式スイート全体を既存ランナーで実行し、対象ケースの期待値・処理段階・結果分類が一致することと、診断の先頭が一致することを確認する。
2. When 本仕様の受入結果を判定する, the 適合検証担当者 shall 全受入基準を公式ケースまたは必要な直接テストへ対応付ける。
   そのうえで、読み書きする幅ごとのスカラーメモリ命令、境界条件、処理するバイト数が0の場合や領域が重なる場合のメモリ一括操作、データ初期化と開始関数の実行、共有される状態を確認する。
3. When 全テストの実行が完了する, the 適合検証担当者 shall 同じ固定入力を使って直前に保存した実行結果baselineと比較し、以前passedだったケースの分類変更や欠落が0件であることを確認する。
4. If 対象に関係するfailed・runner_error・回帰、または本仕様が対応すべき機能の未対応や、その検証に必要な前提の不成立が残る, then the 適合検証担当者 shall 原因を修正して全テストの実行と比較を繰り返し、期待値・結果分類・機能の有効化設定の変更によって合格させない。
5. When tables-referencesまたはsimdで未対応の機能に依存し、複数の仕様にまたがるケースが残る, the 適合検証担当者 shall runtime_unsupportedとblockedの原因を保持し、本仕様の機能だけで実行できるケースの合格や、Core 2.0全体の完成と区別する。
6. When 対象ケースの合格と回帰がないことを確認した結果を、次の比較元として保存する, the 適合検証担当者 shall 比較済みの同じ全テストの実行結果から既存のbaseline-saveを明示的に実行し、保存前に比較元の実行結果baselineを更新しない。
