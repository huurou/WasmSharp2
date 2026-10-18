# 要件文書

## はじめに

WasmSharp2の利用者が、Core 2.0のv128と全SIMD命令を既存のDecode・Validate・Instantiate・Invokeから扱えるようにする。[ブリーフ](brief.md)と[ロードマップ](../../steering/roadmap.md)に従い、既存の値、関数、global、メモリ、ホスト連携と公式ランナーの契約を維持する。

受入基準は「要件番号.項番」で参照する。適合検証担当者とは本仕様の実装結果を確認する開発者を指す。命令名の組合せは固定Core 2.0で定義されたものだけを意味し、同じ接尾辞を全lane型へ拡張しない。

## 対象範囲と隣接仕様

| 区分 | 範囲 |
| --- | --- |
| 対象 | Core 2.0の0xFD命令全体、v128定数、整数・浮動小数点・比較・論理・変換、shuffle・swizzle・splat・lane、vectorメモリ命令 |
| 共通経路 | v128の引数・複数結果・locals・globals・定数初期化・call・分岐・型注釈あり／なしのselect。既存実装を利用し、不足するv128動作を追加する。 |
| 隣接仕様 | numeric-controlの検証・制御、linear-memoryの同じメモリ実体・範囲処理、host-linkingの関数・リソース共有、tables-referencesの間接呼び出しを利用する。 |
| 公式検証 | test-suite-runnerの公開API実行、spectest・register、v128引数構築、laneごとのビット列・NaN判定、診断照合、結果保存・比較を利用する。 |
| 対象外 | relaxed-SIMD、threads、Core 3.0、WASI、WAT/WASTの自作解析、JIT/AOT、独立vector実行系、特定CPUでの速度保証、新しいホスト登録・JSON schema・期待値判定方式 |

## 根拠

規範は固定submoduleの`thirdParties/WebAssembly-spec`（v2.0.0、`05ca4182176763112561ae20153975c12bd689e4`）とする。

- S1: `document/core/binary/instructions.rst`、`document/core/appendix/index-instructions.py` — opcode、即値、命令集合。
- S2: `document/core/valid/instructions.rst`、`interpreter/valid/valid.ml` — 型、lane、alignment、定数式。
- S3: `document/core/exec/instructions.rst`、`document/core/exec/numerics.rst`、`interpreter/exec/` — lane演算、浮動小数点、メモリアクセス。
- S4: `test/core/simd/`およびv128を使用する`test/core/` — 公式ケース。診断互換性は[ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)、固定条件は[thirdParties/README](../../../thirdParties/README.md)に従う。
- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)は参照用とし、版の差が生じたときは上記固定commitを優先する。

## 要件

### 要件1: SIMDバイナリと処理段階

**目的:** 利用者として、SIMDを含むバイナリを読み込み、構文上の不正と型検証の不成立を区別したい。

#### 受入基準

1. When 先行機能とCore 2.0 SIMDからなるバイナリをDecodeする場合, the WasmSharp2 shall バイト列と読み取り可能なストリームの両方で同じ命令・即値を読み取り、関数やstartを実行しない。
2. When 0xFDに続く命令番号を読み取る場合, the WasmSharp2 shall u32の符号化として解釈し、仕様で許される最短以外のLEB128も受け付ける。
3. When SIMD即値を読み取る場合, the WasmSharp2 shall v128.constの16byte、shuffleの16個のbyte、laneの1byte、memargとlaneの組合せを含む各命令の即値を欠落なく保持する。
4. If 命令番号の整数符号化が不正、必要な即値が切断、またはCore 2.0に存在しないopcodeである場合, then the WasmSharp2 shall Decodeの不正として拒否し、未対応やValidate成功として扱わない。
5. When 構文上読めるがlane値・alignment・operand型が不正な命令をDecodeする場合, the WasmSharp2 shall それらの有効性をValidateで判定する。
6. If SIMD処理が失敗する場合, then the WasmSharp2 shall 既存の段階別例外、原因、元バイナリ位置と適用可能な関数・section情報を保持し、否定assertionでは固定公式期待診断への前方一致を満たす。

### 要件2: 型と即値の検証

**目的:** 利用者として、無効なSIMDプログラムを実行前に拒否し、有効なプログラムを未対応と区別したい。

#### 受入基準

1. When SIMD命令をValidateする場合, the WasmSharp2 shall 固定仕様の入力型・個数と結果型・個数を検査し、型不一致や不足を拒否する。
2. When lane即値を持つ命令をValidateする場合, the WasmSharp2 shall lane幅に対応する上限未満のindexだけを受け付ける。
3. When i8x16.shuffleをValidateする場合, the WasmSharp2 shall 16個すべての選択値が0以上32未満であることを要求する。
4. When SIMDメモリ命令をValidateする場合, the WasmSharp2 shall memory 0の存在と命令ごとの自然alignment以下の指定を要求し、拡張load・zero load・splat・lane操作では実際のアクセス幅に対応する上限を使う。
5. While 命令が到達不能部分にある場合, the WasmSharp2 shall 既存のstack polymorphismを適用しつつ、lane・shuffle・alignment・参照するmemoryの静的制約を検査する。
6. When v128.constをglobal初期化式で使用する場合, the WasmSharp2 shall v128の結果型を検証して受け付け、SIMD演算を含む式や要求型の異なる式を定数式として受け付けない。
7. When module全体のValidateが失敗する場合, the WasmSharp2 shall Instantiate可能な検証成功状態を確定せず、既存の再検証契約を維持する。

