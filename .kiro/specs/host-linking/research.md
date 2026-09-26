# host-linking 調査と設計判断

## 概要

- 対象: host-linking。既存runtime-foundationへの拡張として、統合中心のlight discoveryを実施した。
- 要件の現在値は12分野・99受入基準。要件の承認はユーザーの`-y`指定に従う。設計承認・タスク承認・実装着手は含まない。
- 関数実体と呼び出し時instance、instanceと実行コンテキスト、静的limitsと現在サイズをそれぞれ区別することが主要な設計条件となる。
- 調査対象は現在の要件・全steering・CONTEXT・関連ADR・現行ソース・固定Core 2.0。過去の要件件数や旧runner仕様を現行契約として使用しない。

## 調査記録

### 現行実装との接点

**出典:** `src/WasmSharp/WasmModule.cs`、`WasmInstance.cs`、`WasmFunction.cs`、`Modules/ModuleDecoder.cs`、`Modules/ModuleValidator.cs`、`Execution/`、`Instructions/`。

**確認事項:**

- WasmModuleは静的関数定義と検証後コードを保持し、Validate全体成功後だけ実行可能になる。Instantiateの空spanと定数返却の既存テストがある。
- WasmInstanceのGetGlobalはWasmValueを返す。共有global取得のために戻り型を変えると既存APIを壊すため、GetGlobalResourceを追加する。
- ホストmodule・memory・tableは骨組みである。WasmHostCallbackのSpan戻り値は未実装契約として残っている。
- frame/value/depthの保存・復元、ThreadStatic、生成RunLoopは存在する。直接callはcallee frameを追加する拡張で成立し、CLR再帰は不要。
- 現在のExecutionBoundaryは全関数をinstance所属として扱うため、host単独呼出しとstartの入口選択を分ける必要がある。
- 既存FunctionExportは関数だけの情報であるため、4種を表すModuleExportへ置き換える。

**設計への反映:** 成立済み4段階と単一ループを維持し、ModuleInstantiatorへ構築順をまとめる。別の実行エンジン、検証済みmodule複製、後続用hookは導入しない。

### 最新のinstance・context・start契約

**出典:** [ADR 0008](../../../docs/adr/0008-instance-options-and-execution-context.md)、[ADR 0010](../../../docs/adr/0010-host-function-instance-context.md)、[ADR 0011](../../../docs/adr/0011-retain-references-after-start-failure.md)、要件8・9・10。

**確認事項:**

- host関数を取得元instanceへ固定してはならない。C#の明示instanceとWasm frameの所属instanceを経路に応じて渡す。
- 単独host呼出しはinstanceを指定してもcontextを開かない。そこからWasmへ入るたびに、その入口のポリシーで開始し、Wasm終了時に解除する。
- 既存context内のhostも深さに含める。startのcontextはstart所有instanceで開き、対象がimportした定義関数でも実行上限を対象の所属instanceへ切り替えない。
- start失敗後も保存済みのinstance・関数・資源を失効させない。完了済みの副作用も戻さない。

**設計への反映:** 関数同一性、アクセス対象instance、contextの上限を別の値として扱う。RunStartと通常Invokeの入口条件を明示し、finallyの復元範囲を入口snapshotで限定する。

### 固定Core 2.0の型と実体

**出典:**

