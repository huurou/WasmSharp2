# ブリーフ: host-linking

## 課題

埋め込み利用者は、関数に引数を渡し、ホスト関数や共有リソースをWasmへ接続し、複数moduleを組み合わせたい。公式検証でもspectestとregisterを利用するため、import/exportと基本的な関数実行を各命令機能の完成後まで待たせることはできない。

## 現状

runtime-foundationの最小定数返却経路と共通の値・型・例外・実行機構がある。import、引数・localsを使う実行、ホストcallback、global・memory・tableの具体的な生成・共有は未実装。本仕様の着手・完了にconformance-runnerの完成を要求しない。

## 望む結果

関数呼出しと外部要素の生成・リンクを通常の公開APIで扱える。型を明示したホスト関数、既存instanceから得た関数・リソースを別moduleへ接続し、共有状態の同一性を保つ。後続ランナーがこの能力でspectest・registerとimport依存の追跡を実装できる。

## 方針

本仕様を最小基盤の次に実装する実行・リンク基盤とする。関数フレーム、global・memory・tableの実体、外部要素の型照合を一度だけ実装し、後続の数値・制御とリソース命令が同じ機構を拡張する。公開APIの直接テストで成立させ、公式スイートでの統合受入はconformance-runnerの初回完了時に行う。

## 範囲

- **対象**: 関数型、引数、結果0個・1個・複数、locals、local.get/set/tee、直接call、return、drop、unreachable。return・unreachable後の到達不能部分に必要な型スタックの多相性を含めて関数本体を検証し、定義関数・import関数・ホストcallbackを同じ呼出し契約で扱う。unreachableによる実際のWasm trapをInvokeとstartの公開経路で確認する。
- **対象**: globalの型・可変性・実体、スカラー定数とimported immutable global.getによる初期化、global.get/set、公開取得・更新、同一実体の共有。参照・v128の初期化式の拡張はそれぞれの機能仕様が追加する。
- **対象**: Core 2.0のmemory/tableの型・limits、定義・割当・export、通常のホスト利用に必要な生成・取得・内容アクセス。memoryは範囲指定の読み書きとし、内部領域の借用ビューは公開しない。memoryはゼロ、tableは型に対応したnullで初期化し、funcref/externrefの保持と同一性を保つ。
- **対象**: 関数・global・memory・tableのimport/export、名前解決、関数型・可変性・limitsの照合、importと定義の添字空間、再export。リンク不成立を公開失敗分類で示す。
- **対象**: 明示型ホストcallback、引数・結果の所有と寿命、ホスト例外の実体を保つ伝播、同期的な再入。登録時に第1引数にWasmInstanceを受け取る形式と受け取らない形式を区別し、前者はinstanceの省略・nullを実行前に拒否する。関数を取得元instanceへ固定せず、C#からは呼び出し時にinstanceを明示でき、Wasmからは呼び出し元instanceを渡す。Instance引数はWasmの関数型・値引数に含めず、start中も定義memory等のexportを取得可能にする。
- **対象**: 通常利用に必要なimportのmodule名・item名・外部要素の種類と型の取得。instance生成や無関係な未実装命令のDecode成功を前提とせず依存を把握でき、取得情報と未確認範囲を区別する公開契約。
- **対象**: Instantiateの共通の順序、startの型検証と実行、リンク不成立・startのtrap・exhaustion・ホスト例外の区別。start前に構築・接続・リソース初期化を完了し、start失敗後も保存済みのinstance・関数・リソースを無効化しない。
- **対象外**: スカラー数値演算と構造化制御の網羅、guestのmemory/table命令、data/element初期化、call_indirect・参照命令・SIMD命令、WASI。
- **対象外**: WAST/JSONの解釈、spectestの具体的な定義、registerコマンドとbaseline。これらはconformance-runnerが所有する。

## 責務の接点

- 完成済み基盤の4段階・値・型・命令定義・単一実行ループを拡張し、基本呼出しのための第二の実行系を作らない。後続のblock/loop分岐も同じフレームと結果受渡しを使う。
- memory/tableのリソース実体と公開ホスト操作は本仕様、guestのsize/growを含む命令とsegmentはlinear-memory・tables-referencesが所有する。リソースを増やす共通操作も実体側に集め、guest命令固有の失敗値やtrapへの変換は命令側が担当する。
- 参照の保持・null初期値はリソース生成のために扱うが、ref.*命令や宣言済み関数参照、element mode等の検証はtables-referencesへ置く。
- data/elementの初期化と、共有状態・start失敗後の副作用の複合検証は各segmentの所有仕様が同じInstantiate経路へ追加する。未対応のsegmentを無視してstartへ進まない。
- start失敗時はInstantiateを例外で終了し、完了済みの副作用を戻さない。保存済み参照から操作を続けられるが、startによる初期化完了は保証せず、利用継続はホストが判断する。
- 呼び出し深さとcallbackのInstance引数を分離する。既存コンテキストのない単独ホスト呼び出しは、instance指定の有無やリソース操作だけでは開始せず、Wasm定義関数またはstartへ入る時点で、その入口のinstanceの上限を使って開始する。既存Wasm実行からの同期再入は同じコンテキストを引き継ぎ、開始したWasm実行が終了してホストへ戻った後の別のWasm呼び出しは、新しいコンテキストと上限で実行する。
- import情報の取得はmodule全体の有効性や実行可能性の証明ではない。必要情報を完全取得した場合だけ一覧を返し、空一覧によるimportなしと取得失敗を区別する。破損して依存を特定できない場合は部分一覧を公開せず、取得済みの依存先が不成立の場合と区別する。
- instanceにExportsコレクションを追加せず、通常利用に必要な名前による取得・共有操作を設計する。globalの値コピーをリソース共有の代わりにしない。

## この仕様が所有しないこと

公式テスト用の名前や値、結果分類、素材管理をランタイムへ組み込まない。delegateから型を推論せず、reflectionやテスト専用hookで公開能力の不足を補わない。後続の命令・segmentの完成を本仕様の前提にしない。

## 上流・下流

- **上流**: runtime-foundation。
- **下流**: conformance-runner、numeric-control。linear-memory・tables-references・simdも同じ呼出し・リソース・リンク契約を利用する。

## 既存仕様との関係

- **拡張する既存仕様**: runtime-foundationの実装を拡張するが、完成済み仕様の受入範囲と承認状態は変更しない。
- **隣接**: numeric-controlは演算・構造化制御、linear-memoryとtables-referencesはguest命令・segmentを追加する。spectest・registerはconformance-runnerの初期範囲とする。

## 制約と確認事項

本仕様の受入は公開APIを使う正負のTUnitテストで行い、関数と4種の外部要素、型不一致、同一性、callbackの所有・例外、start・呼出し深さ制限を確認する。ランナーの初回受入で同じ能力を公式ケースにも通す。Wasmのtrapに.NET例外を内部伝播として使わず、ホスト境界で変換する。本文書は分担を定めるbriefであり、requirements・design・tasksと実装の承認は別に行う。文書は日本語（ja）。
