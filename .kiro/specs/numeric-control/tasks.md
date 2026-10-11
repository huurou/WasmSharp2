# 実装計画

## 実行方針

- 各実行タスクは原則1〜3時間とし、必要な境界テストを含める。既存の責務分離を完了してから振る舞いを追加する。
- テストを追加・変更したタスクでは、Releaseビルドの警告・エラー0を確認してから、対象のTUnitをコマンドで実行する。公式ケースと同じassertionの大量複製は避ける。
- 共通契約は先に整え、命令宣言の対応済みへの切替は必要なhandlerを用意してから行う。途中のDecode対応や内部テストの成功だけでは機能の完了と扱わない。
- 記載順を基本の依存順とする。5.1だけは3.3完了後、整数handlerの4.1〜4.3と並行できる。共有宣言の編集、ビルド・テスト、公式実行は直列に行い、公式実行中は対象コードを変更しない。
- 2.3、7.2、9.6、11.1を公式全体実行・比較を行う完了単位とする。各実行は保存した直前baselineを比較元とし、最後の受入まで更新しない。

## タスク

- [x] 1. 実装と検証に必要な既存環境を整える
- [x] 1.1 固定素材と比較元を保持した検証環境を準備する
  - .NET 10、固定spec、既存の3つのTUnitプロジェクトとランナーの実行前提を確認し、不足する依存を既存手順で復元する。
  - 既存manifest・素材と直前の実行結果baselineの存在、hash、固定profileを照合し、変更開始前のbaselineを別出力として保存する。素材が不足する場合だけ固定WABT・同一profileで再生成する。
  - 出力先は無視対象の新しい実行用ディレクトリとし、元の素材・baselineを上書きしない。
  - Releaseビルドが警告・エラー0で完了し、後続の全体実行が保存した比較元を参照できる状態を確認する。
  - _Boundary: 受入記録と既存ランナー_
  - _Requirements: 10.1, 10.5_

- [x] 1.2 数値・制御の境界を表すテスト入力を既存fixtureで構築できるようにする
  - 既存バイナリfixtureを再利用し、入れ子制御、型添字、ラベル列、型ベクトル、複数値を後続テストから表せる最小限の構築能力を整える。
  - 不正な符号化と正常な構造を意図的に分け、入力位置と全7値型のビット列・参照を確認できるようにする。
  - fixtureの拡張後も既存の公開APIテストが成功し、WAT/WASTの解析処理を追加せず必要なバイナリを生成できることを確認する。
  - _Boundary: HostLinkingModuleBinary_
  - _Requirements: 1.1, 1.3, 3.9, 7.9_

- [x] 2. 振る舞いを保って式読取りと関数検証を分離する
- [x] 2.1 式の構文読取りを独立した責務へ抽出する
  - 関数本体と定数式で共有する読取りを抽出し、moduleのsection処理と区別する。
  - 通常解析・診断再走査の入口、最外end直後の位置、境界例外、Unsupportedの未確認範囲を維持する。
  - 既存の正常入力・切断入力・複合不正について、抽出前と同じDecode結果と公開診断になることを確認する。
  - _Boundary: ExpressionDecoder_
  - _Requirements: 1.1, 1.4, 1.6, 9.1, 9.6, 9.7_

- [x] 2.2 関数の型検査と線形化を独立した責務へ抽出する
  - 1関数の型スタック・出力・到達不能状態を抽出し、module全体の検査と定数式の適格性判定を呼出し元に残す。
  - 関数型・global型の解決済み情報を利用し、入力やmoduleの検証成功状態を関数単位で変更しない。
  - 既存call・locals・global・return・到達不能列のテストが成功し、後続検査失敗時にmoduleが検証済みにならないことを確認する。
  - _Boundary: FunctionValidator_
  - _Requirements: 1.5, 2.6, 3.8, 8.3, 9.1_

