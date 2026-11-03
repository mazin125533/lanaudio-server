# lanaudio-server（受信側／デスクトップPC）

ノートPC（[lanaudio-client](../lanaudio-client)）から UDP で送られてきた音声を受信し、既定のスピーカーで再生します。

## ビルド
.NET 8 SDK を入れて `build.bat` を実行 → `publish\AudioReceiver.exe`

## 使い方
```
AudioReceiver.exe [ポート=50005] [目標遅延ms=60]
```
起動時に表示される「このPCのIP」をクライアント側で指定します。

- 音が来ない → 管理者権限で
  `netsh advfirewall firewall add rule name="LanAudio" dir=in action=allow protocol=UDP localport=50005`
- 途切れる → 遅延を 100〜150 に

## パケット形式
`src/AudioPacket.cs` 参照（クライアント側と同じファイル。変更する場合は両方そろえる）。
