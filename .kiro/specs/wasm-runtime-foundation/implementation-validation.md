# wasm-runtime-foundation 実装統合検証

検証日: 2026-09-07（JST）

## 対象と判定
- DECISION: GO
- 対象: 承認済み `wasm-runtime-foundation` の全10タスク（小タスク32件、チェック項目42件）と全50受入基準。
- コード状態: HEAD `311bd4c69926612c8bd1a5e03d874f4f1b674427` と開始時のステージ済み8ファイルの変更を含む作業ツリー。
- 初回検証: mainによる機械検証・公開経路確認と、要件網羅・設計境界の担当エージェントによる評価を合成してGO。
- 独立再検証: 会話履歴を引き継がない別エージェント（clean_validation）が、現仕様・コードを読み、機械検証も独自に再実行してGO／VERIFIEDを確認した。
- 今回のコード・テスト修正: なし。
- OWNERSHIP: LOCAL（検証対象。修正対象の欠陥なし）
- UPSTREAM_SPEC: N/A
- BLOCKED_TASKS: なし。未チェック0件、`_Blocked:_` 0件。
- REMEDIATION: なし。

## 機械検証

| 検証 | コマンド | 初回 | 独立再検証 |
| --- | --- | --- | --- |
| Releaseビルド | `dotnet build WasmSharp2.slnx -c Release` | 終了コード0、警告0、エラー0 | 終了コード0、警告0、エラー0 |
| ランタイム全テスト | `dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build` | 終了コード0、441成功、失敗0、スキップ0 | 終了コード0、441成功、失敗0、スキップ0 |
| 生成器全テスト | `dotnet run --project tests/WasmSharp.Generators.Tests/WasmSharp.Generators.Tests.csproj -c Release --no-build` | 終了コード0、27成功、失敗0、スキップ0 | 終了コード0、27成功、失敗0、スキップ0 |
| 整形 | `dotnet csharpier check .` | 終了コード0、99ファイル | 終了コード0、99ファイル |

初回の実動確認には、ビルド済みのランタイムを参照する既存の公開4段階受入テストを使用した。

```powershell
dotnet run --project tests/WasmSharp.Tests/WasmSharp.Tests.csproj -c Release --no-build -- --treenode-filter '/*/*/WasmFunction_InvokeTests/両入力のDecode後に元バッファを変更する_4段階で定数の型とビット列を保持する'
```

終了コード0、16成功、失敗0、スキップ0。各ケースでバイト列とStreamからDecodeし、元バッファ変更後にValidate → Instantiate → GetFunction → Invokeを実行した。i32/i64の境界値、f32/f64の正負0・無限大・NaNの符号とpayloadが保持され、実際の生成ループが動作することを確認した。ライブラリのため常駐サービスの起動はない。

独立再検証では、同じ公開4段階の16ケースをビルド済みDLLから直接実行し、終了コード0、16成功、失敗0、スキップ0を確認した。

```powershell
dotnet tests/WasmSharp.Tests/bin/Release/net10.0/WasmSharp.Tests.dll --treenode-filter '/*/*/WasmFunction_InvokeTests/両入力のDecode後に元バッファを変更する_4段階で定数の型とビット列を保持する'
```

初回の残留マーカー・秘密情報検査は、`src`・`tests`・対象specの105ファイル（bin/objを除外）へ実施し、両方0件。独立再検証でもsrc/tests、README、requirements/design/tasksへ同じ2パターン群をrgで検査し、両方0件（終了コード1＝該当なし）を確認した。CSharpierの初回起動ではツール復元が必要だったため、リポジトリで固定したツールを `dotnet tool restore` で復元し、利用可能な実行環境で検査を完了した。

## 要件の網羅

7/7要件節、50/50受入基準を完了タスク・実装・検証へ対応付けた。未対応の要件節はない。