- [x] 2.3 責務分離後の公開経路と公式結果を統合検証する
  - 抽出した読取り・検証と公開4段階の接続を確認し、Decode・Validate中にstartやホストcallbackが動かないことを確認する。
  - 既存の3つのTUnitを成功させ、固定147入力を新しい出力先で全体実行する。
  - 実行の記録完了・終了0を確認してから保存済みbaselineと比較し、既存passedの退行・欠落と新たなfailed・runner_errorがないことを確認する。
  - _Boundary: ExpressionDecoder, FunctionValidator, WasmModule, 受入記録と既存ランナー_
  - _Depends: 1.1, 2.1, 2.2_
  - _Requirements: 1.1, 1.5, 9.6, 9.7, 10.1, 10.5, 10.7_

- [ ] 3. 後続実装が共有する入力・実行・命令生成の契約を整える
- [x] 3.1 s33読取りと複合即値の所有契約を統合する
  - 空型・単一値型・型添字を混同しないblocktypeと、ラベル列・select型列を不変に所有する入力表現を用意する。
  - 既存LEB128処理を利用し、s33の有効な非最短符号化、幅超過、切断を扱う。負の多byte整数を値型へ読み替えない。
  - 即値を持たない命令の値を統一し、入力バッファの変更が保持済み即値へ波及しないこととs33の境界結果を確認する。
  - 変更した入力表現を埋め込む生成器fixtureにも必要な契約ソースを追加し、重複するスタブを除く。入力表現のテストと生成器テストがこのタスクだけで成功する状態を保つ。
  - _Boundary: ModuleBinaryReader, DecodedInstruction, BlockType, InstructionGenerator_
  - _Requirements: 1.2, 1.3, 1.4_

- [x] 3.2 関数が所有する不変の分岐情報を実行表現へ統合する
  - 分岐先、operand相対高さ、保持個数を関数コードへ保持し、分岐表の候補と最後の既定候補を表現できるようにする。
  - 既定を除く候補数0を許し、既存の命令位置・圧縮locals・最大operand数の契約を維持する。
  - 既存の分岐を持たない関数が同じ結果を返し、複数instanceから共有しても実行状態を静的コードへ書き込まないことを確認する。
  - _Boundary: Instruction, FunctionCode, BranchTarget_
  - _Requirements: 3.4, 7.7, 7.8, 7.9_

- [ ] 3.3 数値型効果の宣言を生成器とテストへ統合する
  - 既存の未対応宣言・対応宣言を維持し、入力個数・入力型・結果型を持つ数値宣言を生成descriptorへ反映する。
  - 入力個数1/2、スカラー4型、Numericとsignatureの整合を検査し、opcode重複・handler不整合の診断を維持する。対象handlerの作成前に未対応命令を対応済みへ切り替えない。
  - 複合即値・制御・selectの規則を表す共通契約も揃える。3.1で更新した生成器fixtureへ数値signature等のこのタスクで追加する契約ソースを埋め込み、重複するスタブを除く。
  - 正常宣言の直接dispatch、不完全宣言の診断、失敗結果の伝播が生成器テストで確認でき、通常ビルドが成功することを確認する。
  - _Boundary: InstructionSet, InstructionGenerator_
  - _Depends: 3.1, 3.2_
  - _Requirements: 1.2, 1.3, 2.1, 2.2, 4.6, 4.7, 9.2_

- [ ] 4. 整数演算の実行処理を追加する
- [ ] 4.1 幅を保つ整数算術と比較を実装する
  - i32/i64のadd・sub・mulを対象幅の剰余として計算し、operandの左右を保つ。
  - eqzと符号付き・符号なし比較を区別し、結果をi32の0/1で返す。
  - 幅境界、符号解釈の相違、通常の桁あふれでtrapにならない結果をhandlerの直接テストで確認する。
  - _Boundary: Interpreter.Integer_
  - _Depends: 3.3_
  - _Requirements: 4.1, 4.7_

- [ ] 4.2 整数除算と剰余の部分演算を実装する
  - 符号付き・符号なしのdiv/remを実装し、商の0方向切捨てと剰余の符号を保つ。
  - 0除算とsigned最小値÷−1を演算前に判定し、対応するtrap結果に関数と入力位置を付ける。
  - signed最小値の−1による剰余は0になり、CLR算術例外を利用せず正常値と2種類のtrapを区別できることを確認する。
  - _Boundary: Interpreter.Integer_
  - _Requirements: 4.2, 4.3, 4.4, 9.2_

