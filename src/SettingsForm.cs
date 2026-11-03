namespace LanAudio.Receiver;

sealed class SettingsForm : Form
{
    readonly NumericUpDown port = new() { Minimum = 1024, Maximum = 65535, Width = 120 };
    readonly NumericUpDown latency = new() { Minimum = 20, Maximum = 1000, Increment = 10, Width = 120 };
    readonly CheckBox autoStart = new() { Text = "アプリ起動時に受信を開始する", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Windows 起動時にアプリを起動する", AutoSize = true };

    public SettingsForm(AppSettings settings, bool startupEnabled)
    {
        Text = "LanAudio 受信 - 設定";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        port.Value = Math.Clamp(settings.Port, (int)port.Minimum, (int)port.Maximum);
        latency.Value = Math.Clamp(settings.TargetLatencyMs, (int)latency.Minimum, (int)latency.Maximum);
        autoStart.Checked = settings.AutoStart;
        startup.Checked = startupEnabled;

        var ips = NetInfo.LocalIPv4();
        var ipBox = new TextBox
        {
            ReadOnly = true,
            Width = 260,
            Text = ips.Count > 0 ? string.Join(", ", ips) : "（ネットワーク未接続）",
        };

        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        AddRow(table, "受信ポート (UDP)", port);
        AddRow(table, "目標遅延 (ms)", latency);
        AddWide(table, new Label
        {
            Text = "音が途切れる場合は 100〜150ms に増やしてください",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
        });
        AddRow(table, "このPCのIP", ipBox);
        AddWide(table, new Label
        {
            Text = "送信側（ノートPC）にはこの IP アドレスを設定します",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
        });
        AddWide(table, autoStart);
        AddWide(table, startup);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        AddWide(table, buttons);

        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;
        // トレイから開いたときに他のウィンドウの裏に隠れないようにする
        Shown += (_, _) => { TopMost = true; Activate(); TopMost = false; };
    }

    public AppSettings Result => new()
    {
        Port = (int)port.Value,
        TargetLatencyMs = (int)latency.Value,
        AutoStart = autoStart.Checked,
    };

    public bool StartupEnabled => startup.Checked;

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 6) });
        table.Controls.Add(control);
    }

    static void AddWide(TableLayoutPanel table, Control control)
    {
        table.Controls.Add(control);
        table.SetColumnSpan(control, 2);
    }
}
