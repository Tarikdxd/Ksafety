using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace KSafetyApp
{
    public class LockedApp
    {
        public string DisplayName { get; set; }
        public string ExePath { get; set; }
        public string ProcessName { get; set; }
        public BindingList<TimeInterval> Intervals { get; set; }
        public LockedApp() { Intervals = new BindingList<TimeInterval>(); }
    }

    public class TimeInterval
    {
        public string Start { get; set; }
        public string End { get; set; }
        public override string ToString() { return Start + " - " + End; }
    }

    public class AppConfig
    {
        public string PinSalt { get; set; }
        public string PinHash { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AskPinOnEveryFocus { get; set; }
        public bool IdleLockEnabled { get; set; }
        public bool ProtectionEnabled { get; set; }
        public int IdleLockMinutes { get; set; }
        public BindingList<LockedApp> Apps { get; set; }
        public AppConfig()
        {
            Apps = new BindingList<LockedApp>();
            IdleLockMinutes = 5;
            IdleLockEnabled = true;
            ProtectionEnabled = true;
        }
    }

    static class ConfigStore
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KSafety");
        public static readonly string FilePath = Path.Combine(Dir, "config.xml");
        public static readonly string OldFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SudeAppPinLock", "config.xml");
        public static AppConfig Load()
        {
            try
            {
                string loadPath = File.Exists(FilePath) ? FilePath : OldFilePath;
                if (!File.Exists(loadPath)) return new AppConfig();
                using (FileStream fs = new FileStream(loadPath, FileMode.Open, FileAccess.Read))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(AppConfig));
                    AppConfig cfg = (AppConfig)xs.Deserialize(fs);
                    if (cfg.Apps == null) cfg.Apps = new BindingList<LockedApp>();
                    if (cfg.IdleLockMinutes <= 0) cfg.IdleLockMinutes = 5;
                    return cfg;
                }
            }
            catch { return new AppConfig(); }
        }
        public static void Save(AppConfig cfg)
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            using (FileStream fs = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
            {
                new XmlSerializer(typeof(AppConfig)).Serialize(fs, cfg);
            }
        }
    }

    static class PinService
    {
        public static string NewSalt()
        {
            byte[] b = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }
        public static string Hash(string pin, string salt)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes((pin ?? "") + ":" + salt)));
        }
        public static bool Verify(string pin, AppConfig cfg)
        {
            return !String.IsNullOrEmpty(cfg.PinSalt) && !String.IsNullOrEmpty(cfg.PinHash) && Hash(pin, cfg.PinSalt) == cfg.PinHash;
        }
    }

    static class Native
    {
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        public const int SW_MINIMIZE = 6;
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class RoundPanel : Panel
    {
        public int Radius = 28;
        public Color BorderColor = Color.FromArgb(32, 38, 50);
        public RoundPanel() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = RoundedRect(r, Radius))
            using (SolidBrush b = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(b, p);
                e.Graphics.DrawPath(pen, p);
            }
        }
        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            using (GraphicsPath p = RoundedRect(new Rectangle(0,0,Width,Height), Radius)) Region = new Region(p);
        }
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class SoftButton : Button
    {
        public int Radius = 16;
        public Color HoverColor = Color.FromArgb(104, 91, 170);
        public Color PressedColor = Color.FromArgb(65, 55, 115);
        bool hover;
        bool pressed;
        public SoftButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            TabStop = false;
            DoubleBuffered = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { pressed = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.Clear(Parent == null ? Color.Transparent : Parent.BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color start = pressed ? PressedColor : (hover ? HoverColor : BackColor);
            Color end = pressed ? Color.FromArgb(48, 56, 96) : (hover ? Color.FromArgb(88, 112, 180) : Color.FromArgb(61, 78, 130));
            using (GraphicsPath p = RoundPanel.RoundedRect(r, Radius))
            using (LinearGradientBrush b = new LinearGradientBrush(r, start, end, 18f))
            using (Pen border = new Pen(Color.FromArgb(98, 112, 150)))
            {
                pevent.Graphics.FillPath(b, p);
                pevent.Graphics.DrawPath(border, p);
            }
            TextRenderer.DrawText(pevent.Graphics, Text, Font, r, Enabled ? ForeColor : Color.FromArgb(120,130,145), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 0 && Height > 0)
            {
                using (GraphicsPath p = RoundPanel.RoundedRect(new Rectangle(0,0,Width,Height), Radius)) Region = new Region(p);
            }
            Invalidate();
        }
    }

    public class LogoBox : Control
    {
        public LogoBox() { DoubleBuffered = true; Width = 54; Height = 54; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(4, 3, Width - 8, Height - 8);
            using (System.Drawing.Drawing2D.LinearGradientBrush br = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(124, 58, 237), Color.FromArgb(34, 211, 238), 45f))
                e.Graphics.FillEllipse(br, r);
            using (Font f = new Font("Segoe UI", 20, FontStyle.Bold))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (Brush wb = new SolidBrush(Color.White))
                e.Graphics.DrawString("K", f, wb, r, sf);
        }
    }

    public class PinDialog : Form
    {
        TextBox pinBox;
        public string Pin { get { return pinBox.Text; } }
        public PinDialog(string title, string message)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; TopMost = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            BackColor = Color.FromArgb(17, 24, 39);
            ForeColor = Color.FromArgb(226, 232, 240);
            ClientSize = new Size(410, 188);

            Label lbl = new Label();
            lbl.Text = message; lbl.Left = 22; lbl.Top = 20; lbl.Width = 365; lbl.Height = 52;
            lbl.ForeColor = Color.FromArgb(226, 232, 240);
            lbl.Font = new Font("Segoe UI", 10);
            Controls.Add(lbl);

            pinBox = new TextBox();
            pinBox.Left = 22; pinBox.Top = 82; pinBox.Width = 365; pinBox.Height = 30;
            pinBox.BackColor = Color.FromArgb(30, 41, 59); pinBox.ForeColor = Color.White; pinBox.BorderStyle = BorderStyle.FixedSingle;
            pinBox.Font = new Font("Segoe UI", 11); pinBox.PasswordChar = '*'; pinBox.MaxLength = 30;
            Controls.Add(pinBox);

            Button ok = ModernButton("Kilidi aç", 202, 134, 90); ok.DialogResult = DialogResult.OK; Controls.Add(ok);
            Button cancel = ModernButton("Vazgeç", 302, 134, 85); cancel.DialogResult = DialogResult.Cancel; cancel.BackColor = Color.FromArgb(51,65,85); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }
        Button ModernButton(string text, int x, int y, int w)
        {
            Button b = new Button(); b.Text = text; b.Left = x; b.Top = y; b.Width = w; b.Height = 34;
            b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(124, 58, 237); b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9, FontStyle.Bold); b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }

    public class ChangePinDialog : Form
    {
        TextBox oldPin, newPin, newPin2; AppConfig cfg; public bool Changed;
        public ChangePinDialog(AppConfig config, bool firstSetup)
        {
            cfg = config; Text = firstSetup ? "İlk PIN ayarı" : "PIN değiştir";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); BackColor = Color.FromArgb(17,24,39); ForeColor = Color.FromArgb(226,232,240);
            ClientSize = new Size(380, firstSetup ? 198 : 238);
            int y = 20;
            if (!firstSetup) { Controls.Add(MakeLabel("Mevcut PIN", y)); oldPin = MakeBox(y); Controls.Add(oldPin); y += 42; }
            Controls.Add(MakeLabel("Yeni PIN", y)); newPin = MakeBox(y); Controls.Add(newPin); y += 42;
            Controls.Add(MakeLabel("Yeni PIN tekrar", y)); newPin2 = MakeBox(y); Controls.Add(newPin2);
            Button ok = Btn("Kaydet", 190, ClientSize.Height - 48); ok.Click += delegate { Save(firstSetup); }; Controls.Add(ok);
            Button cancel = Btn("Vazgeç", 280, ClientSize.Height - 48); cancel.BackColor = Color.FromArgb(100,116,139); cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); }; Controls.Add(cancel);
        }
        Label MakeLabel(string t, int y) { Label l = new Label(); l.Text = t; l.Left = 18; l.Top = y+4; l.Width = 125; l.Font = new Font("Segoe UI", 9); l.ForeColor = Color.FromArgb(203,213,225); return l; }
        TextBox MakeBox(int y) { TextBox b = new TextBox(); b.Left = 150; b.Top = y; b.Width = 205; b.PasswordChar = '*'; b.Font = new Font("Segoe UI", 10); b.BackColor = Color.FromArgb(30,41,59); b.ForeColor = Color.White; b.BorderStyle = BorderStyle.FixedSingle; return b; }
        Button Btn(string t, int x, int y) { Button b = new Button(); b.Text=t; b.Left=x; b.Top=y; b.Width=75; b.Height=32; b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=0; b.BackColor=Color.FromArgb(124,58,237); b.ForeColor=Color.White; return b; }
        void Save(bool firstSetup)
        {
            if (!firstSetup && !PinService.Verify(oldPin.Text, cfg)) { MessageBox.Show("Mevcut PIN yanlış."); return; }
            if (newPin.Text.Length < 4) { MessageBox.Show("PIN en az 4 karakter olmalı."); return; }
            if (newPin.Text != newPin2.Text) { MessageBox.Show("Yeni PIN tekrarı aynı değil."); return; }
            cfg.PinSalt = PinService.NewSalt(); cfg.PinHash = PinService.Hash(newPin.Text, cfg.PinSalt);
            Changed = true; DialogResult = DialogResult.OK; Close();
        }
    }

    public class MainForm : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        AppConfig cfg;
        ListBox appList, intervalList;
        TextBox startBox, endBox, idleBox;
        Timer monitor;
        Dictionary<string, DateTime> allowedUntil = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DateTime> lastActive = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, bool> wasForeground = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> idleLocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> prompting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CheckBox autostartBox, focusAskBox, idleLockBox, protectionBox;
        Label statusLabel;
        NotifyIcon tray;
        DateTime pauseUntil = DateTime.MinValue;
        bool suppressProtectionEvent;
        Color bg = Color.FromArgb(6, 10, 18), card = Color.FromArgb(12, 17, 27), card2 = Color.FromArgb(17, 24, 37), blue = Color.FromArgb(88, 72, 140), cyan = Color.FromArgb(125, 211, 252), text = Color.FromArgb(238, 243, 248), muted = Color.FromArgb(133, 147, 166);

        public MainForm()
        {
            cfg = ConfigStore.Load();
            BuildUi(); RefreshApps(); SetupTray();
            Shown += delegate { EnsurePin(); };
            monitor = new Timer(); monitor.Interval = 800; monitor.Tick += delegate { MonitorApps(); }; monitor.Start();
        }

        void BuildUi()
        {
            Text = "KSafety"; StartPosition = FormStartPosition.CenterScreen; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ClientSize = new Size(1040, 680); MinimumSize = new Size(1040, 680); BackColor = bg; Font = new Font("Segoe UI", 9);

            Panel header = Card(24, 20, 992, 112); header.BackColor = Color.FromArgb(10, 15, 25); Controls.Add(header);
            LogoBox logo = new LogoBox(); logo.Left = 22; logo.Top = 24; header.Controls.Add(logo);
            Label title = new Label(); title.Text = "KSafety"; title.ForeColor = Color.White; title.Font = new Font("Segoe UI", 22, FontStyle.Bold); title.Left = 88; title.Top = 20; title.Width = 260; title.Height = 38; header.Controls.Add(title);
            Label sub = new Label(); sub.Text = "Gizliliğiniz her şeyden önce gelir."; sub.ForeColor = muted; sub.Font = new Font("Segoe UI", 10); sub.Left = 91; sub.Top = 62; sub.Width = 610; header.Controls.Add(sub);
            statusLabel = new Label(); statusLabel.Left = 745; statusLabel.Top = 24; statusLabel.Width = 205; statusLabel.Height = 30; statusLabel.TextAlign = ContentAlignment.MiddleCenter; statusLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold); header.Controls.Add(statusLabel);
            header.Controls.Add(Button("15 dk duraklat", 745, 64, 132, delegate { PauseProtection(); }));
            header.Controls.Add(Button("Klasör", 887, 64, 75, delegate { OpenConfigFolder(); }));

            Panel apps = Card(24, 150, 476, 360); Controls.Add(apps);
            apps.Controls.Add(CardTitle("Korunan uygulamalar", 18, 16));
            apps.Controls.Add(SmallText("PIN kuralı uygulanacak .exe dosyalarını buradan yönet.", 18, 43, 360));
            appList = new ListBox(); appList.Left = 22; appList.Top = 80; appList.Width = 432; appList.Height = 210; appList.BorderStyle = BorderStyle.None; appList.Font = new Font("Segoe UI", 10); appList.BackColor = Color.FromArgb(14, 21, 34); appList.ForeColor = text; appList.SelectedIndexChanged += delegate { RefreshIntervals(); }; apps.Controls.Add(appList);
            apps.Controls.Add(Button("Uygulama ekle", 22, 308, 140, delegate { AddApp(); }));
            apps.Controls.Add(Button("Sil", 174, 308, 74, delegate { RemoveApp(); }));
            apps.Controls.Add(Button("Konumu aç", 260, 308, 118, delegate { OpenLocation(); }));

            Panel times = Card(524, 150, 492, 360); Controls.Add(times);
            times.Controls.Add(CardTitle("Zaman kuralları", 18, 16));
            times.Controls.Add(SmallText("Geceyi aşan aralıklar desteklenir. Örnek: 22:00 - 07:00", 18, 43, 420));
            intervalList = new ListBox(); intervalList.Left = 22; intervalList.Top = 80; intervalList.Width = 448; intervalList.Height = 184; intervalList.BorderStyle = BorderStyle.None; intervalList.Font = new Font("Segoe UI", 10); intervalList.BackColor = Color.FromArgb(14, 21, 34); intervalList.ForeColor = text; times.Controls.Add(intervalList);
            times.Controls.Add(Label("Başlangıç", 22, 284, 75)); startBox = Box("09:00", 100, 280, 82); times.Controls.Add(startBox);
            times.Controls.Add(Label("Bitiş", 198, 284, 40)); endBox = Box("17:00", 240, 280, 82); times.Controls.Add(endBox);
            times.Controls.Add(Button("Saat ekle", 334, 277, 100, delegate { AddInterval(); }));
            times.Controls.Add(Button("Sil", 442, 277, 48, delegate { RemoveInterval(); }));

            Panel settings = Card(24, 530, 992, 126); Controls.Add(settings);
            settings.Controls.Add(CardTitle("Hızlı ayarlar", 18, 14));
            protectionBox = Check("Çalışıyor", 22, 50, cfg.ProtectionEnabled); protectionBox.CheckedChanged += delegate { ToggleProtectionFromUi(); }; settings.Controls.Add(protectionBox);
            autostartBox = Check("Windows ile başlat", 172, 50, cfg.StartWithWindows); autostartBox.CheckedChanged += delegate { SetAutostart(autostartBox.Checked); }; settings.Controls.Add(autostartBox);
            focusAskBox = Check("Her geri dönüşte PIN sor", 362, 50, cfg.AskPinOnEveryFocus); focusAskBox.CheckedChanged += delegate { cfg.AskPinOnEveryFocus = focusAskBox.Checked; SaveConfig(); }; settings.Controls.Add(focusAskBox);
            idleLockBox = Check("Uzak kalınca kilitle", 590, 50, cfg.IdleLockEnabled); idleLockBox.Width = 160; idleLockBox.CheckedChanged += delegate { cfg.IdleLockEnabled = idleLockBox.Checked; SaveConfig(); }; settings.Controls.Add(idleLockBox);
            settings.Controls.Add(Label("Süre", 760, 53, 35)); idleBox = Box(cfg.IdleLockMinutes.ToString(), 800, 49, 48); settings.Controls.Add(idleBox); settings.Controls.Add(Label("dk", 856, 53, 25));
            settings.Controls.Add(Button("PIN değiştir", 760, 82, 110, delegate { ChangePin(false); }));
            settings.Controls.Add(Button("Kaydet", 882, 82, 86, delegate { SaveSettingsFromUi(); }));
            Label maker = new Label(); maker.Text = "Kanser INC."; maker.Left = 22; maker.Top = 92; maker.Width = 260; maker.ForeColor = Color.FromArgb(118, 132, 154); maker.Font = new Font("Segoe UI", 8); settings.Controls.Add(maker);
            UpdateStatus();
        }

        Panel Card(int x, int y, int w, int h) { RoundPanel p = new RoundPanel(); p.Left=x; p.Top=y; p.Width=w; p.Height=h; p.BackColor=card; p.BorderStyle=BorderStyle.None; p.BorderColor=Color.FromArgb(28, 36, 50); return p; }
        Label CardTitle(string t, int x, int y) { Label l = new Label(); l.Text=t; l.Left=x; l.Top=y; l.Width=350; l.Height=25; l.ForeColor=text; l.Font=new Font("Segoe UI", 12, FontStyle.Bold); return l; }
        Label SmallText(string t, int x, int y, int w) { Label l = new Label(); l.Text=t; l.Left=x; l.Top=y; l.Width=w; l.Height=22; l.ForeColor=muted; l.Font=new Font("Segoe UI", 9); return l; }
        Label Label(string t, int x, int y, int w) { Label l = new Label(); l.Text=t; l.Left=x; l.Top=y; l.Width=w; l.ForeColor=muted; return l; }
        TextBox Box(string t, int x, int y, int w) { TextBox b = new TextBox(); b.Text=t; b.Left=x; b.Top=y; b.Width=w; b.Font=new Font("Segoe UI", 10); b.BackColor=Color.FromArgb(14,21,34); b.ForeColor=Color.White; b.BorderStyle=BorderStyle.FixedSingle; return b; }
        CheckBox Check(string t, int x, int y, bool c) { CheckBox cb=new CheckBox(); cb.Text=t; cb.Left=x; cb.Top=y; cb.Width=210; cb.Height=24; cb.Checked=c; cb.ForeColor=text; cb.BackColor=card; cb.FlatStyle=FlatStyle.Flat; return cb; }
        Button Button(string t, int x, int y, int w, EventHandler click) { SoftButton b=new SoftButton(); b.Text=t; b.Left=x; b.Top=y; b.Width=w; b.Height=36; b.BackColor=blue; b.HoverColor=Color.FromArgb(104, 91, 170); b.ForeColor=Color.White; b.Font=new Font("Segoe UI", 9, FontStyle.Bold); b.Click += click; return b; }

        void SetupTray()
        {
            tray = new NotifyIcon(); tray.Text = "KSafety"; tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); tray.Visible = true;
            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Aç", delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.MenuItems.Add("Çık", delegate { SecureExit(); });
            tray.ContextMenu = menu; tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (WindowState == FormWindowState.Minimized) Hide(); }
        protected override void OnFormClosing(FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; } if (tray != null) tray.Visible = false; base.OnFormClosing(e); }
        bool ConfirmPin(string message)
        {
            if (String.IsNullOrEmpty(cfg.PinHash)) return true;
            PinDialog d = new PinDialog("KSafety doğrulama", message);
            return d.ShowDialog(this) == DialogResult.OK && PinService.Verify(d.Pin, cfg);
        }
        void SecureExit()
        {
            Show(); WindowState = FormWindowState.Normal; Activate();
            if (!ConfirmPin("KSafety'den çıkmak için PIN gir.")) { MessageBox.Show("PIN yanlış. KSafety çalışmaya devam edecek."); return; }
            if (tray != null) tray.Visible = false;
            Application.Exit();
        }

        void EnsurePin() { if (String.IsNullOrEmpty(cfg.PinHash)) ChangePin(true); }
        void ChangePin(bool first) { ChangePinDialog d = new ChangePinDialog(cfg, first); if (d.ShowDialog(this) == DialogResult.OK && d.Changed) SaveConfig(); }
        void ToggleProtectionFromUi()
        {
            if (suppressProtectionEvent) return;
            if (!SetProtectionState(protectionBox.Checked, true))
            {
                suppressProtectionEvent = true;
                protectionBox.Checked = cfg.ProtectionEnabled;
                suppressProtectionEvent = false;
            }
        }
        bool SetProtectionState(bool enabled, bool requirePinForDisable)
        {
            if (!enabled && cfg.ProtectionEnabled && requirePinForDisable)
            {
                if (!ConfirmPin("Korumayı kapatmak için PIN gir."))
                {
                    MessageBox.Show("PIN yanlış. Koruma kapatılmadı.");
                    return false;
                }
            }
            cfg.ProtectionEnabled = enabled;
            if (protectionBox != null && protectionBox.Checked != enabled)
            {
                suppressProtectionEvent = true;
                protectionBox.Checked = enabled;
                suppressProtectionEvent = false;
            }
            SaveConfig(); UpdateStatus();
            return true;
        }
        void SaveSettingsFromUi()
        {
            int m; if (!Int32.TryParse(idleBox.Text.Trim(), out m) || m < 1 || m > 1440) { MessageBox.Show("Süre 1 ile 1440 dakika arasında olmalı."); return; }
            if (protectionBox.Checked != cfg.ProtectionEnabled && !SetProtectionState(protectionBox.Checked, true)) return;
            cfg.IdleLockMinutes = m; cfg.IdleLockEnabled = idleLockBox.Checked; cfg.AskPinOnEveryFocus = focusAskBox.Checked; cfg.StartWithWindows = autostartBox.Checked; SaveConfig(); UpdateStatus(); MessageBox.Show("Ayarlar kaydedildi.");
        }
        void SaveConfig() { ConfigStore.Save(cfg); }
        void PauseProtection() { if (!ConfirmPin("Korumayı 15 dakika duraklatmak için PIN gir.")) { MessageBox.Show("PIN yanlış. Duraklatılmadı."); return; } pauseUntil = DateTime.Now.AddMinutes(15); UpdateStatus(); }
        void OpenConfigFolder() { if (!Directory.Exists(ConfigStore.Dir)) Directory.CreateDirectory(ConfigStore.Dir); Process.Start("explorer.exe", ConfigStore.Dir); }
        void UpdateStatus()
        {
            if (statusLabel == null) return;
            if (!cfg.ProtectionEnabled) { statusLabel.Text = "Koruma kapalı"; statusLabel.BackColor = Color.FromArgb(92, 38, 50); statusLabel.ForeColor = Color.FromArgb(255, 220, 226); return; }
            if (pauseUntil > DateTime.Now) { statusLabel.Text = "Duraklatıldı " + pauseUntil.ToString("HH:mm"); statusLabel.BackColor = Color.FromArgb(93, 70, 38); statusLabel.ForeColor = Color.FromArgb(255, 239, 202); return; }
            statusLabel.Text = "Koruma aktif"; statusLabel.BackColor = Color.FromArgb(32, 82, 67); statusLabel.ForeColor = Color.FromArgb(210, 255, 239);
        }

        void RefreshApps()
        {
            appList.Items.Clear(); foreach (LockedApp a in cfg.Apps) appList.Items.Add(a.DisplayName + "  [" + a.ProcessName + "]");
            if (appList.Items.Count > 0 && appList.SelectedIndex < 0) appList.SelectedIndex = 0; RefreshIntervals();
        }
        LockedApp SelectedApp() { if (appList.SelectedIndex < 0 || appList.SelectedIndex >= cfg.Apps.Count) return null; return cfg.Apps[appList.SelectedIndex]; }
        void RefreshIntervals() { intervalList.Items.Clear(); LockedApp a=SelectedApp(); if (a==null) return; foreach(TimeInterval t in a.Intervals) intervalList.Items.Add(t.ToString()); }

        void AddApp()
        {
            OpenFileDialog ofd = new OpenFileDialog(); ofd.Filter = "Uygulamalar (*.exe)|*.exe";
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            string pn = Path.GetFileNameWithoutExtension(ofd.FileName);
            foreach (LockedApp x in cfg.Apps) if (String.Equals(x.ExePath, ofd.FileName, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Bu uygulama zaten listede."); return; }
            LockedApp app = new LockedApp(); app.DisplayName = pn; app.ExePath = ofd.FileName; app.ProcessName = pn; app.Intervals.Add(new TimeInterval { Start="09:00", End="17:00" });
            cfg.Apps.Add(app); SaveConfig(); RefreshApps(); appList.SelectedIndex = cfg.Apps.Count - 1;
        }
        void RemoveApp() { LockedApp a=SelectedApp(); if(a==null) return; if(MessageBox.Show(a.DisplayName+" silinsin mi?","Onay",MessageBoxButtons.YesNo)!=DialogResult.Yes) return; cfg.Apps.RemoveAt(appList.SelectedIndex); SaveConfig(); RefreshApps(); }
        void OpenLocation() { LockedApp a=SelectedApp(); if(a==null||!File.Exists(a.ExePath)) return; Process.Start("explorer.exe", "/select,\""+a.ExePath+"\""); }
        void AddInterval()
        {
            LockedApp a=SelectedApp(); if(a==null){MessageBox.Show("Önce uygulama seç.");return;} TimeSpan st,en;
            if(!TimeSpan.TryParse(startBox.Text,out st)||!TimeSpan.TryParse(endBox.Text,out en)){MessageBox.Show("Saat formatı HH:mm olmalı. Örnek: 22:30");return;}
            a.Intervals.Add(new TimeInterval{Start=st.ToString(@"hh\:mm"),End=en.ToString(@"hh\:mm")}); SaveConfig(); RefreshIntervals();
        }
        void RemoveInterval() { LockedApp a=SelectedApp(); if(a==null||intervalList.SelectedIndex<0)return; a.Intervals.RemoveAt(intervalList.SelectedIndex); SaveConfig(); RefreshIntervals(); }
        void SetAutostart(bool on)
        {
            try { cfg.StartWithWindows=on; RegistryKey rk=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true); if(on) rk.SetValue("KSafety","\""+Application.ExecutablePath+"\""); else { rk.DeleteValue("KSafety",false); rk.DeleteValue("SudeAppPinLock",false); } rk.Close(); SaveConfig(); }
            catch(Exception ex){ MessageBox.Show("Otomatik başlatma ayarlanamadı: "+ex.Message); }
        }

        bool IsBlockedNow(LockedApp app)
        {
            TimeSpan now=DateTime.Now.TimeOfDay;
            foreach(TimeInterval ti in app.Intervals)
            {
                TimeSpan st,en; if(!TimeSpan.TryParse(ti.Start,out st)||!TimeSpan.TryParse(ti.End,out en)) continue;
                if(st==en) return true; if(st<en){ if(now>=st&&now<en)return true; } else { if(now>=st||now<en)return true; }
            }
            return false;
        }
        DateTime CurrentBlockEnd(LockedApp app)
        {
            DateTime nowDt=DateTime.Now; TimeSpan now=nowDt.TimeOfDay; DateTime best=nowDt.AddMinutes(30); bool found=false;
            foreach(TimeInterval ti in app.Intervals)
            {
                TimeSpan st,en; if(!TimeSpan.TryParse(ti.Start,out st)||!TimeSpan.TryParse(ti.End,out en)) continue;
                bool inside=false; DateTime endDt=nowDt.Date.Add(en);
                if(st==en){inside=true; endDt=nowDt.AddHours(24);} else if(st<en){inside=now>=st&&now<en;} else {inside=now>=st||now<en; if(now>=st) endDt=nowDt.Date.AddDays(1).Add(en);} if(inside&&(!found||endDt<best)){best=endDt;found=true;}
            }
            return best;
        }

        LockedApp ForegroundLockedApp()
        {
            IntPtr h = Native.GetForegroundWindow(); if (h == IntPtr.Zero) return null; uint pid; Native.GetWindowThreadProcessId(h, out pid); if (pid == 0) return null;
            try { Process p = Process.GetProcessById((int)pid); foreach (LockedApp a in cfg.Apps) if (String.Equals(a.ProcessName, p.ProcessName, StringComparison.OrdinalIgnoreCase)) return a; }
            catch { }
            return null;
        }
        bool IsRunning(LockedApp app)
        {
            try { return Process.GetProcessesByName(app.ProcessName).Length > 0; } catch { return false; }
        }
        void MinimizeApp(LockedApp app)
        {
            try { foreach(Process p in Process.GetProcessesByName(app.ProcessName)) if(p.MainWindowHandle!=IntPtr.Zero) Native.ShowWindow(p.MainWindowHandle, Native.SW_MINIMIZE); } catch { }
        }

        void MonitorApps()
        {
            UpdateStatus();
            if (String.IsNullOrEmpty(cfg.PinHash) || !cfg.ProtectionEnabled || pauseUntil > DateTime.Now) return;
            LockedApp fg = ForegroundLockedApp(); string fgName = fg == null ? null : fg.ProcessName;

            foreach (LockedApp app in cfg.Apps)
            {
                bool running = IsRunning(app);
                if (!running) { wasForeground[app.ProcessName] = false; idleLocked.Remove(app.ProcessName); continue; }

                bool isFg = String.Equals(fgName, app.ProcessName, StringComparison.OrdinalIgnoreCase);
                bool wasFg = wasForeground.ContainsKey(app.ProcessName) && wasForeground[app.ProcessName];
                if (isFg)
                {
                    lastActive[app.ProcessName] = DateTime.Now;
                    if (idleLocked.Contains(app.ProcessName)) { PromptForForeground(app, "Bu uygulama uzak kaldığın için kilitlendi. Devam etmek için PIN gir."); wasForeground[app.ProcessName] = true; continue; }
                    if (cfg.AskPinOnEveryFocus && !wasFg && IsBlockedNow(app)) PromptForForeground(app, "Bu uygulama ana ekrana geldi. Devam etmek için PIN gir.");
                }
                else
                {
                    if (!lastActive.ContainsKey(app.ProcessName)) lastActive[app.ProcessName] = DateTime.Now;
                    if (cfg.IdleLockEnabled && !idleLocked.Contains(app.ProcessName))
                    {
                        if ((DateTime.Now - lastActive[app.ProcessName]).TotalMinutes >= cfg.IdleLockMinutes) idleLocked.Add(app.ProcessName);
                    }
                }
                wasForeground[app.ProcessName] = isFg;

                DateTime until;
                if (IsBlockedNow(app) && !(allowedUntil.TryGetValue(app.ProcessName, out until) && until > DateTime.Now) && !cfg.AskPinOnEveryFocus)
                {
                    try { foreach (Process p in Process.GetProcessesByName(app.ProcessName)) p.Kill(); } catch { }
                    if (!prompting.Contains(app.ProcessName)) { prompting.Add(app.ProcessName); BeginInvoke(new MethodInvoker(delegate { AskPinAndMaybeLaunch(app); })); }
                }
            }
        }

        void PromptForForeground(LockedApp app, string message)
        {
            if (prompting.Contains(app.ProcessName)) return;
            prompting.Add(app.ProcessName);
            BeginInvoke(new MethodInvoker(delegate
            {
                try
                {
                    PinDialog d = new PinDialog("PIN gerekli", message);
                    if (d.ShowDialog(this) == DialogResult.OK && PinService.Verify(d.Pin, cfg))
                    {
                        idleLocked.Remove(app.ProcessName);
                        allowedUntil[app.ProcessName] = CurrentBlockEnd(app);
                        lastActive[app.ProcessName] = DateTime.Now;
                    }
                    else MinimizeApp(app);
                }
                finally { prompting.Remove(app.ProcessName); }
            }));
        }

        void AskPinAndMaybeLaunch(LockedApp app)
        {
            try
            {
                Show(); WindowState = FormWindowState.Normal; Activate();
                PinDialog d = new PinDialog("PIN gerekli", app.DisplayName + " kilitli saat aralığında. Açmak için PIN gir.");
                if (d.ShowDialog(this) == DialogResult.OK && PinService.Verify(d.Pin, cfg))
                {
                    allowedUntil[app.ProcessName] = CurrentBlockEnd(app); idleLocked.Remove(app.ProcessName); lastActive[app.ProcessName] = DateTime.Now;
                    if (File.Exists(app.ExePath)) Process.Start(app.ExePath);
                }
                else MessageBox.Show("PIN yanlış ya da işlem iptal edildi. Uygulama kapalı kalacak.", "Kilit");
            }
            finally { prompting.Remove(app.ProcessName); }
        }
    }
}