- [ ] 4.3 整数のビット操作とビット数計算を実装する
  - 論理演算、符号付き・符号なし右シフト、左シフト、左右回転を対象幅で扱う。
  - シフト・回転量を幅で剰余化し、clz・ctz・popcntの結果を元の整数幅で返す。
  - 入力0、最上位bit、幅以上の移動量で仕様どおりの値になり、i64のビット数計算もi64結果になることを確認する。
  - _Boundary: Interpreter.Integer_
  - _Requirements: 4.5, 4.6_

- [ ] 5. 浮動小数点演算の実行処理を追加する
- [ ] 5.1 (P) 対象幅で丸める浮動小数点算術とNaN結果を実装する
  - f32/f64のadd・sub・mul・div・sqrtを対象幅で計算し、各命令で丸めた結果を保存する。
  - 算術NaN結果を設計で定めたcanonical NaNへ揃え、0除算・不定形・桁あふれを整数trapにしない。
  - 非正規化数、underflow、無限大、連続演算の丸めを直接テストで確認する。共有命令宣言には触れず、4.1〜4.3から独立した浮動小数点処理だけを変更する。
  - _Boundary: Interpreter.Float_
  - _Depends: 3.3_
  - _Requirements: 5.1, 5.2, 5.6, 5.7_

- [ ] 5.2 整数値化と符号bit操作を実装する
  - ceil・floor・trunc・nearestの丸め方向を区別し、nearestの同距離を偶数へ丸める。
  - 丸め後の0の符号と無限大を保ち、算術NaNの補正を適用する。
  - abs・neg・copysignは符号bitだけを変更し、sNaN/qNaNのpayloadを保持することと、正負の0を直接テストで確認する。
  - _Boundary: Interpreter.Float_
  - _Requirements: 5.3, 5.4, 5.7_

- [ ] 5.3 最小値・最大値と浮動小数点比較を実装する
  - min/maxはNaNを先に処理し、±0の組合せをそれぞれ−0/+0へ揃える。
  - 比較では±0を等値とし、NaNを含む場合はneだけを成立させ、i32の0/1を返す。
  - operandの順序を入れ替えた場合もNaN・符号付き0・等値の境界が仕様に適合することを確認する。
  - _Boundary: Interpreter.Float_
  - _Requirements: 5.5, 5.7, 5.8_

- [ ] 6. 数値変換の実行処理を追加する
- [ ] 6.1 整数の幅変換・符号拡張と同幅の再解釈を実装する
  - wrapとsigned/unsigned extendを区別し、同じ整数型内のextend8/16/32を指定下位幅から計算する。
  - 同幅の整数・浮動小数点間を数値変換せず再解釈し、算術NaNの補正を通さない。
  - 上位bitを含む整数と、NaN payload・符号付き0を含む往復再解釈で全bitが保持されることを確認する。
  - _Boundary: Interpreter.Conversion_
  - _Depends: 3.3_
  - _Requirements: 6.1, 6.2, 6.7_

- [ ] 6.2 trapを伴う浮動小数点から整数への変換を実装する
  - 全変換元・変換先・符号解釈について、NaNの判定後、0方向への切捨て結果を半開区間で判定する。
  - 範囲内でだけ整数へ変換し、NaNと無限大・範囲外を別Reasonのtrap結果にする。
  - 表現可能な隣接値、signed下限付近の小数、−1より大きい負値のunsigned変換、2の31/32/63/64乗の境界を確認する。
  - _Boundary: Interpreter.Conversion_
  - _Requirements: 6.3, 6.4, 9.2_

- [ ] 6.3 飽和する浮動小数点から整数への変換を実装する
  - 全8組合せの飽和変換に切捨てと範囲判定を適用し、NaNを0へ変換する。
  - 無限大と範囲外を変換先の上下限へ飽和させ、cast固有の範囲外動作に依存しない。
  - 非飽和変換と同じ境界入力に対し、正常値・上下限・0を返して数値trapを発生させないことを確認する。
  - _Boundary: Interpreter.Conversion_
  - _Requirements: 6.5_

