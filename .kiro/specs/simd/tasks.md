# 実装タスク

## 実行上の前提

各実行単位は1〜3時間を目安とし、具体的な変更先と契約は[設計](design.md)のファイル構成計画に従う。大項目1で既存の挙動を保った責務整理を完了してからSIMD動作を追加する。

未接続の命令は未対応のまま保つ。大項目2はコードと生成契約の準備を行い、まだ通れないSIMDのDecode・Validate経路をテスト成功として扱わない。大項目3と8でhandler・検証・即値が揃った命令を公開経路へ接続し、その時点で新しい正常・否定テスト、未対応を期待する既存テスト、対応済み集合を更新して実行する。成功するだけの仮handlerや手書きの第2opcode表は作らない。

接続・統合・最終検証を含む各タスクで先行タスクの担当コードの不具合を検出した場合は、その担当タスクを未完了へ戻して再開する。即値読み取りは2.3、静的制約・型検証・線形化は2.4など、元の_Boundary_内で修正と関連検証を行い、不具合を検出したタスクに戻って必要な新規テスト・全体run・比較・verifyを再実行して両タスクを完了する。ビルドと既存回帰テストだけの成功を、新規経路の検証成功へ読み替えない。

接続・統合検証のタスクは、設計のファイル構成計画に記載された関連テスト・fixtureの追加と期待値更新をConformanceVerificationの担当として行える。製品コードの修正は、そのコードを担当するタスクへ戻して行う。

テスト追加・変更のある各タスクは、Releaseビルドの警告・エラー0を確認してから関連テストをコマンドで実行する。公開経路へ接続した機能単位で固定スイート全体をrunし、直前の受入baselineとcompare-runする。ビルド・テスト・公式実行の共通出力は並行使用しない。

並列性を検討したが、基盤契約、同じ演算群ファイル、命令宣言、公式検証出力を順次更新するため、この計画には(P)を付けない。順序と_Depends:_に従う。baselineは最終受入まで保持する。通常の機能タスクに必要な検証を任意扱いにしない。

- [x] 1. 検証前提を整え、既存処理の責務を整理する
- [x] 1.1 固定素材・比較元と既存fixtureを確認する
  - .NET 10、固定spec、3つのテストプロジェクトと公式CLIの既存構成を確認する。
  - 最新tables-referencesの受入baselineと対応manifestの存在・hash・profile・素材を確認し、設計時点から変更があれば現在の受入記録に合わせる。
  - SIMD即値・関数型・global・memoryを持つ入力を既存のバイナリfixtureで作れるよう必要最小限の補助を追加する。
  - ビルドと既存テストが成立し、後続で使う比較元・コマンド・専用出力先を確認できる状態にする。
  - _Boundary: ConformanceVerification_
  - _Requirements: 8.1, 8.3_

- [x] 1.2 数値型効果の共通表現を既存scalar経路へ統合する
  - 同型入力数の表現を、順序付き入力型列と1結果型へ整理する。
  - 命令属性・生成metadata・生成器の検査と型検証の消費側を同じ契約へ移し、既存の属性記法とscalarの動作を維持する。
  - InstructionSet_TryGetTestsとruntime契約を埋め込む生成器テストのconstructor・比較を追随させ、入力型列の順序付き要素比較と結果型の確認へ移す。既存scalarの型・pop順・診断が変わらないことを確認する。
  - このタスクではSIMD命令を対応済みへ変更せず、既存テストの成功を完了条件とする。
  - _Boundary: InstructionMetadata, FunctionValidator_
  - _Depends: 1.1_
  - _Requirements: 2.1, 2.7_

- [x] 1.3 メモリ存在とalignmentの検証を共通処理へ整理する
  - 既存scalarメモリの存在・自然alignment検査を、後続のvectorでも利用できる同じ処理へまとめる。
  - 検査順、診断先頭、関数と元位置、到達不能時の扱いを維持する。
  - scalarメモリの正常・不正ケースが変更前と同じ結果になることを確認する。
  - _Boundary: FunctionValidator_
  - _Requirements: 1.6, 2.4, 2.5_

