// デスクトップ側（サーバー）: ノートPCから UDP で届いた音声を既定の再生デバイスで鳴らす
// 通知領域（タスクバー右下）に常駐し、右クリックメニューから操作する
// 使い方: AudioReceiver.exe [ポート] [目標遅延ms]   ※省略時は保存済みの設定
namespace LanAudio.Receiver;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\LanAudio.Receiver", out bool first);
        if (!first)
        {
            MessageBox.Show("LanAudio 受信はすでに起動しています。\nタスクバー右下の通知領域にあるアイコンから操作してください。",
                "LanAudio 受信", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Yu Gothic UI", 9f));
        Application.Run(new TrayApp(args));
    }
}
