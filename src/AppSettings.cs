using System.Text.Json;
using LanAudio.Shared;

namespace LanAudio.Receiver;

/// <summary>%APPDATA%\LanAudio\receiver.json に保存する設定</summary>
sealed class AppSettings
{
    public int Port { get; set; } = AudioPacket.DefaultPort;
    public int TargetLatencyMs { get; set; } = 40;
    /// <summary>アプリ起動時に受信を開始する</summary>
    public bool AutoStart { get; set; } = true;

    static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanAudio", "receiver.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
