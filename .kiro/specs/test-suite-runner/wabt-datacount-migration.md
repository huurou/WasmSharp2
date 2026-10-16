# WABTのDataCount修正と比較元の移行

2026-10-16、ユーザーの指示により、WABT1.0.42のDataCount出力を修正し、WABT自身のテストで検証する。
linear-memoryのタスク6.1で検出した公式2ケースの変換不具合を対象とする。
同仕様の6.2以降の実装と最終受入は含めない。

現在の固定WABTは修正commit`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`である。
初回のパッチ適用状態での検証を以下に保持し、コミット後の素材と比較元を末尾の「修正コミットの固定」に記録する。

## 修正と固定の方法

パッチ適用元はWABT1.0.42のcommit`ff0ef7e0009402740c805a9744c09b05be063e48`とする。
データセグメントが0件の場合は、既存のExprVisitorで定義関数内のmemory.initとdata.dropを調べ、参照があればDataCount=0を出力する。
既存のデータセグメントがある場合の書き込みと、参照がない場合の出力を維持する。

修正パッチを親リポジトリに保存し、適用元commit、パッチと対象ソースのハッシュ、ビルド条件、実行ファイルのハッシュを記録する。
初回検証のmanifestのWABT commitはパッチ適用元であり、修正後ソース全体のcommitを表すものではない。
ランナーのschemaは変更せず、ビルド記録とmanifestを実行ファイルのSHA-256で対応付ける。

## 検証と移行の手順

1. WABTの標準テストに回帰ケースを追加し、修正前の失敗と修正後の成功を確認する。
2. Windowsの標準CIに対応する単体テスト、C APIテスト、CLI・仕様テストを実行する。
3. 修正版でCore 2.0の全147入力を別出力先へ再生成し、全生成物の差分と公式JSONの一致を調べる。
4. numeric-controlの受入済みrevision`5287c36d7c2a90cc836f4ff2b23b91bffc279f53`を別領域へ展開する。
   旧素材による実行と旧baselineの一致を確認してから、新素材で同じ実装を実行し、移行用baselineを別保存する。
5. 現在のlinear-memory実装を新素材で実行し、移行後の比較元と比較する。

旧素材は`artifacts/numeric-control/wabt-1.0.42-20261012/corpus/`、旧baselineは`artifacts/numeric-control/task-11.4-20261012T-baseline-7ca484f/run-baseline.json`に保持する。
Core 2.0の版、featureの実効状態、公式期待値、command識別子、結果分類は変更しない。
この移行で保存するのは先行受入済み実装の比較元であり、linear-memoryの最終受入baselineではない。

## 実行記録

コマンド、終了値、ログ、監査結果は[証拠ディレクトリ](../../../artifacts/linear-memory/wabt-datacount-20261016/)へ保存した。
以下の証拠ファイル名はこのディレクトリを基準とする。
DataCount変換不具合の修正と、linear-memoryの実装を再開するための素材・比較元の移行は完了した。
WABT自身の全件合格とlinear-memory全体の受入は主張しない。

### 修正版の識別

| 項目 | 値 |
| --- | --- |
| 親管理パッチ | [thirdParties/patches/wabt-datacount-zero.patch](../../../thirdParties/patches/wabt-datacount-zero.patch) |
| パッチSHA-256 | `c532842132b5efaffc01d04684891566eb540d1659fd930f6acee2480cdef58c` |
| ビルド出力 | `artifacts/wabt-datacount-tests/Release/` |
| wast2json.exeのSHA-256 | `2c9114cb9fc457c323f0d45eb507614a9530180b4b7a656bd9914823ff8a31c8` |
| 構成 | Windows x64、MSVC19.51.36260.0、Release、WERROR=ON |
| CMake設定 | BUILD_TESTS=ON、BUILD_LIBWASM=ON、WITH_WASI=OFF、USE_INTERNAL_SHA256=ON |

