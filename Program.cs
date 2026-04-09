using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SCGuard
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            using var mutex = new System.Threading.Mutex(true, "SCGuard_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("SCGuard is already running in the system tray.",
                    "SCGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SCGuardContext());
        }
    }

    enum GuardState { Protected, SCMode, ManualOverride }

    class SCGuardContext : ApplicationContext
    {
        // need this to free icons properly
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly string[] SC_PROCESS_NAMES = { "StarCitizen", "RSI Launcher" };
        private const int POLL_INTERVAL_MS   = 2000;
        private const int MULLVAD_TIMEOUT_MS = 8000;
        private const string MULLVAD_CLI     = "mullvad";

        private GuardState _state        = GuardState.Protected;
        private bool       _scWasRunning = false;

        private readonly NotifyIcon                      _tray;
        private readonly System.Windows.Forms.Timer      _timer;
        private readonly ToolStripMenuItem               _statusItem;
        private readonly ToolStripMenuItem               _toggleOverride;
        private readonly Icon _iconProtected;
        private readonly Icon _iconSCMode;
        private readonly Icon _iconOverride;

        public SCGuardContext()
        {
            _iconProtected = RenderIcon(IconStyle.Protected);
            _iconSCMode    = RenderIcon(IconStyle.SCMode);
            _iconOverride  = RenderIcon(IconStyle.Override);

            _statusItem     = new ToolStripMenuItem("Initialising...") { Enabled = false };
            _toggleOverride = new ToolStripMenuItem("Force SC Mode (pause VPN)", null, OnToggleOverride);

            var menu = new ContextMenuStrip();
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_toggleOverride);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About SCGuard", null, OnAbout);
            menu.Items.Add("Exit", null, OnExit);

            _tray = new NotifyIcon
            {
                Icon             = _iconProtected,
                Text             = "SCGuard - Protected",
                Visible          = true,
                ContextMenuStrip = menu
            };
            _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) FlashStatus(); };

            _timer = new System.Windows.Forms.Timer { Interval = POLL_INTERVAL_MS };
            _timer.Tick += OnTick;
            _timer.Start();

            ApplyState(GuardState.Protected, silent: true);
            OnTick(null, EventArgs.Empty);
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_state == GuardState.ManualOverride) return;

            // dispose these or we leak handles every 2 seconds lol
            bool scRunning = IsAnyProcessRunning(SC_PROCESS_NAMES);

            if (scRunning && !_scWasRunning)       { _scWasRunning = true;  ApplyState(GuardState.SCMode); }
            else if (!scRunning && _scWasRunning)   { _scWasRunning = false; ApplyState(GuardState.Protected); }
        }

        private static bool IsAnyProcessRunning(string[] names)
        {
            foreach (var name in names)
            {
                Process[] procs = Process.GetProcessesByName(name);
                bool found = procs.Length > 0;
                foreach (var p in procs) p.Dispose();
                if (found) return true;
            }
            return false;
        }

        private void ApplyState(GuardState newState, bool silent = false)
        {
            _state = newState;
            switch (newState)
            {
                case GuardState.Protected:
                    RunMullvad("connect");
                    _tray.Icon = _iconProtected;
                    _tray.Text = "SCGuard - Protected";
                    _statusItem.Text = "Protected  -  Mullvad active";
                    _toggleOverride.Text = "Force SC Mode (pause VPN)";
                    if (!silent) Notify("Mullvad reconnected", "RSI Launcher and Star Citizen both closed. VPN restored.", ToolTipIcon.Info);
                    break;

                case GuardState.SCMode:
                    RunMullvad("disconnect");
                    _tray.Icon = _iconSCMode;
                    _tray.Text = "SCGuard - SC Mode (VPN paused)";
                    _statusItem.Text = "SC Mode  -  Mullvad paused";
                    _toggleOverride.Text = "Force Normal Mode (restore VPN)";
                    if (!silent) Notify("Star Citizen detected", "Mullvad paused. Triggers: StarCitizen.exe or RSI Launcher.", ToolTipIcon.Info);
                    break;
            }
        }

        private void RunMullvad(string args)
        {
            try
            {
                var psi = new ProcessStartInfo(MULLVAD_CLI, args)
                {
                    CreateNoWindow = true, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(MULLVAD_TIMEOUT_MS);
            }
            catch (Exception ex)
            {
                Notify("SCGuard Error", $"Could not run 'mullvad {args}': {ex.Message}\n\nMake sure Mullvad is installed.", ToolTipIcon.Error);
            }
        }

        private void OnToggleOverride(object? s, EventArgs e)
        {
            if (_state != GuardState.ManualOverride)
            {
                _state = GuardState.ManualOverride;
                RunMullvad("disconnect");
                _tray.Icon = _iconOverride;
                _tray.Text = "SCGuard - Manual Override";
                _statusItem.Text = "Manual Override  -  VPN paused";
                _toggleOverride.Text = "Release Override (resume automation)";
                Notify("Manual Override", "VPN paused. Auto-detection suspended.", ToolTipIcon.Warning);
            }
            else
            {
                _scWasRunning = false;
                ApplyState(GuardState.Protected);
                OnTick(null, EventArgs.Empty);
            }
        }

        private void OnAbout(object? s, EventArgs e)
        {
            MessageBox.Show(
                "SCGuard v1.0\n" +
                "github.com/ALEKSIS-SENPAI/SCGuard\n\n" +
                "Auto-pauses Mullvad VPN when Star Citizen or the\n" +
                "RSI Launcher is running. Reconnects when both are closed.\n" +
                "Other EAC games are ignored.\n\n" +
                "Watched processes: StarCitizen.exe, RSI Launcher.exe\n\n" +
                "Tray icons:\n" +
                "  Green (VP)  =  Mullvad active\n" +
                "  Amber (SC)  =  SC detected, Mullvad paused\n" +
                "  Blue  (MO)  =  Manual override\n\n" +
                "Calls: mullvad connect / mullvad disconnect\n" +
                "Polls every 2s. Restores VPN on exit.\n\n" +
                "Made by ALEKSIS-SENPAI\n" +
                "(Always down to play SC)",
                "About SCGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExit(object? s, EventArgs e)
        {
            _timer.Stop();
            if (_state != GuardState.Protected) RunMullvad("connect");
            _tray.Visible = false;
            Application.Exit();
        }

        private void FlashStatus()
        {
            string msg = _state switch
            {
                GuardState.Protected    => "Protected - Mullvad is active.\nStar Citizen and RSI Launcher are not running.",
                GuardState.SCMode       => "SC Mode - Mullvad is paused.\nStar Citizen or RSI Launcher is running.",
                GuardState.ManualOverride => "Manual Override - VPN paused.\nAuto-detection suspended.",
                _ => "Unknown state"
            };
            Notify("SCGuard Status", msg, ToolTipIcon.None);
        }

        private void Notify(string title, string body, ToolTipIcon icon)
        {
            _tray.BalloonTipTitle = title;
            _tray.BalloonTipText  = body;
            _tray.BalloonTipIcon  = icon;
            _tray.ShowBalloonTip(4000);
        }

        private enum IconStyle { Protected, SCMode, Override }

        // icon from bitmap without leaking the handle
        private static Icon RenderIcon(IconStyle style)
        {
            const int size = 32;
            using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            Color bg = style switch {
                IconStyle.Protected => Color.FromArgb(20, 200, 90),
                IconStyle.SCMode    => Color.FromArgb(240, 160, 0),
                IconStyle.Override  => Color.FromArgb(30, 130, 240),
                _                   => Color.Gray
            };

            using (var brush = new SolidBrush(bg))
                g.FillEllipse(brush, 1, 1, size - 2, size - 2);
            using (var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1.5f))
                g.DrawEllipse(pen, 1, 1, size - 2, size - 2);

            string label = style switch {
                IconStyle.Protected => "VP",
                IconStyle.SCMode    => "SC",
                IconStyle.Override  => "MO",
                _                   => "??"
            };

            using var font   = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var sf     = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
            using var white  = new SolidBrush(Color.White);
            g.DrawString(label, font, shadow, new RectangleF(1, 2, size, size), sf);
            g.DrawString(label, font, white,  new RectangleF(0, 1, size, size), sf);

            // clone it before destroying the handle or it goes poof
            IntPtr hIcon = bmp.GetHicon();
            using var tmp = Icon.FromHandle(hIcon);
            var result = (Icon)tmp.Clone();
            DestroyIcon(hIcon);
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tray.Dispose(); _timer.Dispose();
                _iconProtected.Dispose(); _iconSCMode.Dispose(); _iconOverride.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
