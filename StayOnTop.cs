using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// StayOnTop: portable Windows 10 always-on-top tray utility.
// Compile with the .NET Framework C# compiler included with Windows.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new StayOnTopContext());
    }
}

internal sealed class StayOnTopContext : ApplicationContext
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x504C;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_Q = 0x51;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x00000008L;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOOWNERZORDER = 0x0200;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);

    private readonly HashSet<IntPtr> pinned = new HashSet<IntPtr>();
    private readonly HotkeyWindow hotkeyWindow;
    private readonly NotifyIcon tray;
    private readonly Icon normalIcon;
    private readonly Icon pinnedIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly Timer cleanupTimer;
    private bool registered;
    private bool disposed;

    public StayOnTopContext()
    {
        normalIcon = MakeIcon(false);
        pinnedIcon = MakeIcon(true);
        hotkeyWindow = new HotkeyWindow(ToggleForeground);
        registered = RegisterHotKey(hotkeyWindow.Handle, HOTKEY_ID, MOD_ALT, VK_Q);

        var menu = new ContextMenuStrip();
        statusItem = new ToolStripMenuItem("0 windows pinned") { Enabled = false };
        menu.Items.Add(statusItem);
        menu.Items.Add("Unpin all", null, delegate { UnpinAll(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        tray = new NotifyIcon
        {
            Icon = normalIcon,
            Text = "StayOnTop - Alt+Q",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += delegate { ShowHelp(); };

        cleanupTimer = new Timer { Interval = 3000 };
        cleanupTimer.Tick += delegate { RefreshStatus(); };
        cleanupTimer.Start();
        if (!registered)
            MessageBox.Show("Could not register Alt+Q. Another application may be using it.\n\nClose that application and restart StayOnTop.", "StayOnTop hotkey unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ShowHelp()
    {
        MessageBox.Show("Alt+Q  -  Toggle Always on Top for the active window\n\nRight-click the tray icon to unpin all windows or exit.\n\nWindows with elevated privileges may require StayOnTop to run elevated too.", "StayOnTop", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ToggleForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == hotkeyWindow.Handle || !IsWindowVisible(hwnd)) return;
        // Resolve child controls to their top-level window.
        var root = GetAncestor(hwnd, 2 /* GA_ROOT */);
        if (root != IntPtr.Zero) hwnd = root;
        if (hwnd == FindWindow("Progman", null) || hwnd == FindWindow("Shell_TrayWnd", null)) return;

        bool isTop = ((uint)GetWindowLong(hwnd, GWL_EXSTYLE) & (uint)WS_EX_TOPMOST) != 0;
        bool makeTop = !isTop;
        bool success = SetWindowPos(hwnd, makeTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        if (!success)
        {
            tray.ShowBalloonTip(2500, "StayOnTop", "Cannot change this window. Try running StayOnTop as administrator if the target is elevated.", ToolTipIcon.Warning);
            return;
        }
        if (makeTop) pinned.Add(hwnd);
        else pinned.Remove(hwnd);
        RefreshStatus();
        string name = WindowTitle(hwnd);
        tray.ShowBalloonTip(1000, makeTop ? "Pinned on top" : "Unpinned", name, ToolTipIcon.Info);
    }

    private static string WindowTitle(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(150);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.Length == 0 ? "Window" : sb.ToString();
    }

    private void RefreshStatus()
    {
        pinned.RemoveWhere(delegate(IntPtr hwnd)
        {
            return !IsWindow(hwnd) || (((uint)GetWindowLong(hwnd, GWL_EXSTYLE) & (uint)WS_EX_TOPMOST) == 0);
        });
        statusItem.Text = pinned.Count + (pinned.Count == 1 ? " window pinned" : " windows pinned");
        tray.Icon = pinned.Count == 0 ? normalIcon : pinnedIcon;
        tray.Text = "StayOnTop - " + pinned.Count + " pinned - Alt+Q";
    }

    private void UnpinAll()
    {
        foreach (IntPtr hwnd in pinned)
        {
            if (IsWindow(hwnd))
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOOWNERZORDER);
        }
        pinned.Clear();
        RefreshStatus();
    }

    protected override void ExitThreadCore()
    {
        if (!disposed)
        {
            disposed = true;
            cleanupTimer.Stop();
            cleanupTimer.Dispose();
            UnpinAll();
            tray.Visible = false;
            tray.ContextMenuStrip.Dispose();
            tray.Dispose();
            if (registered) UnregisterHotKey(hotkeyWindow.Handle, HOTKEY_ID);
            hotkeyWindow.DestroyHandle();
            normalIcon.Dispose();
            pinnedIcon.Dispose();
        }
        base.ExitThreadCore();
    }

    private static Icon MakeIcon(bool active)
    {
        using (var bmp = new Bitmap(32, 32))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var back = new SolidBrush(active ? Color.FromArgb(28, 151, 92) : Color.FromArgb(70, 80, 98)))
                g.FillEllipse(back, 2, 2, 28, 28);
            using (var pen = new Pen(Color.White, 2.5f))
            {
                g.DrawLine(pen, 16, 7, 16, 24);
                g.DrawLine(pen, 10, 14, 22, 14);
                g.DrawLine(pen, 11, 9, 21, 9);
            }
            IntPtr hicon = bmp.GetHicon();
            try { using (var temp = Icon.FromHandle(hicon)) return (Icon)temp.Clone(); }
            finally { DestroyIcon(hicon); }
        }
    }
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private sealed class HotkeyWindow : NativeWindow
    {
        private readonly Action callback;
        public HotkeyWindow(Action callback)
        {
            this.callback = callback;
            CreateHandle(new CreateParams { Caption = "StayOnTop message window" });
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) callback();
            base.WndProc(ref m);
        }
    }
}
