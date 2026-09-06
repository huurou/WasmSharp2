# 要件文書

## はじめに

WasmSharp2の利用者は、WasmバイナリをC#から明示的に扱い、破損・検証不成立・リンク不成立・trap・未実装を区別する必要がある。現在は公開型と操作の骨組みがあり、Decode・Validate・Instantiate・Invokeは未実装である。

本仕様は、後続のランタイム機能が共通の値・型と処理段階を利用するための最小基盤を対象とする。2026-09-06の要件確認により、最初に実行する関数は、引数・localsなし、戻り値1個で、`i32.const`・`i64.const`・`f32.const`・`f64.const`のいずれかと`end`のみからなる定数返却関数とした。

## 対象範囲と隣接仕様

| 区分 | 範囲 |
| --- | --- |
| 最小実行経路 | type・function・export・codeで記述された、importを必要としない上記の定数返却関数を、4段階を通して実行する。受入確認の最小モジュールは1つの型・1つの定義関数・1つの関数exportを持つが、個数を1つに制限しない。対応済みの定数返却関数を複数持つモジュールも対象とする。 |
| 共通の利用契約 | Core 2.0の値の種類、関数の引数型列・結果型列、Wasm値と結果列、静的モジュールとインスタンスの区別、処理段階と失敗分類を扱う。値の構築・取得にはscalar・v128・参照を含む。 |
| バイナリの共通規則 | ヘッダー、長さ、整数、名前、sectionの枠組み、および最小実行経路の構文と検証規則を扱う。全section・全命令の意味論の実装は要求しない。 |
| 初期の利用契約 | 単一スレッドでの同期実行を保証する。検証済みモジュールの共有や独立した別インスタンスの利用を含め、マルチスレッド利用の動作保証は対象外とする。 |
| 後続の実行機能 | 引数・localsを用いる実行、複数戻り値、数値演算、完全な制御構文、分岐、call、unreachable、globalsは`wasm-numeric-control`で扱う。メモリ・テーブル・参照命令・ホスト連携・start・SIMD命令はそれぞれの後続仕様で扱う。 |
| 公式適合検証 | 公式テスト素材の固定・変換は`wasm-test-corpus`、JSONとspectestによる全件実行・集計は`wasm-conformance-runner`で扱う。本仕様の完了にこれらの完成は要求しない。 |
| 全体の対象外 | WAT・WASTの解析、WASI、Component Model、JavaScript/Web API、既存エンジンへの実行委譲、およびCore 2.0外の機能を追加しない。 |

最小経路以外のCore 2.0機能は、未実装である間は要件6の分類を適用する。`v128`や参照の値を扱えることは、それらを用いる命令の実行対応を意味しない。同様に、引数型列・結果型列を表現できることは、引数を使う関数や複数戻り値関数の実行対応を意味しない。

実装方式と後続が共有する内部契約は、[ブリーフ](brief.md)、[ロードマップ](../../steering/roadmap.md)、[用語集](../../../CONTEXT.md)、[ADR 0001](../../../docs/adr/0001-explicit-staged-runtime-api.md)、[ADR 0002](../../../docs/adr/0002-single-pass-linear-interpreter.md)、[ADR 0003](../../../docs/adr/0003-core2-fixed-conformance-profile.md)、[ADR 0004](../../../docs/adr/0004-trap-result-propagation.md)の既存方針を設計へ引き継ぐ。2026-09-06の追加確認で、検証成功時に同じモジュールが実行表現を保持する方式を[ADR 0005](../../../docs/adr/0005-module-owned-validation-state.md)、命令定義と実行処理をコード生成で同期する方式を[ADR 0006](../../../docs/adr/0006-generated-instruction-dispatch.md)に記録した。残る公開APIの引数・戻り値の詳細、実行表現の具体的な構造、生成方式の詳細、分岐・呼び出しのスタック基準は設計段階で具体化する。

## 要件

### 要件1: 明示的な4段階と利用状態

**目的:** ライブラリ利用者として、デコード・検証・インスタンス化・呼び出しを個別に行い、どの段階まで成立したかを判断したい。

#### 受入基準

