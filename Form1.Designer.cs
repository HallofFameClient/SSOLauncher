#nullable enable

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SSOLauncher;

partial class Form1
{
    private System.ComponentModel.IContainer components = null!;

    // --- account / login ---
    private ComboBox cmbEmail = null!;
    private PillButton btnDeleteProfile = null!;
    private TextBox txtPassword = null!;
    private PillButton btnTogglePassword = null!;
    private PillButton btnLogin = null!;
    private CheckBox chkRemember = null!;
    private CheckBox chkSavePassword = null!;
    private CheckBox chkAutoLogin = null!;
    private Label lblAccountInfo = null!;
    private Label lblStatus = null!;
    private Marquee progressQueue = null!;

    // --- settings ---
    private ComboBox cmbLanguage = null!;
    private ComboBox cmbLauncherLang = null!;
    private FieldHost fldLauncherLang = null!;
    private Label lblLauncherLangCaption = null!;
    private TextBox txtInstallDir = null!;
    private PillButton btnBrowse = null!;
    private CheckBox chkVerify = null!;
    private CheckBox chkRandomDevice = null!;
    private CheckBox chkMinimizeToTray = null!;
    private CheckBox chkDarkMode = null!;
    private CheckBox chkStreamerMode = null!;
    private Label lblFooter = null!;

    // --- chrome ---
    private NotifyIcon trayIcon = null!;
    private ToolTip tips = null!;
    private Panel pnlHeader = null!;
    private Label lblSection = null!;
    private CaptionButton btnMin = null!;
    private CaptionButton btnMax = null!;
    private CaptionButton btnClose = null!;

    // --- sidebar ---
    private Panel panelSide = null!;
    private Label lblCharName = null!;
    private StatChip lblBadge = null!;
    private StatChip lblCoins = null!;
    private StatChip lblHorses = null!;
    private Label lblServer = null!;
    private Panel sideSeparator = null!;
    private Panel sideEdge = null!;
    private PillButton btnStart = null!;
    private NavItem btnNavDiscover = null!;
    private NavItem btnNavNews = null!;
    private NavItem btnNavShop = null!;
    private NavItem btnNavAccount = null!;
    private NavItem btnNavSettings = null!;

    // --- pages ---
    private Panel panelMain = null!;
    private Panel pnlDiscover = null!;
    private Panel pnlAccount = null!;
    private Panel pnlSettings = null!;
    private Card cardBanner = null!;
    private PictureBox picBanner = null!;
    private Label lblBannerTitle = null!;
    private Label lblNewsHint = null!;
    private Card[] cards = new Card[4];
    private PictureBox[] cardPics = new PictureBox[4];
    private Label[] cardTitles = new Label[4];
    private Label[] cardTexts = new Label[4];
    private Card cardLogin = null!;
    private Card cardStatus = null!;
    private Card cardClient = null!;
    private Card cardPrefs = null!;
    private FieldHost fldEmail = null!;
    private FieldHost fldPassword = null!;
    private FieldHost fldInstallDir = null!;
    private FieldHost fldLanguage = null!;
    private Label lblLoginTitle = null!;
    private Label lblEmailCaption = null!;
    private Label lblPassCaption = null!;
    private Label lblStatusTitle = null!;
    private Label lblClientTitle = null!;
    private Label lblClientCaption = null!;
    private Label lblPrefsTitle = null!;
    private Label lblLangCaption = null!;

    private const int Gutter = 30;
    private const int SideWidth = 268;

    internal static void ApplyPalette(bool dark) => ThemeColors.SetDarkMode(dark);