ソース4ファイルの適用前後のSHA-256とCMakeCacheのSHA-256は[build-provenance.json](../../../artifacts/linear-memory/wabt-datacount-20261016/build-provenance.json)に記録した。
固定commitへのパッチ再適用で、改行形式を除いて現在の4ファイルを再現した（`patch-replay.json`）。
Git blobのハッシュはLFの生バイト列を対象とする。今回のWindows上の`git archive`はCRLFへ変換するため、未修正版比較用に展開したファイルの生バイトハッシュとは異なる。

WABTのテスト依存はgitlinkが示すcommitの公式アーカイブから取得した。
commit、取得URL、アーカイブのSHA-256は`dependency-sources.json`に記録した。
この初回検証の時点では、親とsubmoduleのHEAD、gitlink、indexを変更していなかった。

### WABTの検証結果

WABTの回帰テストに、データ0件で直接またはネスト・到達不能コード内からdata.dropとmemory.initを参照する4モジュールと、参照しない1モジュールを追加した。
修正前は必要なDataCountがなく失敗し、修正後は前者4件だけにDataCount=0が出力された。
既存のmemory_initとmemory64/memory_initのログ期待値は、欠落DataCountのエラーから本来のインデックス診断へ更新した。公式入力と公式期待値は変更していない。

| 検証 | 結果 | 証拠 |
| --- | --- | --- |
| WERROR=ONのReleaseビルド | 成功、警告・エラー0件 | `green-build.json/.log` |
| 追加回帰テストの修正前 | 期待どおり失敗、終了1 | `regression-red-retry.json/.log` |
| DataCount・memory_init関連 | 11件成功、wasm2c派生4件skip、終了0 | `focused-green.json/.log` |
| 元のCore 2.0最小再現 | 2件ともDataCount=0を確認、終了0 | `original-repro-green.json/.log` |
| run-unittests | 135件成功、終了0 | `wabt-unittests.json/.log` |
| run-c-api-tests | 10件成功、終了0 | `wabt-c-api-tests.json/.log` |
| run-tests | ランナー集計は1,738件成功、779件skip、失敗0件、終了0。ただし下記の誤分類を含む | `wabt-all-tests.json/.log` |

C APIのthreads・finalize・hostrefは上流の既定対象外で、今回の10件には含めない。
WASI、Linux/macOS、Debug構成は検証していない。

779件のskipを調べた結果、7ファイル・計21assertionの失敗がskipへ誤分類されていた。
`spectest-interp`は失敗件数を終了コードとして返すが、`run-tests.py`はツール種別を問わず終了3をwasm2c用のskipとして扱うためである。

| skipの内訳 | 件数 |
| --- | ---: |
| 非0終了を期待する否定テストから生成したroundtrip | 496 |
| 元テストの`--no-check`が引き継がれないroundtrip | 8 |
| 未対応の`--enable-all`を渡したroundtripの引数エラー | 6 |
| Windowsかつmemory64が無効化されていないwasm2cの明示skip | 260 |
| PLATFORMSまたはSKIPで明示された対象外 | 2 |
| 各3assertion失敗をskipへ誤分類したspec実行 | 7 |

失敗した7ファイルは、固定commitの未修正ソースを別領域でReleaseビルドし、修正版と同一の入力・引数で直接実行した。
全7件で標準出力・標準エラーがバイト単位で一致し、終了コードもともに3だった。
この21件は修正前から存在し、今回のDataCount修正による回帰ではない。
証拠は[wabt-existing-failures-comparison.json](../../../artifacts/linear-memory/wabt-datacount-20261016/wabt-existing-failures-comparison.json)と、同名の実行スクリプト`compare-wabt-existing-failures.py`、`wabt-existing-*.log`に保存した。

| 既存不一致 | ファイル | 失敗assertion数 |
| --- | --- | ---: |
| 既定multi-memoryにより複数memoryを受理 | imports、exception-handling/imports | 6 |
| 既定memory64により32bit上限超過のtextを受理 | memory、multi-memory/memory、table、function-references/table | 12 |
| relaxed SIMDの計算結果が期待と不一致 | relaxed-simd/relaxed_dot_product | 3 |

機能設定による不一致、relaxed SIMD、skipの誤分類は今回の変換修正から分離した。
したがって、WABTの標準テストコマンドは実行済みだが、WABT全体のテストがすべて合格したとは扱わない。