- [x] 1.4 共有する浮動小数点の純粋処理を整理する
  - NaN結果化、符号付きゼロを保つ丸め、飽和i32変換の既存補助を再利用可能な状態にする。
  - min/maxで共有が必要な純粋処理だけを既存handlerから抽出し、既に使える補助は移動しない。
  - scalarのNaN・±0・丸め・飽和境界のテストが成立し、演算結果が変わらないことを確認する。
  - _Boundary: ScalarFloatSemantics_
  - _Requirements: 6.2, 6.3, 6.4_

- [x] 2. SIMDの宣言・即値・検証基盤を追加する
- [x] 2.1 SIMDの型効果と即値制約を生成できるようにする
  - 1〜3個の異種数値入力、v128結果、lane上限、vectorメモリの実際のアクセス幅を宣言から生成する。
  - 定数・shuffle・lane・memargとlaneの組合せ、およびlaneメモリの検証規則を表現する。
  - 不正な型列・lane制約・メモリ幅やhandler欠落を生成時に拒否し、既存診断の契約を維持する。
  - 正常・不正の生成器テストが成立し、まだ未接続の命令は未対応として残ることを確認する。
  - _Boundary: InstructionMetadata_
  - _Depends: 1.2_
  - _Requirements: 1.2, 1.3, 2.1, 2.2, 2.3, 2.4_

- [x] 2.2 128bitのlane取得・置換とbyte変換を追加する
  - 既存の上下64bitからlaneを取得・置換し、little-endianの16byteと相互変換する。
  - 8/16/32/64bit幅、上下64bitの境界、64bit幅のmask・shiftを正しく扱う。
  - lane 0の位置、非対象laneの保持、全bitの往復一致をテストで確認する。
  - _Boundary: SimdValue_
  - _Requirements: 3.1, 4.1, 6.6_

- [x] 2.3 SIMD即値を読み取り、入力から独立して保持する
  - 16byte定数、16byte shuffle、1byte lane、memargとlaneを読み取る。
  - byte列とStreamで同じ値を得て、切断・不正LEB128・未割当番号をDecodeの失敗として区別する。
  - lane範囲やshuffle値をDecodeで拒否せず、Validateへ値と元位置を渡す。
  - 即値を保持するコードをビルドでき、既存のDecode回帰テストが成立することを確認する。新しいSIMD経路の正常・切断・LEB128検証は接続後の3.1、8.1、8.2、8.4で実行する。
  - _Boundary: ExpressionDecoder_
  - _Depends: 2.1_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 2.4 SIMDの静的制約と型検証・線形化を追加する
  - 入力型列を逆順に消費し、lane上限、shuffle全要素、memory存在と幅に応じたalignmentを検査する。
  - 通常load/storeとlane load/storeの型効果を区別し、到達不能部分でも静的制約を省略しない。
  - 定数・shuffle・lane・memargと元位置を実行表現へ渡し、検証失敗で成功状態を公開しない。
  - 実行表現へ即値を渡すコードをビルドでき、既存のValidate・線形化の回帰テストが成立することを確認する。新しいSIMDの型順・lane・shuffle・alignment・到達不能の否定ケースは接続後の3.1、8.1、8.2、8.4で実行する。
  - _Boundary: FunctionValidator_
  - _Depends: 1.3, 2.1, 2.3_
  - _Requirements: 1.5, 1.6, 2.1, 2.2, 2.3, 2.4, 2.5, 2.7_