- [ ] 6.4 整数から浮動小数点への変換と浮動小数点の幅変換を実装する
  - 符号解釈した整数から目的幅へ直接変換し、i64/unsigned i64からf32へのdouble経由の二重丸めを避ける。
  - promoteの非NaN値を正確に保ち、demoteの丸め、符号付き0・無限大・NaN結果を既存の浮動小数点処理へ合わせる。
  - 固定公式の二重丸めを検出する8値を含む境界を確認し、目的幅のbit列とNaN分類が成立することを確認する。
  - _Boundary: Interpreter.Conversion_
  - _Depends: 5.1_
  - _Requirements: 5.7, 6.6_

- [ ] 7. 数値命令を型検証と公開実行へ統合する
- [ ] 7.1 数値命令の宣言・型検証・生成dispatchを接続する
  - 完成した整数・浮動小数点・変換handlerを全対象命令へ宣言し、入力個数・型・結果型を宣言から検証する。
  - constは既存規則を維持し、比較のi32結果、i64ビット数のi64結果、0xFCの全飽和変換を固定opcodeと照合する。
  - 到達可能な入力不足・型不一致をValidateで拒否し、数値命令を定数式へ許可しない。従来の未対応例が対象命令を使用している場合は元の目的を保つ後続命令へ置き換える。
  - 対象命令が通常ビルドの生成dispatchから実行でき、代表的な正常入力・型不成立・定数式不成立を公開APIで確認する。
  - _Boundary: InstructionSet, InstructionGenerator, FunctionValidator, ModuleValidator, Interpreter_
  - _Depends: 4.3, 5.3, 6.4_
  - _Requirements: 1.3, 2.1, 2.2, 2.6, 4.6, 4.7, 9.1_

- [ ] 7.2 数値trapの公開診断を統合し数値機能の公式結果を確認する
  - 0除算・整数overflow・不正整数変換を既存公開境界でReason別の診断へ変換し、Invokeとstartの段階・位置を保つ。
  - 数値trap後の命令が実行されず、公開診断の先頭が固定期待値に一致することを確認する。
  - 3つのTUnitと固定147入力の全体実行を行い、終了0と記録完了後に保存したbaselineへ比較する。数値機能で前提が揃うケースの不一致を該当実装へ戻して解消し、再実行・再比較する。
  - _Boundary: ExecutionBoundary, Interpreter, 受入記録と既存ランナー_
  - _Depends: 1.1, 7.1_
  - _Requirements: 4.3, 6.4, 9.2, 9.3, 9.6, 10.1, 10.3, 10.5, 10.7_

- [ ] 8. 分岐と選択に必要な実行処理を追加する
- [ ] 8.1 現在の関数内で分岐値を移動できるようにする
  - operand基準の相対高さへ末尾の保持値を移し、重なりがあっても順序・全値型のbit列・参照の同一性を保つ。
  - 破棄範囲の参照を解除して値数と実行位置だけを更新し、locals・外側operand・呼出し元・深さ・frame数を変更しない。
  - 保持値0/1/複数、重なる移動、非null参照の破棄と保持を内部テストで確認する。
  - _Boundary: InterpreterContext_
  - _Depends: 3.2_
  - _Requirements: 7.8, 7.9, 9.5_

- [ ] 8.2 条件分岐・分岐表・nop・selectの実行処理を追加する
  - 直接分岐、if用のゼロ分岐、br_ifの非ゼロ分岐を扱い、非分岐時には条件以外の値を残す。
  - br_tableの選択値を符号なしとし、空候補・負値・範囲外を既定候補へ送る。
  - nopは状態を変えず、selectは条件・第2候補・第1候補の順に取り出して選択値をそのまま返す。
  - handlerの直接テストで分岐先と保持値、selectのNaN payload・符号付き0、深さが増えないことを確認する。
  - _Boundary: Interpreter.Control_
  - _Requirements: 7.1, 7.3, 7.4, 7.5, 7.6, 7.7, 8.1_

