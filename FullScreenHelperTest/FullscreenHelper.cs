using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

namespace Lyricify_for_Spotify.Helpers.Device
{
    public static class FullscreenHelper
    {
        // 已知的非全屏应用黑名单
        private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "wallpaper32",
            "wallpaper64",
            "ui32",
            "ui64",
            "nvcontainer",
            "nvidia share",
            "nvidia geforce experience"
        };

        // 已知的非全屏窗口类名黑名单
        private static readonly HashSet<string> ExcludedClassNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "WorkerW",           // Windows 桌面工作窗口
            "Progman",           // 程序管理器（桌面）
            "CEF-OSC-WIDGET"     // Wallpaper Engine 的 CEF 窗口
        };

        public static UserNotificationState State()
        {
            _ = SHQueryUserNotificationState(out UserNotificationState state);
            return state;
        }

        public static bool GameMode
        {
            get
            {
                if (SHQueryUserNotificationState(out var state) == 0)
                {
                    return state == UserNotificationState.RunningDirect3dFullScreen
                        || IsQunsAppState(state);
                }
                return false;
            }
        }

        public static bool FullScreen
        {
            get
            {
                if (SHQueryUserNotificationState(out var state) == 0)
                {
                    // 如果状态表明可能有全屏应用
                    if (state == UserNotificationState.Busy
                        || state == UserNotificationState.PresentationMode
                        || state == UserNotificationState.RunningDirect3dFullScreen
                        || IsQunsAppState(state))
                    {
                        // 进一步验证前台窗口是否真的是全屏
                        return IsActualFullscreenWindow();
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 检测前台窗口是否真的是全屏窗口
        /// </summary>
        private static bool IsActualFullscreenWindow()
        {
            try
            {
                var hwnd = User32.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return false;

                // 检查窗口类名是否在黑名单中
                var className = GetClassName(hwnd);
                if (ExcludedClassNames.Contains(className))
                    return false;

                // 检查进程名是否在黑名单中
                var processName = GetProcessName(hwnd);
                if (ExcludedProcessNames.Contains(processName))
                    return false;

                // 检查窗口样式
                var style = User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_STYLE);
                var exStyle = User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE);

                // 排除工具窗口
                if ((exStyle & (int)User32.WindowStylesEx.WS_EX_TOOLWINDOW) != 0)
                    return false;

                // 排除透明窗口（如 Wallpaper Engine）
                if ((exStyle & (int)User32.WindowStylesEx.WS_EX_LAYERED) != 0 &&
                    (exStyle & (int)User32.WindowStylesEx.WS_EX_TRANSPARENT) != 0)
                    return false;

                // 检查窗口是否最大化或覆盖整个屏幕
                if (!User32.GetWindowRect(hwnd, out var rect))
                    return false;

                // 获取窗口所在显示器的工作区
                var monitor = User32.MonitorFromWindow(hwnd, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new User32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>() };
                
                if (!User32.GetMonitorInfo(monitor, ref monitorInfo))
                    return false;

                var screenRect = monitorInfo.rcMonitor;
                
                // 窗口必须覆盖整个屏幕（允许小误差）
                const int tolerance = 5;
                bool coversScreen = 
                    Math.Abs(rect.left - screenRect.left) <= tolerance &&
                    Math.Abs(rect.top - screenRect.top) <= tolerance &&
                    Math.Abs(rect.right - screenRect.right) <= tolerance &&
                    Math.Abs(rect.bottom - screenRect.bottom) <= tolerance;

                if (!coversScreen)
                    return false;

                // 窗口必须没有标题栏和边框（真正的全屏）
                bool hasNoBorder = (style & (int)User32.WindowStyles.WS_CAPTION) == 0 &&
                                   (style & (int)User32.WindowStyles.WS_THICKFRAME) == 0;

                return hasNoBorder;
            }
            catch
            {
                // 如果检测出错，保守起见返回 false
                return false;
            }
        }

        private static bool IsQunsAppState(UserNotificationState state)
        {
            if (state == UserNotificationState.QUNS_APP)
            {
                var hwnd = User32.GetForegroundWindow();
                var className = GetClassName(hwnd);

                // Windows Store 应用但不在黑名单中
                if (className == "Windows.UI.Core.CoreWindow" || 
                    className == "ApplicationFrameWindow")
                {
                    return !IsExcludedApp(hwnd);
                }
                
                return true;
            }
            return false;
        }

        private static bool IsExcludedApp(IntPtr hwnd)
        {
            var processName = GetProcessName(hwnd);
            return ExcludedProcessNames.Contains(processName);
        }

        private static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            _ = User32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetProcessName(IntPtr hwnd)
        {
            try
            {
                _ = User32.GetWindowThreadProcessId(hwnd, out uint processId);
                using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out UserNotificationState userNotificationState);
    }

    public enum UserNotificationState
    {
        /// <summary>
        /// A screen saver is displayed, the machine is locked,
        /// or a nonactive Fast User Switching session is in progress.
        /// </summary>
        NotPresent = 1,

        /// <summary>
        /// A full-screen application is running or Presentation Settings are applied.
        /// Presentation Settings allow a user to put their machine into a state fit
        /// for an uninterrupted presentation, such as a set of PowerPoint slides, with a single click.
        /// </summary>
        Busy = 2,

        /// <summary>
        /// A full-screen (exclusive mode) Direct3D application is running.
        /// </summary>
        RunningDirect3dFullScreen = 3,

        /// <summary>
        /// The user has activated Windows presentation settings to block notifications and pop-up messages.
        /// </summary>
        PresentationMode = 4,

        /// <summary>
        /// None of the other states are found, notifications can be freely sent.
        /// </summary>
        AcceptsNotifications = 5,

        /// <summary>
        /// Introduced in Windows 7. The current user is in "quiet time", which is the first hour after
        /// a new user logs into his or her account for the first time. During this time, most notifications
        /// should not be sent or shown. This lets a user become accustomed to a new computer system
        /// without those distractions.
        /// Quiet time also occurs for each user after an operating system upgrade or clean installation.
        /// </summary>
        QuietTime = 6,

        /// <summary>
        /// Introduced in Windows 8. A Windows Store app is running.
        /// </summary>
        QUNS_APP = 7
    }
}