    /// Assigns every visible text from the current launcher language. Called once at
    /// startup and again whenever the language picker changes.
    private void ApplyStrings()
    {
        SuspendLayout();
        btnNavDiscover.Text = L.Discover;
        btnNavNews.Text = L.News;
        btnNavShop.Text = L.Shop;
        btnNavAccount.Text = L.Account;
        btnNavSettings.Text = L.Settings;
        lblSection.Text = SectionTitle(_panel);

        if (_session == null) lblCharName.Text = L.NotLoggedIn;
        btnStart.Text = _cts == null ? L.Play : L.Cancel;

        lblLoginTitle.Text = L.SignIn;
        lblEmailCaption.Text = L.EmailCaption;
        lblPassCaption.Text = L.PasswordCaption;
        btnDeleteProfile.Text = L.Delete;
        btnTogglePassword.Text = txtPassword.UseSystemPasswordChar ? L.Show : L.Hide;
        chkRemember.Text = L.RememberEmail;
        chkSavePassword.Text = L.SaveLogin;
        chkAutoLogin.Text = L.AutoLogin;
        if (_cts == null) btnLogin.Text = L.LogIn;
        lblStatusTitle.Text = L.Account;
        if (_session == null) lblAccountInfo.Text = L.NotLoggedIn + ".";

        lblNewsHint.Text = L.NewsHint;

        lblClientTitle.Text = L.GameClient;
        lblClientCaption.Text = L.ClientFolderCaption;
        btnBrowse.Text = L.Browse;
        lblPrefsTitle.Text = L.Preferences;
        lblLauncherLangCaption.Text = L.LauncherLanguageCaption;
        lblLangCaption.Text = L.GameLanguageCaption;
        chkVerify.Text = L.VerifyFiles;
        chkRandomDevice.Text = L.RandomDevice;
        chkMinimizeToTray.Text = L.MinimizeToTray;
        chkDarkMode.Text = L.DarkMode;
        chkStreamerMode.Text = L.StreamerMode;
        lblFooter.Text = $"v{AppVersion()}  •  SSO Custom Launcher  •  {L.NotAffiliated}";

        tips.SetToolTip(btnStart, L.TipPlay);
        tips.SetToolTip(btnDeleteProfile, L.TipDelete);
        tips.SetToolTip(chkSavePassword, L.TipSavePassword);
        tips.SetToolTip(chkAutoLogin, L.TipAutoLogin);
        tips.SetToolTip(txtInstallDir, L.TipInstallDir);
        tips.SetToolTip(chkVerify, L.TipVerify);
        tips.SetToolTip(chkRandomDevice, L.TipRandomDevice);
        tips.SetToolTip(chkStreamerMode, L.TipStreamerMode);
        tips.SetToolTip(lblCoins, L.TipCoins);
        tips.SetToolTip(lblHorses, L.TipHorses);

        if (trayIcon.ContextMenuStrip is { } menu && menu.Items.Count >= 2)
        {
            menu.Items[0].Text = L.TrayShow;
            menu.Items[1].Text = L.TrayExit;
        }

        ResumeLayout();
        Invalidate(true);
    }

    private static string SectionTitle(string panel) => panel switch
    {
        "account" => L.Account,
        "settings" => L.Settings,
        "news" => L.News,
        _ => L.Discover,
    };