### 要件3: v128の保持と共通経路

**目的:** 利用者として、vector専用の呼び出し方式を覚えずに128bitを正確に受け渡したい。

#### 受入基準

1. When v128を構築・取得・受け渡す場合, the WasmSharp2 shall 128bitすべてを保持し、lane 0を低位側とする固定仕様の並びで解釈する。
2. When v128.constを実行する場合, the WasmSharp2 shall 即値の128bitをそのまま結果とし、浮動小数点として見たNaN payloadや符号付きゼロを変えない。
3. When v128を引数・複数結果・locals・直接call・間接call・return・block・loop・if・分岐・dropで扱う場合, the WasmSharp2 shall 既存の型規則と順序で値を運び、未初期化localを全bitゼロにする。
4. When v128の型注釈ありまたはなしのselectを実行する場合, the WasmSharp2 shall i32条件に従って同型の候補を選び、選択した128bitを保持し、不適合な候補型をValidateで拒否する。
5. When v128 globalを定義・import・export・get・setする場合, the WasmSharp2 shall 定数式による初期値、mutable制約、型照合と共有するglobalの同一性を維持する。
6. When v128をホスト関数・再export・同期再入・startを含む既存経路で扱う場合, the WasmSharp2 shall ビット列、明示的な関数型、呼び出し元情報、失敗伝播と実行コンテキストの既存契約を維持する。

### 要件4: lane操作と論理・比較

**目的:** 利用者として、vectorの構築・選択・条件判定を仕様どおりに実行したい。

#### 受入基準

1. When splat・extract_lane・replace_laneを実行する場合, the WasmSharp2 shall 指定lane幅とスカラー型に従い、整数の切詰め・符号拡張・ゼロ拡張と、置換対象以外のlaneの保持を行う。
2. When i8x16.shuffleを実行する場合, the WasmSharp2 shall 第1vectorに続けて第2vectorを並べた32byteから即値で選択した16byteを返す。
3. When i8x16.swizzleを実行する場合, the WasmSharp2 shall index vectorの各byteが16未満なら対応byteを選び、それ以外ならゼロを返す。
4. When v128のnot・and・andnot・or・xor・bitselectを実行する場合, the WasmSharp2 shall 128bitの各bitへ指定演算を適用し、bitselectではmaskのbitが1の箇所を第1operandから選ぶ。
5. When 整数または浮動小数点のvector比較を実行する場合, the WasmSharp2 shall laneごとの比較結果を同幅の全bit1または全bit0で返し、符号付き・符号なしとNaNの規則を区別する。
6. When any_true・all_true・bitmaskを実行する場合, the WasmSharp2 shall 非ゼロ判定または各laneの最上位bitに従ったi32を返し、lane 0の符号bitをbitmaskの最下位bitへ配置する。

### 要件5: 整数SIMD演算

**目的:** 利用者として、lane幅に依存する整数演算をホストCPUの挙動に左右されず使用したい。

#### 受入基準

1. When abs・neg・add・sub・mulを実行する場合, the WasmSharp2 shall 各lane幅の固定仕様に従って演算し、通常整数演算のoverflowはその幅に折り返す。
2. When add_sat・sub_sat・narrowを実行する場合, the WasmSharp2 shall 命令固有の入力解釈と出力の符号付き／符号なし範囲に従って飽和し、narrowの2operandを規定順に連結する。
3. When shl・shr_s・shr_uを実行する場合, the WasmSharp2 shall i32シフト量をlane幅で剰余化し、符号付き右シフトとゼロ埋め右シフトを区別する。
4. When min・max・avgr_u・popcntを実行する場合, the WasmSharp2 shall 対象laneの符号解釈、平均の切上げ、byteごとのbit数を命令規則に従って計算する。
5. When extend_low・extend_high・extmul_low・extmul_highを実行する場合, the WasmSharp2 shall 選択した半分のlaneを指定の符号解釈で拡張し、拡張幅で結果を返す。
6. When extadd_pairwise・dot・q15mulr_sat_sを実行する場合, the WasmSharp2 shall laneの組合せ、符号解釈、加算幅、丸めと飽和の境界を固定仕様どおりに扱い、特殊な最小値の組合せも正しく処理する。

### 要件6: 浮動小数点と変換

**目的:** 利用者として、vectorでも丸め、NaN、飽和変換を仕様に従って扱いたい。

#### 受入基準

