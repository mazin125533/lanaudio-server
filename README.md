# lanaudio-server（受信側／デスクトップPC）

ノートPC（[lanaudio-client](https://github.com/mazin125533/lanaudio-client)）から UDP で送られてきた音声を受信し、既定のスピーカーで再生します。
タスクバー右下の通知領域に常駐するアプリです。

## 使い方
1. [Releases](https://github.com/mazin125533/lanaudio-server/releases) から `AudioReceiver.exe` をダウンロードして起動
2. 通知領域に青いスピーカーのアイコンが出ます（初回にファイアウォールの確認が出たら「許可」）
3. アイコンを右クリック →「このPCのIPアドレス」で IP を確認（クリックでコピー）し、送信側に設定

| アイコン | 状態 |
|---|---|
| グレー | 停止中 |
| 青 | 待受中（音が届いていない） |
| 青＋緑の丸 | 受信・再生中 |

右クリックメニュー
- **受信を開始／停止**
- **設定...**（アイコンのダブルクリックでも開く）: ポート、目標遅延、起動時に自動で受信開始、Windows 起動時に起動
- **Windows 起動時に起動**
- **ファイアウォールで受信を許可...**: 音が届かないときに。管理者権限で UDP ポートの受信許可ルールを追加します

設定は `%APPDATA%\LanAudio\receiver.json` に保存されます。
既定の再生デバイスを切り替えると自動で追従します。

## トラブルシューティング
- 音が来ない → メニューの「ファイアウォールで受信を許可...」を実行
- 途切れる → 設定で目標遅延を増やす（有線LAN: 20〜40ms、Wi-Fi: 60〜100ms が目安）

## ビルド
.NET 8 SDK を入れて `build.bat` を実行 → `publish\AudioReceiver.exe`

アイコン（`src/app.ico`）は `src/IconArt.cs` の描画から `tools/make-icon.ps1` で生成しています。

## パケット形式
`src/AudioPacket.cs` 参照（クライアント側と同じファイル。変更する場合は両方そろえる）。

## ライセンス

[MIT License](LICENSE)。自由に使用・改変・再配布できます。

音声処理に [NAudio](https://github.com/naudio/NAudio)（MIT License）を使用しています。