- [x] 3. v128定数と選択を公開経路へ統合する
- [x] 3.1 定数・global初期化とv128 selectを接続する
  - v128定数を既存の定数pushと初期化式へ接続し、専用評価器を追加しない。
  - typed/untyped selectのv128未対応制限だけを解消し、型・条件の検証を保つ。
  - 定数の128bit、global初期値、NaN payload、±0と選択した値の保持を公開4段階で確認する。
  - 定数の16byte切断、byte列とStream、許容非最短／不正LEB128、要求型の不一致を確認し、該当する未対応期待を更新する。演算を含む定数式の拒否は、演算を接続する8.1でも確認する。
  - ModuleDecoder_ReadElementTestsとWasmModule_DecodeGlobalsTestsを含む定数・selectの未対応期待を、完全な即値・切断・要求型不一致に分けて更新する。接続後も診断位置とDecode/Validateの境界を検証する。
  - _Boundary: InstructionMetadata, FunctionValidator, ConstantInitializer, ConformanceVerification_
  - _Depends: 2.2, 2.4_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.6, 2.6, 2.7, 3.1, 3.2, 3.4, 3.5_

- [x] 3.2 定数・選択の統合結果を公式全体実行で確認する
  - 新規出力先で全体runと既存baselineとの比較を行う。
  - 接続した命令を通るケースの結果と診断を確認し、残る未接続命令による未対応・blockedを原因付きで区別する。
  - 定数・selectに起因する失敗と既存passedの回帰がなく、結果が保存されていることを確認する。
  - _Boundary: ConformanceVerification_
  - _Depends: 3.1_
  - _Requirements: 1.6, 8.1, 8.2, 8.3, 8.4_

- [x] 4. lane・論理・比較の実行を追加する
- [x] 4.1 整数と浮動小数点のsplat・extract・replaceを追加する
  - 各lane幅とスカラー型の組合せを扱い、i8/i16の切詰め・符号拡張・ゼロ拡張を区別する。
  - 指定lane以外の128bitを保ち、浮動小数点のbitをそのまま運ぶ。
  - 各幅の最初・最後のlaneと境界bitのテストが成立する状態にする。
  - _Boundary: SimdLaneExecution_
  - _Depends: 2.2, 3.2_
  - _Requirements: 4.1_

- [x] 4.2 shuffle・swizzleと128bit論理演算を追加する
  - shuffleの2operand連結順、swizzleの範囲外ゼロ、bitselectのmask選択を扱う。
  - not・and・andnot・or・xorを128bitすべてに適用する。
  - shuffleの0/15/16/31、swizzleの15/16/255とbit単位のmaskで期待bitが得られることを確認する。
  - _Boundary: SimdLaneExecution_
  - _Requirements: 4.2, 4.3, 4.4_

- [x] 4.3 整数比較と縮約判定を追加する
  - 固定仕様にある整数lane型ごとの比較を符号付き・符号なしで区別する。
  - any_true・all_true・bitmaskを追加し、比較の全bit1/0とlane順を保つ。
  - i64に存在しないunsigned比較を増やさず、符号境界と混在laneの結果を確認する。
  - _Boundary: SimdLaneExecution_
  - _Requirements: 4.5, 4.6_

- [x] 4.4 浮動小数点のvector比較を追加する
  - f32x4/f64x2の6比較を追加し、NaN・±0・無限大の比較結果を扱う。
  - true/falseを各laneの全bit1/0として返す。
  - 両精度でNaNを左右operandへ置いたテストが成立する状態にする。
  - _Boundary: SimdLaneExecution_
  - _Requirements: 4.5_

- [ ] 5. 整数SIMDの実行を追加する
- [x] 5.1 通常の整数算術とシフトを追加する
  - 固定仕様に存在する各lane幅のabs・neg・add・sub・mulと3種類のshiftを扱う。
  - 演算結果をlane幅へ折り返し、i32シフト量をlane幅で剰余化する。
  - 最小負数、overflow、幅以上のシフトとsigned/unsigned右シフトの差を確認する。
  - _Boundary: SimdIntegerExecution_
  - _Depends: 2.2_
  - _Requirements: 5.1, 5.3_