### Core 2.0素材と比較元の移行

全147入力の生成に成功した。5,821生成物のうち変更は次の2つだけで、どちらもオフセット28へ`0c 01 00`（DataCount=0）を追加した差分だった。

- `modules/memory_init.4.wasm`
- `modules/memory_init.9.wasm`

147JSONは生バイト列で一致し、profile、公式期待値、53,907commandの識別子と順序も一致した。
`compare-conversion`は出典差1件（実行ファイルSHA-256）と生成物差2件を報告して終了1となった。
差分を監査した後、変換baselineを別保存した（`conversion-comparison.json`、`conversion-audit.json`、`conversion-baseline-save.json`）。

numeric-controlの受入済みrevisionを展開し、旧素材による全体実行と旧baselineを比較した。
全53,907ケースの詳細、比較条件、出典が一致し、差分0件・終了0だった（`baseline-old-audit.json`）。
同じソースで新素材を実行すると、全ケースの結果分類と期待値は不変で、以前のpassed16,924件もすべて維持された。
2件だけ未対応診断の停止位置がdata.drop/memory.initから追加されたDataCount節へ移った（`baseline-new-audit.json`）。
この結果を既存CLIのbaseline-saveで別保存した。保存元のrunと保存後baselineはバイト単位で一致した（`baseline-new-save-audit.json`）。
旧素材・旧baselineのハッシュは変わっていない。

初回検証で保存した組を以下に示す。現在の検証で使う組は末尾の「修正コミットの固定」に記載する。パスは初回の証拠ディレクトリからの相対パスである。

| 用途 | ファイル | SHA-256 |
| --- | --- | --- |
| 修正後素材 | `corpus/manifest.json` | `13ab25b6adcfaed009fdc01cea33818847c911f0b7d4aa0dd5d1b26f481d1b84` |
| 変換baseline | `conversion-baseline.json` | `13ab25b6adcfaed009fdc01cea33818847c911f0b7d4aa0dd5d1b26f481d1b84` |
| 先行受入済み実装の移行後baseline | `run-baseline.json` | `70fc7bd5a5fc44225272552950ed45efd25f2f6ae45c05cb660a4850dad1f968` |

### 現在のlinear-memory実装での確認

既存の実装途中の12ファイルを変更せず、`dotnet build WasmSharp2.slnx -c Release --warnaserror`で警告・エラー0件を確認した。
その後、ランタイム3,218件、生成器93件、ランナー702件、合計4,013件のテストがすべて成功し、失敗・skipは0件だった（`current-*-tests.json/.log`）。

新素材によるrunと移行後baselineに対するcompare-runはいずれも終了0だった。
全53,907commandを記録し、failed・runner_error・回帰・欠落・未比較は0件だった。
分類はpassed18,455件、runtime_unsupported1,631件、blocked32,744件、out_of_scope1,077件で、未実装部分を含むためCore 2.0全件合格ではない。

修正前素材での同じ実装の結果と比較すると、変化したのは次の2件だけだった。

| command | 修正後の結果 |
| --- | --- |
| memory_init.wast#128 | Validateで`unknown data segment`となりpassed |
| memory_init.wast#136 | Validateで`unknown memory 0`となりpassed |

詳細は[current-audit.json](../../../artifacts/linear-memory/wabt-datacount-20261016/current-audit.json)に記録した。
現在の実装の`run.json`はSHA-256`ee3515989ae2c4abca2f46ca6469cf3c2dd75f7df350d7eb6a92107022f2a681`、`comparison.json`は`392332ec84c02c4fdd8d242980b42cc35e16115c3bdbde442b5a2893f55f1991`である。
このrunはlinear-memoryの受入baselineとして保存していない。

### 判定

独立レビューでDataCount修正のコード、回帰テスト、パッチ再現、全生成物の差分を確認し、当該修正の範囲でGOとなった。
WABT全体には上記の既存不一致と未実行範囲が残る。
linear-memoryのタスク6.1は変換不具合による停止条件を解消したが、タスクの完了判定と仕様全体の受入は別途行う。