- [ ] 9. 構造化制御の読取り・型検証・実行を接続する
- [ ] 9.1 制御とselectの命令宣言を式読取りへ統合する
  - 用意したhandlerと複合即値の契約を命令宣言へ接続し、block/loop/ifの型、br_tableの候補と既定、select型ベクトルを読み取る。
  - CLR再帰を増やさず入れ子とelse有無を管理し、最外end・内側end・不正else・重複else・終端欠落を区別する。型添字の存在や型ベクトル長の適格性はValidateへ残す。
  - 通常解析と診断再走査に同じ処理を用い、巨大な宣言長の先行確保を避け、元の境界診断・位置・未確認範囲を保持する。
  - bytes/Stream、非最短s33・0xFC番号、空br_table、select長0/複数、入れ子切断のDecode結果を確認する。既存のblock等を使う未対応fixtureは目的を保って置換する。
  - _Boundary: ExpressionDecoder, InstructionSet_
  - _Depends: 2.1, 3.1, 3.3, 8.2_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.6, 2.5, 9.6, 9.7_

- [ ] 9.2 制御ごとの型検査と通常経路の線形化を追加する
  - 暗黙の関数ラベルと各制御の引数・結果・operand底・局所的到達不能を管理し、全7値型を共通に運ぶ。
  - block/loop/ifの0/1/複数値、if条件i32、両側の検証、省略elseの引数型列と結果型列の一致を検査する。
  - block/loopと内側endのmarkerを出力せず、if/elseの前方分岐を制御終了時に解決する。関数endだけを既存の関数終了へ接続し、入力数と出力数が異なる場合も命令列を確定できるようにする。
  - 具体型の不一致、内側の不足・余剰、外側operandの消費を拒否し、両分岐と入口引数を含む最大operand数と通常終了の結果を確認する。
  - _Boundary: FunctionValidator_
  - _Depends: 2.2, 3.2, 9.1_
  - _Requirements: 1.5, 3.1, 3.2, 3.3, 3.7, 3.8, 3.9, 7.2, 7.3, 9.1_

- [ ] 9.3 単一ラベルへの分岐と局所的多相性を追加する
  - block/ifには結果型、loopには引数型、関数には結果型を適用し、ラベル存在と分岐値を検査する。
  - br_ifは条件0でもラベルと値を検査して非分岐側へ値を残し、br・return・unreachableの後では現在の制御だけにUnknownを適用する。
  - loop入口への後方分岐、制御終了と関数末尾の終了命令への前方分岐を確定し、未解決の分岐先を公開しない。
  - 定数条件による検証省略がなく、内側の到達不能が外側へ漏れず、関数終了と深さ減算が1回だけ行われることを確認する。
  - _Boundary: FunctionValidator_
  - _Requirements: 3.4, 3.5, 3.7, 3.8, 7.4, 7.5, 7.6, 7.8, 9.7_

- [ ] 9.4 分岐表の型適合と診断選択を追加する
  - 既定を含む全ラベルの存在と同じ引数個数を検査し、同じoperand型列を各ラベルへ照合する。
  - Unknownを最初の候補型へ具体化せず、有効な到達不能列と具体型が衝突する列を区別する。
  - 既定候補・分岐値・候補順・selectorの検査順を固定参照に合わせ、候補と既定の確定済み分岐先を保存する。
  - meet-bottom、候補0、異なる具体ラベル型に適合するUnknown、不正ラベルと型不一致の複合入力について結果と診断先頭を確認する。
  - _Boundary: FunctionValidator_
  - _Requirements: 3.6, 3.8, 7.7, 9.1, 9.6, 9.7_

- [ ] 9.5 selectの型検証と後続機能の未対応を区別する
  - 型なしselectの候補型一致・条件i32・Unknownを検査し、参照候補を拒否する。
  - typed selectは型ベクトル長1を先に要求し、両候補と条件を検査する。長さ0/複数はDecode成功後のValidate不成立にする。
  - v128と参照typed selectは、その場の型・個数不成立を先に拒否し、有効な対象外型だけを設計で定めたFeatureと未確認範囲を持つUnsupportedにする。
  - スカラー4型のselectが検証成功し、両候補Unknownから未対応を推測しないことと、型不成立・未対応の段階と診断を確認する。
  - _Boundary: FunctionValidator_
  - _Requirements: 1.6, 2.3, 2.4, 2.5, 3.8, 9.1, 9.6, 9.7_