- [x] 5.2 飽和算術とnarrowを追加する
  - 対象幅のsigned/unsigned加減算を飽和させる。
  - narrowはsigned入力を解釈して出力範囲へ飽和し、2operandのlane順を保つ。
  - 負入力からunsigned出力へのゼロ化と上下限前後の結果を確認する。
  - _Boundary: SimdIntegerExecution_
  - _Requirements: 5.2_

- [x] 5.3 最小・最大・切上げ平均・bit数を追加する
  - 対象幅のsigned/unsigned min/max、avgr_u、i8x16.popcntを扱う。
  - 平均計算の中間値を保持し、奇数和を正しく切り上げる。
  - 符号境界、最大値同士、全byteのbit数を代表的境界値で確認する。
  - _Boundary: SimdIntegerExecution_
  - _Requirements: 5.4_

- [ ] 5.4 low/highの拡張と拡張乗算を追加する
  - 固定仕様のextendとextmulをlow/high、signed/unsignedごとに実装する。
  - 入力の半分の選択と拡張先lane幅での演算を保つ。
  - 上下で異なる入力と符号境界で、選択順・拡張・積のbitが正しいことを確認する。
  - _Boundary: SimdIntegerExecution_
  - _Requirements: 5.5_

- [ ] 5.5 隣接laneの拡張加算とdotを追加する
  - extadd_pairwiseの符号解釈と隣接対を扱う。
  - signed i16の積をi32の隣接対ごとに加算し、結果の折返しを保つ。
  - lane順とdotのoverflowを含むテストが成立する状態にする。
  - _Boundary: SimdIntegerExecution_
  - _Requirements: 5.6_

- [ ] 5.6 Q15の丸め付き飽和乗算を追加する
  - 符号付き積、丸め加算、算術シフトとi16への飽和を順に扱う。
  - 丸め境界の正負と最小値同士の特殊な組合せを検証する。
  - 最小値同士が32767となり、他の境界結果も固定規則に一致する状態にする。
  - _Boundary: SimdIntegerExecution_
  - _Requirements: 5.6_

- [ ] 6. 浮動小数点と変換の実行を追加する
- [ ] 6.1 符号bit操作と基本算術を追加する
  - f32x4/f64x2のabs/negをbit操作で扱い、NaN payloadを保持する。
  - add/sub/mul/div/sqrtを同精度の規則で計算し、許されるNaN結果を返す。
  - 両精度の±0・無限大・NaN・非正規化数が期待どおりになることを確認する。
  - _Boundary: SimdFloatExecution_
  - _Depends: 1.4, 2.2_
  - _Requirements: 6.1, 6.2, 6.6_

- [ ] 6.2 丸めと2種類の最小・最大演算を追加する
  - ceil/floor/trunc/nearestと符号付きゼロの保持を扱う。
  - min/maxとpmin/pmaxを分け、比較選択されたoperandのbitを保つ。
  - 最近接偶数丸め、左右を入れ替えたNaNと±0のテストが成立する状態にする。
  - _Boundary: SimdFloatExecution_
  - _Requirements: 6.2, 6.3_

- [ ] 6.3 浮動小数点から整数への飽和変換を追加する
  - f32x4/f64x2からのsigned/unsigned i32変換を扱い、NaNを0、範囲外を端点とする。
  - f64x2のzero形式は結果の上位laneをゼロにする。
  - 上下限の内外、NaN・無限大とゼロ方向切捨てを確認し、数値trapを返さない状態にする。
  - _Boundary: SimdFloatExecution_
  - _Requirements: 6.4, 6.5_

- [ ] 6.4 整数から浮動小数点への変換と精度変更を追加する
  - convertのsigned/unsigned、low lane選択、demoteとpromoteを扱う。
  - demote_zeroの未使用laneをゼロにし、変換精度の丸めを保つ。
  - 符号境界、精度境界と非対象laneの結果を確認する。
  - _Boundary: SimdFloatExecution_
  - _Requirements: 6.5, 6.6_

