# WasmSharp2

## 開発環境のセットアップ

クローン後、リポジトリのルートで次のコマンドを実行してください。

```sh
dotnet tool restore
dotnet husky install
```

Husky.NetとCSharpierは、`.config/dotnet-tools.json`でバージョンを固定した.NETローカルツールです。
コミット時に、ステージ済みのC#ファイル（`.cs`、`.csx`）と.NET設定ファイル（`.csproj`、`.props`、`.targets`、`.xml`、`.config`）をCSharpierで自動整形します。

フックを手動で実行する場合は、次のコマンドを使用してください。

```sh
git hook run pre-commit
```

## GitHub Actions

[Unit tests](.github/workflows/unit-tests.yml)は、push・pull request・手動実行で起動します。Ubuntu上で.NET 10と固定したCore 2.0仕様を取得し、警告をエラーとして扱うReleaseビルド後に、ランタイムと生成器の両テストを実行します。

テスト結果のTRXファイルは、各実行のArtifactsに`unit-test-results`として保存します。片方のテストが失敗した場合も、もう片方の実行と結果の保存を行います。
