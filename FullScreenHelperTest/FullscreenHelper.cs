using System;
using System.Runtime.InteropServices;
using System.Text;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.DwmApi;

namespace Lyricify_for_Spotify.Helpers.Device
{
    public static class FullscreenHelper
    {
        // 依旧保留：返回系统的通知状态（不用于“是否全屏”的直接判断）
        public static UserNotificationState State()
        {
            _ = SHQueryUserNotificationState(out UserNotificationState state);
            return state;
        }

        // 更严格的 GameMode 判断：仅当系统报告 D3D 独占全屏时返回 true
        public static bool GameMode
        {
            get
            {
                return SHQueryUserNotificationState(out var state) == 0
                    && state == UserNotificationState.RunningDirect3dFullScreen;
            }
        }

        // 推荐使用的“是否全屏”判断：基于前景窗口几何+过滤
        public static bool FullScreen => IsForegroundWindowFullscreen();

        // 可选：提供参数版以调节容差与覆盖率阈值
        public static bool IsForegroundWindowFullscreen(int tolerancePx = 4, double coverageThreshold = 0.995)
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == HWND.NULL)
                return false;

            // 取根窗口，避免子/拥有者窗口影响
            hwnd = GetAncestor(hwnd, GetAncestorFlags.GA_ROOT);
            if (hwnd == HWND.NULL)
                return false;

            // 过滤桌面/Shell/任务栏/壁纸层等特殊窗口
            if (IsShellOrDesktopLike(hwnd))
                return false;

            // 不可见或被 Cloak（例如 UWP 后台）的窗口不算
            if (!IsWindowVisible(hwnd) || IsCloaked(hwnd))
                return false;

            // 工具窗口、不可激活窗口通常不是 Alt-Tab 前台应用
            if (IsToolOrNoActivateWindow(hwnd))
                return false;

            // 最准确的窗口边界使用扩展帧边界（包含无边框/阴影）
            if (!TryGetWindowBounds(hwnd, out var wndRect))
                return false;

            // 当前窗口所在显示器
            var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTO.MONITOR_DEFAULTTONEAREST);
            if (hMon == HMONITOR.NULL)
                return false;

            var mi = new MONITORINFO();
            mi.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMon, ref mi))
                return false;

            var monitorRect = mi.rcMonitor;

            // 判断窗口与显示器的覆盖情况（考虑阴影/舍入误差用容差与覆盖率阈值）
            return CoversMonitor(wndRect, monitorRect, tolerancePx, coverageThreshold);
        }

        private static bool TryGetWindowBounds(HWND hwnd, out RECT rect)
        {
            // 优先使用 DWM 扩展帧边界
            var hr = DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect, Marshal.SizeOf<RECT>());
            if (hr.Succeeded)
                return true;

            // 退化到传统窗口矩形
            return GetWindowRect(hwnd, out rect);
        }

        private static bool IsCloaked(HWND hwnd)
        {
            // 被 Cloak 的窗口（例如切到后台的 UWP）不可见
            if (DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
                out int cloaked, Marshal.SizeOf<int>()).Succeeded)
            {
                return cloaked != 0;
            }
            return false;
        }

        private static bool IsToolOrNoActivateWindow(HWND hwnd)
        {
            var ex = (WindowStylesEx)(nint)GetWindowLongPtr(hwnd, WindowLongIndex.GWL_EXSTYLE);
            if (ex.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW) ||
                ex.HasFlag(WindowStylesEx.WS_EX_NOACTIVATE))
                return true;

            // 有 Owner 且非 AppWindow 的情况一般不是 Alt-Tab 窗口
            var owner = GetWindow(hwnd, GetWindowCmd.GW_OWNER);
            if (owner != HWND.NULL && !ex.HasFlag(WindowStylesEx.WS_EX_APPWINDOW))
                return true;

            return false;
        }

        private static bool IsShellOrDesktopLike(HWND hwnd)
        {
            // Shell 窗口、桌面窗口不算
            if (hwnd == GetShellWindow())
                return true;

            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            var cls = sb.ToString();

            // 常见的壳/任务栏/壁纸层窗口类名
            if (cls == "#32769" ||            // Desktop
                cls == "Progman" ||           // Program Manager
                cls == "WorkerW" ||           // 壁纸承载层
                cls == "Shell_TrayWnd" ||     // 主任务栏
                cls == "Shell_SecondaryTrayWnd") // 副任务栏（多屏）
            {
                return true;
            }

            return false;
        }

        private static bool CoversMonitor(RECT window, RECT monitor, int tolerancePx, double coverageThreshold)
        {
            // 窗口与显示器的交集
            var inter = Intersect(window, monitor);

            // 显示器面积
            var monArea = Area(monitor);
            if (monArea <= 0)
                return false;

            // 覆盖率（允许阈值，比如 99.5%）
            var coverage = (double)Area(inter) / monArea;
            if (coverage < coverageThreshold)
                return false;

            // 位置容差校验：窗口包住显示器区域（或有少量阴影/边缘误差）
            return (inter.left <= monitor.left + tolerancePx) &&
                   (inter.top <= monitor.top + tolerancePx) &&
                   (inter.right >= monitor.right - tolerancePx) &&
                   (inter.bottom >= monitor.bottom - tolerancePx);
        }

        private static RECT Intersect(RECT a, RECT b)
        {
            var left = Math.Max(a.left, b.left);
            var top = Math.Max(a.top, b.top);
            var right = Math.Min(a.right, b.right);
            var bottom = Math.Min(a.bottom, b.bottom);

            if (right <= left || bottom <= top)
                return new RECT(0, 0, 0, 0);

            return new RECT(left, top, right, bottom);
        }

        private static int Area(RECT r)
        {
            var w = Math.Max(0, r.right - r.left);
            var h = Math.Max(0, r.bottom - r.top);
            return w * h;
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