    /// Re-colours every control that stores a colour of its own. The custom-painted
    /// controls read ThemeColors while painting, so they only need to be invalidated.
    private void ApplyTheme()
    {
        SuspendLayout();
        BackColor = ThemeColors.Background;
        ForeColor = ThemeColors.Ink;

        panelSide.BackColor = ThemeColors.Surface;
        sideSeparator.BackColor = ThemeColors.Line;
        sideEdge.BackColor = ThemeColors.Line;
        foreach (var panel in new[] { panelMain, pnlHeader, pnlDiscover, pnlAccount, pnlSettings })
            panel.BackColor = ThemeColors.Background;
        foreach (var card in new[] { cardBanner, cardLogin, cardStatus, cardClient, cardPrefs })
            card.BackColor = ThemeColors.Background;
        foreach (var card in cards) card.BackColor = ThemeColors.Background;

        foreach (var label in new[] { lblCharName, lblLoginTitle, lblStatusTitle,
                                      lblClientTitle, lblPrefsTitle, lblAccountInfo })
            label.ForeColor = ThemeColors.Ink;

        lblCoins.ForeColor = ThemeColors.Ink;
        lblCoins.Accent = ThemeColors.Primary;
        lblHorses.ForeColor = ThemeColors.Ink;
        lblHorses.Accent = ThemeColors.Gold;
        ApplyBadgeStyle();
        foreach (var label in new[] { lblSection, lblStatus })
            label.ForeColor = ThemeColors.Muted;
        foreach (var label in new[] { lblEmailCaption, lblPassCaption, lblClientCaption, lblLangCaption,
                                      lblNewsHint, lblFooter })
            label.ForeColor = ThemeColors.Faint;
        foreach (var label in cardTitles) label.ForeColor = ThemeColors.Ink;
        foreach (var label in cardTexts) label.ForeColor = ThemeColors.Muted;

        foreach (var check in new[] { chkRemember, chkSavePassword, chkAutoLogin, chkVerify,
                                      chkRandomDevice, chkMinimizeToTray, chkDarkMode, chkStreamerMode })
            check.ForeColor = ThemeColors.Muted;

        foreach (var field in new Control[] { cmbEmail, cmbLanguage, txtPassword, txtInstallDir })
        {
            field.BackColor = ThemeColors.Card;
            field.ForeColor = ThemeColors.Ink;
        }

        picBanner.BackColor = ThemeColors.Highlight;
        foreach (var pic in cardPics) pic.BackColor = ThemeColors.Highlight;

        ResumeLayout();
        Invalidate(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    // Borderless window that Windows still treats as resizable (WS_SIZEBOX keeps
    // snap + edge resizing) and minimisable from the taskbar (WS_MINIMIZEBOX).
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= 0x00040000 | 0x00020000;
            return cp;
        }
    }

    // No caption, no non-client frame - but the window still resizes from its edges:
    // WM_NCCALCSIZE hands the whole window to the client area, and WM_NCHITTEST turns
    // the outer few pixels back into resize grips.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCCALCSIZE = 0x0083;
        const int WM_NCHITTEST = 0x0084;
        const int WM_NCPAINT = 0x0085;
        const int WM_NCACTIVATE = 0x0086;
        const int WM_NCUAHDRAWCAPTION = 0x00AE;
        const int WM_NCUAHDRAWFRAME = 0x00AF;

        switch (m.Msg)
        {
            case WM_NCCALCSIZE when m.WParam != IntPtr.Zero:
                m.Result = IntPtr.Zero;
                return;

            // The window still carries WS_SIZEBOX, so Windows would happily paint a
            // classic caption and border over the client area - most visibly when the
            // window is activated or deactivated. There is no non-client area left to
            // draw, so these are swallowed.
            case WM_NCPAINT:
            case WM_NCUAHDRAWCAPTION:
            case WM_NCUAHDRAWFRAME:
                m.Result = IntPtr.Zero;
                return;

            case WM_NCACTIVATE:
                m.LParam = -1; // tells DefWindowProc to skip the non-client repaint
                break;
        }

        base.WndProc(ref m);

