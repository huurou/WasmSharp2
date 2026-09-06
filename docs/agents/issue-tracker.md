# 課題管理: GitHub

このリポジトリの課題はGitHub Issuesで管理し、操作には`gh` CLIを使用する。
対象リポジトリは`git remote -v`から判断する。
Kiroの仕様文書は引き続き`.kiro/specs/`で管理する。

## 基本操作

- 作成: `gh issue create --title "..." --body-file <本文ファイル>`
- 参照: `gh issue view <番号> --comments`
- 本文・ラベルの取得: `gh issue view <番号> --json number,title,body,labels,comments`
- 一覧: `gh issue list --state open --json number,title,body,labels,comments`
  必要に応じて`--label`や`--state`で絞り込む。
- コメント: `gh issue comment <番号> --body-file <本文ファイル>`
- ラベル追加・削除: `gh issue edit <番号> --add-label "..."`または`--remove-label "..."`
- 完了: `gh issue close <番号>`

複数行の本文は一時ファイルに書き、`--body-file`で渡す。

スキルの「課題管理へ公開」はIssueの作成、
「関連チケットを取得」はIssueの本文・コメント・ラベルの取得を意味する。

## PRの扱い

**PRs as a request surface: no.**

`yes`へ変更した場合、外部PRもIssueと同じラベル・状態でトリアージする。
外部PRは投稿者の`authorAssociation`が`CONTRIBUTOR`、
`FIRST_TIME_CONTRIBUTOR`、`NONE`のものとする。
参照・差分取得・コメント・ラベル変更・終了には`gh pr`の対応する操作を使う。

IssueとPRは番号を共有する。種別が不明な参照は
`gh pr view <番号>`で確認し、PRでなければ`gh issue view <番号>`で取得する。

## wayfinderの操作

- マップ: `wayfinder:map`ラベルを付けた1つのIssueとし、
  本文にNotes、Decisions-so-far、Fogを保持する。
- 子チケット: GitHubのサブIssueとしてマップに関連付ける。
  利用できない場合はマップ本文のタスクリストに追加し、
  子の本文冒頭に`Part of #<マップ番号>`を記載する。
  種別ラベルは`wayfinder:research`、`wayfinder:prototype`、
  `wayfinder:grilling`、`wayfinder:task`を使用する。
- 依存関係: GitHubのIssue依存関係を使用する。
  利用できない場合は子の本文冒頭に`Blocked by: #<番号>, #<番号>`を記載する。
  すべてのブロッカーが閉じられたら着手可能とする。
- 次の作業: マップの未完了の子から、未完了のブロッカーがなく、
  担当者もいない最初のチケットをマップ順に選ぶ。
- 担当取得: `gh issue edit <番号> --add-assignee "@me"`。
- 解決: 回答をコメントし、チケットを閉じ、
  マップのDecisions-so-farへ要旨とリンクを追記する。
