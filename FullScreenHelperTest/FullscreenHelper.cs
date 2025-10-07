using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace Lyricify_for_Spotify.Helpers.Device
{
    public static class FullscreenHelper
    {
        public static UserNotificationState State()
        {
            _ = SHQueryUserNotificationState(out UserNotificationState state);
            return state;
        }

        /// <summary>
        /// 通过结合用户通知状态和前台窗口的几何尺寸来精确判断当前是否有程序正在全屏运行。
        /// </summary>
        public static bool FullScreen
        {
            get
            {
                // 第一步：快速状态检测
                if (SHQueryUserNotificationState(out var state) != HRESULT.S_OK)
                {
                    return false; // API 调用失败，默认为非全屏
                }

                // 排除明确的非全屏状态
                if (state == UserNotificationState.AcceptsNotifications || state == UserNotificationState.QuietTime)
                {
                    return false;
                }

                // RunningDirect3dFullScreen 是最强的全屏信号，通常可信。
                // Busy 和 PresentationMode 是潜在的全屏信号，需要进一步验证。
                bool isPotentiallyFullScreen = state == UserNotificationState.RunningDirect3dFullScreen ||
                                               state == UserNotificationState.Busy ||
                                               state == UserNotificationState.PresentationMode;

                if (!isPotentiallyFullScreen)
                {
                    return false;
                }

                // 第二步：精确几何验证
                try
                {
                    // 获取前台窗口句柄
                    HWND foregroundHwnd = User32.GetForegroundWindow();
                    if (foregroundHwnd.IsNull)
                    {
                        return false; // 没有前台窗口
                    }

                    // 排除桌面窗口本身，防止壁纸程序等被误判
                    if (foregroundHwnd == User32.GetShellWindow())
                    {
                        return false;
                    }

                    // 获取前台窗口所在的显示器信息
                    HMONITOR hMonitor = User32.MonitorFromWindow(foregroundHwnd, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
                    var monitorInfo = new User32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>() };
                    if (!User32.GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        return false; // 获取显示器信息失败
                    }

                    // 获取前台窗口的矩形区域
                    if (!User32.GetWindowRect(foregroundHwnd, out RECT windowRect))
                    {
                        return false; // 获取窗口矩形失败
                    }

                    // 核心判断：窗口矩形是否与显示器矩形完全相等
                    return windowRect.Equals(monitorInfo.rcMonitor);
                }
                catch
                {
                    // 任何P/Invoke调用出错都默认为非全屏
                    return false;
                }
            }
        }

        // GameMode 的逻辑也可以沿用 FullScreen 的精确判断
        public static bool GameMode => FullScreen;


        [DllImport("shell32.dll")]
        private static extern HRESULT SHQueryUserNotificationState(out UserNotificationState userNotificationState);
    }

    // UserNotificationState 枚举保持不变
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
