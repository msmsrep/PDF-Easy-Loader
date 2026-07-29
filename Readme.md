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

## メール情報の抽出方法

対象のPDFはメールをPDF化するゲートウェイが生成したもので、ページに描画された見た目のほかに、機械可読な情報を持っている。これをそのまま利用する。

- ヘッダー部が「ラベル列（`From:` など）」と「値列」に分かれた表組みとして描かれている
- 各アドレスに `mailto:` のリンク注釈が張られている
- 元のメール本文が `body.html` として添付されている

### アドレスとヘッダー

1. 1ページ目を**座標付き**で読み、ベースラインごとの行に組み立てる（`PdfPageLayout`）
2. ラベル列の位置にあり `From:` / `To:` / `Cc:` / `Subject:` などにマッチする行を**フィールドの開始**とする
3. 値列から始まる行は**折り返しの継続行**として直前のフィールドに連結する
4. 区切り線・定型文・大きな行間が来たらヘッダーブロックの終わりとし、以降を本文とする
5. アドレスは**`mailto:` リンク注釈のURI**を正とし、注釈のY座標が属するフィールドへ振り分ける
6. 表示名はフィールドのテキスト側から補い、表示名がアドレスと同一なら落とす

この方式には次の利点がある。

- 署名や定型文に含まれるアドレス（`<mailto:...>` を含む署名など）が、ヘッダーのY範囲外なので構造的に混入しない
- アドレスがURI文字列から得られるため、CJKフォントのCMapに依存するテキスト抽出の乱れの影響を受けない
- 1アドレス＝1注釈なので、行の折り返しでアドレスが分断されない
- Outlookが出力する `"addr" <addr>` の冗長な形が畳まれ、貼り付ける文字列が短くなる

リンク注釈が無いPDFでは、同じヘッダー範囲のテキストを解析するフォールバックへ落ちる。アドレスリストの分解は引用符と山括弧を考慮した分割を行い、1件ごとのパースは `System.Net.Mail.MailAddress.TryCreate` に任せる（`AddressListParser`）。アドレスは大文字小文字を無視して重複排除する。

### 本文

本文は、添付された `body.html`（元のメール本文そのもの）を読んでプレーンテキストに変換する（`PdfEmbeddedBody`）。文字コードはBOMと `<meta charset>` から判別する。対象のPDFの本文は `iso-2022-jp` で入っている。

ページに描画されたテキストを拾うのに比べて、

- 「添付は Adobe Reader で開け」という定型文が構造的に混ざらない
- 署名のリンクが `oshita@susumu.co.jp<mailto:oshita@susumu.co.jp>` のように二重に出ない
- 段落の空行や表のセル区切りが元のまま残る

`body.html` が無いPDFでは、ヘッダーブロックより下のページテキストから本文を組み立てるフォールバックへ落ちる（行間の空きから段落の切れ目を復元し、定型文を落とす）。

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
  PdfService           iTextによる暗号化判定・復号・抽出の入口
  PdfPageLayout        1ページ目を座標付きの行と mailto: リンク注釈として読み出す
  MailHeaderParser     レイアウトを From/To/Cc/Subject/本文 のフィールドに分解する
  AddressListParser    アドレスリストの分割・正規化・重複排除
  PdfEmbeddedBody      添付された body.html を読んでプレーンテキストにする
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

- 抽出はPDFの **1ページ目のみ** を対象とする。
- ヘッダーのラベルは `From:` / `To:` / `Cc:` / `Bcc:` / `Reply-To:` / `Subject:` / `Date:` / `Sent:` という**英語表記**を前提にしている（`MailHeaderParser` の `LabelRegex` に追加すれば拡張できる）。
- ページの回転（`/Rotate`）が設定されたPDFは想定していない。対象のPDFは回転なしで生成される。
- HTMLからテキストへの変換は自前の簡易実装で、タグの除去・改行の復元・実体参照の展開までを行う。凝ったレイアウトのHTMLメールは字面どおりには再現されない。
- 添付ファイル本体（`.xlsx` など）は現状取り出していない。本文の `body.html` のみを使う。
- 複数ファイルを同時にドロップした場合、クリップボード履歴へ流し込むのは最後の1件のみ（履歴が混ざらないようにするため）。
- テキスト抽出に失敗してもPDFを開く処理自体は続行し、抽出結果が空になる。