| 要件節 | 受入基準数 | 主な完了タスク | 実装と検証 |
| --- | --- | --- | --- |
| 要件1 | 6 | 7.5、8.3、9.3、10.1〜10.2 | 明示4段階、同一moduleへの全成功時反映、再Validate省略、失敗後Instantiate拒否、元入力からの独立。Module/Invokeの公開テスト |
| 要件2 | 9 | 2.1〜2.3、5.1、9.3、10.1 | scalar/v128/参照の明示構築・取得、bitと参照同一性、型違い拒否、不変の型・結果コレクション。Value/FunctionType/Resultsテスト |
| 要件3 | 7 | 1.2、7.1〜7.5、10.1〜10.2 | 両入力、short read、非seek、LEB・section・UTF-8の正負例、customの配置、Stream寿命とI/O例外。BinaryReader/Decoder/公開Decode・Invokeテスト |
| 要件4 | 5 | 8.1〜8.3、10.2 | 全体の添字とexport名を先行検査し、型検査と線形化を同一パスで実行。結果不整合、未対応実行形、部分失敗。Validator/公開Validateテスト |
| 要件5 | 11 | 5.2、5.4、6.2、9.1〜9.3、10.1〜10.2 | instance固有の関数実体、名前解決、4種定数・反復・結果寿命、instanceの実行上限、同期コンテキストとfinally復元。Instantiate/GetFunction/Invoke/Context/Interpreter/Boundaryテスト |
| 要件6 | 9 | 3.1〜3.2、5.1〜5.2、6.2〜6.5、7.4、9.2、10.2 | 段階別例外・未確認範囲、Core 2.0割当分類、確定違反の優先、trap/exhaustion/保持上限の分離。例外/InstructionSet/Decode/Validate/境界テスト |
| 要件7 | 3 | 10.1〜10.3 | 公開4段階の正負受入、全テスト、実施範囲と限界を本報告に記録 |

要件5.9〜5.11、6.6〜6.9のうち後続機能が利用する部分は、承認済み設計どおり共通の内部契約として評価した。実call・start・host callbackの完成を基盤の完了条件には含めない。

## 統合・設計・境界

- Cross-task contracts: PASS。公開操作 → ModuleDecoder/ModuleValidator → 不変の定義・実行コード、Invoke → ExecutionBoundary → Interpreter → Contextの接続が整合する。
- Shared state consistency: PASS。検証結果とordinalの名前辞書は全成功後にmoduleへ反映。各instanceが固有の関数実体と実行ポリシーを持ち、戻り値は作業スタックから独立する。
- Boundary audit: PASS。命令情報はInstructionSetの宣言を正本にlookupと単一while/switchを生成し、DecodeとValidateも同じdescriptorを使用。ホスト処理・リソース意味論・公式ランナーの責務を取り込んでいない。
- Architecture drift: なし。「構成と接点」「処理フロー」「モジュールの公開契約と所有」「実行表現と同期コンテキスト」に整合する。
- Dependency direction: 違反なし。実行からDecode/Validateへの逆呼び出しなし。生成器はランタイムを参照せず、ランタイムの実行依存にRoslyn・生成器・WABTを含まない。
- File Structure Plan vs actual: 一致。追加のテスト・fixtureも定められた境界内にあり、生成物はobj配下に置かれる。
- Revalidation Triggers: 現在のSDK/Roslynに対する通常ビルド・生成器テスト・公開実行を確認。今回、下流の契約を変更するコード修正はない。後続の数値・制御、ホスト連携、ランナーへの引き継ぎは現設計に保持されている。
- 固定Core 2.0命令付録のsubmodule HEAD: `05ca4182176763112561ae20153975c12bd689e4`。割当照合は通常183件・FC18件・FD236件であり、全命令の実行対応を意味しない。

## 実施範囲と限界

本判定は、引数・localsなし、戻り値1個、scalar constとendの基盤に対するGOである。

後続の数値・制御・call・start・メモリ・テーブル・ホスト連携・SIMD命令、実host callbackが投げる例外の同一性と実経路の復元、実trap命令、公式corpusの生成と固定スイート全件適合は未実装または未検証。並行実行保証、性能の数値保証、約2GBの実入力による保持上限到達試験も実施していない。Core 2.0全体の準拠完了を意味しない。

## 完了検証

- STATUS: VERIFIED
- CLAIM_TYPE: FEATURE_GO
- CLAIM: wasm-runtime-foundationは承認済みの基盤範囲で機能統合検証と履歴なしの独立再検証を通過した。
- EVIDENCE: 上記の全テスト、実動確認、全50受入基準の対応、設計・境界・共有状態の評価、Blockedなし。
- コード状態の一致: 初回検証と独立再検証の間にsrc/testsのソース・プロジェクトが変更されていないことをSHA256で照合した。
- GAPS: 必須検証の不足なし。後続仕様と明示された保証外の範囲は前節に記録した。
- roadmap: 両検証のGO後に対象行だけをチェック済み。
- Git: 開始時のステージ済み差分を保持し、ステージング・コミットは実施していない。

