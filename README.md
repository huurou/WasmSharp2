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
