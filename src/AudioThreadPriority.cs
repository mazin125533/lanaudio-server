// 音声処理スレッドを MMCSS（Multimedia Class Scheduler Service）の "Pro Audio" タスクとして登録し、
// 他の処理が重いときでも後回しにされにくくする。両リポジトリで同一ファイル。
using System.Runtime.InteropServices;

namespace LanAudio.Shared;

static class AudioThreadPriority
{
    [ThreadStatic] static bool registered;

    [DllImport("avrt.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

    [DllImport("avrt.dll")]
    static extern bool AvSetMmThreadPriority(IntPtr handle, int priority);

    const int AVRT_PRIORITY_HIGH = 1;

    /// <summary>呼び出したスレッドを優先させる（スレッドごとに1回だけ実行される）</summary>
    public static void EnsureCurrentThread()
    {
        if (registered) return;
        registered = true;
        try
        {
            uint index = 0;
            IntPtr h = AvSetMmThreadCharacteristics("Pro Audio", ref index);
            if (h != IntPtr.Zero) AvSetMmThreadPriority(h, AVRT_PRIORITY_HIGH);
            else Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }
        catch
        {
            // MMCSS が使えない環境では通常優先度のまま動かす
        }
    }
}
