# 検証用fixture

`SuiteWorkspace.CreateAsync()`は固定内容・著者・日時の一時Gitリポジトリと、その外側に独立した出力ディレクトリを作ります。取得した`Commit`を小さなテスト用profileの固定commitに使用できます。Gitのユーザー設定を引き継がず、実リポジトリや公式入力は変更しません。`CreateProfile()`は一時リポジトリの全入力と`Commit`をspec/WABTの固定値に持つprofileを作り、`CreateRequest()`は同じリポジトリをspec-rootとWABT-rootに使う生成要求を作ります。

`ConverterFixture`は.NETビルドで用意するテスト専用実行ファイルです。`SuiteWorkspace.ConverterPath`はテスト出力へ配置した実行OS用apphostの絶対pathを返します。引数は`<input.wast> -o <output.json>`です。出力先の親ディレクトリは呼び出し側で事前に作成します。WASTの内容は解析せず、入力名で動作を選びます。

| 入力名 | 終了値 | 生成物 |
| --- | --- | --- |
| `success.wast` | 0 | JSONと空moduleの有効なbinary |
| `failure.wast` | 1 | なし |
| `partial.wast` | 0 | JSONのみ。参照先binaryは欠落 |
| `partial-failure.wast` | 1 | JSONのみ。参照先binaryは欠落 |

標準出力は受け取った引数と作業ディレクトリのJSON、標準エラーは失敗・部分生成の診断です。入出力障害ではpathと理由を標準エラーへ出し、終了2を返します。出力先の親に既存ファイルを置くことで、OSの権限設定に依存せず保存失敗を再現できます。

`Data/minimal-script.json`と`Data/module.wasm`は変換結果の最小入力です。`Data/incomplete-run-report.json`は素材スナップショットや集計を欠いた不完全な結果の標本で、正常なRunReportや公式受入の証拠には使用しません。保存済みファイルの保護や不正な結果の読取検証に使用します。

`TestProcess`の30秒制限は検証用プロセスの異常によるテスト停止を防ぐためだけのものです。製品ランナーの公式ケース実行には適用しません。