        if (m.Msg != WM_NCHITTEST || WindowState != FormWindowState.Normal) return;
        const int grip = 6;
        var p = PointToClient(new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16))));
        bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
        bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
        int hit = (left, right, top, bottom) switch
        {
            (true, _, true, _) => 13,   // HTTOPLEFT
            (_, true, true, _) => 14,   // HTTOPRIGHT
            (true, _, _, true) => 16,   // HTBOTTOMLEFT
            (_, true, _, true) => 17,   // HTBOTTOMRIGHT
            (true, _, _, _) => 10,      // HTLEFT
            (_, true, _, _) => 11,      // HTRIGHT
            (_, _, true, _) => 12,      // HTTOP
            (_, _, _, true) => 15,      // HTBOTTOM
            _ => 0,
        };
        if (hit != 0) m.Result = hit;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        btnMax.Glyph = WindowState == FormWindowState.Maximized ? Glyphs.Restore : Glyphs.Maximize;
        btnMax.Invalidate();
    }

    // The launcher's own icon, read back out of the exe's PE resources. Works in the
    // single-file AOT build too, where no icon file sits next to the executable.
    private static Icon? _appIcon;

    private static Icon AppIcon()
    {
        if (_appIcon != null) return _appIcon;
        try { _appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? ""); }
        catch { }
        return _appIcon ??= SystemIcons.Application;
    }

    private static void RoundPicture(PictureBox pic, int radius)
    {
        if (pic.Width <= 0 || pic.Height <= 0) return;
        var old = pic.Region;
        using var path = Draw.Round(new Rectangle(0, 0, pic.Width, pic.Height), radius);
        pic.Region = new Region(path);
        old?.Dispose();
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(1140, 720);
        MinimumSize = new Size(1000, 660);
        Text = "Tevvez SSO Custom Launcher";
        Icon = AppIcon();
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ThemeColors.Background;
        ForeColor = ThemeColors.Ink;
        Font = Ui.Body;
        DoubleBuffered = true;
        KeyPreview = true;

        tips = new ToolTip(components);

        trayIcon = new NotifyIcon(components)
        {
            Icon = AppIcon(),
            Text = "Tevvez Custom Launcher",
            Visible = false,
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(L.TrayShow, null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add(L.TrayExit, null, (_, _) => { _forceClose = true; Close(); });
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        BuildSidebar();
        BuildPages();

        Controls.Add(panelMain);
        Controls.Add(panelSide);

        Shown += (_, _) => { LayoutDiscover(); LayoutAccount(); LayoutSettings(); };
    }

    // ============================== sidebar ==============================
    private void BuildSidebar()
    {
        // Height must be final before the anchored children are added, otherwise their
        // bottom anchors are computed against the panel's 100px default size.
        panelSide = new BufferedPanel
        {
            Dock = DockStyle.Left,
            Width = SideWidth,
            Height = ClientSize.Height,
            BackColor = ThemeColors.Surface,
        };
        panelSide.MouseDown += DragWindow;

        lblCharName = Ui.Label(L.NotLoggedIn, new Font("Segoe UI", 14F, FontStyle.Bold), ThemeColors.Ink, ContentAlignment.MiddleCenter);
        lblCharName.SetBounds(12, 40, SideWidth - 24, 30);

        lblBadge = new StatChip { Bubble = false, Accent = ThemeColors.Primary, ForeColor = ThemeColors.Primary };
        lblBadge.SetBounds(12, 70, SideWidth - 24, 26);

        lblCoins = new StatChip { Glyph = Glyphs.Coin, Accent = ThemeColors.Primary, Text = "—" };
        lblCoins.SetBounds(20, 106, 112, 30);
        tips.SetToolTip(lblCoins, "Star Coins");

        lblHorses = new StatChip
        {
            Glyph = Glyphs.Horse, GlyphFont = Ui.Symbol, Accent = ThemeColors.Gold, Text = "—",
        };
        lblHorses.SetBounds(136, 106, 112, 30);
        tips.SetToolTip(lblHorses, "Horses waiting to be claimed");

        sideSeparator = new Panel { Bounds = new Rectangle(30, 150, SideWidth - 60, 1), BackColor = ThemeColors.Line };

        btnNavDiscover = MakeNav("Discover", Glyphs.Discover, 180);
        btnNavNews = MakeNav("News", Glyphs.News, 232);
        btnNavShop = MakeNav("Telegram", Glyphs.Shop, 284);
        btnNavAccount = MakeNav("Account", Glyphs.Account, 336);
        btnNavSettings = MakeNav("Settings", Glyphs.Settings, 388);
        btnNavDiscover.Click += (_, _) => ShowPanel("discover");
        btnNavNews.Click += (_, _) => ShowPanel("news");
        btnNavShop.Click += (_, _) => OpenShop();
        btnNavAccount.Click += (_, _) => ShowPanel("account");
        btnNavSettings.Click += (_, _) => ShowPanel("settings");

        btnStart = new PillButton
        {
            Text = L.Play,
            Glyph = Glyphs.Play,
            Font = Ui.Play,
            Enabled = false,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        btnStart.SetBounds(24, ClientSize.Height - 128, SideWidth - 48, 58);
        btnStart.Click += BtnStart_Click;
        tips.SetToolTip(btnStart, "Start the game (log in first).");

        lblServer = Ui.Label($"● {L.Server}: {L.Checking}", Ui.Small, ThemeColors.Muted, ContentAlignment.MiddleCenter);
        lblServer.SetBounds(12, ClientSize.Height - 54, SideWidth - 24, 22);
        lblServer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        sideEdge = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = ThemeColors.Line };

        panelSide.Controls.AddRange(new Control[]
        {
            lblCharName, lblBadge, lblCoins, lblHorses, sideSeparator,
            btnNavDiscover, btnNavNews, btnNavShop, btnNavAccount, btnNavSettings,
            btnStart, lblServer, sideEdge,
        });
    }

    private static NavItem MakeNav(string text, string glyph, int top)
    {
        var item = new NavItem { Text = text, Glyph = glyph };
        item.SetBounds(16, top, SideWidth - 32, 48);
        return item;
    }

    // ============================== pages ==============================
    private void BuildPages()
    {
        panelMain = new BufferedPanel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };

        pnlHeader = new BufferedPanel { Dock = DockStyle.Top, Height = 42, BackColor = ThemeColors.Background };
        pnlHeader.MouseDown += DragWindow;
        pnlHeader.DoubleClick += (_, _) => ToggleMaximize();

        lblSection = Ui.Label("Discover", Ui.H3, ThemeColors.Muted, ContentAlignment.MiddleLeft);
        lblSection.SetBounds(Gutter, 0, 400, 42);
        lblSection.MouseDown += DragWindow;

        btnMin = new CaptionButton { Glyph = Glyphs.Minimize, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnMax = new CaptionButton { Glyph = Glyphs.Maximize, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnClose = new CaptionButton { Glyph = Glyphs.Close, Danger = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;
        btnMax.Click += (_, _) => ToggleMaximize();
        btnClose.Click += (_, _) => Close();
        pnlHeader.Resize += (_, _) =>
        {
            int right = pnlHeader.ClientSize.Width - 10;
            btnClose.SetBounds(right - 44, 6, 44, 30);
            btnMax.SetBounds(right - 88, 6, 44, 30);
            btnMin.SetBounds(right - 132, 6, 44, 30);
        };
        pnlHeader.Controls.AddRange(new Control[] { lblSection, btnMin, btnMax, btnClose });

        pnlDiscover = new BufferedPanel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };
        pnlAccount = new BufferedPanel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background, Visible = false };
        pnlSettings = new BufferedPanel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background, Visible = false };

        BuildDiscover();
        BuildAccount();
        BuildSettings();

        panelMain.Controls.Add(pnlDiscover);
        panelMain.Controls.Add(pnlAccount);
        panelMain.Controls.Add(pnlSettings);
        panelMain.Controls.Add(pnlHeader);
    }

    private void BuildDiscover()
    {
        cardBanner = new Card { Radius = 20 };
        picBanner = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = ThemeColors.Highlight,
        };
        lblBannerTitle = Ui.Label(L.BannerFallback, new Font("Segoe UI", 15F, FontStyle.Bold), Color.White, ContentAlignment.MiddleLeft);
        lblBannerTitle.BackColor = Color.FromArgb(130, 18, 10, 24);
        lblBannerTitle.Padding = new Padding(20, 0, 20, 0);
        picBanner.Controls.Add(lblBannerTitle);
        cardBanner.Controls.Add(picBanner);
        pnlDiscover.Controls.Add(cardBanner);

        for (int i = 0; i < 4; i++)
        {
            var card = new Card { Radius = 16, Hoverable = true };
            var pic = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = ThemeColors.Highlight };
            var title = Ui.Label("", Ui.H3, ThemeColors.Ink);
            var text = Ui.Label("", Ui.Small, ThemeColors.Muted);
            card.Controls.Add(pic);
            card.Controls.Add(title);
            card.Controls.Add(text);
            cards[i] = card;
            cardPics[i] = pic;
            cardTitles[i] = title;
            cardTexts[i] = text;
            pnlDiscover.Controls.Add(card);
        }

        lblNewsHint = Ui.Label(L.NewsHint, Ui.Small, ThemeColors.Faint);
        pnlDiscover.Controls.Add(lblNewsHint);
        pnlDiscover.Resize += (_, _) => LayoutDiscover();
    }

    private void LayoutDiscover()
    {
        int w = pnlDiscover.ClientSize.Width, h = pnlDiscover.ClientSize.Height;
        int inner = w - Gutter * 2;
        if (inner < 200 || h < 200) return;

        int bannerH = Math.Max(150, Math.Min(240, h - 380));
        cardBanner.SetFace(Gutter, 4, inner, bannerH);
        var bf = cardBanner.Face;
        picBanner.SetBounds(bf.X + 1, bf.Y + 1, bf.Width - 2, bf.Height - 2);
        RoundPicture(picBanner, 19);
        lblBannerTitle.SetBounds(0, picBanner.Height - 56, picBanner.Width, 56);

        const int gapX = 22, gapY = 20;
        int colW = (inner - gapX) / 2;
        int top = 4 + bannerH + 26;
        int cardH = Math.Max(112, (h - top - 44 - gapY) / 2);

        for (int i = 0; i < 4; i++)
        {
            int col = i % 2, row = i / 2;
            cards[i].SetFace(Gutter + col * (colW + gapX), top + row * (cardH + gapY), colW, cardH);
            var f = cards[i].Face;
            int picW = Math.Min(140, colW / 3);
            cardPics[i].SetBounds(f.X + 12, f.Y + 12, picW, cardH - 24);
            RoundPicture(cardPics[i], 12);
            int tx = f.X + picW + 26;
            int tw = f.Width - picW - 42;
            cardTitles[i].SetBounds(tx, f.Y + 16, tw, 44);
            cardTexts[i].SetBounds(tx, f.Y + 62, tw, cardH - 76);
        }

        lblNewsHint.SetBounds(Gutter, h - 30, inner, 20);
    }

    private void BuildAccount()
    {
        cardLogin = new Card();
        lblLoginTitle = Ui.Label("Sign in", Ui.H2, ThemeColors.Ink);
        lblEmailCaption = Ui.Label("EMAIL", Ui.Caps, ThemeColors.Faint);
        lblPassCaption = Ui.Label("PASSWORD", Ui.Caps, ThemeColors.Faint);

        cmbEmail = new FlatCombo { DropDownStyle = ComboBoxStyle.DropDown };
        cmbEmail.SelectedIndexChanged += CmbEmail_SelectedIndexChanged;
        cmbEmail.TextUpdate += CmbEmail_TextUpdate;
        cmbEmail.Enter += CmbEmail_Enter;
        fldEmail = new FieldHost { Glyph = Glyphs.Mail };
        fldEmail.Host(cmbEmail);

        btnDeleteProfile = new PillButton { Text = "Delete", Glyph = Glyphs.Delete, GlyphFont = Ui.IconSmall, Style = PillButton.Kind.Ghost };
        btnDeleteProfile.Click += BtnDeleteProfile_Click;
        tips.SetToolTip(btnDeleteProfile, "Remove the saved account for this email.");

        txtPassword = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Body, UseSystemPasswordChar = true };
        fldPassword = new FieldHost { Glyph = Glyphs.Lock };
        fldPassword.Host(txtPassword);

        btnTogglePassword = new PillButton { Text = "Show", Glyph = Glyphs.Eye, GlyphFont = Ui.IconSmall, Style = PillButton.Kind.Ghost };
        btnTogglePassword.Click += BtnTogglePassword_Click;

        chkRemember = new ModernCheck { Text = "Remember email" };
        chkSavePassword = new ModernCheck { Text = "Save login securely" };
        tips.SetToolTip(chkSavePassword, "Stores email + password DPAPI-encrypted for this Windows user only.");
        chkAutoLogin = new ModernCheck { Text = "Auto-login" };
        tips.SetToolTip(chkAutoLogin, "Log in automatically on start via refresh token (no password needed).");

        btnLogin = new PillButton { Text = L.LogIn, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
        btnLogin.Click += BtnLogin_Click;

        cardLogin.Controls.AddRange(new Control[]
        {
            lblLoginTitle, lblEmailCaption, fldEmail, btnDeleteProfile,
            lblPassCaption, fldPassword, btnTogglePassword,
            chkRemember, chkSavePassword, chkAutoLogin, btnLogin,
        });

        cardStatus = new Card();
        lblStatusTitle = Ui.Label("Account", Ui.H2, ThemeColors.Ink);
        lblAccountInfo = Ui.Label(L.NotLoggedIn + ".", Ui.Body, ThemeColors.Ink);
        progressQueue = new Marquee { Visible = false };
        lblStatus = Ui.Label("", Ui.Small, ThemeColors.Muted);
        cardStatus.Controls.AddRange(new Control[] { lblStatusTitle, lblAccountInfo, progressQueue, lblStatus });

        pnlAccount.Controls.Add(cardLogin);
        pnlAccount.Controls.Add(cardStatus);
        pnlAccount.Resize += (_, _) => LayoutAccount();
        AcceptButton = btnLogin;
    }

    private void LayoutAccount()
    {
        int w = pnlAccount.ClientSize.Width, h = pnlAccount.ClientSize.Height;
        int colW = Math.Min(780, w - Gutter * 2);
        if (colW < 300 || h < 200) return;
        int x = (w - colW) / 2;

        const int loginH = 332;
        cardLogin.SetFace(x, 4, colW, loginH);
        var f = cardLogin.Face;
        int btnW = 118;
        int fieldW = colW - 48 - btnW - 12;

        lblLoginTitle.SetBounds(f.X + 26, f.Y + 22, colW - 52, 28);
        lblEmailCaption.SetBounds(f.X + 28, f.Y + 62, 200, 16);
        fldEmail.SetBounds(f.X + 26, f.Y + 82, fieldW, 44);
        btnDeleteProfile.SetBounds(f.X + colW - 26 - btnW, f.Y + 82, btnW, 44);
        lblPassCaption.SetBounds(f.X + 28, f.Y + 138, 200, 16);
        fldPassword.SetBounds(f.X + 26, f.Y + 158, fieldW, 44);
        btnTogglePassword.SetBounds(f.X + colW - 26 - btnW, f.Y + 158, btnW, 44);

        int checkY = f.Y + 214;
        int checkW = (colW - 52) / 3;
        chkRemember.SetBounds(f.X + 26, checkY, checkW, 26);
        chkSavePassword.SetBounds(f.X + 26 + checkW, checkY, checkW, 26);
        chkAutoLogin.SetBounds(f.X + 26 + checkW * 2, checkY, checkW, 26);
        btnLogin.SetBounds(f.X + 26, f.Y + 254, colW - 52, 52);

        int statusTop = 4 + loginH + 26;
        int statusH = Math.Max(150, h - statusTop - 28);
        cardStatus.SetFace(x, statusTop, colW, statusH);
        var s = cardStatus.Face;
        lblStatusTitle.SetBounds(s.X + 26, s.Y + 20, colW - 52, 26);
        lblAccountInfo.SetBounds(s.X + 26, s.Y + 54, colW - 52, 88);
        progressQueue.SetBounds(s.X + 26, s.Y + 150, colW - 52, 6);
        lblStatus.SetBounds(s.X + 26, s.Y + 166, colW - 52, Math.Max(20, statusH - 186));
    }

    private void BuildSettings()
    {
        cardClient = new Card();
        lblClientTitle = Ui.Label("Game client", Ui.H2, ThemeColors.Ink);
        lblClientCaption = Ui.Label("FOLDER CONTAINING SSOCLIENT.EXE", Ui.Caps, ThemeColors.Faint);
        txtInstallDir = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Body };
        fldInstallDir = new FieldHost { Glyph = Glyphs.Folder };
        fldInstallDir.Host(txtInstallDir);
        tips.SetToolTip(txtInstallDir, "Folder containing SSOClient.exe (...\\Star Stable Online\\client).");
        btnBrowse = new PillButton { Text = "Browse", Style = PillButton.Kind.Ghost };
        btnBrowse.Click += BtnBrowse_Click;
        cardClient.Controls.AddRange(new Control[] { lblClientTitle, lblClientCaption, fldInstallDir, btnBrowse });

        cardPrefs = new Card();
        lblPrefsTitle = Ui.Label("Preferences", Ui.H2, ThemeColors.Ink);
        lblLangCaption = Ui.Label("GAME LANGUAGE", Ui.Caps, ThemeColors.Faint);
        cmbLanguage = new FlatCombo { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbLanguage.Items.AddRange(new object[] { "de", "en", "sv", "fr", "es", "it", "nl", "pl", "pt", "fi", "da", "no", "hu", "ru" });
        cmbLanguage.SelectedItem = "de";
        fldLanguage = new FieldHost { Glyph = Glyphs.Globe };
        fldLanguage.Host(cmbLanguage);

        lblLauncherLangCaption = Ui.Label("", Ui.Caps, ThemeColors.Faint);
        cmbLauncherLang = new FlatCombo { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbLauncherLang.Items.AddRange(L.Names);
        cmbLauncherLang.SelectedIndex = 0;
        fldLauncherLang = new FieldHost { Glyph = Glyphs.Globe };
        fldLauncherLang.Host(cmbLauncherLang);

        chkVerify = new ModernCheck { Text = "Verify game files before start" };
        tips.SetToolTip(chkVerify, "Runs SSOGamelib.exe to verify/repair files, like the official launcher.");
        chkRandomDevice = new ModernCheck { Text = "Random device ID" };
        tips.SetToolTip(chkRandomDevice, "Log in as an unknown device - the server will then require email verification.");
        chkMinimizeToTray = new ModernCheck { Text = "Minimize to tray instead of closing" };
        chkDarkMode = new ModernCheck { Text = "Dark mode" };
        chkStreamerMode = new ModernCheck { Text = "Streamer mode" };
        cardPrefs.Controls.AddRange(new Control[]
        {
            lblPrefsTitle, lblLauncherLangCaption, fldLauncherLang, lblLangCaption, fldLanguage,
            chkVerify, chkRandomDevice, chkMinimizeToTray, chkDarkMode, chkStreamerMode,
        });

        lblFooter = Ui.Label("", Ui.Small, ThemeColors.Faint);

        pnlSettings.Controls.Add(cardClient);
        pnlSettings.Controls.Add(cardPrefs);
        pnlSettings.Controls.Add(lblFooter);
        pnlSettings.Resize += (_, _) => LayoutSettings();
    }

    private void LayoutSettings()
    {
        int w = pnlSettings.ClientSize.Width, h = pnlSettings.ClientSize.Height;
        int colW = Math.Min(780, w - Gutter * 2);
        if (colW < 300 || h < 200) return;
        int x = (w - colW) / 2;

        cardClient.SetFace(x, 4, colW, 152);
        var c = cardClient.Face;
        int btnW = 118;
        lblClientTitle.SetBounds(c.X + 26, c.Y + 20, colW - 52, 26);
        lblClientCaption.SetBounds(c.X + 28, c.Y + 58, 320, 16);
        fldInstallDir.SetBounds(c.X + 26, c.Y + 78, colW - 52 - btnW - 12, 44);
        btnBrowse.SetBounds(c.X + colW - 26 - btnW, c.Y + 78, btnW, 44);

        cardPrefs.SetFace(x, 182, colW, 300);
        var p = cardPrefs.Face;
        lblPrefsTitle.SetBounds(p.X + 26, p.Y + 20, colW - 52, 26);
        int comboW = Math.Min(240, (colW - 72) / 2);
        lblLauncherLangCaption.SetBounds(p.X + 28, p.Y + 58, comboW, 16);
        fldLauncherLang.SetBounds(p.X + 26, p.Y + 78, comboW, 44);
        lblLangCaption.SetBounds(p.X + 46 + comboW, p.Y + 58, comboW, 16);
        fldLanguage.SetBounds(p.X + 44 + comboW, p.Y + 78, comboW, 44);
        int y = p.Y + 140;
        foreach (var check in new[] { chkVerify, chkRandomDevice, chkMinimizeToTray, chkDarkMode, chkStreamerMode })
        {
            check.SetBounds(p.X + 26, y, colW - 52, 26);
            y += 30;
        }

        lblFooter.SetBounds(x, h - 30, colW, 20);
    }
}
