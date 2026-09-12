using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SSOLauncher;

public partial class Form1 : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowText(IntPtr hWnd, string text);

    private string _lastDisplayName = "";
    private CancellationTokenSource? _cts;
    private bool _passwordIsMasked;
    private string _maskedPassword = "";
    private bool _emailIsMasked;
    private string _maskedEmail = "";
    private List<SsoProfile> _profiles = new();
    private bool _suppressComboEvents;
    private bool _forceClose;
    private bool _settingsLoaded;
    private string _panel = "discover";

    public Form1()
    {
        var settings = SettingsStore.Load();
        ApplyPalette(settings.DarkMode); // must run before InitializeComponent builds the controls
        L.Set(settings.LauncherLanguage);
        InitializeComponent();
        cmbLauncherLang.SelectedIndex = Math.Max(0, Array.IndexOf(L.Codes, L.Current));
        ApplyStrings();

        txtInstallDir.Text = string.IsNullOrWhiteSpace(settings.InstallDir)
            ? @"D:\Other_Games\SSO\Star Stable Online\client" // Standard-Pfad vorbelegen: ...\Star Stable Online\client (dort liegt SSOClient.exe)
            : settings.InstallDir;
        if (cmbLanguage.Items.Contains(settings.Language)) cmbLanguage.SelectedItem = settings.Language;
        chkVerify.Checked = settings.VerifyFiles;
        chkRandomDevice.Checked = settings.RandomDevice;
        chkMinimizeToTray.Checked = settings.MinimizeToTray;
        chkDarkMode.Checked = settings.DarkMode;
        chkStreamerMode.Checked = settings.StreamerMode;
        NewAlias();
        if (settings.WindowWidth >= MinimumSize.Width && settings.WindowHeight >= MinimumSize.Height)
            Size = new Size(settings.WindowWidth, settings.WindowHeight);
        _settingsLoaded = true;

        // Bei Fokus Masken löschen, damit der User neu tippen/ändern kann
        txtPassword.Enter += (_, _) =>
        {
            if (_passwordIsMasked) { _passwordIsMasked = false; _maskedPassword = ""; txtPassword.Text = ""; }
        };

        txtInstallDir.Leave += (_, _) => SaveSettings();
        cmbLanguage.SelectedIndexChanged += (_, _) => SaveSettings();
        cmbLauncherLang.SelectedIndexChanged += (_, _) =>
        {
            L.Set(L.Codes[cmbLauncherLang.SelectedIndex]);
            ApplyStrings();
            SaveSettings();
            _ = RefreshServerStatusAsync();  // the server line is built from translated words
            _ = LoadDiscoverAsync();         // and the news feed follows the launcher language
        };
        chkVerify.CheckedChanged += (_, _) => SaveSettings();
        chkRandomDevice.CheckedChanged += (_, _) => SaveSettings();
        chkMinimizeToTray.CheckedChanged += (_, _) => SaveSettings();
        chkStreamerMode.CheckedChanged += (_, _) =>
        {
            NewAlias();          // a fresh stand-in every time it is switched on
            RenderIdentity();
            RefreshAccountCombo();
            if (_emailIsMasked) SetMaskedEmail(_maskedEmail);
            SaveSettings();
        };
        chkDarkMode.CheckedChanged += (_, _) =>
        {
            ApplyPalette(chkDarkMode.Checked);
            ApplyTheme();
            SaveSettings();
        };
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && chkMinimizeToTray.Checked)
            {
                Hide();
                trayIcon.Visible = true;
            }
        };
        FormClosing += (_, e) =>
        {
            if (!_forceClose && chkMinimizeToTray.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                trayIcon.Visible = true;
                return;
            }
            SaveSettings();
            trayIcon.Visible = false;
        };

        try
        {
            _profiles = ProfileStore.Load();
            RefreshAccountCombo();
            if (_profiles.Count > 0)
            {
                cmbEmail.SelectedIndex = 0;
                LoadProfile(_profiles[0]);
                chkSavePassword.Checked = true;
            }
            else
            {
                string? legacy = ProfileStore.LegacyEmail();
                if (!string.IsNullOrEmpty(legacy)) SetMaskedEmail(legacy);
            }
        }
        catch { }
        ShowPanel("discover");
        Load += async (_, _) =>
        {
            await RefreshServerStatusAsync();
            await TryAutoLoginAsync();
            await LoadDiscoverAsync();
        };
    }

    private static string AppVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "dev" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        trayIcon.Visible = false;
        Activate();
    }

    private void SaveSettings()
    {
        if (!_settingsLoaded) return; // don't clobber the file with pre-load defaults
        SettingsStore.Save(new LauncherSettings
        {
            InstallDir = txtInstallDir.Text.Trim().Trim('"'),
            Language = cmbLanguage.SelectedItem?.ToString() ?? "de",
            LauncherLanguage = L.Current,
            VerifyFiles = chkVerify.Checked,
            RandomDevice = chkRandomDevice.Checked,
            MinimizeToTray = chkMinimizeToTray.Checked,
            DarkMode = chkDarkMode.Checked,
            StreamerMode = chkStreamerMode.Checked,
            WindowWidth = WindowState == FormWindowState.Normal ? Width : RestoreBounds.Width,
            WindowHeight = WindowState == FormWindowState.Normal ? Height : RestoreBounds.Height,
        });
    }

    private void ShowPanel(string name)
    {
        _panel = name;
        bool discover = name is "discover" or "news";
        pnlDiscover.Visible = discover;
        pnlAccount.Visible = name == "account";
        pnlSettings.Visible = name == "settings";
        btnNavDiscover.Selected = name == "discover";
        btnNavNews.Selected = name == "news";
        btnNavAccount.Selected = name == "account";
        btnNavSettings.Selected = name == "settings";
        lblSection.Text = SectionTitle(name);
        if (discover) _ = LoadDiscoverAsync();
    }

    private void OpenShop()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://t.me/+Ba98xQhu8MIzMjBi") { UseShellExecute = true });
        }
        catch (Exception ex) { SetStatus(L.ErrorPrefix + ex.Message); }
    }

    // Discover/News: real articles from launcher/news, images loaded async (best effort).
    private bool _newsLoading;
    private async Task LoadDiscoverAsync()
    {
        if (_newsLoading) return;
        _newsLoading = true;
        try
        {
            string lang = L.Current;
            SsoApiClient api = _session?.Api ?? new SsoApiClient(GetOrCreateDeviceId());
            bool owned = _session == null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                JsonNode news = await api.GetNewsAsync(lang, 5, cts.Token);
                var items = SsoApiClient.ParseNews(news, 5);
                if (items.Count > 0)
                {
                    var first = items[0];
                    lblBannerTitle.Text = first.Title;
                    // The images must finish downloading before the finally block below
                    // disposes a client we own, otherwise every request is cancelled.
                    var images = new List<Task> { LoadImageAsync(api, first.Image, picBanner, true) };
                    for (int i = 0; i < 4; i++)
                    {
                        if (i + 1 < items.Count)
                        {
                            cardTitles[i].Text = items[i + 1].Title;
                            cardTexts[i].Text = items[i + 1].Text;
                            images.Add(LoadImageAsync(api, items[i + 1].Image, cardPics[i], false));
                        }
                        else { cardTitles[i].Text = ""; cardTexts[i].Text = ""; cardPics[i].Image = null; }
                    }
                    await Task.WhenAll(images);
                }
            }
            finally { if (owned) api.Dispose(); }
        }
        catch { }
        finally { _newsLoading = false; }
    }

    private static async Task LoadImageAsync(SsoApiClient api, string url, PictureBox pic, bool banner)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            byte[]? bytes = await api.DownloadBytesAsync(url, cts.Token);
            if (bytes == null || bytes.Length == 0) return;
            // Image.FromStream keeps using the stream, which is gone by the time the UI
            // thread runs the callback - copy the pixels so the bitmap stands on its own.
            Bitmap img;
            using (var ms = new MemoryStream(bytes))
            using (var decoded = Image.FromStream(ms))
                img = new Bitmap(decoded);
            if (pic.IsDisposed) { img.Dispose(); return; }
            pic.BeginInvoke(() =>
            {
                pic.Image?.Dispose();
                pic.Image = banner ? img : CropToFill(img, pic.Size);
                pic.SizeMode = PictureBoxSizeMode.StretchImage;
                if (!banner) img.Dispose();
            });
        }
        catch { }
    }

    // Wide press images in a near-square thumbnail would letterbox with Zoom, so centre-crop
    // them to the box instead - the CSS "object-fit: cover" behaviour.
    private static Bitmap CropToFill(Image src, Size target)
    {
        if (target.Width <= 0 || target.Height <= 0) return new Bitmap(src);
        double want = (double)target.Width / target.Height;
        double have = (double)src.Width / src.Height;
        Rectangle crop;
        if (have > want)
        {
            int w = Math.Max(1, (int)(src.Height * want));
            crop = new Rectangle((src.Width - w) / 2, 0, w, src.Height);
        }
        else
        {
            int h = Math.Max(1, (int)(src.Width / want));
            crop = new Rectangle(0, (src.Height - h) / 2, src.Width, h);
        }
        var bmp = new Bitmap(target.Width, target.Height);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, new Rectangle(0, 0, target.Width, target.Height), crop, GraphicsUnit.Pixel);
        return bmp;
    }

    // outputCode from the last login; the badge has to be restyled on theme changes too.
    private int _outputCode;

    // ---- identity shown in the UI ----
    // Streamer mode swaps the character name, email and account id for random stand-ins.
    // The real values stay in these fields so the display can be rebuilt when the
    // setting is toggled mid-session - a streamer must not have to log in again.
    private string _realDisplayName = "";
    private string _accountName = "";
    private string _accountId = "";
    private string _accountStatusLine = "";
    private string _accountExtraLine = "";
    private string _accountCoinsLine = "";
    private bool _hasAccountData;
    private string _aliasName = "";
    private string _aliasId = "";

    private static readonly string[] AliasFirst =
    {
        "Aurora", "Misty", "Luna", "Willow", "Skye", "Ivy", "Amber", "Hazel", "Rosa", "Sunny",
        "Star", "Pepper", "Daisy", "Juniper", "Clover", "Fable", "Echo", "Birch", "Coral", "Wren",
    };

    private static readonly string[] AliasSecond =
    {
        "Meadowbow", "Starrider", "Moonfield", "Windrider", "Sunhoof", "Nightbrook", "Silverfern",
        "Rainhollow", "Frostmane", "Emberbrook", "Dawnvalley", "Stormcrest", "Willowbrook",
        "Ambergale", "Mosswood", "Brightwater", "Snowpine", "Thornfield", "Duskrider", "Goldmeadow",
    };

    private void NewAlias()
    {
        _aliasName = $"{AliasFirst[Random.Shared.Next(AliasFirst.Length)]} {AliasSecond[Random.Shared.Next(AliasSecond.Length)]}";
        _aliasId = Random.Shared.Next(10_000_000, 99_999_999).ToString();
    }

    private bool StreamerMode => chkStreamerMode.Checked;

    private string PublicName(string real) => StreamerMode ? _aliasName : real;
    private string PublicEmail(string real) => StreamerMode ? "***@***" : MaskEmail(real);
    private string PublicId(string real) => StreamerMode ? _aliasId : real;

    /// Repaints everything that carries the account's identity, from the cached values.
    private void RenderIdentity()
    {
        if (InvokeRequired) { BeginInvoke(RenderIdentity); return; }
        if (!_hasAccountData) return;

        lblCharName.Text = PublicName(_realDisplayName);
        var lines = new List<string>
        {
            $"{L.FieldUser}: {PublicEmail(_accountName)}  (ID {PublicId(_accountId)})",
            _accountStatusLine,
        };
        if (!string.IsNullOrEmpty(_accountExtraLine)) lines.Add(_accountExtraLine);
        if (!string.IsNullOrEmpty(_accountCoinsLine)) lines.Add(_accountCoinsLine);
        lblAccountInfo.Text = string.Join("\n", lines);
    }

    private void ApplyBadgeStyle()
    {
        (string text, Color color) = _outputCode switch
        {
            >= 70 => ("Lifetime Star Rider", ThemeColors.Gold),
            >= 50 => ("Star Rider", ThemeColors.Primary),
            > 0 => ("Free to play", ThemeColors.Muted),
            _ => ("", ThemeColors.Muted),
        };
        lblBadge.Glyph = _outputCode >= 50 ? Glyphs.Shield : "";
        lblBadge.Accent = color;
        lblBadge.ForeColor = color;
        lblBadge.Text = text;
    }

    // The sidebar shows the character name like the official launcher does. The account
    // payload nests it differently depending on the endpoint, so search the character
    // subtree first and reject anything that is clearly an email address or a numeric id.
    private static readonly string[] CharacterNameKeys =
    {
        "characterName", "character_name", "nickname", "nickName",
        "displayName", "display_name", "screenName", "name",
    };

    private static string? FindCharacterName(JsonNode? init)
    {
        JsonNode? scoped = FindSubtree(init, "character") ?? FindSubtree(init, "characters");
        foreach (var source in new[] { scoped, init })
        {
            if (source == null) continue;
            foreach (var key in CharacterNameKeys)
            {
                string? value = FindFirst(source, key)?.Trim();
                if (string.IsNullOrEmpty(value)) continue;
                if (value.Contains('@') || long.TryParse(value, out _)) continue;
                return value;
            }
        }
        return null;
    }

    private static JsonNode? FindSubtree(JsonNode? node, string key)
    {
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                if (kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonObject or JsonArray)
                    return kv.Value;
                var found = FindSubtree(kv.Value, key);
                if (found != null) return found;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var found = FindSubtree(item, key);
                if (found != null) return found;
            }
        }
        return null;
    }

    private void SetSideText(Control lbl, string text, Color? color = null)
    {
        if (InvokeRequired) { BeginInvoke(() => { lbl.Text = text; if (color != null) lbl.ForeColor = color.Value; }); }
        else { lbl.Text = text; if (color != null) lbl.ForeColor = color.Value; }
    }

    // The drop-down would otherwise spell out every saved address; under streamer mode
    // the entries are numbered instead so they stay tellable apart without leaking.
    private void RefreshAccountCombo()
    {
        _suppressComboEvents = true;
        try
        {
            int selected = cmbEmail.SelectedIndex;
            cmbEmail.Items.Clear();
            for (int i = 0; i < _profiles.Count; i++)
                cmbEmail.Items.Add(StreamerMode ? $"{L.Account} {i + 1}" : _profiles[i].Email);
            if (selected >= 0 && selected < cmbEmail.Items.Count) cmbEmail.SelectedIndex = selected;
        }
        finally { _suppressComboEvents = false; }
    }

    private void LoadProfile(SsoProfile p)
    {
        _suppressComboEvents = true;
        try
        {
            SetMaskedEmail(p.Email);
            if (!string.IsNullOrEmpty(p.Password))
            {
                _maskedPassword = p.Password;
                _passwordIsMasked = true;
                txtPassword.Text = new string('*', _maskedPassword.Length + 4);
            }
            else
            {
                _maskedPassword = "";
                _passwordIsMasked = false;
                txtPassword.Text = "";
            }
            chkAutoLogin.Checked = p.AutoLogin;
        }
        finally { _suppressComboEvents = false; }
    }

    private void CmbEmail_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Matched by position, not by the displayed text - streamer mode replaces that text.
        if (_suppressComboEvents || cmbEmail.SelectedIndex < 0 || cmbEmail.SelectedIndex >= _profiles.Count) return;
        var p = _profiles[cmbEmail.SelectedIndex];
        LoadProfile(p);
        chkSavePassword.Checked = true;
        chkAutoLogin.Checked = p.AutoLogin;
    }

    private void CmbEmail_TextUpdate(object? sender, EventArgs e)
    {
        // User is typing: use the typed text (session validity is checked at Play).
        if (_suppressComboEvents) return;
        _emailIsMasked = false;
        _maskedEmail = "";
    }

    private void CmbEmail_Enter(object? sender, EventArgs e)
    {
        // Maske nicht löschen, nur alles markieren: Tippen ersetzt, Klick allein ändert nichts.
        if (_emailIsMasked) cmbEmail.SelectAll();
    }

    private void BtnDeleteProfile_Click(object? sender, EventArgs e)
    {
        string mail = _emailIsMasked ? _maskedEmail : cmbEmail.Text.Trim();
        int removed = _profiles.RemoveAll(x => x.Email.Equals(mail, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            ProfileStore.Save(_profiles);
            RefreshAccountCombo();
        }
        _emailIsMasked = false; _maskedEmail = "";
        _passwordIsMasked = false; _maskedPassword = "";
        _suppressComboEvents = true; cmbEmail.Text = ""; txtPassword.Text = ""; _suppressComboEvents = false;
        chkSavePassword.Checked = false;
        // Only drop the active session if it's the one being deleted - deleting an unrelated
        // saved profile shouldn't log the user out of the account they're currently using.
        if (_session != null && _session.Email.Equals(mail, StringComparison.OrdinalIgnoreCase))
            InvalidateSession();
        SetStatus(removed > 0 ? L.AccountDeleted(mail) : L.NoSavedAccount);
    }

    // Zeigt die E-Mail verkürzt (z.B. "m***@***"), damit sie in Aufnahmen nicht leakt.
    // Die volle Adresse bleibt nur im Speicher.
    private void SetMaskedEmail(string fullEmail)
    {
        _maskedEmail = fullEmail;
        _emailIsMasked = true;
        _suppressComboEvents = true;
        try
        {
            cmbEmail.Text = PublicEmail(fullEmail);
        }
        finally { _suppressComboEvents = false; }
    }

    private static string MaskEmail(string fullEmail)
    {
        int at = fullEmail.IndexOf('@');
        return at > 0 ? $"{fullEmail[0]}***@***" : "***";
    }

    // Server-Status ohne Login (Remote-Config ist öffentlich): Wartung / deaktiviert / online.
    private async Task RefreshServerStatusAsync()
    {
        try
        {
            using var api = new SsoApiClient(GetOrCreateDeviceId());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            JsonNode rc = await api.GetRemoteConfigAsync(cts.Token);
            bool maintenance = rc["maintenanceMode"]?.GetValue<bool>() ?? false;
            bool enabled = rc["serversEnabled"]?.GetValue<bool>() ?? true;
            int msgCode = rc["messageCode"]?.GetValue<int>() ?? 0;
            if (maintenance || msgCode == 210) SetServer($"● {L.Server}: {L.Maintenance}", ThemeColors.Error);
            else if (!enabled || msgCode == 200) SetServer($"● {L.Server}: {L.Disabled}", ThemeColors.Error);
            else SetServer($"● {L.Server}: {L.Online}", ThemeColors.Success);
        }
        catch { SetServer($"● {L.Server}: {L.Unknown}", ThemeColors.Muted); }
    }

    private void SetServer(string text, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => { lblServer.Text = text; lblServer.ForeColor = color; }); }
        else { lblServer.Text = text; lblServer.ForeColor = color; }
    }

    private void SetAccountInfo(string text)
    {
        if (InvokeRequired) { BeginInvoke(() => lblAccountInfo.Text = text); }
        else lblAccountInfo.Text = text;
    }
    private void BtnTogglePassword_Click(object? sender, EventArgs e)
    {
        if (_passwordIsMasked) return; // gespeicherte Maske nicht aufdecken
        txtPassword.UseSystemPasswordChar = !txtPassword.UseSystemPasswordChar;
        btnTogglePassword.Text = txtPassword.UseSystemPasswordChar ? "Show" : "Hide";
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        dlg.Description = "Pick the client folder (where SSOClient.exe is)";
        dlg.SelectedPath = txtInstallDir.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            txtInstallDir.Text = dlg.SelectedPath;
            SaveSettings();
        }
    }

    // Logged-in state, kept between Login and Play clicks.
    private sealed record LoginSession(
        SsoApiClient Api, string Email, string Password, string AccountId,
        string MetricsUrl, string MetricsGroups,
        string EnableActor, string EnableNebula,
        string GameVersion, string GameFilesEndpoint,
        string InstallDir, string ExePath, string Language);
    private LoginSession? _session;

    private void InvalidateSession()
    {
        _session?.Api.Dispose();
        _session = null;
        btnStart.Enabled = false;
        _hasAccountData = false;
        _realDisplayName = "";
        SetAccountInfo(L.NotLoggedIn + ".");
        SetSideText(lblCharName, L.NotLoggedIn);
        _outputCode = 0;
        if (InvokeRequired) BeginInvoke(ApplyBadgeStyle); else ApplyBadgeStyle();
        SetSideText(lblCoins, "—");
        SetSideText(lblHorses, "—");
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (_cts != null) { _cts.Cancel(); return; }

        string email = _emailIsMasked ? _maskedEmail : cmbEmail.Text.Trim();
        string password = _passwordIsMasked ? _maskedPassword : txtPassword.Text;
        string installDir = txtInstallDir.Text.Trim().Trim('"');
        string language = cmbLanguage.SelectedItem?.ToString() ?? "de";
        string exePath = Path.Combine(installDir, "SSOClient.exe");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        { SetStatus(L.EnterCredentials); return; }
        if (!File.Exists(exePath))
        { SetStatus($"SSOClient.exe not found:\n{exePath}\nCheck the client folder (…\\Star Stable Online\\client)."); return; }

        if (chkRemember.Checked)
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "email.txt"), email); } catch { }
        }

        InvalidateSession();
        _cts = new CancellationTokenSource();
        btnLogin.Text = L.Cancel;
        btnLogin.Enabled = true;
        btnStart.Enabled = false;
        progressQueue.Visible = true;
        try
        {
            _session = await DoLoginAsync(email, password, installDir, exePath, language, _cts.Token);
            PersistProfile(email, password, _pendingRefreshToken);
            AttachRefreshSaver();
            AfterLoginUI(email, password);
        }
        catch (OperationCanceledException)
        {
            SetStatus(L.Cancelled);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "last-error.txt"), $"[{DateTime.Now}] {ex}"); } catch { }
            SetStatus(L.ErrorPrefix + ex.Message + "\n(last-error.txt)");
        }
        finally
        {
            _cts.Dispose(); _cts = null;
            btnLogin.Text = L.LogIn;
            progressQueue.Visible = false;
        }
    }

    // Saves/removes the profile after a successful login (refresh token included).
    // Only one profile can be the auto-login profile.
    private void PersistProfile(string email, string password, string? refreshToken)
    {
        var list = new List<SsoProfile>();
        foreach (var p in _profiles)
        {
            if (p.Email.Equals(email, StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(chkAutoLogin.Checked && p.AutoLogin ? p with { AutoLogin = false } : p);
        }
        if (chkSavePassword.Checked)
            list.Add(new SsoProfile(email, password, refreshToken ?? "", chkAutoLogin.Checked));
        _profiles = list;
        ProfileStore.Save(_profiles);
        RefreshAccountCombo();
    }

    // Keeps the stored refresh token fresh when the API client rotates it mid-session.
    private void AttachRefreshSaver()
    {
        var s = _session;
        if (s == null) return;
        s.Api.OnRefreshTokenChanged = t =>
        {
            _profiles = _profiles
                .Select(p => p.Email.Equals(s.Email, StringComparison.OrdinalIgnoreCase) ? p with { RefreshToken = t } : p)
                .ToList();
            try { ProfileStore.Save(_profiles); } catch { }
            return Task.CompletedTask;
        };
    }

    private void AfterLoginUI(string email, string password)
    {
        if (chkSavePassword.Checked && !string.IsNullOrEmpty(password))
        {
            _maskedPassword = password;
            _passwordIsMasked = true;
            txtPassword.Text = new string('*', password.Length + 4);
        }
        if (chkSavePassword.Checked || chkRemember.Checked)
            SetMaskedEmail(email);
        chkAutoLogin.Checked = _profiles.Any(p => p.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && p.AutoLogin);
        btnStart.Enabled = true;
        SetStatus(L.LoggedInPressPlay);
        ShowPanel("discover");
    }

    // Auto-login on startup via saved refresh token (no password needed).
    private async Task TryAutoLoginAsync()
    {
        var auto = _profiles.FirstOrDefault(p => p.AutoLogin && !string.IsNullOrEmpty(p.RefreshToken));
        if (auto == null) return;
        string installDir = txtInstallDir.Text.Trim().Trim('"');
        string language = cmbLanguage.SelectedItem?.ToString() ?? "de";
        string exePath = Path.Combine(installDir, "SSOClient.exe");
        if (!File.Exists(exePath)) return;

        // Show which profile is used
        _suppressComboEvents = true;
        try
        {
            if (cmbEmail.Items.Contains(auto.Email)) cmbEmail.SelectedItem = auto.Email;
            else { cmbEmail.Items.Add(auto.Email); cmbEmail.SelectedItem = auto.Email; }
        }
        finally { _suppressComboEvents = false; }
        LoadProfile(auto);

        InvalidateSession();
        _cts = new CancellationTokenSource();
        btnLogin.Text = L.Cancel;
        progressQueue.Visible = true;
        try
        {
            _session = await DoAutoLoginAsync(auto.Email, auto.RefreshToken, installDir, exePath, language, _cts.Token);
            PersistProfile(auto.Email, auto.Password, _pendingRefreshToken);
            AttachRefreshSaver();
            AfterLoginUI(auto.Email, auto.Password);
        }
        catch (OperationCanceledException)
        {
            SetStatus(L.Cancelled);
        }
        catch (Exception ex)
        {
            // Refresh-Token tot (z.B. offizeller Launcher/Website hat rotiert)?
            // Fallback: gespeichertes Passwort nutzen, statt aufzugeben.
            if (!string.IsNullOrEmpty(auto.Password))
            {
                try
                {
                    SetStatus("Refresh token expired, logging in with password...");
                    InvalidateSession();
                    _session = await DoLoginAsync(auto.Email, auto.Password, installDir, exePath, language, _cts.Token);
                    PersistProfile(auto.Email, auto.Password, _pendingRefreshToken);
                    AttachRefreshSaver();
                    AfterLoginUI(auto.Email, auto.Password);
                    return;
                }
                catch (Exception ex2) { ex = ex2; }
            }
            // Refresh token dead -> disable auto-login for this profile, user logs in manually.
            _profiles = _profiles
                .Select(p => p.Email.Equals(auto.Email, StringComparison.OrdinalIgnoreCase) ? p with { AutoLogin = false, RefreshToken = "" } : p)
                .ToList();
            try { ProfileStore.Save(_profiles); } catch { }
            chkAutoLogin.Checked = false;
            ShowPanel("account");
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "last-error.txt"), $"[{DateTime.Now}] {ex}"); } catch { }
            SetStatus(L.AutoLoginFailed + "\n" + ex.Message);
        }
        finally
        {
            _cts.Dispose(); _cts = null;
            btnLogin.Text = L.LogIn;
            progressQueue.Visible = false;
        }
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_cts != null) { _cts.Cancel(); return; }
        if (_session == null)
        { SetStatus(L.LoginFirst); return; }
        // Credentials changed after login? Never start with the wrong account.
        // Note: auto-login sessions don't know the password -> only compare it when known.
        string curEmail = _emailIsMasked ? _maskedEmail : cmbEmail.Text.Trim();
        string curPass = _passwordIsMasked ? _maskedPassword : txtPassword.Text;
        bool emailOk = curEmail.Equals(_session.Email, StringComparison.OrdinalIgnoreCase);
        bool passOk = string.IsNullOrEmpty(_session.Password) || curPass == _session.Password;
        if (!emailOk || !passOk)
        {
            InvalidateSession();
            SetStatus(L.CredentialsChanged);
            return;
        }

        _cts = new CancellationTokenSource();
        btnStart.Text = L.Cancel; btnStart.Glyph = "";
        progressQueue.Visible = true;
        try
        {
            await DoPlayAsync(_session, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus(L.Cancelled);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "last-error.txt"), $"[{DateTime.Now}] {ex}"); } catch { }
            SetStatus(L.ErrorPrefix + ex.Message + "\n(last-error.txt)");
        }
        finally
        {
            _cts.Dispose(); _cts = null;
            btnStart.Text = L.Play; btnStart.Glyph = Glyphs.Play;
            progressQueue.Visible = false;
        }
    }

    // Step 1: login + load everything, show full account info. No game start.
    private async Task<LoginSession> DoLoginAsync(string email, string password, string installDir, string exePath, string language, CancellationToken ct)
    {
        string deviceId = chkRandomDevice.Checked ? RandomDeviceId() : GetOrCreateDeviceId();
        var api = new SsoApiClient(deviceId);
        bool ownsApi = true;
        string? latestRefresh = null;
        api.OnRefreshTokenChanged = t => { latestRefresh = t; return Task.CompletedTask; };
        try
        {
            SetStatus(L.StepLogin);
            await api.LoginAsync(email, password, ct);
            var session = await FinishLoginAsync(api, email, password, installDir, exePath, language, ct);
            ownsApi = false; // ownership moves to the returned session
            _pendingRefreshToken = latestRefresh;
            return session;
        }
        finally { if (ownsApi) api.Dispose(); }
    }

    // Auto-Login per Refresh-Token (kein Passwort nötig).
    private async Task<LoginSession> DoAutoLoginAsync(string email, string refreshToken, string installDir, string exePath, string language, CancellationToken ct)
    {
        string deviceId = GetOrCreateDeviceId();
        var api = new SsoApiClient(deviceId);
        bool ownsApi = true;
        string? latestRefresh = null;
        api.OnRefreshTokenChanged = t => { latestRefresh = t; return Task.CompletedTask; };
        try
        {
            SetStatus(L.StepAutoLogin);
            await api.RefreshSessionAsync(refreshToken, ct);
            var session = await FinishLoginAsync(api, email, "", installDir, exePath, language, ct);
            ownsApi = false;
            _pendingRefreshToken = latestRefresh;
            return session;
        }
        finally { if (ownsApi) api.Dispose(); }
    }

    private string? _pendingRefreshToken;

    private async Task<LoginSession> FinishLoginAsync(SsoApiClient api, string email, string password, string installDir, string exePath, string language, CancellationToken ct)
    {
        bool ownsApi = true;
        try
        {
            SetStatus(L.StepInit);
            JsonNode init = await api.InitializeUserAsync(ct);
            string accountId = SsoApiClient.GetString(init, "accountId", "account_id", "accountID", "id");
            string metricsGroups = SsoApiClient.GetString(init, "metricsGroups", "metrics_groups");
            if (string.IsNullOrEmpty(accountId))
                accountId = FindFirst(init, "accountId") ?? "";
            if (string.IsNullOrEmpty(accountId))
                throw new Exception("initializeUser returned no accountId. Response: " + init.ToJsonString());

            await ShowAccountInfoAsync(init, api, email, ct);

            SetStatus(L.StepConfig);
            string metricsUrl = "";
            string gameFilesEndpoint = "";
            string enableActor = "0";
            string enableNebula = "0";
            try
            {
                JsonNode rc = await api.GetRemoteConfigAsync(ct);
                metricsUrl = SsoApiClient.GetString(rc, "metricsUrl", "metrics_url");
                gameFilesEndpoint = SsoApiClient.GetString(rc, "gameFilesEndpoint", "game_files_endpoint");
                string a = SsoApiClient.GetString(rc, "enableActorSerializationOnDemand");
                string n = SsoApiClient.GetString(rc, "enableNebula");
                if (a.Equals("true", StringComparison.OrdinalIgnoreCase) || a == "1") enableActor = "1";
                if (n.Equals("true", StringComparison.OrdinalIgnoreCase) || n == "1") enableNebula = "1";
            }
            catch { /* optional */ }

            SetStatus(L.StepServer);
            string gameVersion = "";
            bool updateInProgress = false;
            try
            {
                JsonNode gs = await api.GetGameServerDataAsync(ct);
                gameVersion = SsoApiClient.GetString(gs, "gameVersion", "game_version")
                    ?? FindFirst(gs, "gameVersion") ?? "";
                string upd = SsoApiClient.GetString(gs, "updateInProgress", "update_in_progress")
                    ?? FindFirst(gs, "updateInProgress") ?? "";
                updateInProgress = upd.Equals("true", StringComparison.OrdinalIgnoreCase) || upd == "1";
                if (updateInProgress) SetServer($"● {L.Server}: {L.GameUpdateRunning}", ThemeColors.Gold);
                else
                {
                    string serverName = FindFirst(gs, "serverName") ?? FindFirst(gs, "currentServer")
                        ?? FindFirst(gs, "serverDisplayName") ?? "";
                    if (!string.IsNullOrEmpty(serverName)) SetServer($"● {L.Server}: {serverName}", ThemeColors.Success);
                }
            }
            catch { }

            var session = new LoginSession(api, email, password, accountId, metricsUrl, metricsGroups,
                enableActor, enableNebula, gameVersion, gameFilesEndpoint,
                installDir, exePath, language);
            ownsApi = false; // ownership moves to the returned session
            return session;
        }
        finally { if (ownsApi) api.Dispose(); }
    }

    // Step 2: queue + optional file check + version check + start. Needs a login session.
    private async Task DoPlayAsync(LoginSession s, CancellationToken ct)
    {
        var api = s.Api;

        if (chkVerify.Checked)
        {
            if (!string.IsNullOrEmpty(s.GameVersion) && !string.IsNullOrEmpty(s.GameFilesEndpoint))
                await RunGameLibCheckAsync(s.InstallDir, s.GameFilesEndpoint, s.GameVersion, ct);
            else
                SetStatus("File check skipped (no gameVersion/gameFilesEndpoint).");
        }

        SetStatus(L.LoginQueue);
        string? queueToken = null;
        var (passed, pos, token) = await api.CreateQueuePositionAsync(ct);
        queueToken = token;
        while (!passed)
        {
            ct.ThrowIfCancellationRequested();
            SetStatus(L.QueuePosition(pos));
            await Task.Delay(5000, ct);
            (passed, pos) = await api.CheckQueuePositionAsync(ct);
        }

        var args = new List<string>
        {
            $"-Language=\"{s.Language}\"",
            $"-NetworkUserId=\"{s.AccountId}\"",
            $"-NetworkLauncherHash=\"{api.SessionId}\"",
            $"-ProjectUserDataPath=\"{s.InstallDir}\"",
            $"-NetworkLauncherServer=\"{SsoApiClient.ApiEndpoint}\"",
        };
        if (!string.IsNullOrEmpty(s.MetricsUrl)) args.Add($"-MetricsServer=\"{s.MetricsUrl}\"");
        if (!string.IsNullOrEmpty(s.MetricsGroups)) args.Add($"-MetricsGroup=\"{s.MetricsGroups}\"");
        if (!string.IsNullOrEmpty(queueToken)) args.Add($"-LoginQueueToken=\"{queueToken}\"");
        args.Add($"-EnableActorSerializationOnDemand=\"{s.EnableActor}\"");
        args.Add($"-EnableNebula=\"{s.EnableNebula}\"");

        string argString = string.Join(" ", args);

        CheckClientVersion(s.InstallDir, s.GameVersion, ct);

        SetStatus(L.StartingClient);

        if (!File.Exists(s.ExePath))
            throw new Exception($"SSOClient.exe not found:\n{s.ExePath}");
        var psi = new ProcessStartInfo
        {
            FileName = s.ExePath,
            Arguments = argString,
            WorkingDirectory = s.InstallDir,
            UseShellExecute = false,
        };
        Process? p = Process.Start(psi);
        if (p == null) throw new Exception("Could not start process.");
        // Diagnose: stirbt das Spiel sofort wieder (falsche Tokens, zweite Instanz, ...)?
        await Task.Delay(5000, ct);
        p.Refresh();
        if (p.HasExited)
        {
            int code = -1;
            try { code = p.ExitCode; } catch { }
            throw new Exception($"SSOClient.exe exited immediately (code {code}).\n"
                + "Possible causes:\n"
                + "- Login/queue token expired -> press LOGIN again, then PLAY quickly\n"
                + "- Another SSOClient.exe is still running (check Task Manager)\n"
                + "- Client version outdated (enable file check or update via official launcher)");
        }

        SetStatus(L.GameRunning(p.Id));

        // Multi-account: rename the game window so instances can be told apart.
        _ = RenameGameWindowAsync(p, PublicName(_lastDisplayName));

        await Task.Delay(2000, ct);
        BeginInvoke(() => WindowState = FormWindowState.Minimized);
    }

    // Waits for the game window, then sets its title once (re-applied twice in case
    // the game resets it while loading). Fire-and-forget, never blocks.
    private static async Task RenameGameWindowAsync(Process game, string displayName)
    {
        try
        {
            string who = string.IsNullOrEmpty(displayName) ? $"PID {game.Id}" : displayName;
            string title = $"Star Stable Online - {who}";
            // Bis zu ~90s auf das Fenster warten (Spiel braucht zum Hochfahren)
            IntPtr handle = IntPtr.Zero;
            for (int i = 0; i < 90 && !game.HasExited; i++)
            {
                game.Refresh();
                handle = game.MainWindowHandle;
                if (handle != IntPtr.Zero) break;
                await Task.Delay(1000);
            }
            if (handle == IntPtr.Zero || game.HasExited) return;
            SetWindowText(handle, title);
            // Falls das Spiel den Titel beim Laden zurücksetzt, zweimal nachsetzen
            foreach (int waitMs in new[] { 10000, 30000 })
            {
                await Task.Delay(waitMs);
                if (game.HasExited) return;
                game.Refresh();
                if (game.MainWindowHandle != IntPtr.Zero)
                    SetWindowText(game.MainWindowHandle, title);
            }
        }
        catch { }
    }

    // Full account info after login (outputCode rules like the official launcher).
    // Coins + horses are awaited here with a short timeout: no more endless "loading...".
    private async Task ShowAccountInfoAsync(JsonNode init, SsoApiClient api, string loginEmail, CancellationToken ct)
    {
        try
        {
            string P(params string[] names) => SsoApiClient.GetString(init, names) ?? FindFirstAny(init, names) ?? "";
            string name = P("username", "email", "character");
            string accountId = P("accountId");
            int outputCode = 0;
            int.TryParse(P("outputCode", "output_code"), out outputCode);
            string rider = outputCode >= 70 ? "Lifetime Star Rider" : outputCode >= 50 ? "Star Rider" : "no Star Rider";
            if (outputCode == 3) rider += " (limited)";
            string V(string s) => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" ? L.Yes : L.No;
            string charName = FindCharacterName(init) ?? "";
            _realDisplayName = !string.IsNullOrEmpty(charName) ? charName : MaskEmail(name);
            _lastDisplayName = !string.IsNullOrEmpty(charName) ? charName : name;
            _outputCode = outputCode;
            if (InvokeRequired) BeginInvoke(ApplyBadgeStyle); else ApplyBadgeStyle();
            _accountName = name;
            _accountId = accountId;
            _accountStatusLine = $"{L.FieldStatus}: {rider}  •  {L.FieldVerified}: {V(P("isVerified", "is_verified"))}  •  {L.FieldMinor}: {V(P("isMinor", "is_minor"))}";
            string sub = P("subscriptionLevel", "subscription_level");
            string region = P("regionId", "region_id");
            string created = P("created");
            _accountExtraLine = string.Join("  •  ", new[] {
                string.IsNullOrEmpty(sub) ? "" : $"{L.FieldSubscription}: {sub}",
                string.IsNullOrEmpty(region) ? "" : $"{L.FieldRegion}: {region}",
                string.IsNullOrEmpty(created) ? "" : $"{L.FieldCreated}: {created}",
            }.Where(x => x != ""));
            _accountCoinsLine = $"{L.FieldStarCoins}: ...";
            _hasAccountData = true;
            RenderIdentity();
            // Coins + horses for info box and sidebar (best effort, max ~8s, never blocks longer).
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            {
                var c = linked.Token;
                try
                {
                    JsonNode coins = await api.GetStarCoinsAsync(c);
                    string? balance = FindFirst(coins, "starCoins") ?? FindFirst(coins, "balance") ?? FindFirst(coins, "amount");
                    _accountCoinsLine = $"{L.FieldStarCoins}: " + (string.IsNullOrEmpty(balance) ? L.NotAvailable : balance);
                    if (!string.IsNullOrEmpty(balance)) SetSideText(lblCoins, balance);
                }
                catch { _accountCoinsLine = $"{L.FieldStarCoins}: {L.NotAvailable}"; }
                try
                {
                    JsonNode horses = await api.GetPendingHorsesAsync(c);
                    int count = horses is JsonArray ha ? ha.Count : -1;
                    if (count >= 0) SetSideText(lblHorses, count.ToString());
                }
                catch { }
            }
            RenderIdentity();
        }
        catch { }
    }

    private static string? FindFirstAny(JsonNode? node, params string[] keys)
    {
        foreach (var k in keys)
        {
            var r = FindFirst(node, k);
            if (!string.IsNullOrEmpty(r)) return r;
        }
        return null;
    }

    // Vergleicht manifest.json (lokal) mit der Server-Version.
    // Unterschiedlich -> Warnung, User darf trotzdem starten. Niemals blockieren.
    private void CheckClientVersion(string installDir, string serverVersion, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(serverVersion)) return;
            string appDir = Directory.GetParent(installDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? installDir;
            string manifest = Path.Combine(appDir, "manifest.json");
            if (!File.Exists(manifest)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            string local = "";
            if (doc.RootElement.TryGetProperty("client", out var client)
                && client.TryGetProperty("version", out var ver))
                local = ver.GetString() ?? "";
            if (string.IsNullOrEmpty(local) || local == serverVersion) return;

            SetStatus($"Note: old game version detected.\nLocal: {local}\nServer: {serverVersion}");
            var res = MessageBox.Show(this,
                "Your game version is outdated.\n\n"
                + $"Installed: {local}\nLatest:      {serverVersion}\n\n"
                + "Please update (e.g. run the official launcher once)\n"
                + "to keep using the launcher normally.\n\n"
                + "Start with the old SSOClient.exe anyway?",
                "Old version",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (res != DialogResult.Yes)
                throw new OperationCanceledException();
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Check darf nie blockieren */ }
    }

    // Führt SSOGamelib.exe wie der offizielle Launcher aus (nativeBinaries.js):
    // SSOGamelib.exe --gameFilesEndpoint <url> --gameVersion <hash> --gameDirectory <appdir>
    //   --gameClientVariant client --patchBinPath <HDiffPatch.exe> --logLevel trace
    // Wird nur ausgeführt, wenn die Checkbox "Spieldateien prüfen" aktiv ist (Standard: aus).
    private async Task RunGameLibCheckAsync(string installDir, string gameFilesEndpoint, string gameVersion, CancellationToken ct)
    {
        string appDir = Directory.GetParent(installDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? installDir;
        string gameLib = Path.Combine(appDir, "SSOGamelib.exe");
        string patchBin = Path.Combine(appDir, "HDiffPatch.exe");
        if (!File.Exists(gameLib))
            throw new Exception($"SSOGamelib.exe not found:\n{gameLib}");
        if (!File.Exists(patchBin))
            throw new Exception($"HDiffPatch.exe not found:\n{patchBin}");

        string args = $"--gameFilesEndpoint \"{gameFilesEndpoint}\" --gameVersion \"{gameVersion}\" "
            + $"--gameDirectory \"{appDir}\" --gameClientVariant client --patchBinPath \"{patchBin}\" --logLevel trace";

        var psi = new ProcessStartInfo
        {
            FileName = gameLib,
            Arguments = args,
            WorkingDirectory = appDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var done = new TaskCompletionSource<int>();
        proc.Exited += (_, _) => done.TrySetResult(proc.ExitCode);
        proc.OutputDataReceived += (_, ev) =>
        {
            if (string.IsNullOrEmpty(ev.Data)) return;
            // Offizieller Launcher parst: "Installation type: X" und "X progress: N%"
            if (ev.Data.Contains("Installation type:"))
                SetStatus($"File check: {ev.Data.Trim()}");
            else if (ev.Data.Contains("progress:"))
                SetStatus($"File check: {ev.Data.Trim()}");
        };
        proc.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrEmpty(ev.Data)) SetStatus($"File check: {ev.Data.Trim()}");
        };
        if (!proc.Start()) throw new Exception("Could not start SSOGamelib.exe.");
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        using (ct.Register(() => { try { proc.Kill(); } catch { } done.TrySetCanceled(); }))
            await done.Task;
        if (proc.ExitCode != 0)
            throw new Exception($"SSOGamelib.exe reported an error (exit code {proc.ExitCode}). Game will not start.");
        SetStatus(L.FileCheckOk);
    }

    // Nur Skalare zurückgeben – keine Objekte/Arrays (sonst "{...}" als Text).
    private static string? FindFirst(JsonNode? node, string key)
    {
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                if (kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    string? s = SsoApiClient.ScalarString(kv.Value);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                var r = FindFirst(kv.Value, key);
                if (r != null) return r;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var r = FindFirst(item, key);
                if (r != null) return r;
            }
        }
        return null;
    }

    private void SetStatus(string s)
    {
        if (InvokeRequired) { BeginInvoke(() => lblStatus.Text = s); }
        else lblStatus.Text = s;
    }

    // Zufällige Geräte-ID im gleichen Format (64 Hex-Zeichen). Achtung: unbekanntes
    // Gerät -> Server verlangt Verifizierung ("verification pending").
    private static string RandomDeviceId()
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Wie der offizielle Launcher (node-machine-id): SHA256(MachineGuid) als Hex-String.
    // Die rohe GUID zu senden erzeugt ein "unbekanntes Gerät" -> Server fordert Verifizierung.
    private static string GetOrCreateDeviceId()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            string? guid = key?.GetValue("MachineGuid")?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(guid));
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
        }
        catch { }
        try
        {
            string f = Path.Combine(AppContext.BaseDirectory, "device.id");
            if (File.Exists(f)) return File.ReadAllText(f).Trim();
            string id = Guid.NewGuid().ToString();
            File.WriteAllText(f, id);
            return id;
        }
        catch { return Guid.NewGuid().ToString(); }
    }
}
