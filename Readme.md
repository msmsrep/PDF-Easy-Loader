# PDF-Easy-Loader

## ビルド

```powershell
dotnet publish
```

## MSIX
- https://learn.microsoft.com/ja-jp/windows/apps/dev-tools/winapp-cli/guides/dotnet
- https://github.com/microsoft/winappCli/tree/main/samples/wpf-app

```powershell
# マニフェストファイル作成など
winapp init
# csprojにEnableMsixToolingが追加されるが不要
# csprojにPackageMsixのタスクを入れる事でビルド/msixパッケージ/証明書書き込みをまとめて実行する
# リリース
dotnet publish
# 自己証明書の作成
winapp cert generate --if-exists skip
# 自己証明書をmsixに入れてmsixパッケージング
dotnet publish
# 自己証明書を読み込む　管理者権限で実行（一度だけ）
winapp cert install .\devcert.pfx
```