- [ ] 9.6 制御・selectを公開4段階へ統合して公式結果を確認する
  - 確定した分岐情報を生成dispatchと値移動へ接続し、全体検証成功前にInstantiateを許可しない。
  - 数値・select・構造化制御を定数式で拒否し、Decode・Validateにホストcallbackやstartの副作用がないことを確認する。
  - nop、通常終了、両if経路、loop引数、入れ子を越える分岐、空・負値・範囲外の分岐表を公開APIで確認し、loop反復で値や深さが蓄積しないことを確認する。
  - 3つのTUnitと固定147入力の全体実行・保存済みbaseline比較を完了し、不一致は原因を所有するタスクへ戻して修正後に再実行する。
  - _Boundary: FunctionValidator, ModuleValidator, WasmModule, Interpreter.Control, InterpreterContext, 受入記録と既存ランナー_
  - _Depends: 1.1, 8.1, 8.2, 9.2, 9.3, 9.4, 9.5_
  - _Requirements: 1.1, 1.5, 2.6, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 10.1, 10.3, 10.5, 10.7_

- [ ] 10. 先行機能との公開経路を統合検証する
- [ ] 10.1 呼出しと制御を越える値の保持を確認する
  - 数値・制御をcall・return・locals・dropと組み合わせ、引数と結果の0/1/複数の順序および呼出しごとのlocalsの独立性を確認する。
  - v128、生NaN、nullと非nullのfuncref/externrefを公開APIから制御・分岐へ渡し、bit列と参照の同一性を確認する。
  - 内側分岐やreturnが外側operand・呼出し元を壊さず、保存済み結果も後続呼出しで変化しないテストを成功させる。
  - _Boundary: FunctionValidator, Interpreter, InterpreterContext, WasmValue_
  - _Depends: 7.2, 9.6_
  - _Requirements: 3.9, 7.8, 7.9, 8.3_

- [ ] 10.2 選択・分岐と共有状態・ホスト連携の順序を確認する
  - selectの両候補と条件でglobal・ホストcallbackを操作し、非短絡で命令順に評価されることと、分岐で飛び越えた副作用が発生しないことを確認する。
  - globalの型・可変性・importによる共有を維持し、実行した更新だけが後続呼出しから観測できることを確認する。
  - importした定義関数の所属instanceと同一性、ホストcallbackの呼出し元情報、ホスト例外の型と実体が保たれる統合テストを成功させる。
  - _Boundary: Interpreter, Interpreter.Control, ExecutionBoundary_
  - _Requirements: 8.1, 8.2, 8.4, 8.5, 9.2_

- [ ] 10.3 startでの計算と失敗前の状態を確認する
  - 既存の構築・接続・初期化順序で、数値・制御を使うstartが実行されることを確認する。
  - 正常終了と数値trapを公開Instantiateから確認し、Reason・診断先頭・入力位置と処理段階を検査する。
  - 失敗前のglobal更新とホストが保存したinstance・関数・リソース参照が保持され、以後の副作用が実行されないことを確認する。
  - _Boundary: ModuleInstantiator, ExecutionBoundary, Interpreter_
  - _Requirements: 8.6, 9.2, 9.3, 9.6_

- [ ] 10.4 再帰・同期再入の上限と失敗後の復元を確認する
  - 制御を使う直接再帰・相互再帰・ホストcallback経由の同期再入を小さな既存深さ制限で止める。
  - 最外側の実行ポリシーを共有し、loop反復と終了済み呼出しが深さを増やさないことを確認する。
  - trap・exhaustion・同一ホスト例外の後に独立呼出しを成功させ、一時値・参照・深さが残らず、保持すべき状態が維持されることを確認する。
  - _Boundary: Interpreter, InterpreterContext, ExecutionBoundary_
  - _Requirements: 8.5, 9.4, 9.5_