1. When f32x4またはf64x2のabs・negを実行する場合, the WasmSharp2 shall 符号bitだけを操作し、他のbitを保持する。
2. When 浮動小数点のadd・sub・mul・div・sqrt・ceil・floor・trunc・nearestを実行する場合, the WasmSharp2 shall lane精度に対応した結果を返し、最近接偶数丸め、非正規化数、無限大、符号付きゼロとNaNを固定仕様の許容結果に従って扱う。
3. When 浮動小数点のmin・max・pmin・pmaxを実行する場合, the WasmSharp2 shall 通常のmin/maxと比較選択によるpmin/pmaxを区別し、NaNと符号付きゼロでoperand順に依存する規則を保持する。
4. When vectorのtrunc_sat変換を実行する場合, the WasmSharp2 shall NaNをゼロ、範囲外を変換先の上下限とし、有限値をゼロ方向へ切り捨て、数値trapを発生させない。
5. When vectorのconvert・demote・promoteおよびzero付き変換を実行する場合, the WasmSharp2 shall 符号解釈と変換先精度に従い、lowで指定されたlaneだけを変換し、zeroで指定された上位laneをゼロにする。
6. While CPU固有のSIMD支援が利用できない場合, the WasmSharp2 shall 同じCore 2.0命令を実行でき、支援の有無によって仕様で許されない結果や未対応を生じさせない。

### 要件7: SIMDメモリアクセス

**目的:** 利用者として、既存の線形メモリに対するvectorの読み書きと範囲外trapを扱いたい。

#### 受入基準

1. When v128.load・v128.storeを実行する場合, the WasmSharp2 shall 実効アドレスから16byteをlittle-endianで読み書きし、非整列アドレスも受け付ける。
2. When 拡張load・splat load・zero loadを実行する場合, the WasmSharp2 shall 命令が指定するbyte数だけを読み、符号拡張／ゼロ拡張、複製、残りのbitのゼロ化を行う。
3. When load_lane・store_laneを実行する場合, the WasmSharp2 shall 指定lane幅のbyte数だけを読み書きし、loadではそれ以外のlaneを保持し、storeでは対象範囲外のメモリを変更しない。
4. When SIMDメモリ命令の実効アドレスを求める場合, the WasmSharp2 shall i32アドレスを符号なしとしてoffsetを加え、32bitの折返しで本来範囲外のアクセスを受け付けない。
5. If アクセスする範囲が現在のメモリ領域を超える場合, then the WasmSharp2 shall memory境界のtrapを返し、失敗したstoreによる部分書込みを行わない。
6. When ホストや別instanceと共有するメモリをSIMDで操作する場合, the WasmSharp2 shall 同じメモリ実体へ反映し、data初期化・memory.grow・スカラーメモリ操作と一貫した内容を観測できるようにする。
7. When SIMDメモリtrapがInvokeまたはstartで発生する場合, the WasmSharp2 shall 該当段階と位置を報告し、先行する成功済み副作用、ホストが保持した参照、および後続の呼び出しに対する既存契約を維持する。

### 要件8: 固定公式検証と完了判定

**目的:** 適合検証担当者として、命令の追加を同じ公式条件で確認し、既存動作の回帰を検出したい。

#### 受入基準

1. When SIMDを検証する場合, the 適合検証担当者 shall 固定Core 2.0のprofile・公式素材を使い、SIMDを有効のまま保ち、relaxed-SIMD等の対象外機能を追加しない。
2. When SIMD機能の実装単位を統合する場合, the 適合検証担当者 shall 既存ランナーで固定スイート全体を実行し、v128のlaneビット列・NaN pattern、型・個数、段階・診断の既存判定を使用する。
3. When 全体実行の結果を確認する場合, the 適合検証担当者 shall 直前の受入済みbaselineと元入力・command単位で比較し、以前のpassedの別分類への変化や欠落を回帰として拒否する。
4. If 対象ケースに失敗や未対応・前提不成立が残る場合, then the 適合検証担当者 shall 出典・期待・実際・原因を記録し、ランナーやホスト設定の不足をランタイム未対応へ置き換えず、SIMD自身の未完成を合格扱いにしない。
5. When SIMD仕様の完了を判定する場合, the 適合検証担当者 shall 前提の揃った全対象ケースのpassed、failed・runner_error・回帰0と、Core 2.0の対象規則と実装・確認結果の対応を要求し、命令登録数だけで完成としない。
6. When 比較結果に問題がなく受入結果を採用する場合, the 適合検証担当者 shall 比較済みの全体実行結果を既存の保存操作で明示的にbaselineへ保存し、保存成功とスイート合格を区別する。
7. When SIMDを最後の機能としてCore 2.0全体の完了を判定する場合, the 適合検証担当者 shall 全8仕様を統合した単一の全体実行結果に既存verifyを適用し、out_of_scopeを除く全ケースがpassedで、未処理・欠落がなく、failed・runtime_unsupported・runner_error・blockedがすべて0であることを要求する。
