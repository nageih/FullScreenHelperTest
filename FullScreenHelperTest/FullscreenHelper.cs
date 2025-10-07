using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;

namespace Lyricify_for_Spotify.Helpers.Device
{
    public static class FullscreenHelper
    {
        // 已知的非全屏窗口类名黑名单
        private static readonly string[] NonFullScreenClasses = 
        {
            "WorkerW",           // 桌面壁纸窗口
            "Shell_TrayWnd",     // 任务栏
            "DV2ControlHost",    // 壁纸引擎
            "NVIDIA Share",      // NVIDIA覆盖层
            "NVIDIA Overlay",    // NVIDIA即时重放
            "ForegroundStaging", // 系统窗口
            "MSCTFIME UI",       // 输入法窗口
            "IME"                // 输入法相关
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
                        && IsValidFullScreenWindow();
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
                    return (state == UserNotificationState.RunningDirect3dFullScreen && IsValidFullScreenWindow())
                        || state == UserNotificationState.Busy
                        || state == UserNotificationState.PresentationMode
                        || IsQunsAppState(state);
                }
                return false;
            }
        }

        private static bool IsQunsAppState(UserNotificationState state)
        {
            if (state == UserNotificationState.QUNS_APP)
            {
                var hwnd = User32.GetForegroundWindow();
                var sb = new StringBuilder(256);
                _ = User32.GetClassName(hwnd, sb, sb.Capacity);
                var className = sb.ToString();

                if (className != "Windows.UI.Core.CoreWindow") return true;
            }
            return false;
        }

        private static bool IsValidFullScreenWindow()
        {
            IntPtr hwnd = User32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            // 检查窗口类名是否在黑名单中
            var className = GetWindowClassName(hwnd);
            if (IsNonFullScreenClass(className)) return false;

            // 检查窗口是否可见
            if (!User32.IsWindowVisible(hwnd)) return false;

            // 检查窗口是否覆盖整个屏幕
            if (!IsWindowFullScreen(hwnd)) return false;

            // 检查窗口是否为真正的应用窗口
            return IsApplicationWindow(hwnd);
        }

        private static bool IsNonFullScreenClass(string className)
        {
            foreach (var blacklisted in NonFullScreenClasses)
            {
                if (className.Contains(blacklisted))
                    return true;
            }
            return false;
        }

        private static string GetWindowClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            User32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool IsWindowFullScreen(IntPtr hwnd)
        {
            User32.GetWindowRect(hwnd, out var rect);
            var screenWidth = User32.GetSystemMetrics(0);
            var screenHeight = User32.GetSystemMetrics(1);

            // 允许1像素的误差
            return Math.Abs(rect.Width - screenWidth) <= 1 && 
                   Math.Abs(rect.Height - screenHeight) <= 1;
        }

        private static bool IsApplicationWindow(IntPtr hwnd)
        {
            // 检查窗口是否具有应用窗口特征
            var style = User32.GetWindowLong(hwnd, -16); // GWL_STYLE
            var exStyle = User32.GetWindowLong(hwnd, -20); // GWL_EXSTYLE

            // 排除工具窗口和子窗口
            return (style & 0x80L) == 0 && // WS_POPUP
                   (style & 0x40000000L) != 0 && // WS_CHILD
                   (exStyle & 0x00000080L) == 0; // WS_EX_TOOLWINDOW
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
