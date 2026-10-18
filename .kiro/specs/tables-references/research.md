# 調査と設計判断

## 概要（Summary）

- 対象：tables-references。調査日は2026-10-18、確認したHEADは4d53fe44d5da591d97971fbfc4ad85de28a3f50d。
- 調査区分：既存システムの拡張。design-discovery-lightに従い、固定仕様の参照規則と現行コードの統合点を調査した。
- 既存のWasmValue、WasmTable、ConstantInitializer、関数実行を再利用できる。関数実体の先行構築と対象解決後の呼び出し処理の共通化を、挙動追加より先に行う。
- 現在のlinear-memoryは実装と受入記録を持つ。比較元は同仕様の最終保存を使用し、古いnumeric-control時点の素材へ戻さない。
- 調査は読み取りのみであり、ビルド、テスト、公式スイートの再実行は行っていない。

## 調査記録（Research Log）

### 現行実装の接続点

参照したソースはsrc/WasmSharp/WasmValue.cs、WasmTable.cs、WasmInstance.cs、Modules/ModuleInstantiator.cs、ConstantInitializer.cs、ModuleValidator.cs、FunctionValidator.cs、ExpressionDecoder.cs、Execution/Interpreter.cs、ExecutionBoundary.cs、DataInstance.csである。

- 値と追加localの参照型保持は実装済み。typed selectは型検証後に参照型を未対応とする。
- elementは空sectionを含めて未対応。対象命令はopcodeだけ登録済みで、即値・検証・実行の接続が必要。
- ModuleInstantiatorはglobalを評価してからWasmInstanceを作り、WasmInstanceがDefinedFunctionを生成する。ref.funcのglobal初期化には順序変更が必要。
- WasmTableは配列の交換前にコピー・増大領域の初期化を完了する。公開TryGrowのOOM伝播契約は維持し、guest handlerの呼び出し箇所だけで-1へ変換できる。
- Interpreter.Callは関数添字の解決と、定義関数・ホスト関数の実行準備を兼ねる。後半を共通化すれば間接callでも所属instance、結果、再入、上限を再利用できる。
- 生成器はenum値を属性から生成コードへ出力する既存方式を持つ。新属性や別の命令辞書は不要。

調査は履歴を引き継がない2つのサブエージェントにも分担し、現行実装と固定仕様の結果をmain contextで統合した。
スキルはkiro-spec-quick、kiro-spec-requirements、kiro-spec-design、kiro-spec-tasks、japanese-tech-writing、csharp-conventions、codebase-designを使用した。
EARSと各フェーズのレビュー条件、既存C#規約、小さい内部契約への責務集約を適用する。
日本語と英数字の間の空白はユーザー指定を優先する。

### 固定Core 2.0の参照規則

根拠はthirdParties/WebAssembly-specの固定commit
`05ca4182176763112561ae20153975c12bd689e4`にある次の文書と参照実装である。

| 根拠 | 確認した規則 | 設計への反映 |
| --- | --- | --- |
| document/core/valid/modules.rstのModules | C.refsは関数本体とstartを除くmoduleの関数添字集合 | elementだけから宣言を集めない。global式と関数export等も事前収集 |
| document/core/valid/instructions.rstのReference Instructions、Constant Expressions | ref.funcには存在と宣言が必要。global.getはimport immutableのみ | 共通ConstantInitializerと検証用contextへ集約 |
| document/core/binary/modules.rstのElement Section | flags0〜7と旧funcidx形式・式形式 | modeを明示し、旧形式をref.func式へ正規化 |
| document/core/binary/instructions.rst | call_indirectはtype→table、table.initはelem→tableの即値順 | IndexとSecondaryIndexの対応を命令表で明示 |
| document/core/exec/instructions.rstのTable Instructions | 一括操作の全範囲検査、重複コピー、0長、grow失敗、drop | WasmTableのTry操作とElementInstanceの寿命に分離 |
| document/core/exec/modules.rstのInstantiation | 関数を式評価前に用意し、element→data→start | 完成したinstanceを用意してから共有tableを変更 |
| interpreter/exec/eval.ml | 間接callの境界→null→型、呼び出し、添字を含むtrap診断 | 共通callへ対象を渡し、診断種別とselectorを構造化して公開境界へ渡す |

固定資料のexec/modules.rstには、説明手順のactive全部→declarative全部という記述と、数式の宣言順runelemに差がある。
参照実装eval.mlも宣言順で処理する。
ADR0012の参照実装互換と失敗後の観測を踏まえ、宣言順のactive転送・drop、declarative dropを採用した。
先行activeのtrap後にあるdeclarativeの状態は、共有tableへ残った関数から観測できるため、統合テストへ含める。

exec/modules.rst末尾の副作用に関する注記だけを根拠に、全segmentの事前境界検査を導入しない。
linking.wastには、先行elementが成功し、後続element/dataが失敗しても共有tableへの変更が残るケースがある。
当該転送の変更なしと、インスタンス化全体のロールバックなしを区別する。

