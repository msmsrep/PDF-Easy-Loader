# PDF-Easy-Loader

パスワード付きPDFをドラッグ＆ドロップするだけで復号して開き、中に書かれたメール情報（From / To / Cc / Subject / 本文）を抽出してクリップボードへ流し込むWindowsデスクトップアプリ。

暗号化されたメール本文PDFを開いて宛先や本文を手作業でコピーする手間をなくすことを目的にしている。

## 主な機能

- **ドラッグ＆ドロップ** — ウィンドウにPDFを落とすと処理が始まる。複数ファイルをまとめて落とせる。
- **起動引数からの処理** — ファイルの関連付けや「送る」から起動された場合も、そのまま同じ処理を行う。
- **パスワードの自動試行** — 登録済みのパスワードを最終利用日時の新しい順に試す。成功したパスワードは利用日時を更新するので、よく使うものが先に試されるようになる。
- **その場でのパスワード入力** — 登録済みで全滅した場合のみ入力を求める。ここで入力したパスワードは保存しない。
- **暗号化なしPDFの素通し** — 暗号化されていなければ複製せず、元ファイルをそのまま開く。
- **件名でリネームして表示** — 復号したファイルは `<件名>_<元のファイル名>.pdf` にリネームしてから既定のビューアで開く。対象PDFはファイル名が分かりにくいため、件名を足して見分けやすくしている。
- **クリップボード履歴への流し込み** — 本文 → Cc → To → From の順にコピーするので、`Win+V` の履歴に4件が並び、Fromが先頭に来る。個別の「コピー」ボタンも各項目にある。
- **一時ファイルを残さない** — 復号したPDFは一時フォルダに置き、起動時と終了時に丸ごと削除する。

## 動作要件

- Windows 10 バージョン1809（10.0.17763）以降 / x64
  - MSIXパッケージのマニフェストは MinVersion 10.0.18362 を要求する
- .NET 10 デスクトップランタイム（MSIXでは同梱せず、フレームワーク依存で動作する）

## 使い方

1. アプリを起動する
2. 「パスワード設定」から、業務で使うPDFのパスワードを識別名付きで登録する
3. PDFをウィンドウにドラッグ＆ドロップする
4. 復号されたPDFが既定のビューアで開き、抽出されたメール情報が一覧に出る
5. 各項目の「コピー」ボタン、または `Win+V` のクリップボード履歴から貼り付ける

パスワードは登録時に識別名（ラベル）だけを一覧表示し、パスワード本体は画面に一切表示しない。

## データの保存場所とセキュリティ

| 対象 | パッケージ実行時 | 非パッケージ実行時（F5デバッグなど） |
| --- | --- | --- |
| パスワード | MSIXコンテナ内 `LocalState` | `%LOCALAPPDATA%\PDF-Easy-Loader` |
| 復号済みPDF | MSIXコンテナ内 `TempState\decrypted` | `%TEMP%\PDF-Easy-Loader\decrypted` |

- パスワードは **DPAPI（CurrentUser スコープ）** で暗号化して `passwords.dat` に保存する。追加エントロピーを与えているため、ファイルを他のPCや他のユーザーへコピーしても復号できない。
- 復号済みPDFは起動時・終了時に削除する。ビューアがファイルを掴んでいて消せなかった場合も、次回起動時に消える。
- パッケージ実行時のデータはコンテナ内にあるため、アンインストールすると一緒に消える。

## プロジェクト構成

```
App.xaml(.cs)          DIコンテナの構築、起動引数の処理、一時ファイルの後始末
Models/                MailContent（抽出結果）/ PasswordEntry / PdfLoadResult
Services/
  AppEnvironment       MSIXパッケージ実行かを判定し、保存先フォルダを決める
  PdfService           iTextによる暗号化判定・復号・テキスト抽出
  MailHeaderParser     抽出テキストを From/To/Cc/Subject/本文 に分解する正規表現
  PdfLoader            「復号 → 抽出 → リネーム → 起動」を通す本体
  DpapiPasswordStore   DPAPIによるパスワードの永続化
  TempWorkspace        復号済みPDFの一時フォルダ管理
  ClipboardService     クリップボードおよび履歴への書き込み
  DialogService        パスワード入力・設定画面の表示
ViewModels/            Main / PdfResult / PasswordPrompt / Settings
Views/                 MainWindow / PasswordPromptWindow / SettingsWindow
Behaviors/             FileDropBehavior（ドロップをコマンドへ橋渡しする添付ビヘイビア）
MSIX/                  マニフェスト、署名証明書、生成された .msix
```

MVVM（CommunityToolkit.Mvvm）＋ `Microsoft.Extensions.DependencyInjection` によるコンストラクタインジェクションで構成している。

### 主な依存パッケージ

| パッケージ | 用途 |
| --- | --- |
| itext / itext.bouncy-castle-adapter | PDFの復号とテキスト抽出 |
| itext.font-asian | CJKフォントのCMap読み込み（日本語PDFの抽出に必要） |
| CommunityToolkit.Mvvm | ObservableObject / RelayCommand |
| Microsoft.Extensions.DependencyInjection | DIコンテナ |

DPAPI（`System.Security.Cryptography.ProtectedData`）は .NET 10 のWindows向けランタイムに同梱されているため、パッケージ参照は不要。

## ビルド

```powershell
# ビルド
dotnet build
# 実行（非パッケージ実行になる）
dotnet run
```

## MSIX

- https://learn.microsoft.com/ja-jp/windows/apps/dev-tools/winapp-cli/guides/dotnet
- https://github.com/microsoft/winappCli/tree/main/samples/wpf-app

`dotnet publish` すると、csprojの `PackageMsix` ターゲット（`AfterTargets="Publish"`）が `winapp pack` を呼び、ビルド → MSIXパッケージ化 → 署名までをまとめて実行する。マニフェストと証明書は `MSIX\` から読み、生成された `.msix` は `MSIX\Package\` に出る。出力ファイル名はマニフェストの `Identity` から `<Name>_<Version>_<Arch>.msix` として組み立てられる。

```powershell
# マニフェストファイル作成など（初回のみ）
winapp init
# csprojにEnableMsixToolingが追加されるが不要
# csprojにPackageMsixのタスクを入れる事でビルド/msixパッケージ/証明書書き込みをまとめて実行する

# 自己署名証明書の作成（初回のみ）
winapp cert generate --if-exists skip
# 自己署名証明書を信頼する　管理者権限で実行（一度だけ）
winapp cert install .\MSIX\devcert.pfx

# リリースビルド＋MSIXパッケージング＋署名
dotnet publish
```

`winapp init` は証明書をプロジェクト直下に作るので、`MSIX\devcert.pfx` へ移動しておく（csprojの `MsixCertPath` がこのパスを見る）。証明書が無い場合は署名なしでパッケージングされる。

## 制限事項

- 抽出はPDFの **1ページ目のテキストのみ** を対象とする。
- メールヘッダーの解析は正規表現ベースで、`From:` / `To:` / `Cc:` / `Subject:` / `Date:` という英語ラベルの並びを前提にしている。レイアウトの異なるPDFでは正しく分解できないことがある。
- 複数ファイルを同時にドロップした場合、クリップボード履歴へ流し込むのは最後の1件のみ（履歴が混ざらないようにするため）。
- テキスト抽出に失敗してもPDFを開く処理自体は続行し、抽出結果が空になる。