- [ ] 7. SIMDメモリ命令の実行を追加する
- [ ] 7.1 通常のvector load/storeを追加する
  - unsigned i32アドレスとoffsetをulongで合成し、既存の同じmemory実体へ16byteを転送する。
  - little-endianと非整列アクセスを扱い、失敗を位置付きの既存trap結果で返す。
  - 末尾ぴったり・1byte超過・ページ境界・最大uintとoffset、失敗storeの未変更を確認する。
  - _Boundary: SimdMemoryExecution_
  - _Depends: 2.2, 2.4_
  - _Requirements: 7.1, 7.4, 7.5_

- [ ] 7.2 拡張load・splat load・zero loadを追加する
  - 命令固有のbyte幅だけを読み、符号拡張／ゼロ拡張、複製、上位ゼロ化を行う。
  - zero loadにlane即値を要求せず、出力幅とアクセス幅を区別する。
  - 各幅の境界直前・超過と読み出したbitの変換結果を確認する。
  - _Boundary: SimdMemoryExecution_
  - _Requirements: 7.2, 7.4, 7.5_

- [ ] 7.3 lane load/storeを追加する
  - 検証済みlane indexに従い、対象幅だけを転送する。
  - loadは非対象lane、storeは非対象memoryを保持し、storeの結果値を作らない。
  - 全lane幅で先頭・末尾laneと境界trap、失敗時の未変更を確認する。
  - _Boundary: SimdMemoryExecution_
  - _Requirements: 7.3, 7.4, 7.5, 7.7_

- [ ] 8. 演算群を命令宣言と公式実行へ統合する
- [ ] 8.1 lane・論理・比較の命令を公開経路へ接続する
  - 対応する即値、型効果、lane制約、handlerを共通の命令宣言へ接続する。
  - 既存の未対応期待と対応済み命令集合を更新し、公開経路で引数・戻り値を確認する。
  - lane/shuffle即値の切断、lane上限、shuffle31/32、mixed入力の逆順、到達不能時の静的制約、SIMD演算を定数式として拒否するケースを追加・実行する。
  - 固定スイート全体のrun・比較により、本群の不一致と既存passedの回帰がないことを確認する。
  - _Boundary: InstructionMetadata, SimdLaneExecution, ConformanceVerification_
  - _Depends: 3.2, 4.1, 4.2, 4.3, 4.4_
  - _Requirements: 1.1, 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.3, 2.5, 2.6, 2.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 8.2, 8.3, 8.4_

- [ ] 8.2 整数演算の命令を公開経路へ接続する
  - 通常・飽和・シフト・拡張・積和を型効果とhandlerへ対応付け、存在しないlane型の命令を追加しない。
  - 既存の未対応期待と対応済み集合を更新し、公開経路の境界値、128以上のsubopcodeのLEB128、shiftのv128とi32の型順を確認する。
  - 固定スイート全体のrun・比較により、整数群の不一致と既存passedの回帰がないことを確認する。
  - _Boundary: InstructionMetadata, SimdIntegerExecution, ConformanceVerification_
  - _Depends: 3.2, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_
  - _Requirements: 1.2, 2.1, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 8.2, 8.3, 8.4_

- [ ] 8.3 浮動小数点演算と変換の命令を公開経路へ接続する
  - 演算・丸め・2種類のmin/max・変換を型効果とhandlerへ対応付ける。
  - lane別の具体bitとNaN patternを既存ランナーで判定し、期待値を正規化しない。
  - 固定スイート全体のrun・比較により、本群の不一致と既存passedの回帰がないことを確認する。
  - _Boundary: InstructionMetadata, SimdFloatExecution, ConformanceVerification_
  - _Depends: 3.2, 6.1, 6.2, 6.3, 6.4_
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 8.2, 8.3, 8.4_