Webでは[Core 2.0保存版](https://webassembly.github.io/spec/versions/core/WebAssembly-2.0.pdf)と[固定commitの実行規則](https://raw.githubusercontent.com/WebAssembly/spec/05ca4182176763112561ae20153975c12bd689e4/document/core/exec/modules.rst)も確認した。
詳細の判断はローカルの固定ソースと公式ケースに基づく。

### 既存ADRとの整合

- ADR0004：内部trapはExecutionResult、公開例外への変換はExecutionBoundary。
- ADR0005：Validate全成功時だけmoduleの検証状態を確定する。
- ADR0006：InstructionSetの宣言とhandlerから分岐を生成する。
- ADR0008・0010：間接callも同じ実行コンテキストと呼び出し元のホストinstance情報を使用する。
- ADR0011：初期化・start失敗後に保持された参照を無効化しない。
- ADR0012：公式診断の前方一致と参照実装の診断選択を維持する。

ADRの変更は必要ない。

### 公式素材と比較元

[linear-memoryの受入記録](../linear-memory/acceptance.md)と[外部ソースの固定](../../../thirdParties/README.md)を確認した。
現在のWABTは1.0.42にDataCount修正を適用した固定commit
`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`であり、メモリの古いWABT記録は採用しない。

次機能の比較元は
`artifacts/linear-memory/task-8.4-20261016T001148Z/run-baseline.json`、
対応素材は
`artifacts/linear-memory/wabt-commit-20261016/corpus/manifest.json`である。
2026-10-17のレビュー修正後もこのbaselineは保存し直されていない。
既知の診断補足文33件の差は受入記録とauditで確認する計画とし、今回の調査を新たな公式runの証拠にはしない。
今回、両ファイルの存在とSHA-256を読み取り、受入記録のmanifest値c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e、baseline値70e9453f222304b22a04f1e2b2f4cd2e56208192d5b59e0603615dade18309a1との一致を確認した。
実装着手時にもhash・profileの対応を確認する。

## 方式の比較（Architecture Pattern Evaluation）

| 案 | 利点 | 制約 | 判断 |
| --- | --- | --- | --- |
| 既存table・call・定数式を拡張 | 同一性と失敗規則を1か所で維持できる | 内部構築順の整理が必要 | 採用 |
| 参照専用ストアと呼び出し実行系 | 既存処理を隔離できる | 同じ関数とtableを二重管理し、再入や上限が分岐する | 不採用 |
| データと要素を汎用segment基底へ統合 | 型の見た目を揃えられる | バイト列と参照列の型・modeを隠し、現在の要件には不要 | 不採用 |

## 設計判断（Design Decisions）

### 既存の構築責務をWasmInstance内で完結させる

関数実体は既にWasmInstanceが生成するため、定義global生成とelement実体の構築もその内部構築契約へまとめる。
ModuleInstantiatorはLinkと共有領域への初期化・startの順序に集中する。
外部へ未完成instanceを返す、関数実体を後から差し替える、公開の初期化setterを足す案は採用しない。
既存動作のリファクタリングを先行し、参照定数式はその後に追加する。

### 要素の表現を正規化し、型の異なる状態は統合しない

8種のバイナリ表現を3modeと参照式列へまとめる。
これは同じ意味論を二重に検証・評価しないための正規化である。
DataInstanceとElementInstanceは寿命の形が似ていても、バイトと参照、宣言modeが異なるため別の具体型とする。
汎用segmentインターフェースは作らない。

### 領域操作とguestの意味論を分ける

WasmTableに範囲検査と変更を集約し、handlerはoperand、添字解決、ExecutionResultだけを扱う。
.NET配列とSpanの重複コピーを採用し、独自の参照コピー機構は作らない。
公開ホスト操作とguestでは失敗契約が異なるため、その変換は呼び出し側で行う。

### 間接callの選択と呼び出しを分ける

直接callと間接callは関数の取得だけが異なる。
関数実体を入力とする内部CallFunctionへ共通処理を移す。
公開Invokeをhandlerから呼び直す案は、コンテキストと境界の二重化につながるため採用しない。

### 公開原因と内部診断選択を分ける

既存TableOutOfBoundsを維持し、call_indirectのundefined elementだけを内部診断種別で識別する。
任意の診断文字列をhandlerへ持たせず、ExecutionBoundaryが公開文言を決める。
原因enumの追加やテストIDへの依存は不要である。
null要素と範囲外の間接callはselectorもExecutionResultへ保持し、公開境界で符号付き10進表記を付ける。
bulk.wastはuninitialized element 2まで要求するため、診断種別だけでは公式期待値を満たせない。

### 即値の格納順と診断の検査順を分ける

固定valid.mlはtable.initをtable→elem→型一致、call_indirectをtable→type→funcrefの順に検査し、どちらもoperandより先に静的な対象を確認する。
バイナリの格納順はelem/typeが先であるため、SecondaryIndexを先に検査する。
要素の診断カテゴリはelem segmentである。
また定数式は定数性の走査を終えてからref.funcの存在・宣言を検査し、後続の非定数命令を未知関数より先に拒否する。

## リスクと対処（Risks & Mitigations）

- 構築順変更で不完全なinstanceが漏れる：全関数・global・segment実体の構築を終えてから共有領域の変更とstartを許す。
- 宣言集合の不足：global・export・各modeのelement、関数本体/start/importだけの否定例を組にする。
- 一括操作のoverflowや部分書込み：uintをulongへ拡張して全範囲検査し、境界が成立してからintへ変換する。
- 宣言的elementの破棄順の誤り：初期化失敗後に残った定義関数から観測する補助テストを追加する。
- 共通ファイルの編集衝突：命令表・validator・instance構築は順に統合し、並列タスクは5.1の参照handlerと5.2の基本table handlerだけに限定する。WasmTableの一括操作4.1とElementInstanceの4.2は依存順に進める。
- 資源不足試験の不安定さ：実際のOOMを必須再現とせず、決定的な上限試験と捕捉範囲・状態確定順のレビューを区別する。

## 参照（References）

- [要件](requirements.md)、[設計](design.md)、[ブリーフ](brief.md)
- [Core 2.0固定ソース](../../../thirdParties/WebAssembly-spec/document/core/)
- [固定公式テスト](../../../thirdParties/WebAssembly-spec/test/core/)
- [ランナー操作手順](../../../tools/WasmSharp.TestSuiteRunner/README.md)
- [linear-memoryの受入記録](../linear-memory/acceptance.md)

## 仕様生成の確認

2026-10-18にkiro-spec-quickの自動モードで4フェーズを実施した。
要件と設計の内部レビューはPASS、保存前のタスクグラフレビューはindependentでPASSとなった。
タスクグラフの初回指摘は、8.2の検証作業と実装修正の担当を分ける1件であり、タスク草案を1回修正して再レビューを受けた。
保存後は別の新規サブエージェントがrequirements.md・design.md・tasks.mdを直接読み、最終sanity reviewをPASSと判定した。

機械確認では9領域・55受入基準、8大項目・29実行単位、並列候補2件を照合した。
要件IDの未対応・未知ID・不正な依存参照・ローカルリンク切れ・プレースホルダー・行末空白はなかった。
正式なkiro-validate-gapとkiro-validate-designは省略した。
実装・ビルド・テスト・公式スイートの実行・baseline更新は行っていない。

## 設計検証（2026-10-18）

goal-validate-designの依頼に基づき、kiro-validate-designの基準で主担当が検証した後、会話履歴を引き継がない新しい別エージェントでも独立検証した。
同じ要件・設計に対して両者ともGO、重大指摘は0件となった。
設計の修正は不要と判断し、今回の変更はこの検証記録の追記に限った。

### 判定根拠

- 55受入基準が設計の要件追跡表に対応し、欠落・余分・重複IDは0件だった。要件・設計のローカルリンクにも参照先の欠落はなかった。
- 宣言集合、定数式の二段階の検査、table.initとcall_indirectの診断選択は、固定仕様のvalid.mlとfree.mlに整合する。
- 関数・global・全segment実体を構築してから共有領域を変更する順序により、初期化失敗後に残る関数の所属instanceを維持できる。宣言順のactive転送とdeclarative破棄は、実行規則のrunelemとeval.mlに整合する。
- WasmTableの内部範囲操作とCallFunctionへの責務整理は、既存の公開例外、関数同一性、同期再入、深さ管理を維持して組み込める。公開TryGrowのOOM伝播とguestのgrowに限る-1変換も分離されている。
- 比較元のmanifestと実行結果baselineは、「公式素材と比較元」に記載したSHA-256と一致した。今回の判定のために素材や結果を生成し直していない。

### 検証対象の識別

検証時のHEADは`4d53fe44d5da591d97971fbfc4ad85de28a3f50d`、固定仕様は`05ca4182176763112561ae20153975c12bd689e4`だった。
独立検証エージェントは`fork_turns="none"`で起動し、過去の判定・指摘・修正経緯を渡さず、現在の文書とコードを読み取り専用で確認した。
独立検証が報告したSHA-256は主担当の確認値と一致した。

| 対象 | SHA-256 |
| --- | --- |
| requirements.md | 84565de3945b2568bc7223029a4b7b9cffb1d1c5184795e3fbf46944ce2d7ce1 |
| design.md | a0999f6d94086ebd21c4b77fd6c7cd2a2f07e6022f7208c0f15899cf9f9de9f8 |

### 確認範囲

今回のGOは設計品質の判定である。
実装、ビルド、テスト、公式スイート、実際のOOMの再現は行っていない。
spec.jsonの承認情報、既存タスク、baselineは変更せず、タスク生成、ステージング、コミットも行っていない。