1. The WasmSharp2 shall Decode・Validate・Instantiate・Invokeを、利用者が個別に実行する公開操作として提供する。
2. When Decodeが成功した場合, the WasmSharp2 shall 実行時インスタンスの生成や関数の実行を伴わずに、静的モジュール定義を利用者へ返す。
3. When Validateが成功した場合, the WasmSharp2 shall 検証対象とした同じ`WasmModule`を返してその定義をインスタンス化可能にし、インスタンスの生成や関数の実行は行わない。
4. If 検証が未実施または成功していない定義に対してInstantiateが要求された場合, then the WasmSharp2 shall インスタンスの生成を拒否し、呼び出しの契約違反として識別可能にする。
5. While モジュール定義が検証済みとして利用される間, the WasmSharp2 shall 検証成功の根拠となった定義と異なる内容が検証済みとして実行されることを防ぐ。
6. When 検証済みのモジュールに再度Validateを要求した場合, the WasmSharp2 shall 再検証せずに同じ`WasmModule`を返す。

### 要件2: 明示的な値・型と結果列

**目的:** ライブラリ利用者として、Wasmの型と値を明示して構築・取得し、暗黙変換による情報の変化なく受け渡したい。

#### 受入基準

1. The WasmSharp2 shall Core 2.0の値の種類として`i32`・`i64`・`f32`・`f64`・`v128`・`funcref`・`externref`を区別できるようにする。
2. When 利用者が数値のWasm値を明示的に構築して同じ型で取得した場合, the WasmSharp2 shall 元のビット列を保持し、浮動小数点数では符号付き0、無限大、NaNの符号とpayloadを区別できるようにする。
3. When 利用者が`v128`のWasm値を明示的に構築して取得した場合, the WasmSharp2 shall 128ビットの内容を保持する。
4. When 利用者が`funcref`または`externref`のWasm値を明示的に構築して取得した場合, the WasmSharp2 shall 参照の種類、nullかどうか、および非null参照の同一性を保持する。
5. If 利用者がWasm値の種類と異なる型で値を取得しようとした場合, then the WasmSharp2 shall 暗黙変換せずに拒否し、呼び出しの契約違反として識別可能にする。
6. When 利用者が関数型を構築または参照した場合, the WasmSharp2 shall 引数型列と結果型列を区別し、それぞれの型・個数・順序を取得可能にする。
7. When 利用者が結果列を取得した場合, the WasmSharp2 shall 結果の個数、順序、および各要素の型と値を取得可能にする。
8. The WasmSharp2 shall 値の受け渡しを`WasmValue`で明示する利用契約とし、汎用の`object`・`dynamic`引数、CLR型からの暗黙変換、delegateからの関数型推論を提供しない。
9. When 利用者がCLRオブジェクトの参照から`externref`のWasm値を明示的に構築して取得した場合, the WasmSharp2 shall 元のCLRオブジェクトの参照を取得可能にし、同じオブジェクトから再度構築した場合も参照先の同一性を保持する。

要件2.9の`externref`専用の構築・取得操作は、要件2.8で禁止する汎用の引数変換には含めない。関数呼び出し時には`externref`も`WasmValue`として渡す。

### 要件3: バイナリのデコードと破損の識別

**目的:** ライブラリ利用者として、バイナリ入力から静的モジュールを取得し、構文上の破損を実行前に識別したい。

#### 受入基準

1. When 最小実行経路のモジュールをバイト列または読み取り可能なストリームからDecodeした場合, the WasmSharp2 shall 同じ内容の静的モジュール定義を取得可能にする。
2. If デコードで検査する範囲に、magic・バイナリversionの不一致、入力の途中終了、宣言した長さと内容の不一致、またはCore 2.0の符号化規則に反する整数が存在した場合, then the WasmSharp2 shall `WasmDecodeException`で破損を通知する。
3. When Core 2.0で許容される長さと未使用ビットの制約を満たす非最短の整数表現をDecodeした場合, the WasmSharp2 shall 最短表現ではないことだけを理由に拒否しない。
4. If デコードで検査するsectionの枠組みに、Core 2.0で禁止された順序・重複・section ID、またはfunctionとcodeの件数不一致が存在した場合, then the WasmSharp2 shall `WasmDecodeException`で破損を通知する。
5. When 正しい形式のcustom sectionを最小実行経路のモジュール内の許容位置に1個または複数配置した場合, the WasmSharp2 shall custom sectionの存在や内容によって関数の実行結果を変更しない。
6. If デコード対象の名前が正しいUTF-8でない場合, then the WasmSharp2 shall 置換文字で受理せず、`WasmDecodeException`で破損を通知する。
7. If 読み取り不可のストリームが渡された場合, then the WasmSharp2 shall 呼び出しの契約違反として拒否し、Wasmバイナリの破損とは区別する。