- [ ] 8.4 メモリ命令を公開経路へ接続する
  - 通常・拡張・splat・zero・laneの全22命令を、即値・アクセス幅・lane制約・handlerへ対応付ける。
  - memargとlane即値の切断、memory不在、各幅のalignment上限、lane上限と型順、到達不能部分の否定ケースを追加・実行し、診断と公開Invoke/startのmemory trapを確認する。
  - ModuleDecoder_DecodeInstructionTests、WasmModule_DecodeDataTests、WasmModule_InspectImportsTestsを含むメモリ命令の未対応期待を見直す。設計の方針に従い、接続後の入力でも診断の再走査・検査順・非実行・import情報の未確認範囲を検証し、未対応状態だけを対象にしたケースの廃止理由を記録する。
  - 固定スイート全体のrun・比較により、メモリ群の不一致と既存passedの回帰がないことを確認する。
  - _Boundary: InstructionMetadata, FunctionValidator, SimdMemoryExecution, ConformanceVerification_
  - _Depends: 3.2, 7.1, 7.2, 7.3_
  - _Requirements: 1.3, 1.4, 1.5, 1.6, 2.1, 2.2, 2.4, 2.5, 2.7, 7.1, 7.2, 7.3, 7.4, 7.5, 7.7, 8.2, 8.3, 8.4_

- [ ] 9. 既存の呼び出し・状態共有との統合を確認する
- [ ] 9.1 v128の共通値経路とホスト連携を確認する
  - 引数・複数結果・local初期値・直接call・間接call・return・block/loop/if・分岐・dropで128bitを保持する。
  - typed/untyped select、ホストcallback、再exportと同期再入で既存の型・instance・失敗契約を確認する。
  - 既存テストで満たす範囲は再利用し、不足経路だけ公開APIの直接テストを追加して全項目を確認できる状態にする。
  - _Boundary: ConformanceVerification_
  - _Depends: 8.1, 8.2, 8.3, 8.4_
  - _Requirements: 3.1, 3.3, 3.4, 3.6_

- [ ] 9.2 v128 globalと共有メモリ・startを確認する
  - 定義・immutable/mutable import・export・get/setでglobalの初期値と同一性を確認する。
  - data初期化、grow、scalarとの混在、別instanceとホストによる共有memoryの観測を確認する。
  - start/Invokeのtrapで失敗storeが未変更となり、先行副作用・保存参照・次の呼び出しの契約が維持されることを確認する。
  - _Boundary: ConformanceVerification_
  - _Depends: 8.4_
  - _Requirements: 2.6, 3.5, 3.6, 7.6, 7.7_

- [ ] 10. 固定公式スイートで最終受入を行う
- [ ] 10.1 命令集合・仕様規則と局所検証の対応を確認する
  - 固定付録の236命令と生成metadataの対応・欠番を確認し、名前だけ登録された未対応命令が残らないことを確認する。
  - 9テストファイルの未対応期待の更新・廃止を確認し、固定Core 2.0の正常入力に未対応命令が残らないことと、維持した汎用の未対応経路の到達性・保持理由をacceptance.mdへ記録する。
  - 51受入基準を固定仕様の規則・実装・既存または追加テストへ対応付け、公式未収録の共通経路も確認する。
  - 警告・エラー0のReleaseビルド後、3つのテストプロジェクトを実行し、未解決の失敗と確認漏れがない状態にする。
  - 新しいテストプロセスにDOTNET_EnableHWIntrinsic=0を指定してSIMD handlerの関連テストだけを一度再実行し、通常実行と同じ期待値を満たすことを確認する。設定を他の実行へ持ち越さず、環境条件・コマンド・件数・終了値をacceptance.mdへ記録する。
  - _Boundary: ConformanceVerification_
  - _Depends: 9.1, 9.2_
  - _Requirements: 1.1, 1.6, 2.7, 6.6, 8.1, 8.5_