- [ ] 11. 全体受入と比較済みbaselineの更新を完了する
- [ ] 11.1 最終の単体・統合テストと固定公式全体の回帰比較を実行する
  - Releaseビルドの警告・エラー0を確認し、ランタイム・生成器・ランナーの3つのTUnitをコマンドで成功させる。
  - 固定profileと全147入力を変えず新しい出力へ実行し、入力と全commandの記録・終了0を確認してから、保存した直前baselineと比較する。
  - 数値結果、NaNの許容範囲、状態、否定assertionの段階・例外型・Reason・診断先頭を既存ランナーの判定のまま確認する。
  - 全体結果と比較結果が保存され、passedだったケースの退行・欠落を検出できることを確認する。
  - _Boundary: 受入記録と既存ランナー_
  - _Depends: 1.1, 10.1, 10.2, 10.3, 10.4_
  - _Requirements: 9.6, 10.1, 10.3, 10.5, 10.6_

- [ ] 11.2 対象規則の検証漏れと残存未対応の原因を監査する
  - S1〜S8、73受入基準、対象opcode・型規則、実装、公式ケースまたは必要な直接テストを対応付け、単なるWAST名の一覧で網羅扱いにしない。
  - runtime_unsupportedのFeatureと元入力・commandを後続所有仕様へ対応付け、blockedを原因commandまでたどる。
  - 本仕様の未実装やランナー能力不足の付替えを認めず、必要な前提が揃う全ケースのpassedを確認する。
  - 対応と未対応の原因を保存結果から検証できるよう受入記録へ残し、未検証の対象規則があれば該当タスクの検証を補う。
  - _Boundary: 受入記録と既存ランナー_
  - _Requirements: 10.2, 10.3, 10.4, 10.6_

- [ ] 11.3 不一致解消後の受入条件を確定する
  - 不一致・runner_error・回帰・対象規則の欠落があれば、原因を所有する実装タスクへ戻して修正と必要な境界テストを行う。
  - 修正後はビルド・該当テストと11.1・11.2を新しい出力先でやり直し、期待値・診断比較・対象集合・feature flag・分類を変更して解消扱いにしない。
  - 同じ最終結果についてfailed・runner_error・回帰0、対象ケースpassed、後続所有の未対応とその依存だけが残ることを確認する。
  - _Boundary: 受入記録と既存ランナー_
  - _Requirements: 10.3, 10.4, 10.5, 10.6, 10.7_

- [ ] 11.4 受入済みの同じ全体結果からbaselineを明示更新する
  - 受入条件を満たした比較済みの保存結果を入力として、既存のbaseline保存操作を明示実行する。
  - 更新元の結果・比較結果・固定profile・hash・終了コードを受入記録から追跡できるようにし、比較前のbaselineも保持する。
  - 保存されたbaselineが受入済み結果と一致することを確認し、本仕様の完了と、後続のruntime_unsupported・blockedも0を要求するCore 2.0全体の完成を区別する。
  - _Boundary: 受入記録と既存ランナー_
  - _Requirements: 10.8_

## Implementation Notes

- 1.2:TUnitの今回のbyte[]比較では`IsEqualTo`が参照一致を調べた。順序に意味がある符号化は、既知hex等でバイト列の内容を比較する。
- 2.1:関数本体と定数式の式読取りは`ExpressionDecoder.Read`、sectionと式の共通Unsupported診断は`ModuleBinaryReader.Unsupported`が担う。Bounded/Diagnosticの入口と範囲管理は`ModuleDecoder`に残した。
- 2.2:1関数の検証は`FunctionValidator.Validate`、module全体と共有する診断2メソッドは`ValidationDiagnostics`が担う。TUnitのfilterにはファイル名ではなく実際のクラス名を指定する。
- 3.1:入力命令は`DecodedInstruction`の即値別factoryで構築する。生成器fixtureはBlockType・WasmValue・WasmValueKindの本体ソースを取り込み、WasmFunction参照型だけを最小stubに残した。
- 3.2:`FunctionCode`の末尾引数で不変の`BranchTargets`を渡す。`Instruction.TargetCount`は既定を除く候補数で、省略時は0。既存の3引数・4引数構築を維持した。