### 要件4: 最小実行経路の検証

**目的:** ライブラリ利用者として、定数返却関数の型と参照関係が正しいことを確認してからインスタンス化したい。

#### 受入基準

1. When 構文が正しい最小実行経路のモジュールをValidateし、使用する型と関数の添字、export名、および関数の結果型がCore 2.0の検証規則を満たす場合, the WasmSharp2 shall 検証を成功させる。
2. If 検証する範囲に存在しない型・関数を指す添字、または重複したexport名が存在した場合, then the WasmSharp2 shall `WasmValidateException`で検証不成立を通知する。
3. If 対応する命令だけからなる関数本体の終了時に、得られる値の型または個数が宣言した結果型列と一致しない場合, then the WasmSharp2 shall `WasmValidateException`で検証不成立を通知する。
4. If 型や命令などの未実装機能によって検証を完了できない場合, then the WasmSharp2 shall 要件6の未実装分類を通知し、その定義を検証済みにしない。
5. If 検証が途中で失敗した場合, then the WasmSharp2 shall 途中まで処理した内容をインスタンス化可能な結果として利用者へ提供しない。

### 要件5: インスタンス化と定数関数の呼び出し

**目的:** ライブラリ利用者として、検証済みモジュールから関数を取得して呼び出し、宣言された型の定数を結果として受け取りたい。

#### 受入基準

1. When 検証済みの最小実行経路のモジュールに、空のホストモジュール入力でInstantiateを要求した場合, the WasmSharp2 shall 呼び出し可能な関数を持つインスタンスを生成する。
2. When 同じ検証済みモジュールから複数回Instantiateした場合, the WasmSharp2 shall 同じ静的定義に基づく別個のインスタンスを生成する。
3. When 利用者がインスタンスから存在する関数export名を指定した場合, the WasmSharp2 shall そのexportに対応する呼び出し対象と関数型を取得可能にする。
4. If 利用者が存在しない関数export名を指定した場合, then the WasmSharp2 shall `ArgumentException`系の例外で呼び出し対象を取得できないことを通知し、モジュールの検証不成立やWasmのtrapとは区別する。
5. When `i32.const`・`i64.const`・`f32.const`・`f64.const`のいずれかと`end`だけからなる、引数・localsなし、戻り値1個の関数を空の引数列でInvokeした場合, the WasmSharp2 shall 定数の型とビット列を保持した1個のWasm値を結果列として返す。
6. If 最小実行経路の引数なし関数に空でない引数列を渡してInvokeした場合, then the WasmSharp2 shall 呼び出しを拒否し、Wasmのtrapとは異なる呼び出しの契約違反として通知する。
7. When 最小実行経路の同じ関数を繰り返しInvokeした場合, the WasmSharp2 shall 各呼び出しで同じ型とビット列の1個の結果を返す。
8. The WasmSharp2 shall インスタンスが新たなWasm実行コンテキストを開くときに用いる上限値を実行ポリシーとしてインスタンスに保持し、Invokeの公開操作には呼び出しごとの実行オプション引数を設けない。
9. When 同じWasm実行コンテキスト内で関数を呼び出した場合, the WasmSharp2 shall 呼び出し深さをその実行コンテキスト単位で共有し、個々のInvokeの開始によって初期化しない。
10. When 最外側のInvokeまたはstart実行がWasm実行コンテキストを開いた場合, the WasmSharp2 shall 開いたインスタンスの上限をそのコンテキストの終了まで固定し、内側で呼ばれたインスタンスの上限は適用しない。
11. When 同じスレッド上で同期的にネストした関数呼び出しを行う場合, the WasmSharp2 shall ホストコールバックからの再呼び出しも含めて現在のWasm実行コンテキストを共有し、最外側の実行が終了したときに、そのコンテキストを後の独立した実行へ残さない。

初期の保証範囲は単一スレッドでの同期実行とする。同じインスタンスや共有リソースへの同時アクセスに加え、検証済みモジュールの共有や独立した別インスタンスの並行利用も動作保証の対象に含めない。

実行ポリシーと深さの所有、`[ThreadStatic]`による現在の実行コンテキストの保持は[ADR 0008](../../../docs/adr/0008-instance-options-and-execution-context.md)に従う。呼び出し深さはネスト中の深さであり、終了済みの呼び出しを含む通算回数ではない。別スレッドの実行へのコンテキストの引き継ぎと非同期フローへの伝播は初期対象外とする。要件5.9〜5.11は後続の関数呼び出しやホスト連携が使う共通契約であり、基盤にこれらの実行機能を追加する要求ではない。