- [ ] 10.2 単一の公式全体結果で回帰比較と全体判定を行う
  - 同一の統合状態で全147入力を新規出力先へrunし、直前baselineとcommand単位で比較する。
  - 出典・期待・実際・原因を分類し、failed・runner_error・既存passedの回帰を確認する。このタスクは実行・比較・判定を担当し、他の責務の実装修正を取り込まない。
  - 問題があれば原因に対応する既存実装タスクへ戻す。既存単位では大きい修正は原因別・責務別に1〜3時間の修正タスクへ分割する。担当側で必要なビルド・テストを完了後、このタスクの全体run・比較・verifyを再実行する。
  - 同じ最終runにverifyを適用し、out_of_scope以外の全件passed、未処理・欠落なし、未対応・blockedを含む4分類0を確認する。
  - SIMD機能受入とCore 2.0全体判定の結果を区別して確認できる状態にする。
  - _Boundary: ConformanceVerification_
  - _Depends: 10.1_
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.7_

- [ ] 10.3 比較・受入済み結果をbaselineへ明示保存する
  - 最終の比較・判定に使用した同じrunを既存の保存コマンドへ渡す。
  - 入力と保存結果の一致、対応manifest、profileと素材の識別情報を確認する。
  - 保存操作の終了値と採用した結果を確認でき、保存成功をテスト成功へ読み替えていない状態にする。
  - _Boundary: ConformanceVerification_
  - _Depends: 10.2_
  - _Requirements: 8.6_

## Implementation Notes

- 5.2: `SaturateSimdInteger(int value, int minimum, int maximum)`で中間値を飽和しlaneのbit列を返す。narrowは`ExecuteSimdIntegerNarrow`と純粋な`MapSimdIntegerNarrow`を使う。
- 5.1: `ExecuteSimdIntegerUnary/Binary/Shift`でstack操作、`MapSimdIntegerUnary/Binary/Shift`で純粋なlane計算を扱う。後続の同幅演算では再利用し、新規機能の一時フラグを既存共通処理へ掛けない。
- 4.3: `CompareSimdLanes(InterpreterContext, int, Func<ulong, ulong, bool>)`がlane走査と全bit1/0の結果構築を共有する。符号・型の解釈はhandlerのstatic predicateへ置き、4.4でも再利用する。
- 3.1: `v128.const`とtyped/untypedのv128 selectを公開経路へ接続した。残る235個の0xFD命令は未接続。定数・globalのbyte/Stream、bit保持と型不一致を確認済みで、公式全体run・比較は3.2で行う。
- 2.4: lane・shuffleの診断先頭は`invalid lane index`。laneメモリはmemory存在→alignment→lane→operandの順を維持する。`Instruction`へ`ShuffleLanes`・`LaneIndex`を渡す準備が完了し、新規経路の否定テストは接続時に行う。
- 2.2: `SimdValue`のbyte変換は`FromLittleEndian(ReadOnlySpan<byte>)`と`WriteLittleEndian(Span<byte>)`。lane幅・indexは検証済みとして扱い、浮動小数点や符号の解釈はhandlerに残す。
- 2.1: SIMD即値は`V128`・`Shuffle`・`LaneIndex`・`MemoryArgumentLaneIndex`、laneメモリ規則は`MemoryLoadLane`・`MemoryStoreLane`を使う。生成metadataの`LaneCount`はlane即値なしで0。2.1では命令を接続せず生成契約を準備した。
- 1.4: `Interpreter.Float.cs`内の`F32MinResult`・`F32MaxResult`・`F64MinResult`・`F64MaxResult`と、既存の算術結果・丸め・飽和変換の補助をSIMDのpartialから再利用する。
- 1.2: `NumericSignature`は`InputTypes`と`ResultType`へ統一した。入力型列はスタック下側から上側の順で、validatorは逆順に消費する。型列のテスト比較には`SequenceEqual`を使う。生成契約の異種入力・3入力・v128拡張は2.1で行う。
- 1.1: 比較元は`artifacts/tables-references/task-8.4-20261018T090714Z/run-baseline.json`、manifestは`artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json`。既存fixtureで必要な入力を構成できる。固定条件・検証コマンドは`acceptance.md`を参照する。

