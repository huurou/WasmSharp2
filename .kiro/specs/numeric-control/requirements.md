# 要件文書

## はじめに

WasmSharp2の利用者が、スカラー数値演算と構造化制御を持つWebAssembly Core 2.0バイナリを、既存のDecode・Validate・Instantiate・Invokeから扱えるようにする。数値結果だけでなく、型検証、分岐による値の受渡し、数値trap、制御構文を使う再帰とexhaustionを対象とする。

入力は[ブリーフ](brief.md)と[ロードマップ](../../steering/roadmap.md)とする。ロードマップに従い、`runtime-foundation`・`host-linking`・`test-suite-runner`・`test-suite-conformance`の完成済み契約と受入記録を引き継ぐ。用語は[CONTEXT.md](../../../CONTEXT.md)に従う。受入基準は「要件番号.項番」で参照し、「適合検証担当者」は本仕様の実装結果を確認する開発者を指す。

## 対象範囲と隣接仕様

| 区分 | 範囲と前提 |
| --- | --- |
| 整数命令 | i32/i64の`const`、`clz/ctz/popcnt`、`add/sub/mul/div_s/div_u/rem_s/rem_u`、`and/or/xor/shl/shr_s/shr_u/rotl/rotr`、`eqz/eq/ne/lt_s/lt_u/gt_s/gt_u/le_s/le_u/ge_s/ge_u` |
| 浮動小数点命令 | f32/f64の`const`、`abs/neg/sqrt/ceil/floor/trunc/nearest`、`add/sub/mul/div/min/max/copysign`、`eq/ne/lt/gt/le/ge` |
| 変換命令 | `i32.wrap_i64`、`i64.extend_i32_s/u`、f32/f64からi32/i64への全組合せの`trunc_s/u`・`trunc_sat_s/u`、i32/i64からf32/f64への全組合せの`convert_s/u`、`f32.demote_f64`、`f64.promote_f32`、同幅の整数と浮動小数点の双方向`reinterpret` |
| 同じ整数型内の符号拡張 | `i32.extend8_s/extend16_s`、`i64.extend8_s/extend16_s/extend32_s` |
| 制御と選択 | `nop`、`block/loop/if/else/end`、`br/br_if/br_table`、複数値block・loop引数、i32/i64/f32/f64の型注釈なし・ありの`select` |
| 先行契約との統合 | const、直接call・return・unreachable・drop、引数・locals・結果、global、import/export、ホストcallback・同期再入、start、実行ポリシーによる深さ制限。既存の値・状態・失敗契約を維持する。 |
| 共通の制御による値の受渡し | block/loop/ifと分岐の引数・結果はCore 2.0の値型を扱い、先行契約が扱うv128・funcref・externrefもビット列または参照の同一性を保持して運ぶ。参照固有命令やSIMD演算の追加とは分ける。 |
| 公式検証 | 既存ランナーによる固定公式スイート全体の実行、期待値・診断の照合、直前baselineとの回帰比較。spectest・register、各値型の比較、結果分類・保存は既存の能力を利用する。 |
| 後続仕様 | memory命令・data初期化は`linear-memory`、table命令・element初期化・call_indirect・参照命令・参照のtyped selectは`tables-references`、SIMD命令とv128のselectは`simd`で扱う。 |
| 対象外 | 既存の関数・リソース・ホスト登録・ランナーの再実装、WAT/WASTの構文解析、Core 3.0、extended-const、WASI、JIT/AOT、実行時間や命令数による新しい中断保証、性能の数値保証 |

上表の命令名はCore 2.0の正式な組合せだけを指す。スカラーselectの対象を限定することは、Core 2.0がv128や型注釈付き参照selectを禁止することを意味しない。未実装と検証不成立の区別は要件1.6に従う。定数式の許可命令はCore 2.0のままとし、関数内で数値・制御命令を実行できることを、global初期化式等での使用許可に広げない。

## 参照するWasm仕様

