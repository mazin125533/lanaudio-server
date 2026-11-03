using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;

namespace LanAudio.Receiver;

/// <summary>通知領域（タスクバー右下）に常駐し、メニューから受信を操作する</summary>
sealed class TrayApp : ApplicationContext
{
    const string AppName = "LanAudio 受信";

    readonly NotifyIcon tray;
    readonly ToolStripMenuItem statusItem, detailItem, toggleItem, ipMenu, startupItem;
    readonly System.Windows.Forms.Timer statusTimer, restartTimer;
    readonly Icon iconOff, iconIdle, iconActive;
    readonly SynchronizationContext ui;
    readonly DefaultDeviceWatcher deviceWatcher = new();
    AppSettings settings;
    ReceiverEngine? engine;
    SettingsForm? settingsForm;

    public TrayApp(string[] args)
    {
        // 互換: AudioReceiver.exe [ポート] [目標遅延ms]
        settings = AppSettings.Load();
        if (args.Length >= 1 && int.TryParse(args[0], out int p)) settings.Port = p;
        if (args.Length >= 2 && int.TryParse(args[1], out int l)) settings.TargetLatencyMs = l;
        if (args.Length > 0) settings.Save();

        int size = SystemInformation.SmallIconSize.Width;
        iconOff = ToIcon(IconArt.Render(size, IconArt.Disabled, false));
        iconIdle = ToIcon(IconArt.Render(size, IconArt.Accent, false));
        iconActive = ToIcon(IconArt.Render(size, IconArt.Accent, true));

        statusItem = new ToolStripMenuItem { Enabled = false };
        detailItem = new ToolStripMenuItem { Enabled = false };
        toggleItem = new ToolStripMenuItem("", null, (_, _) => Toggle()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        ipMenu = new ToolStripMenuItem("このPCのIPアドレス");
        startupItem = new ToolStripMenuItem("Windows 起動時に起動", null, (_, _) => Startup.SetEnabled(!Startup.IsEnabled));

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            statusItem, detailItem,
            new ToolStripSeparator(),
            toggleItem,
            new ToolStripMenuItem("設定...", null, (_, _) => ShowSettings()),
            ipMenu,
            new ToolStripSeparator(),
            startupItem,
            new ToolStripMenuItem("ファイアウォールで受信を許可...", null, (_, _) => AllowFirewall()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("終了", null, (_, _) => ExitThread()),
        });
        menu.Opening += (_, _) =>
        {
            RefreshIpMenu();
            startupItem.Checked = Startup.IsEnabled;
            UpdateStatus();
        };
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        tray = new NotifyIcon { ContextMenuStrip = menu, Icon = iconOff, Text = AppName, Visible = true };
        tray.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };

        statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        statusTimer.Tick += (_, _) => UpdateStatus();
        statusTimer.Start();

        // 既定の再生デバイスが変わったら作り直す（連続で通知が来るので少し待つ）
        restartTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        restartTimer.Tick += (_, _) => { restartTimer.Stop(); if (engine != null) Start(); };
        deviceWatcher.Changed += () => ui.Post(_ => { restartTimer.Stop(); restartTimer.Start(); }, null);

        Startup.RefreshPath();
        if (settings.AutoStart || args.Length > 0) Start();
        UpdateStatus();
    }

    void Toggle()
    {
        if (engine == null) Start();
        else Stop();
    }

    void Start()
    {
        Stop();
        try
        {
            engine = new ReceiverEngine(settings.Port, settings.TargetLatencyMs);
        }
        catch (SocketException ex)
        {
            Notify($"UDP {settings.Port} を開けませんでした。\n{ex.Message}", ToolTipIcon.Error);
            UpdateStatus();
            return;
        }
        var started = engine;
        engine.Failed += ex => ui.Post(_ =>
        {
            if (engine != started) return;
            Stop();
            Notify($"受信を停止しました。\n{ex.Message}", ToolTipIcon.Error);
        }, null);
        UpdateStatus();
    }

    void Stop()
    {
        engine?.Dispose();
        engine = null;
        UpdateStatus();
    }

    void UpdateStatus()
    {
        string status, detail = "";
        Icon icon;
        if (engine == null)
        {
            status = "停止中";
            icon = iconOff;
        }
        else if (engine.Source is string src)
        {
            status = $"受信中: {src}";
            detail = $"遅延 {engine.BufferedMs:0}ms / 欠落 {engine.Lost} / 破棄 {engine.Dropped}";
            icon = iconActive;
        }
        else
        {
            status = $"待受中: UDP {engine.Port}";
            icon = iconIdle;
        }

        statusItem.Text = status;
        detailItem.Text = detail;
        detailItem.Visible = detail.Length > 0;
        toggleItem.Text = engine == null ? "受信を開始" : "受信を停止";
        string tip = $"{AppName}\n{status}";
        tray.Text = tip.Length > 127 ? tip[..127] : tip;
        if (tray.Icon != icon) tray.Icon = icon;
    }

    void RefreshIpMenu()
    {
        ipMenu.DropDownItems.Clear();
        var ips = NetInfo.LocalIPv4();
        if (ips.Count == 0)
            ipMenu.DropDownItems.Add(new ToolStripMenuItem("（ネットワーク未接続）") { Enabled = false });
        foreach (var ip in ips)
            ipMenu.DropDownItems.Add(new ToolStripMenuItem($"{ip}（クリックでコピー）", null, (_, _) =>
            {
                Clipboard.SetText(ip);
                Notify($"{ip} をコピーしました", ToolTipIcon.Info);
            }));
    }

    void ShowSettings()
    {
        if (settingsForm != null)
        {
            settingsForm.Activate();
            return;
        }
        using (settingsForm = new SettingsForm(settings, Startup.IsEnabled))
        {
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settings = settingsForm.Result;
                settings.Save();
                Startup.SetEnabled(settingsForm.StartupEnabled);
                if (engine != null) Start();   // 新しい設定で再起動
            }
        }
        settingsForm = null;
    }

    async void AllowFirewall()
    {
        const string rule = "LanAudio Receiver";
        string cmd = $"/c netsh advfirewall firewall delete rule name=\"{rule}\" >nul"
                   + $" & netsh advfirewall firewall add rule name=\"{rule}\" dir=in action=allow protocol=UDP localport={settings.Port}";
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("cmd.exe", cmd)
            {
                UseShellExecute = true,
                Verb = "runas",       // 管理者権限で実行（UAC の確認が出る）
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (proc == null) return;
            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0)
                Notify($"ファイアウォールで UDP {settings.Port} の受信を許可しました", ToolTipIcon.Info);
            else
                Notify("ファイアウォールの設定に失敗しました", ToolTipIcon.Error);
        }
        catch (Win32Exception)
        {
            // UAC でキャンセルされた
        }
    }

    void Notify(string message, ToolTipIcon icon) => tray.ShowBalloonTip(5000, AppName, message, icon);

    static Icon ToIcon(Bitmap bmp)
    {
        using (bmp) return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        statusTimer.Stop();
        restartTimer.Stop();
        deviceWatcher.Dispose();
        engine?.Dispose();
        engine = null;
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
