using Lyricify_for_Spotify.Helpers.Device;

namespace FullScreenHelperTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("开始监测全屏状态...");
            Console.WriteLine("按 Ctrl+C 退出\n");

            UserNotificationState? prevState = null;
            bool? prevFullscreen = null;

            while (true)
            {
                var state = FullscreenHelper.State();
                var isFullscreen = FullscreenHelper.FullScreen;
                var isGameMode = FullscreenHelper.GameMode;

                // 状态改变时输出详细信息
                if (prevState != state || prevFullscreen != isFullscreen)
                {
                    prevState = state;
                    prevFullscreen = isFullscreen;

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Console.WriteLine($"[{timestamp}]");
                    Console.WriteLine($"  系统状态: {Enum.GetName(typeof(UserNotificationState), state)}");
                    Console.WriteLine($"  检测结果: {(isFullscreen ? "全屏" : "非全屏")}");
                    Console.WriteLine($"  游戏模式: {(isGameMode ? "是" : "否")}");
                    
                    // 输出当前前台窗口信息用于调试
                    var hwnd = Vanara.PInvoke.User32.GetForegroundWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        try
                        {
                            Vanara.PInvoke.User32.GetWindowThreadProcessId(hwnd, out uint processId);
                            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                            Console.WriteLine($"  前台程序: {process.ProcessName}");
                        }
                        catch { }
                    }
                    Console.WriteLine();
                }

                Thread.Sleep(500); // 降低 CPU 使用率
            }
        }
    }
}