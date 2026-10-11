# WABT1.0.42の固定と比較元移行

2026-10-12、ユーザー承認に基づきWABTを1.0.42（`ff0ef7e0009402740c805a9744c09b05be063e48`）へ更新した。test-suite-runner15.1・15.2とnumeric-control9.6は、独立レビューAPPROVEDと親のTASK: VERIFIEDを確認して完了した。後続タスクやCore2.0全体の完成判定は含まない。

## 変更内容と受入条件

- 固定specは`05ca4182176763112561ae20153975c12bd689e4`、公式入力は全147件を維持する。
- WABTの全21機能をソースとビルド済み`--help`へ照合し、1.0.42で既定ONになったexceptions・tail-call・annotations・memory64・multi-memory・extended-const・relaxed-simdを明示的に無効化する。実効ON/OFFは旧profileと一致する。
- `CorpusGenerator.Convert`はprofileの論理引数から入力・出力pathだけを解決し、変換器へ渡す引数とmanifestの記録を一致させる。既存fixtureへ明示的な引数を渡すテストを追加した。
- 上流ソースは改変せず、WABTの固定commitとprofileを更新する。公式JSON、期待値、判定条件、旧素材、旧baselineは保持する。

空typed selectの変換修正は[上流PR#2810](https://github.com/WebAssembly/wabt/pull/2810)に含まれ、[1.0.42](https://github.com/WebAssembly/wabt/releases/tag/1.0.42)に収録されている。

## 保存先

すべてのコマンド・開始終了時刻・native終了値・標準出力・標準エラーは`artifacts/numeric-control/wabt-1.0.42-20261012/`の`<label>.result.json`と同名logへ保存した。

| 内容 | path |
| --- | --- |
| 旧素材 | `artifacts/test-suite-runner-acceptance-20261001/portable/corpus/manifest.json` |
| 旧比較元 | `artifacts/test-suite-runner-acceptance-20261001/run-baseline.json` |
| 新素材 | `artifacts/numeric-control/wabt-1.0.42-20261012/corpus/manifest.json` |
| 変換比較 | 同ディレクトリの`conversion-comparison.json` |
| 新変換baseline | 同ディレクトリの`conversion-baseline.json` |
| 移行後の実行baseline | 同ディレクトリの`run-baseline.json` |
| numeric-control9.6の結果・比較 | 同ディレクトリの`run.json`・`comparison.json` |
| 素材と移行の全件監査 | 同ディレクトリの`migration-audit.json`・`audit.py` |

## 変換と比較元移行の結果

WABTは`artifacts/wabt-core2-1.0.42/`へ別ビルドした。構成・Releaseビルドは終了0、WERROR有効。実行ファイルのSHA-256は`5c377ca185191854aa5e29c8f96e9fe347730d964185adfc6a859ecb5ffbabf0`。

全147入力の生成は終了0、入力異常0、生成物5,821件。全生成物のhashを再計算し、旧素材との差は`modules/select.2.wasm`の1件だけだった。全147個の公式JSONが同一で、53,907commandのID・順序・期待値を維持する。型なしselectは`0x1B`、空typed selectは`0x1C 0x00`となった。

`compare-conversion`は比較成立・完了、条件差9件（WABT commit・論理引数・7機能の既定値）、出典差1件（変換器hash）、素材差1件、未比較0を記録し、意図した更新差があるため終了1。この結果を同条件の一致とは扱わない。

比較元にはnumeric-control着手直前の`2c7bd25551802583c0792db93496f510bfa763ce`を`git archive`で別領域へ展開した。受入後修正の`39a2748`とsrc・tools・testsに差がないことを確認した。旧profile・旧素材で実行し、旧baselineとの比較は終了0、条件差・素材差・全ケース差・回帰・欠落0だった。

続いてsnapshot内の埋込みprofile1ファイルだけを新profileへ置き換えて再ビルドした。archiveとの全ファイル照合で、それ以外の変更がないことを確認した。新素材による旧ランタイムの実行は終了0、全53,907commandの分類は旧結果と一致し、既存passed2,491件を保持した。failed・runner_error・入力異常0。新結果を`baseline-save`で別pathへ保存し、旧baselineのSHA-256`0f47b1fc0b648b8e539297a60a3ef5a36c7d78c79ef3f8550ef86b10a5814285`は維持した。これをtest-suite-conformanceの受入済み範囲の新素材による再確認とする。

## numeric-control9.6の結果

現在のランタイムで新素材の全147入力・53,907commandを実行し、記録・出力完了、終了0。passed16,924、failed0、runner_error0、runtime_unsupported2,100、blocked33,806、out_of_scope1,077。`select.wast`のcommand119と120は、それぞれ`type mismatch`・`invalid result arity`で始まるValidate診断でpassedとなった。

新baselineとの`compare-run`は終了0、比較成立・完了。条件差・出典差・素材差・追加・欠落・回帰・未比較0、変化14,524件。既存passed2,491件を保持する。未対応とその依存は後続仕様の範囲として残る。

前回9.6の全caseとの比較では、変わったのは`select.wast/command120`だけだった。blocked33,806件の全起点はruntime_unsupportedであり、残存未対応2,100件はlinear-memory645件・tables-references377件・simd1,078件へ対応する。今回のnumeric-control結果をbaselineへ保存する11.4は実施していない。

## 単体テストと確認範囲

profile引数を使う一時flagのOFFでは、Releaseビルドの警告・エラー0後に追加テスト1件が引数不一致で失敗し、既存4件が成功した（native終了2）。ONで5件成功後、一時flagを除去した。RED・GREENのコードとログを保存した。

最終Releaseビルドは警告・エラー0。ランタイム2,790件、生成器76件、ランナー702件が成功し、失敗・スキップ0。初回の全ランナーテストは、既存のコピー独立性テストが引数3個を仮定していたため1件失敗した。変更前の引数列との一致を確認する形へ直し、再ビルド後の全702件成功を確認した。初回ログも保持している。

9.6の3テストファイルは開始時点の未コミット変更を引き継いだ。新しいランタイム動作の追加はなく、公開4段階の副作用、定数式拒否、深さ1でのloop反復・分岐・selectを含む11ケースを再実行した。今回追加した動作は変換器へのprofile引数適用であり、ランタイムに公式ケース依存の処理は加えていない。

## 独立レビューと完了確認

履歴を引き継がない別エージェントが全差分・仕様・RED/GREEN・保存結果を確認し、独立ビルドと同じ3suite、CSharpier9ファイル、diff-check、CLI helpを終了0で実行した。旧新5,821素材、snapshotの全Git blob、全caseの期待値と分類、blockedの全起点を別スクリプトで照合し、APPROVEDと判定した。公式全体runそのものは親が実行し、レビュー担当は保存結果を独立監査した。

レビュー中のLINQ引数名の指摘を採用し、`argument`を規約どおり`x`へ変更した。修正後のReleaseビルドは警告・エラー0、関連5テストと整形は終了0。レビュー担当と親が最終差分・原ログ・終了値を確認した。親は最終18ファイルのhash一致を確認して`kiro-verify-completion`を適用し、指定タスクを`TASK: VERIFIED`とした。その後の変更は完了checkboxと検証記録のみである。

- [独立レビュー](../../../artifacts/numeric-control/wabt-1.0.42-review-20261012/review.md)
- [独立監査](../../../artifacts/numeric-control/wabt-1.0.42-review-20261012/independent-audit.json)
- [親の完了確認](../../../artifacts/numeric-control/wabt-1.0.42-20261012/completion-verification.md)
- [前回9.6との比較とbaseline保存確認](../../../artifacts/numeric-control/wabt-1.0.42-20261012/task9-completion-audit.json)

移行後の実行baselineのSHA-256は`4229a52dec5000a2fc1204b9059890d090b3b316d4b1d56808797d6b834f6c12`、変換baselineは`4979e98df30308d4418e3050899f322ec1b085b88b7d2b3c053e2f9e0abe1d3b`。それぞれ移行run・新manifestとバイト一致する。