2026-10-08に、親リポジトリのgitlinkとローカルsubmoduleのHEADがともに`05ca4182176763112561ae20153975c12bd689e4`であることを確認した。以下はすべて[固定したWebAssembly/spec v2.0.0](https://github.com/WebAssembly/spec/tree/05ca4182176763112561ae20153975c12bd689e4)の資料である。別版の仕様・テスト・参照実装を混在させない。読みやすい版として[Core 2.0保存版PDF](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)も参照できるが、本書の規則との対応は固定commitの本文で確認する。

| ID | 参照する本文・節 | 本書で確認する内容 |
| --- | --- | --- |
| S1 | [構造: Numeric Instructions / Control Instructions](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/syntax/instructions.rst)、[値: Floating-Point](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/syntax/values.rst) | 命令集合、値型、canonical/arithmetic NaN |
| S2 | [バイナリ: Control / Parametric / Numeric Instructions](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/binary/instructions.rst)、[値の符号化](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/binary/values.rst) | blocktypeのs33、ラベル添字、selectの型ベクトル、定数、0xFCの命令番号、else/end |
| S3 | [検証: Numeric / Parametric / Control Instructions、Instruction Sequences、Constant Expressions](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/instructions.rst)、[Block Types](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/types.rst)、[Functions](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/modules.rst) | 入出力の型、ラベル、複数値、型の多相性、関数の暗黙ラベル、定数式の制限 |
| S4 | [実行: Numerics](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/numerics.rst)のInteger Operations、Floating-Point Operations、Rounding、NaN Propagation、Conversions | 整数・浮動小数点・変換の意味論、部分演算、丸めとNaNの許容範囲 |
| S5 | [実行: Numeric / Parametric / Control Instructions](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/instructions.rst) | operandの順序、select、分岐先、値の保持と破棄、call・return・trap |
| S6 | [実行: Modules](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/modules.rst)、[Implementation Limitations](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/appendix/implementation.rst) | start・呼出し、実行資源の上限。具体的な公開契約は先行仕様を継承する。 |
| S7 | [Validation Algorithm](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/appendix/algorithm.rst)、[参照実装の検証](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/interpreter/valid/valid.ml) | 到達不能部分とbr_tableの型適合を照合する補助資料。アルゴリズムの採用は本書では指定しない。 |
| S8 | [参照実装の実行・診断](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/interpreter/exec/eval.ml)、[i32変換](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/interpreter/exec/i32_convert.ml)、[i64変換](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/interpreter/exec/i64_convert.ml)、[公式test/core](https://github.com/WebAssembly/spec/tree/05ca4182176763112561ae20153975c12bd689e4/test/core) | 数値trapの原因と診断先頭、公式の期待結果。診断文字列の互換性はWasm仕様の意味論に加えた既存プロジェクト契約。 |

公開段階と失敗分類は[host-linking要件](../host-linking/requirements.md)、診断互換性は[test-suite-conformance要件](../test-suite-conformance/requirements.md)と[ADR 0012](../../../docs/adr/0012-reference-diagnostic-compatibility.md)、固定素材と検証プロファイルは[外部ソースの固定](../../../thirdParties/README.md)に従う。内部の型検査・線形化・実行方式は既存ADRを設計時に適用し、以下の受入基準では観測できる結果を規定する。

## 要件

### 要件1: 対象バイナリと処理段階

**目的:** 利用者として、数値・制御を含むmoduleを段階ごとに処理し、構文違反と型検証の不成立を区別したい。

**根拠:** S1・S2・S3、先行仕様の公開4段階契約。

#### 受入基準

1. When 先行機能と本仕様の命令だけで構成したバイナリをDecodeする場合, the WasmSharp2 shall バイト列と読み取り可能なストリームのどちらからも同じmodule定義を得られるようにし、関数・ホストcallback・startを実行しない。
2. When block/loop/ifのblocktypeをDecodeする場合, the WasmSharp2 shall 空型、単一の値型、非負のs33で表した型添字の3形式を受け付け、型添字の存在と引数・結果の適合をValidateで判定できるようにする。
3. When 対象命令の即値をDecodeする場合, the WasmSharp2 shall 定数、ラベル添字、br_tableの候補列と既定ラベル、selectの型ベクトル、0xFCに続く飽和変換のu32命令番号を固定Core 2.0の符号化に従って解釈し、仕様で許される最短以外の整数符号化も受け付ける。
4. If 対象範囲に不正なopcode・即値の符号化・切れた入力・不正なelse/endの配置または式とsectionの境界違反がある場合, then the WasmSharp2 shall 対応するDecodeの失敗として拒否し、末尾のendを省略した入力や入れ子の終端を取り違えた入力を受け入れない。
5. When 対象moduleのValidateが成功する場合, the WasmSharp2 shall module全体が検証済みとなった後にだけInstantiateを許可し、Validate中にホストcallbackやstartを実行しない。
6. If 後続仕様の未実装機能により処理を完了できない場合, then the WasmSharp2 shall `WasmUnsupportedFeatureException`で段階・機能・未確認範囲を識別可能にし、仕様上の有効性が確認済みとは扱わず、未実装を検証不成立や成功へ読み替えない。

### 要件2: 数値命令とスカラーselectの型検証

**目的:** 利用者として、数値演算や選択に必要な値の型と個数が実行前に検証されることを期待する。

**根拠:** S3のNumeric Instructions・Parametric Instructions・Constant Expressions。

#### 受入基準

1. When 数値命令をValidateする場合, the WasmSharp2 shall 定数、単項演算、二項演算、比較、変換ごとに入力型・個数と出力型・個数を検証し、eqzと比較の結果型をi32として扱う。
2. If 到達可能な命令の入力値が不足するか必要な型と一致しない場合, then the WasmSharp2 shall 暗黙の数値変換で補わずValidateで拒否する。
3. When 型注釈なしのスカラーselectをValidateする場合, the WasmSharp2 shall 2候補が同じスカラー数値型で条件がi32なら受け付け、候補間の型不一致・i32以外の条件・参照型候補を拒否する。
4. When 型注釈付きのスカラーselectをValidateする場合, the WasmSharp2 shall 型ベクトルが1個の対象型を持ち、両候補がその型で条件がi32の場合に受け付ける。
5. If 構文上は正しい型付きselectの型ベクトル長が0または2以上の場合, then the WasmSharp2 shall Decodeの構文違反ではなくValidateの不成立として拒否する。
6. If global初期化式等の定数式にCore 2.0で許可されない数値演算・select・構造化制御が現れる場合, then the WasmSharp2 shall 関数内での対応状況にかかわらずValidateで拒否する。

### 要件3: 構造化制御と分岐の型検証

**目的:** 利用者として、入れ子の制御と複数値の受渡しを検証し、到達不能部分も仕様どおり判定したい。

**根拠:** S3のControl Instructions・Instruction Sequences・Block Types・Functions、S7。

#### 受入基準

1. When block/loop/ifをValidateする場合, the WasmSharp2 shall 宣言した引数・結果の0個・1個・複数と順序上の型を検証し、型添字が存在しない場合はValidateで拒否する。
2. When ifをValidateする場合, the WasmSharp2 shall 条件にi32を要求し、条件が定数でも両側の命令列を同じ宣言引数・結果に対して検証する。
3. When elseを省略したifをValidateする場合, the WasmSharp2 shall 省略部分を空の命令列として判定し、引数型列と結果型列が同じなら結果付きifも受け付け、一致しない場合は拒否する。
4. When 分岐先ラベルをValidateする場合, the WasmSharp2 shall 最内側を深さ0とし、関数本体の暗黙ラベルまでを有効範囲として、block/ifでは結果型列、loopでは引数型列、関数では関数結果型列への適合を検証する。
5. When br_ifをValidateする場合, the WasmSharp2 shall 条件にi32を要求し、条件が定数0でも分岐先と分岐値を検証し、非分岐時に同じ分岐値が残る型規則を適用する。
6. When br_tableをValidateする場合, the WasmSharp2 shall i32の選択値と、既定ラベルを含む全ラベルの存在・同じ引数個数・分岐値の型列との適合を検証し、到達不能部分で未確定のoperand型が各ラベル型に適合する場合は、具体的なラベル型の一律一致を要求せず受け付ける。
7. When 制御の入口・通常終了・分岐をValidateする場合, the WasmSharp2 shall 要件3.8の多相性を適用して必要な値の型と個数を検証し、通常終了時には宣言結果以外の当該制御内の未消費値を拒否し、内側の制御が引数として受け取っていない外側の値を消費することを許可しない。
8. When unreachable・br・br_table・returnの後続部分をValidateする場合, the WasmSharp2 shall Core 2.0の局所的なスタック多相性を適用し、有効な到達不能列を受け付けつつ、具体型の矛盾・不正な添字・余分な値・入れ子内の不足を検出し、内側の到達不能を外側へ無条件に適用しない。
9. When 既存の引数・locals・結果から得たv128・funcref・externrefを制御の引数・結果や分岐値に使う場合, the WasmSharp2 shall スカラーに限定せず共通の値型として検証し、それらの運搬だけを理由にSIMD演算や参照固有命令の実装を要求しない。

### 要件4: 整数演算とビット操作

**目的:** 利用者として、i32/i64の計算結果をWasmの幅と符号解釈で得たい。

**根拠:** S4のInteger Operations、S5のNumeric Instructions。

#### 受入基準

1. When i32/i64のadd・sub・mulを実行する場合, the WasmSharp2 shall それぞれ2の32乗・2の64乗を法とする結果を返し、通常の桁あふれをtrapにしない。
2. When 整数のdiv_s/div_u/rem_s/rem_uを非ゼロ除数で実行し、div_sの最小値÷−1に該当しない場合, the WasmSharp2 shall 命令の符号解釈に従い、商を0方向へ切り捨て、符号付き剰余の非ゼロ結果を被除数と同じ符号にする。
3. If 整数div/remの除数が0、またはdiv_sの被除数が符号付き最小値で除数が−1の場合, then the WasmSharp2 shall 対応する数値trapで処理を中断する。
4. When rem_sで符号付き最小値と−1を計算する場合, the WasmSharp2 shall trapを発生させず0を返す。
5. When and/or/xor・shl/shr_s/shr_u・rotl/rotrを実行する場合, the WasmSharp2 shall 対象幅のビット演算を行い、シフト・回転量を対象幅を法として扱い、右シフトの符号拡張とゼロ拡張を命令に従って区別する。
6. When clz/ctz/popcntを実行する場合, the WasmSharp2 shall 対象幅内の先頭ゼロ・末尾ゼロ・1の個数を返し、入力0のclz/ctzは対象幅、popcntは0とする。
7. When eqz・整数比較を実行する場合, the WasmSharp2 shall 命令ごとの符号付き・符号なしの解釈で判定し、成立ならi32の1、不成立ならi32の0を返す。

### 要件5: 浮動小数点演算とNaN

**目的:** 利用者として、f32/f64の演算で丸め、NaN、無限大、符号付き0を仕様どおり観測したい。

**根拠:** S1のFloating-Point、S4のFloating-Point Operations・Rounding・NaN Propagation。

#### 受入基準

1. When f32/f64のadd/sub/mul/div/sqrtを実行する場合, the WasmSharp2 shall 各命令の結果を対象幅へ最近接・同距離なら偶数となる規則で丸め、非正規化数、桁あふれ、アンダーフローを含めS4の結果に一致させる。
2. When 複数の浮動小数点命令を続けて実行する場合, the WasmSharp2 shall 各命令の丸めと特殊値の規則を適用した結果を返し、途中の丸めの省略や演算の結合によって観測結果を変えない。
3. When ceil/floor/trunc/nearestを実行する場合, the WasmSharp2 shall 順に正の無限大方向・負の無限大方向・0方向・最近接偶数への整数値化を行い、仕様で定める無限大と0の符号を保持する。
4. When abs/neg/copysignを実行する場合, the WasmSharp2 shall 順に符号を正へ変更・符号を反転・第2operandの符号を第1operandへ設定し、第1operandの符号以外のビット列をNaN payloadも含め保持する。
5. When min/maxを実行する場合, the WasmSharp2 shall NaNを含む場合はS4のNaN規則に従い、非NaNでは小さい値・大きい値を返し、+0と−0の組合せではminを−0、maxを+0とする。
6. If 浮動小数点演算が0除算・負数の平方根・無限大同士の不定な演算・桁あふれ等に該当する場合, then the WasmSharp2 shall S4が定めるNaN・無限大等を返し、それらを整数演算のtrapへ置き換えない。
7. When abs/neg/copysign以外の浮動小数点演算がNaNを返す場合, the WasmSharp2 shall NaN入力がすべてcanonicalまたはNaN入力がない場合はcanonical NaN、それ以外はarithmetic NaNを返し、仕様で許される符号とpayloadの範囲を守る。
8. When 浮動小数点比較を実行する場合, the WasmSharp2 shall +0と−0を等しい値として扱い、NaNを含む比較ではneだけを成立とし、結果をi32の0または1で返す。

### 要件6: 数値変換と再解釈

**目的:** 利用者として、数値型や整数幅を変える際の境界値、飽和、ビット列の保持を区別したい。

**根拠:** S4のConversions・Sign Extension、S8の変換と診断。

#### 受入基準

1. When i32.wrap_i64またはi64.extend_i32_s/uを実行する場合, the WasmSharp2 shall 下位32bitの抽出、32bit値の符号拡張、32bit値のゼロ拡張をそれぞれ行う。
2. When extend8_s/extend16_s/extend32_sの対象命令を実行する場合, the WasmSharp2 shall 指定した下位幅を符号付きとして対象の整数幅へ拡張し、入力の残りの上位bitを結果に反映しない。
3. When 有限の浮動小数点値をtrunc_s/uで整数へ変換する場合, the WasmSharp2 shall 0方向へ切り捨てた後の整数が変換先の符号付き・符号なし範囲に入る場合にその値を返し、符号なし変換でも−1より大きく0より小さい入力は0とする。
4. If trunc_s/uの入力がNaN・無限大、または切り捨て後の整数が変換先の範囲外である場合, then the WasmSharp2 shall NaNによる不正整数変換と、それ以外の整数overflowを区別した数値trapを通知する。
5. When trunc_sat_s/uを実行する場合, the WasmSharp2 shall NaNを0、有限値を0方向へ切り捨てて変換先範囲へ飽和した値、負・正の無限大を変換先の下限・上限へ変換し、数値変換によるtrapを発生させない。
6. When convert_s/u・demote・promoteを実行する場合, the WasmSharp2 shall S4に従い、整数の符号解釈と変換先幅への最近接偶数丸め、非NaNのf32からf64への正確な値の保持、符号付き0・無限大、変換先のcanonical/arithmetic NaNの規則を適用する。
7. When 同幅の整数と浮動小数点のreinterpretを実行する場合, the WasmSharp2 shall 数値としての変換をせず、NaNの符号・payloadや符号付き0も含む全ビット列を保持する。

### 要件7: 構造化制御と分岐の実行

**目的:** 利用者として、条件分岐・繰返し・複数値の受渡しを組み合わせたWasmの結果を得たい。

**根拠:** S5のControl Instructions、S3のラベルと関数結果の規則。

#### 受入基準

1. When nopを実行する場合, the WasmSharp2 shall 値や観測可能な状態を変更せず後続命令へ進む。
2. When blockまたはloopへ入り通常終了する場合, the WasmSharp2 shall 宣言順の引数を本体へ渡し、宣言順の結果を後続へ渡し、loopの通常終了を繰返しとして扱わない。
3. When ifを実行する場合, the WasmSharp2 shall i32条件が0ならelse、それ以外ならthenだけを実行し、選択した側へ宣言引数を渡して宣言結果を得る。
4. When brでblock/ifまたは関数の暗黙ラベルへ分岐する場合, the WasmSharp2 shall 対象の結果値を保持してその制御または関数を終了し、飛び越えた命令を実行しない。
5. When brでloopラベルへ分岐する場合, the WasmSharp2 shall 対象loopの引数を保持して次の反復の入口へ渡し、loopの結果を返して終了する動作と区別する。
6. When br_ifを実行する場合, the WasmSharp2 shall 条件を消費し、非ゼロなら対応するbrと同じ分岐を行い、0なら分岐値をそのまま残して後続命令へ進む。
7. When br_tableを実行する場合, the WasmSharp2 shall i32選択値を符号なしとして解釈し、候補数より小さければ該当候補、それ以外または候補が空なら既定ラベルへ分岐する。
8. When 入れ子を越える分岐やreturnを実行する場合, the WasmSharp2 shall 必要な結果またはloop引数の個数・順序を保持し、抜ける範囲のその他の一時値を破棄し、残る外側の値・locals・呼出し元の値を変更しない。
9. When 制御の引数・結果または分岐値としてスカラー・v128・参照を運ぶ場合, the WasmSharp2 shall 演算による変換を加えず、ビット列・null・参照先の同一性を保って渡す。

### 要件8: selectと先行機能との組合せ

**目的:** 利用者として、数値・制御を既存の関数、global、ホスト連携の中でも同じ意味で使いたい。

**根拠:** S5のParametric Instructions・call/return・Variable Instructions、S6、host-linkingの既存契約。

#### 受入基準

1. When 型注釈なし・ありのスカラーselectを実行する場合, the WasmSharp2 shall i32条件が非ゼロなら第1候補、0なら第2候補の1値を返し、選択した値のNaN payloadと符号付き0を含むビット列を保持する。
2. When selectの候補や条件を計算する命令列を実行する場合, the WasmSharp2 shall 命令列の順序に従って計算と副作用を実行し、選ばれない候補の計算を省略する短絡評価へ変えない。
3. When 数値・制御命令を直接call・return・locals・dropと組み合わせる場合, the WasmSharp2 shall 引数・結果の0個・1個・複数の順序と呼出しごとのlocalsの独立性を保ち、呼出し元と呼出し先で同じ命令意味論を適用する。
4. When 数値・制御命令からglobalを参照・更新する場合, the WasmSharp2 shall 宣言型と可変性およびimportによる共有を維持し、実行した命令に対応する値の変更を後続の呼出しから観測可能にする。
5. When importした定義関数やホストcallbackを制御構文から呼び出す場合, the WasmSharp2 shall 関数の同一性、定義関数の所属instance、ホストcallbackへ渡す呼出し元の情報、ホスト例外の型と実体を保つ既存契約を維持する。
6. When startが数値・制御命令を使う場合, the WasmSharp2 shall 既存のInstantiate順序に従って実行し、成功時の結果と失敗前のglobal等への副作用・ホストが保持した参照を既存契約どおり扱う。

### 要件9: 失敗・公開診断・実行資源の上限

**目的:** 利用者として、不正なmodule、Wasmのtrap、exhaustion、ホスト側の失敗を原因と段階で区別したい。

**根拠:** S3・S4・S6・S8、test-suite-conformanceとhost-linkingの公開契約。

#### 受入基準

1. If 対象命令の構文違反または型・ラベルの検証不成立が発生する場合, then the WasmSharp2 shall それぞれ既存の`WasmDecodeException`または`WasmValidateException`で通知し、moduleを検証成功状態にしない。
2. If 数値trapまたはunreachableに到達する場合, then the WasmSharp2 shall 以後の命令を実行せず、原因に対応する`WasmTrapException`と既存のReasonを通知し、通常のInvokeとInstantiate中のstartの段階を区別する。
3. If 整数div/remの0除算、signed最小値÷−1、truncのNaN・無限大・範囲外によるtrapが発生する場合, then the WasmSharp2 shall 順に`integer divide by zero`、`integer overflow`、NaNは`invalid conversion to integer`・無限大と範囲外は`integer overflow`から始まる公開診断を返す。
4. If 制御構文を使う直接再帰・相互再帰・ホストcallback経由の同期再入が既存の深さ制限に達する場合, then the WasmSharp2 shall 既存の実行ポリシーとWasm実行コンテキストの契約に従って`WasmExhaustionException`を通知し、CLRのStackOverflowによるプロセス終了を起こさない。
5. When trap・exhaustion・ホスト例外で呼出しが終了した後に独立したWasm呼出しを開始する場合, the WasmSharp2 shall 終了した呼出しの一時値や深さを残さず、既存契約で保持される状態と参照を使って新しい呼出しを実行可能にする。
6. When 対象の否定assertionに対応する公開診断を通知する場合, the WasmSharp2 shall 固定公式期待診断への`Message.StartsWith(expectedText, StringComparison.Ordinal)`を成立させ、段階・例外型・Reason・位置等との整合を維持する。
7. If 複数の不正または後続機能の未実装を含む入力を処理する場合, then the WasmSharp2 shall 固定公式ケースと固定参照実装から確認した原因選択規則を通常入力にも適用し、診断のために公式ケースID・期待文字列への依存を必要とせず、未確認範囲を保持する。

本仕様の診断互換性は、先行仕様と同様に固定公式ケースと確認した原因選択規則の範囲とする。任意入力のあらゆる不正の組合せについて参照実装との完全互換を保証するものではない。

### 要件10: 固定公式スイートによる受入と回帰確認

**目的:** 適合検証担当者として、対象機能の完成と既存機能の維持を、同じ公式素材と判定基準で確認したい。

**根拠:** S8、ロードマップの公式検証方針、test-suite-runnerの既存契約。

#### 受入基準

1. When 本仕様の実装結果を受け入れる場合, the 適合検証担当者 shall 同じ固定Core 2.0プロファイル・公式素材の全147入力を既存ランナーで実行し、入力と全commandの結果を記録する。
2. When 対象命令群と意味論の対応を確認する場合, the 適合検証担当者 shall S1〜S8の規則に要件・実装・検証を対応付け、公式ケースと必要な境界条件の確認によって、対象命令や規則の抜けを確認する。
3. When 数値・制御と先行機能で必要な前提が揃う公式ケースを判定する場合, the 適合検証担当者 shall 数値の型・個数・ビット列・NaNの許容範囲、分岐結果、観測対象の状態、否定assertionの段階・例外型・診断を含めpassedを要求する。
4. If 後続のmemory/table・segment・参照固有命令・SIMD等を必要とするケースが残る場合, then the 適合検証担当者 shall ランタイムが報告する未実装機能と元ケース、および依存してblockedとなったケースの原因を記録し、ランナーの能力不足や本仕様の未実装を後続機能へ付け替えない。
5. When 全体実行と直前の実行結果baselineを比較する場合, the 適合検証担当者 shall 固定素材・プロファイルの比較条件を満たすことと、以前passedだったケースの別分類への変化または欠落がないことを確認する。
6. When 本仕様を完了と判定する場合, the 適合検証担当者 shall 全体実行と回帰比較が完了し、failed・runner_error・回帰が0で、本仕様の対象に必要な機能が揃うケースがpassedであることを要求する。
7. If 不一致や回帰が検出された場合, then the 適合検証担当者 shall 原因を修正して全体実行と比較をやり直し、期待値の変更、診断の正規化、ケース除外、feature flagの縮小、分類の読み替えで解消扱いにしない。
8. When 受入条件を満たす全体結果を次の比較基準として保存する場合, the 適合検証担当者 shall 比較済みの保存結果に対して明示的にbaselineを更新し、本仕様の完了と、後続機能のruntime_unsupported・blockedも0を要求するCore 2.0全体の完了を区別する。

## 仕様規則と代表公式テストの対応

以下は調査時に確認した代表例であり、実行対象を限定する一覧ではない。パスは固定specの`test/core/`からの相対パスである。実装時の受入では要件10に従って全体を実行する。

| 要件 | 重点となる規則 | 代表公式テスト |
| --- | --- | --- |
| 1〜3 | blocktypeの符号化、即値、終端、両分岐の型、複数値、selectの型ベクトル | `binary.wast`、`binary-leb128.wast`、`block.wast`、`loop.wast`、`if.wast`、`select.wast`、`type.wast` |
| 3・7 | ラベル深さ、loop引数とblock結果、br_ifの非分岐、br_tableの既定分岐と多相的な型適合 | `br.wast`、`br_if.wast`、`br_table.wast`、`unreached-valid.wast`、`unreached-invalid.wast`、`unwind.wast` |
| 4・6 | 幅と符号解釈、除算と剰余のMIN/−1の違い、シフト量、符号拡張 | `i32.wast`、`i64.wast`、`int_exprs.wast`、`conversions.wast` |
| 5・6 | 各命令の丸め、非正規化数、NaN分類、符号付き0、変換境界と再解釈 | `f32.wast`、`f64.wast`、`f32_cmp.wast`、`f64_cmp.wast`、`f32_bitwise.wast`、`f64_bitwise.wast`、`float_exprs.wast`、`float_misc.wast`、`conversions.wast` |
| 7・8 | nop、selectの非短絡評価、計算と副作用の順序、複数値と呼出し・globalの統合 | `nop.wast`、`select.wast`、`left-to-right.wast`、`call.wast`、`return.wast`、`local_get.wast`、`local_set.wast`、`local_tee.wast`、`global.wast` |
| 8・9 | start、再帰、exhaustion、ホスト連携とリンク、公開診断 | `start.wast`、`fac.wast`、`call.wast`、`imports.wast`、`linking.wast`、`unreachable.wast`、`conversions.wast` |

整数変換の境界では変換元の浮動小数点型で表せる隣接値を用いる。たとえば`i32.trunc_f64_s`の−2147483648.9は切捨て後に範囲内となり、`i32.trunc_f64_u`の−0.9も0になる。到達不能部分では`unreached-valid.wast`の`meet-bottom`等を参照し、br_tableの具体型の一律一致や到達不能部分の一括省略を合格条件にしない。