## 修正コミットの固定

同日、ユーザー指示によりWABT内の4ファイルをcommit`c8dc7b6eae42b3aa3e95b9014dc02b71f8e0e1db`へ保存した。
親gitlink、`core2-profile.json`、対応する固定値のテストもこのcommitへ揃えた。
上流未公開のcommitを復元できるよう、親リポジトリに[Git bundle](../../../thirdParties/patches/wabt-datacount-zero.bundle)を保存した。
bundleのSHA-256は`9acda200aba8351ddee07e6cbacc71bb9f42ffc0cf83e824a25cf06603da54e9`で、上流1.0.42の固定commitとbundleだけを使い、別リポジトリに同じ修正commitを復元できた。
差分パッチは確認用として保持する。通常の取得・ビルド時には修正済みcommitを使い、パッチを再適用しない。

新しい検証証拠は`artifacts/linear-memory/wabt-commit-20261016/`へ保存した。
WABTソースと実行ファイルは先のテスト時点から不変で、既存のビルド・WABTテスト証拠を引き継ぐ。
WABTの追加ビルド・全テスト再実行は行わず、ソース4ファイルと実行ファイルのSHA-256を照合した。
現行素材にも、初回のパッチ適用状態で構成した`artifacts/wabt-datacount-tests/Release/wast2json.exe`（`BUILD_TESTS=ON`・`BUILD_LIBWASM=ON`、SHA-256`2c9114cb9fc457c323f0d45eb507614a9530180b4b7a656bd9914823ff8a31c8`）を使用した。
`thirdParties/README.md`の`wabt-core2-1.0.42-datacount`への専用ビルドは、新しい実行ファイルを用意する代替手順であり、上記のビルド記録をその実行ファイルの証拠としては扱わない。
テスト依存も、初回検証では固定commitの公式アーカイブから取得し、READMEでは同じgitlinkをsubmoduleとして取得する手順を示している。
ランナーはReleaseビルドで警告・エラー0件、変更した固定値のテストを含む702テストが成功した。

新profileで全147入力を再生成し、直前の5,821素材と全バイトが一致した。
compare-conversionは条件差1件（WABT commit）だけで終了1、素材差・出典差・未比較は0件だった。
numeric-controlの受入済みrevisionを新領域へ展開し、952ファイルのうち埋込みprofileだけを更新した。
JSON差は`wabt.commit`のみで、再実行した全53,907ケースは直前の実行結果baselineと詳細まで一致した。
結果を既存CLIのbaseline-saveで別保存し、現在のlinear-memory実装のrunとcompare-runも終了0、failed・runner_error・回帰0件を確認した。
現在のrun自体も直前の全53,907ケースと一致した。

現在の素材と比較元は次の組である。パスは新しい証拠ディレクトリからの相対パスとする。

| 用途 | ファイル | SHA-256 |
| --- | --- | --- |
| 素材・変換baseline | `corpus/manifest.json`、`conversion-baseline.json` | `c60dec91f788791a9b790835b2629c2d970e3fb44afab4d63f209df9097e263e` |
| 先行受入済み実装のbaseline | `run-baseline.json` | `619961b91dfa83c233864799e7f87323f811e5889c3d6faa7b460812fb72c4f5` |
| 現在の実装の実行結果 | `run.json` | `61760da23fd70dc73d2cf7fb37198653653aa90068caf5617ac89625cb62812e` |
| 現在の実装の比較結果 | `comparison.json` | `3824b10809b191359910942bdd4297908fa4fc872e661f99979f5f4f9de96ce2` |

監査は[commit-audit.json](../../../artifacts/linear-memory/wabt-commit-20261016/commit-audit.json)、[baseline-migration-audit.json](../../../artifacts/linear-memory/wabt-commit-20261016/baseline-migration-audit.json)、[baseline-save-audit.json](../../../artifacts/linear-memory/wabt-commit-20261016/baseline-save-audit.json)に記録した。
旧素材・旧baselineは引き続き保持し、今回もlinear-memoryの受入baselineは保存していない。