### 要件6: 原因別の失敗分類と未実装の境界

**目的:** ライブラリ利用者と後続機能の実装者として、仕様違反・利用契約違反・実装不足を区別し、未実装を入力の有効性の証明と誤認せずに扱いたい。

#### 受入基準

1. The WasmSharp2 shall 入力の破損、検証不成立、リンク不成立、Wasmのtrap、および対象仕様の未実装を、公開される例外の型によって区別できるようにする。
2. If 対象とするCore 2.0の機能が未実装であるため処理を続行できない場合, then the WasmSharp2 shall `WasmUnsupportedFeatureException`で、未実装の機能と処理を中断した段階を識別可能にする。
3. If 未実装のため構文検査または検証を完了できなかった場合, then the WasmSharp2 shall 未確認の範囲を識別可能にし、入力全体の有効性を確認済みとは表示しない。
4. If 処理を中断するまでの検査で入力の構文違反または検証規則違反が確定した場合, then the WasmSharp2 shall その違反を該当段階の失敗として通知し、未実装を理由に分類を置き換えない。
5. If 検査した入力がCore 2.0の構文または検証規則に反する場合, then the WasmSharp2 shall 後の仕様版で認められる機能であってもCore 2.0の規則に従って分類する。
6. The WasmSharp2 shall 後続機能にも共通する公開失敗契約として、Wasmのtrapを`WasmTrapException`で識別可能にし、インスタンス化中に発生するtrapもリンク不成立と区別する。
7. The WasmSharp2 shall ランタイムが通知する呼び出しの契約違反、実装制限・資源枯渇、実行環境の能力不足を、Wasmの仕様上のtrapと区別できる失敗契約を提供する。
8. If ホスト処理が.NET例外を投げた場合, then the WasmSharp2 shall 元の例外をラップせず、型と実体を維持して利用者へ伝播し、内部のtrap結果やリンク不成立へ変換しない。
9. If 呼び出し深さなどのランタイムが管理する実行資源の上限を超える場合, then the WasmSharp2 shall `WasmExhaustionException`で実行を継続できないことを通知する。

APIの誤用には.NET標準例外を使う。引数不正とexport名の不在は`ArgumentException`系、検証前のInstantiateなどの状態不正は`InvalidOperationException`とする。

要件6.6〜6.9は後続機能が利用する共通の失敗契約を含む。本仕様の定数命令にtrapやホスト呼び出しを追加する要求ではなく、start・リンク処理・再帰などを実行して分類を確認する経路は該当する後続仕様で扱う。ホスト例外の扱いは[ADR 0007](../../../docs/adr/0007-propagate-host-exceptions.md)に従い、ホストがランタイムと同じ例外型を投げた場合に、型だけで発生元を必ず区別できることは保証しない。残る例外型の割り当てと原因情報の形式は設計で確定する。

### 要件7: 公開操作による基盤の受入確認

**目的:** 利用者と実装者として、基盤で実行できる範囲と失敗の意味を確認し、後続機能やCore 2.0全体の完成と混同しない証拠を得たい。

#### 受入基準

1. When 基盤の受入確認を行う場合, the WasmSharp2の基盤検証 shall 4種類の定数返却を、バイナリ入力からDecode・Validate・Instantiate・Invokeまでの公開操作で確認する。
2. When 基盤の受入確認を行う場合, the WasmSharp2の基盤検証 shall 小さな正負のバイナリと公開操作を用いて、破損・検証不成立・未実装・段階を飛ばした利用・呼び出し契約違反を区別する要件を確認する。
3. When 基盤の検証結果を報告する場合, the WasmSharp2の基盤検証 shall 実行した対象と未実装・対象外・未検証の範囲を区別し、最小経路の成功をCore 2.0全体への準拠と表現しない。

## 仕様上の根拠

バイナリと検証の分類、値の意味は[WebAssembly Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)に従う。主な対応は、値・型が§2.3と§4.2.1、定数命令が§4.4.1、整数・浮動小数点数・名前の符号化が§5.2.2〜§5.2.4、モジュールとsectionの構文が§5.5、関数・exportの検証が§3.4.1と§3.4.8である。最小実行範囲は本要件でのユーザー回答に基づき、Core 2.0に含まれるという理由だけで他の命令を基盤へ追加しない。
