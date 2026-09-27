---
status: accepted
---

# 公式期待診断と参照実装の診断選択に合わせる

例外型と処理段階だけの一致では、期待とは別の原因による失敗を見逃す。2026-09-27の判断により、固定Core 2.0公式テストの期待診断を公開例外の`Message`の先頭に一致させ、参照実装特有の診断選択・検査の優先順位まで合わせる。補助説明は診断の後ろへ追加できる。

判定は未実装の[`test-suite-runner`](../../.kiro/specs/test-suite-runner/requirements.md)の初期要件とし、対象の否定assertionに一律で`actualException.Message.StartsWith(expectedText, StringComparison.Ordinal)`を適用する。型・段階・失敗分類も検査し、期待文字列の正規化、Reasonによる代替、ケース別除外は設けない。`module_type=text`は従来どおり対象外とする。

ランタイムの診断文と診断選択への対応は、公式テストスイートで判明した実装上の問題を集約する[`test-suite-conformance`](../../.kiro/specs/test-suite-conformance/brief.md)の一部として扱う。本ADRはそのうち診断互換性の判断を記録する。意味的に妥当な別診断を許容する案は採用せず、解析・検証順序の調整も必要となる負担を受け入れる。安全な入力処理を維持し、公式期待値やテスト識別子へのランタイム依存は導入しない。

順序は`test-suite-runner → test-suite-conformance → numeric-control`とする。ランナーは最初から不一致を`failed`として記録し、後続仕様がランタイムを合わせる。判定基準を後から切り替える段階は作らず、完了済みランタイム仕様の要件・承認・完成状態は遡って変更しない。

同日の要件検討で、初期必須の公式ケースを実行・判定できるまでの修正は、ランタイム側も含めて`test-suite-runner`で行う境界を明確にした。動かない公式ケースを独自テストで代替してランナーを受け入れない。判定で得た`failed`の解消と、初期必須の実行経路を妨げず原因をランタイム側と確認できた`runner_error`は後続へ引き継ぐ。ランナーの受入と公式結果の合格を区別し、分類や非0終了は変更しない。