- [Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)。
- [固定版のimport subtyping](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/types.rst#L238)。
- [固定版のリソース増大とインスタンス化](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/modules.rst#L358)。
- [固定版の型検証アルゴリズム](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/appendix/algorithm.rst#L69)。
- [固定版の定数式](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/instructions.rst#L1595)、[global用context](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/valid/modules.rst#L539)。

**確認事項:**

- memory/tableのimport照合に使う提供側minimumは現在サイズ。要求maximumがあれば提供maximumも存在し、要求値以下でなければならない。
- memoryは65,536ページまで、tableの意味論的サイズはuintの範囲。実装配列の上限は検証規則と分ける。
- startは存在する[] → []関数を指し、import関数も使える。
- 到達不能化では関数底まで型stackを戻し、その底でのpopだけをUnknownとする。その後pushされた具体型とindex・可変性の検査は残る。
- global初期化式のglobal.getにはimported immutableだけを利用できる。ref.*とv128.constはCore 2.0では有効だが本仕様の実行範囲外である。

**設計への反映:** 型規則と実装範囲を混同しない。limits違反はValidate、未対応機能はUnsupported、現在サイズによるリンクはInstantiateへ分ける。

### import情報取得の検査範囲

**出典:** 要件11、[固定版binary module](https://github.com/WebAssembly/spec/blob/05ca4182176763112561ae20153975c12bd689e4/document/core/binary/modules.rst#L52)、現行ModuleBinaryReader/ModuleDecoder。

**確認事項:**

- sectionの長さでpayloadを移動できるが、importの完全性には全section headerの終端までの検査が必要である。
- type/import内部は完全に読み、関数型indexを解決する必要がある。単にsection外枠だけを読む方式では要求型が得られない。
- codeやsegment payloadの解析を要求すると、無関係な未実装機能からの独立性を失う。
- data_countの順序はID順ではなくelementとcodeの間である。既存decoderの順位規則を共有する。

**設計への反映:** ModuleBinaryFormatのreaderを共有し、ImportInspectorはtype/importだけを解釈する。その他payloadのDecode未確認と全体Validate未実施を成功結果にも残す。型添字を解決できない失敗は構文破損と区別し、途中一覧は公開しない。limitsの意味論は取得結果の有効性保証に含めない。

### memoryの寿命と資源制限

**出典:** [ADR 0009](../../../docs/adr/0009-range-based-host-memory-access.md)、要件5・6、現行WasmImplementationLimitException。

**確認事項:** ホストは範囲コピーでmemoryを操作し、内部領域を借用しない。Core 2.0の最大memory byte長はint範囲を超える。tableの実装上限は仕様上限と異なる。

**設計への反映:** memoryはページ配列とulong offsetを使う。tableは単一配列と明示的実装上限を使う。増大を全体成功時に確定することで失敗時不変を保証する。初期割当不能とリンク不成立を分け、実OOMを隠さない。

**割当失敗の契約:** TryGrowは宣言最大値・仕様最大値・実装上限を事前に検査してfalseを返す。実際の割当不能によるOutOfMemoryExceptionは元のまま伝播する。要求サイズの検査で実機の割当成功までは予測できないため、必要な割当・コピー・初期化を終えるまで既存状態を変更しない。要件5.4・6.5はfalseだけを要求しておらず、実OOMをtrap・リンク不成立と分けて伝えるこの契約で満たす。

[.NET公式のOutOfMemoryException解説](https://learn.microsoft.com/en-us/dotnet/api/system.outofmemoryexception?view=net-10.0)は、実割当不能からの一般的な処理継続を推奨していない。一方、「例外発生時にヒープが既に破損している」という説明は同資料から確認できず、設計の根拠に採用しない。[Array.Resizeの公式契約](https://learn.microsoft.com/en-us/dotnet/api/system.array.resize?view=net-10.0)も、新配列の確保・コピー後に参照を置換する順序を採る。

### CLR同期再入とexhaustion

**出典:** 要件10.5、ExecutionBoundary、[.NET 10 TryEnsureSufficientExecutionStack](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimehelpers.tryensuresufficientexecutionstack?view=net-10.0)。

**確認事項:** guest間のcallをframe化しても、host callbackと公開Invokeの同期往復ではCLR stackが増える。.NETの非throwingな確認操作はstackに平均的な.NET関数を実行する余裕があるかを返す。任意hostコード自身の再帰を制御するものではない。

**設計への反映:** 管理する再入入口とcallback直前で余裕を検査し、CallDepthLimitとは別のHostStackLimitへ分類する。実測していないstack容量を数値として作らず、Limitをnullableへ拡張する。これは既存消費者への変更点としてdesignに明示する。

### 依存版と技術選択

現行csprojでnet10.0、生成器netstandard2.0/C#13.0/Roslyn5.9.0、TUnit1.66.16を確認した。新しい外部ライブラリは不要。公開入力のSpan、保持するImmutableArray、Ordinal比較、ThreadStaticを既存どおり使う。

## 構成案の比較

| 案 | 利点 | 制約・不採用理由 |
| --- | --- | --- |
| 既存frameと単一RunLoopを拡張 | 基盤と後続制御命令の共通性を保つ | import後のindexと入口snapshotの整理が必要。採用 |
| 関数ごとにCLR再帰 | 小さな実装でcallできる | guest再帰でCLR stackへ依存するため不採用 |
| 取得元ごとにhost関数ラッパーを生成 | 呼出し時のinstanceを隠せる | 同一性と最新ADRに反するため不採用 |
| 借用memory Span | コピー不要 | 増大・同期再入の寿命管理が公開契約に漏れるため不採用 |
| 完全Decodeをimport調査に再利用 | 単独のparser入口 | 未対応codeから独立できないため不採用。reader共有に限定 |
| リンク先を汎用resolver interfaceで抽象化 | 複数の動的解決方式を追加できる | 今回は明示登録だけで足り、未使用拡張になるため不採用 |

## 設計判断

### 判断: 提供登録集合を一つの型で所有する

- 背景: 別々のWasmHostModuleへ登録したitemが、将来同じInstantiateへ渡されるかは個々のDefine時には分からない。
- 選択: WasmImports.Addがmodule/itemの組を一括登録し、重複時には全件不変更で拒否する。WasmHostModuleは名前付きitem群を作る既存型として具体化する。
- 代案: span内の重複をリンク失敗とする案は、登録契約違反とリンク不成立を混同するため採らない。
- 互換性: 既存span overloadは内部でAddを行ってからリンクへ進む。新型をcollection expression対象にせず既存Instantiate([])を維持する。
- 確認事項: Addの原子性、登録後のbuilder変更、別module名・同module名の非重複itemをテストする。

### 判断: 関数実体・instance・contextを分離する

- 一般化: import/export/start/C#呼出しは同じ関数実体に対する入口の違いである。
- 型の分離: WasmFunctionは公開操作を集約するabstract classとし、Execution配下のDefinedFunction・HostFunction・InstanceHostFunctionをinternal sealedの具体型とする。基底のprivate protectedコンストラクターで外部継承を閉じる。所属instanceと定義コード、各形式のcallbackを種類ごとに保持し、nullableメンバーの組合せで種類を表さない。
- 実行との接続: ExecutionBoundaryで共通型から具体型へ分岐する。ExecutionFrameとInterpreter.RunはDefinedFunctionを受け取り、hostをWasmコードとして実行する経路を型で制約する。定義関数は両添字を明示して構築する。公開GetFunction・CreateHost・funcref・登録表は同じWasmFunction参照を使い続ける。
- 代案との比較: 種類enumとnullable群の併用は不正な組合せを残す。公開sealed型が別の実体を包む案は同じ関数に2つのobjectを要し、現在の呼出し側に必要な能力を増やさない。具体型を公開する必要もない。通常のclass継承だけで表現でき、追加ライブラリや仮想Invokeの拡張点は不要とする。
- 移行と検証: 型保持のタスク3.3で既存関数と実行fixtureを移行し、生成時null拒否・型/参照同一性・別添字・定数Invoke・引数拒否・実行状態復元を確認する。callback実行とinstance付きInvokeは既存のタスク10で接続し、タスク3.3では既存の未対応拒否を維持する。
- 選択: hostは型とcallback形式だけを保持し、instanceは呼出し時情報とする。startの入口ポリシーはstart所有instanceから選ぶ。
- 公開署名: instanceの明示は`Invoke(WasmInstance instance, ReadOnlySpan<WasmValue> arguments)`、省略は`Invoke(ReadOnlySpan<WasmValue> arguments)`とする。明示指定側をnullableにする必要はない。実行時にnullが渡された場合の要件8.10・8.11の扱いは型注釈と分ける。
- 簡素化: 元instanceを覚えるexportラッパー、start失敗後の失効フラグ、contextを持つhost専用objectを作らない。

### 判断: 共有実体に資源操作をまとめる

- 一般化: hostと後続guest命令は同じ増大・内容保持を必要とする。
- 選択: WasmGlobal/Memory/Tableを状態の唯一の所有者とし、後続guest命令はその内部操作を再利用する。
- 簡素化: guest固有のtrapや失敗値への変換は命令側に残し、今はリソースの成功/失敗だけを定める。

### 判断: import調査は成功情報と未確認範囲を同時に返す

- 選択: 成功専用WasmImportInspectionと専用失敗例外。空一覧は確定成功、部分一覧は公開しない。
- 代案: Try操作で空一覧を返す方式は未確認とimportなしを混同しやすい。完全module生成は独立性を失う。
- 影響: ランナーは通常公開APIから依存を取得できるが、Decode/Validateの代替として使えない。

## リスクと対応

- 同期再入で外側stackを上書きするリスク: callback引数の所有コピー、入口snapshot、内側終了frame数を固定する。
- import後の関数indexを定義配列へ直結するリスク: 全体indexと定義indexを区別する。
- 後続命令のscopeが混ざるリスク: data/element/ref/SIMDをUnsupportedのまま明示し、公開値を運べることと命令対応を分ける。
- 資源最大値の実機割当へテストが依存するリスク: limits検証と実割当のテストを分け、通常テストで4GiB確保を必須にしない。
- 新規registry導入の過剰化: 一つの閉じた登録集合に限定し、interface・DI・動的resolver・置換APIを作らない。

## 設計レビューと検証の記録

この節は設計文書のレビュー証拠を記録する。実装・build/test・公式適合の証拠とは区別する。

- 要件対応の機械確認: 99/99受入基準がdesignの対応表に存在し、欠落0件。
- 設計ドラフトの独立read-onlyレビューでは設計ブロッカー0件。再利用型のファイルパスを構成計画へ補完し、1回の修正後にreview gateを通過してdesign.mdへ確定した。
- Claude Codeの読み取り専用レビューはGO。受入基準の欠落・設計上のブロッカーはなく、補足指摘3件を確認した。実行は終了コード0で完了した。

| 指摘 | 採否と設計への反映 |
| --- | --- |
| TryGrowでのOOM捕捉 | 実OOMを捕捉して継続する契約の見直しを採用。予測可能な上限はfalse、実割当不能は元の例外伝播へ分け、確定前に既存状態を変更しない。「ヒープ破損がある」という理由付けは一次資料で確認できず採用しない |
| 定義関数へのinstance付きInvoke | 補強を採用。null・定義元・別instanceのすべてで追加引数を検証せず無視する契約と、定義元環境・入口上限を確認するテスト観点を明記 |
| 承認待ち等の過程メモ | 整理を採用。未実施・承認待ち・実行中の記述を除き、判定と指摘の採否を記録 |

修正後のClaude Code再レビューもGOで、修正1・2は解消、新しい契約矛盾は0件。修正3で残っていた再レビュー予定の文も本結果へ置き換えた。初回・再レビューとも終了コード0で完了した。レビュー原文は`artifacts/host-linking-design-review/claude-review.md`と`artifacts/host-linking-design-review/claude-recheck.md`に保存した。

最終文書検証はPowerShellで`& 'artifacts/host-linking-design-review/verify-design.ps1'`を実行し、終了コード0。受入基準99/99・欠落0、境界節・配置計画、相対リンク、コードフェンス、placeholder、空白、spec.jsonの承認状態、要件本文のSHA256不変、`git diff --check`を確認した。設計は生成済み・未承認、タスク未生成・実装未許可とする。コードの変更、build/test、公式スイート実行は本作業の対象外であり未実施。

### Codexによる設計検証（2026-09-18）

`kiro-validate-design host-linking`として、要件12分野・99受入基準、設計、steering、関連ADR、現行コード、固定Core 2.0仕様を照合した。

| 検証 | 判定 | 重大指摘 |
| --- | --- | --- |
| 初回: 文書・契約担当と既存コード統合担当の並列レビューをmainが統合 | GO | 0件 |
| 初回GO後: 会話履歴を引き継がない別エージェントによる独立再検証 | GO | 0件 |

独立再検証では過去レビュー、memory、既存のレビュー記録を参照せず、対象文書・コード・一次仕様から判定した。定義関数の所属instance、callbackへ渡すinstance、実行上限の分離、単一RunLoopと同期再入時の復元、リソース同一性と増大失敗時の状態維持、start失敗後の保存参照、import情報の完全取得と未確認範囲に重大な矛盾はなく、設計修正は不要と判断した。

文書検証は`& 'artifacts/host-linking-design-review/verify-design.ps1'`で終了コード0。要件対応表の別途照合も終了コード0で、99/99件、欠落・余分なID・重複は各0件だった。設計本文のSHA256は`552D22E6C35DAE8D2F318936FEA4833C8949A93481BCE5EEF9ED3F9163EAC5DD`。本検証で変更するのはこの記録だけで、要件・設計・spec.json、既存のステージ済み内容とHEADは維持する。

このGOは設計品質の判定であり、人による設計承認・タスク生成・実装着手の許可ではない。実装、build/test、公式適合確認は対象外・未実施。設計は生成済み・未承認、タスク未生成、`ready_for_implementation=false`を維持する。

## 参照

- [要件](requirements.md)、[ブリーフ](brief.md)、[設計](design.md)。
- [固定外部ソースの版と手順](../../../thirdParties/README.md)。
- [明示4段階](../../../docs/adr/0001-explicit-staged-runtime-api.md)、[単一ループ](../../../docs/adr/0002-single-pass-linear-interpreter.md)、[trap結果伝播](../../../docs/adr/0004-trap-result-propagation.md)、[ホスト例外](../../../docs/adr/0007-propagate-host-exceptions.md)。

---

## 実装ギャップ分析（2026-09-17）

### 分析対象と結論

`kiro-validate-gap host-linking`として、現行の要件12分野・99受入基準と、作業ツリー上のランタイム・生成器・テストを照合した。要件は承認済み、設計は生成済み・未承認、タスクは未生成、`ready_for_implementation=false`である。既存の設計・調査記録は実装証拠と区別し、本節では不足能力と統合上の制約を記録する。

- 公開4段階、値と結果の所有、バイナリreader、検証成功の一括反映、フレームと共通失敗境界を再利用できる。
- 現行の公開実行経路はimportなし・引数と追加localsなし・スカラー定数1個の返却に限られる。ホスト型や命令名の存在だけでは、接続・実行が成立していない。
- 主な不足は4種の静的定義とリンク、一般の関数実行、共有リソース、両callback形式、start、独立したimport情報取得である。全4段階と生成命令契約へ変更が及ぶ。
- 既存基盤を拡張し、共有実体・リンク構築・import調査を責務ごとに追加する混合案が有力であり、現行designの構成とも合う。ここでは設計の承認や実装方式の最終決定は行わない。
- 規模は **XL（2週間超の目安）**、リスクは **High**。外部サービスの統合ではなく、添字空間、関数とinstanceの同一性、同期再入、失敗時の状態復元を同時に拡張することが理由である。工期の確約ではない。

### 現行資産と再利用できる境界

行番号は本分析時点。以下のE番号を要件対応表の根拠として用いる。

| 根拠 | 現行資産 | 確認した状態と拡張点 |
| --- | --- | --- |
| E1 | [WasmModule](../../../src/WasmSharp/WasmModule.cs) 18–39、71–156 | 静的情報は型・定義関数・関数export。Validate全体成功後だけコード・export索引・成功フラグを反映する。Instantiateは検証前を拒否するが、`hostModules`を参照せずinstanceを生成する。import解決は未実装 |
| E2 | [ModuleBinaryReader](../../../src/WasmSharp/Modules/ModuleBinaryReader.cs)、[ModuleDecoder](../../../src/WasmSharp/Modules/ModuleDecoder.cs) 12–117、167–190、247–329 | 長さ制限、LEB、UTF-8、位置診断、section順位、型列と圧縮localsを再利用できる。import/table/memory/global/startはUnsupported、exportは関数のみ。InspectImportsの独立入口はない |
| E3 | [ModuleValidator](../../../src/WasmSharp/Modules/ModuleValidator.cs) 19–176 | 関数型index・関数export名/添字を先に検証し、定数とendの型検査・線形化を同時に行う。引数、非0のlocals、結果0個/複数はUnsupported。型多相性、call、local/global、limits、startの検証はない |
| E4 | [InstructionSet](../../../src/WasmSharp/Instructions/InstructionSet.cs) 8–72、[InstructionAttribute](../../../src/WasmSharp/Instructions/InstructionAttribute.cs)、[命令生成器](../../../src/WasmSharp.Generators/InstructionGenerator.cs) 312以降 | 定数4種とendにhandlerがある。call/return/drop/unreachable/local/globalは名前とopcodeのみの未対応宣言。生成RunLoopとhandler署名は再利用し、uint添字の即値・検証規則・handlerを追加する |
| E5 | [WasmFunction](../../../src/WasmSharp/WasmFunction.cs)、[WasmInstance](../../../src/WasmSharp/WasmInstance.cs) | 関数は所属instanceとFunctionIndexを保持し、定義/コード配列へ直接添字アクセスする。関数の名前取得は実装済み、global/memory/tableの取得は常にArgumentException。4種のimport先行表と定義indexの変換が必要 |
| E6 | [ExecutionBoundary](../../../src/WasmSharp/Execution/ExecutionBoundary.cs)、[Interpreter](../../../src/WasmSharp/Execution/Interpreter.cs)、[InterpreterContext](../../../src/WasmSharp/Execution/InterpreterContext.cs)、[ExecutionFrame](../../../src/WasmSharp/Execution/ExecutionFrame.cs) | ThreadStatic、固定上限、frame/value/depthの保存復元、結果コピーがある。Interpreter.Runは引数を配置せずlocals数0で入場し、定数/endのみを実行する。host/start入口と直接callは未実装 |
| E7 | [WasmHostModule](../../../src/WasmSharp/WasmHostModule.cs)、[WasmMemory](../../../src/WasmSharp/WasmMemory.cs)、[WasmTable](../../../src/WasmSharp/WasmTable.cs) | 3型とも空のクラスで、ホストmoduleの名前・Define、memory/tableの状態・操作がない。WasmGlobal、WasmLimits、WasmImportsは新規資産となる |
| E8 | [WasmValue](../../../src/WasmSharp/WasmValue.cs)、[WasmFunctionType](../../../src/WasmSharp/WasmFunctionType.cs)、[WasmResults](../../../src/WasmSharp/WasmResults.cs)、[公開例外](../../../src/WasmSharp/Exceptions/) | 7種の値、ビット列と参照同一性、型列と結果の所有コピーを再利用する。リンク診断、import調査失敗、HostStackLimitの表現は追加が必要 |
| E9 | [ランタイムテスト](../../../tests/WasmSharp.Tests/)、[生成器テスト](../../../tests/WasmSharp.Generators.Tests/) | 公開定数経路、破損/未対応、失敗境界、内部contextの保存復元を持つ。ホスト連携の公開統合経路を受入済みとは扱えない。生成器テストは実際の命令/実行契約ソースをEmbeddedResourceとして取り込む |

公開型は既存の`src/WasmSharp`直下、内部処理は`Modules`・`Execution`・`Instructions`へ配置する。新しい技術レイヤーや別の実行エンジンは必要ない。現在の依存はランタイムnet10.0、生成器netstandard2.0/C#13/Roslyn 5.9.0、TUnit 1.66.16で、各csprojとdesignの指定が一致する。追加NuGet依存は本分析では必要としない。

### 要件と資産の対応

**Missing**は未実装の能力、**Constraint**は既存契約・配置・対象範囲による制約、**Unknown**は設計方針があっても実装上の成立をまだ実証していない事項を示す。既存資産がある行も、拡張後の受入成功を意味しない。

| 要件ID | 再利用資産 | ギャップと統合上の意味 |
| --- | --- | --- |
| 1.1, 1.2 | E1–E3 | **Missing**: import/global/memory/table/startの静的定義とdecode/validate。既存の型・関数定義だけでは必要なmoduleを保持できない |
| 1.3, 1.6 | E1、E8、E9 | **Constraint**: 検証前Instantiate拒否、同じmoduleの成功状態、定数経路とビット列の契約を維持する。実装範囲拡張を基盤契約の作り直しにしない |
| 1.4, 1.5 | E2、E4、E9 | **Missing / Constraint**: 新規構文の破損診断を追加し、既知の構文違反をUnsupportedへ変換しない。data/element/data_countは未対応を維持し、startへ到達させない |
| 2.1, 2.2, 2.3, 2.4 | E3、E5、E6、E8 | **Missing**: 引数のframe配置、型別ゼロ/nullのlocals、0/複数結果、local.get/set/tee。公開Invokeには引数の個数・型検査が既にあるが、実行できるmoduleが限定されている |
| 2.5, 2.6, 2.7, 2.8, 2.9, 2.10 | E4–E6、E8 | **Missing / Constraint**: call、return、drop、呼出しごとのlocals分離。importした定義関数は元instanceで実行する。共有frame方式を拡張し、guest再帰をCLR再帰へ移さない |
| 3.1, 3.2, 3.3 | E2–E4 | **Missing**: 全index空間、local/global/callの入力・結果型検査。uint添字を値即値と区別してDecodeから実行コードまで保持する |
| 3.4, 3.5, 3.6 | E1、E3 | **Missing / Constraint**: return/unreachable後の型多相性と具体型・添字・可変性の検査。全体成功後だけ実行情報を反映する既存契約は継続する |
| 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7 | E2、E3、E5、E8 | **Missing**: 共有global実体と型、初期化式、get/set、ホスト更新、instanceごとの独立性。WasmValueを値として取得するだけでは可変globalの共有を表せない |
| 5.1, 5.2, 5.3, 5.4, 5.5, 5.6 | E7、E8 | **Missing / Constraint**: memoryの型・割当・範囲Read/Write・TryGrow・共有。範囲コピーと増大失敗時不変を維持し、4GiBのbyte長をintへ縮小しない |
| 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7 | E7、E8 | **Missing / Constraint**: tableの参照型・null初期化・Get/Set/TryGrow・共有。仕様のuint上限と実装配列の上限、参照同一性と値のコピーを区別する |
| 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7 | E1、E2、E5、E7 | **Missing**: 提供登録、宣言ごとの名前/種類/型/現在limits照合、構造化リンク診断、4種のimport先行index表。同名import宣言は個別照合し、同じ実体を接続する |
| 7.8, 7.9, 7.10, 7.11 | E3、E5 | **Missing / Constraint**: 種類横断export名、memory合計、limits、4種Get操作、再exportと定義の独立性。関数Getの名前不在/種類違い拒否を拡張し、Exportsコレクションは追加しない |
| 7.12, 7.13 | E1、E7 | **Missing**: 未参照提供元を使わないリンクと、登録集合単位での重複拒否・不変更。現状のhostModules未使用を、完成した名前解決の証拠にはできない |
| 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7 | E6–E8 | **Missing / Constraint**: 明示型host生成、両callback形式、結果検査、再入で壊れない引数、元例外伝播。WasmResultsの所有コピーとfinally復元を利用する |
| 8.8, 8.9, 8.10, 8.11 | E5–E7 | **Missing**: instance付きInvoke、必須形式の事前拒否、各経路のinstance選択。hostを取得元instanceへ固定するラッパーでは同一性の要件を満たせない |
| 9.1, 9.2, 9.3, 9.4, 9.5, 9.6 | E1–E3、E6 | **Missing**: startのindex/型検証、全リンク・割当・初期化後の一度の実行、start専用入口、失敗時の返却抑止。例外変換の部品はあるが公開start経路はない |
| 9.7, 9.8 | E5–E7 | **Missing / Constraint**: start前に参照表を完成し、失敗後も保存参照と完了済み副作用を保持する。ロールバックや失効状態は追加しない |
| 10.1, 10.2 | E4、E6、E8 | **Missing**: unreachableの実行handlerとInvoke/startからの実trap。既存の内部ExecutionResult変換テストを公開経路の確認へ拡張する |
| 10.3, 10.4, 10.5, 10.6, 10.7, 10.8 | E6、E8 | **Missing / Constraint / Unknown**: 直接call・host・startの共通深さ管理、入口ごとの復元、CLR同期往復の余裕確認。現在のCallDepthLimit契約だけでは全経路の成立を実証できない |
| 10.9, 10.10, 10.11 | E6 | **Missing**: host単独Invokeではcontextを開かず、Wasm/startへ入るときに開始する。Aの実行終了後にBへ入る場合は上限を引き継がない入口制御が必要 |
| 11.1, 11.2, 11.3, 11.4, 11.5, 11.6 | E2、E8 | **Missing / Constraint**: 型解決済みimport一覧、独立入口、成功/失敗の未確認範囲。完全Decodeの再利用だけでは未対応codeから独立できず、readerとsection/type/import読取を共有する必要がある |
| 12.1, 12.2, 12.3, 12.4 | E9 | **Missing**: 各新規能力を公開APIと正負バイナリでつなぐ受入。既存内部テストは基盤回帰の根拠であり、共有・host再入・startの代替ではない |
| 12.5, 12.6, 12.7, 12.8 | E9、現行designのテスト戦略 | **Missing / Constraint**: 両callback形式、instance/contextの経路別確認、start失敗後参照の公開受入と実行記録。公式全件合格・後続機能の完成とは区別する |

### 統合と互換性の注意点

1. **関数indexを定義配列のindexとして扱う前提を外す。** `WasmFunction.Definition/Code`は現在`FunctionIndex`で配列へ直接アクセスする。import数を含むmodule全体index、定義配列index、元instanceの関係を明示し、診断の関数indexもずらさない。別instanceからimportした定義関数の所属は変更しない。
2. **アクセス対象と実行上限の所有者を分ける。** BがAの定義関数を呼び、その関数がhostを呼ぶ場合はcallbackへAを渡す。Bが再exportされたhostを直接呼ぶ場合はBを渡す。一方、startの新規contextはstart所有instanceの上限を使う。単独hostの明示instanceだけではcontextを開始しない。
3. **既存公開操作の意味を保つ。** `Instantiate(ReadOnlySpan<WasmHostModule>, options)`、`Instantiate([])`、`new WasmHostModule()`、`GetGlobal(string): WasmValue`、既存InvokeとWasmResultsの所有契約を維持する。共有global取得は設計のGetGlobalResourceを追加する案で補える。
4. **公開署名の変更を互換と誤記しない。** 現在のWasmHostCallbackは`Span<WasmValue>`を返す骨組みであり、設計ではWasmResultsへ変わる。未実装であっても既存delegate宣言を利用するソースには移行が必要である。WasmExhaustionException.Limitの`int`から`int?`への変更も利用者に影響する。既存CallDepthLimitの数値契約は保持し、HostStackLimitだけがnullとなる設計である。
5. **import調査の成功を完全検証へ昇格させない。** type/importを完全に読み、後続sectionの外枠も末尾まで確認してから一覧を公開する。他payloadをスキップするため、成功にも未確認範囲が残る。完全Decodeと共通化するのは読取規則であり、両入口の検査範囲まで同一にはしない。
6. **生成ソースとテストの契約を同時に更新する。** handler署名とRunLoopを維持するなら生成器本体の変更は現時点で必須ではない。ただしImmediateKind/ValidationRule/Instruction等を変更すると、実ソースを埋め込む生成器テストのコンパイル入力にも影響する。通常ビルドによる生成と両テストプロジェクトで確認する。

### 既存テストからの移行と受入の不足

| 現行の証拠 | 実装時に必要な扱い |
| --- | --- |
| [ModuleDecoder_DecodeFailureTests](../../../tests/WasmSharp.Tests/Modules/ModuleDecoder_DecodeFailureTests.cs) 10–17、58、132–145は、今回追加するsection・call・非関数exportをUnsupportedと期待 | 対応するものだけ、成功または本来の破損/検証失敗へ期待を更新する。例として`call`の未終端即値`1080`は、即値読取を実装するとDecodeエラーになる。data/element/data_countや後続命令のUnsupported確認は残す |
| [ModuleValidator_ValidateTests](../../../tests/WasmSharp.Tests/Modules/ModuleValidator_ValidateTests.cs) 10–16は引数・locals・結果0/複数をUnsupportedと期待 | 実行範囲の制限を外した正例へ移す。巨大locals宣言は安易に展開せず、入力保持・型規則・実装保持上限を分離する。既存の終端型不一致や全体成功前未反映の確認は維持する |
| [ExecutionBoundary_InvokeTests](../../../tests/WasmSharp.Tests/Execution/ExecutionBoundary_InvokeTests.cs) と内部contextテスト | 手動で作った外側contextや内部結果だけで、実hostの再入、実unreachable、start、start失敗後の継続を確認済みにしない。公開正負バイナリで不足する経路を追加する |
| [既存バイナリfixture](../../../tests/WasmSharp.Tests/Fixtures/ConstantModuleBinary.cs) | 定数専用fixtureを残し、新たな型/import/リソース/start用のバイト列構築を用意する。WAT/WASTパーサーやランタイム内部への専用hookを受入へ持ち込まない |

追加確認は要件の異なる境界へ絞る。代表的には4種の共有と再export、増大後のlimits照合、到達不能後の型/添字/可変性、host引数と結果の寿命、A/Bの上限選択、start前構築と失敗後参照、import完全取得/空/失敗を扱う。既存のビット列・reader・所有コピーの確認を意味なく複製しない。

### 実装アプローチの比較

いずれも公開4段階、単一RunLoop、型検証と線形化の同一パスを維持する。guestのCLR再帰化、取得元固定のhostラッパー、完全Decodeだけに依存するimport調査は既存契約に反するため、実行可能な代案に含めない。

| 案 | 具体的な構成 | 利点 | 制約・不利益 |
| --- | --- | --- | --- |
| A: 既存構成を中心に拡張 | Decoder/Validator/Interpreter/Boundaryを直接拡張し、リンク構築をWasmModule/WasmInstanceのprivate処理へ置く。必須の公開リソース・登録・import情報型は追加する | 呼出し経路の移動が少なく、既存の回帰テストを当てやすい | WasmModuleとDecoderへ調査・接続・初期化の責務が集中する。import調査との読取重複を避ける工夫が必要 |
| B: 内部の専用構成要素を多めに追加 | 既存公開入口から、リンク/資源初期化、関数本体検証、呼出し準備、import調査をそれぞれの具体クラスへ委譲する。実行ループ自体は共通 | 機能ごとの責務と確認点を小さく保ちやすい | static定義・frame・診断を受け渡す内部契約とファイル数が増える。既存処理の移動も増え、単なる転送クラスや重複モデルになりやすい |
| C: 既存基盤の拡張と必要な専用構成要素の追加 | Decoder/Validator/Interpreter/Boundaryは拡張し、共有実体、提供登録、ModuleInstantiator、ImportInspector、共有ModuleBinaryFormatを追加する | 再利用と責務分離を両立し、現行designの配置と一致する。後続のguest命令も同じリソースを使える | import先行index、構築順、frame基準、共有readerの契約を先に揃える必要がある。共通ファイルの同時編集に注意する |

**設計への推奨候補はC。** 新規のinterface・DI・汎用resolver・後続用hookは不要である。既存designは既にこの構成を採っているため、本分析を理由に再生成する必要はなく、上記の実装差分・互換性・受入移行を設計確認とタスク分割に使う。

### Research Neededと設計確認へ持ち越す事項

既存design・ADRで決まった公開方針は未決定扱いに戻さない。以下は実装上の実証が残る事項である。

| 項目 | 現在分かっていること | 未確認事項と解消方法 |
| --- | --- | --- |
| **Unknown: CLR同期再入の余裕確認** | guest callはframe化し、host往復の再入入口とcallback直前にTryEnsureSufficientExecutionStackを使う設計。HostStackLimitとnull上限も定義済み | 実host往復でチェックが働き、context/frame/depthを復元して次の独立呼出しを行えることは未実証。低い管理深さでの通常確認と、設計にある独立プロセスでのstack境界確認へ分ける。任意hostコード自身の再帰は対象外 |
| **Unknown: 増大処理の確定手順** | 予測可能な上限はfalse、実OOMは捕捉・変換せず伝播し、既存状態を維持する設計 | ページ表・table配列の準備から、追加割当のない確定処理までを実コードへ落とす必要がある。境界・成功・上限失敗を小さい資源で確認し、実OOMや4GiB確保を通常テストの必須条件にしない |
| **Constraint: localsと添字の保持上限** | 現行Decodeは圧縮localsを保持し、uint最大値の宣言も巨大配列へ展開しない。設計はuint添字と型別locals初期値を追加する | 引数数と追加locals合計、配列長への変換、必要stack量を実装制限と仕様違反に分けて具体化する。到達不能を理由に静的検査を省略しない |
| **Constraint: reader共通化後の診断** | InspectImportsの検査範囲、失敗理由、部分一覧非公開は設計済み | 共通化後も完全Decodeの破損/Unsupported優先順位、位置、未確認範囲を維持する。後続の壊れたsectionと未対応payloadを含む正負入力で差を確認する |

追加外部依存の互換性調査は不要と判断した。固定Core 2.0と.NET標準機能を前提とする既存設計を使用し、今回新たな上流版の採用や性能保証は提案しない。実機での資源割当・CLR stackの挙動は未実測である。

### 推奨する実装分割と確認範囲

1. 値型を再利用してglobal/memory/table、提供登録、関数の定義/host区分を具体化する。
2. 共通readerと静的定義を拡張し、全index空間・初期化式・start・対象命令の検証と線形化を追加する。import情報取得は共有reader確定後に分けて進められる。
3. 引数・locals・結果、直接call/return、host callbackと同期再入を同じframe/contextへ接続する。
4. 全import照合・定義割当・参照表完成・startの順序を統合し、公開APIから共有、失敗分類、失敗後継続を確認する。

共通のDecoder・Validator・Interpreterを同時編集する分担は避ける。実装時は変更単位で必要なテストとレビューを行い、警告・エラー0のReleaseビルド後に両TUnitプロジェクトをコマンドで実行する。正式な実装タスクと検証記録は、設計確認・承認後に生成するtasks.mdへ置く。

### 本分析の検証記録

- 現行要件を独立に数え直し、12分野・99件を確認した。上の要件対応表は99件すべてを対象とする。これは分析の対応範囲であり、受入テストの合格件数ではない。
- 要件・設計・steering・CONTEXT・関連ADR、現行ソースとテストを読み取り、実行側と仕様整合の調査を並行して統合した。最新のinstance/context/startの主要契約について新たな文書間矛盾は確認しなかった。
- 追記後のPowerShellによる文書検証は終了コード0。要件対応99/99、欠落・余分・重複0、相対リンク26件の実在、末尾空白なし、spec.jsonの承認状態を確認した。追記前本文のSHA256一致と他の既存作業ファイル16件のSHA256不変、`git diff --check`も確認した。追記節の独立した読み取り専用照合では修正指摘なし。
- 今回の変更は本research.mdへの追記のみ。実装コード、テスト、requirements/design/spec.jsonの変更、Gitの状態変更操作は行っていない。既存の設計レビュー記録は保持した。
- ビルド、TUnit、公式スイート、実資源割当・CLR stackの実測は未実施。実装完了・設計承認・Core 2.0適合の判定は本分析に含めない。
