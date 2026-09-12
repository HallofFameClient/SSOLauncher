# SSOLauncher — Unofficial Desktop Launcher for Star Stable Online

> **Disclaimer: Not affiliated with Star Stable Entertainment AB.**
> This is an unofficial, open-source community project. All game assets, trademarks, and server infrastructure belong to Star Stable Entertainment AB.

SSOLauncher is a lightweight, modern desktop launcher for **Star Stable Online (SSO)** on Windows. It replicates the official launcher flow — login, queue, config, game start — but as a single self-contained `.exe` with multi-account support, streamer privacy, auto-login, and file verification.

Built with **.NET 8 + WinForms + Native AOT**. No Electron, no installer, no DLL mess.

Current version: **1.10.0** — Product name: `Tevvez SSO Custom Launcher`

---

## ✨ Features

**Core launcher flow (like the official launcher):**

- Email + password login via official `api-gateway` (`session/create`)
- Automatic token refresh (`session/refresh`, 5-min margin, like `ensureValidAccessToken`)
- `initializeUser`, `remoteConfig`, `game-server/token`
- Login queue support (`login-queue/v2/desktop/token` + polling)
- Starts `SSOClient.exe` with correct args: `-Language`, `-NetworkUserId`, `-NetworkLauncherHash`, `-ProjectUserDataPath`, `-NetworkLauncherServer`, `-MetricsServer`, `-LoginQueueToken`, `-EnableActorSerializationOnDemand`, `-EnableNebula`
- Launcher version auto-detected from `launcher-release-prod.starstable.com/latest.yml` with fallback

**Quality of life:**

- **Multi-account profiles** — save multiple logins, switch via dropdown
- **Auto-login on start** via refresh token (no password needed), with password fallback if token expired
- **DPAPI-encrypted storage** — `profiles.dat` can only be read by your Windows user
- **Discover / News feed** — real articles from `launcher/news/desktop` with async images, 14 languages
- **Live server status** — Online / Maintenance / Disabled / Game update in progress, no login required
- **Account dashboard** — Star Rider / Lifetime Star Rider / Free badge, verified/minor status, subscription, region, Star Coins balance, pending horses
- **Game file verification** — runs `SSOGamelib.exe` exactly like the official launcher (optional checkbox)
- **Client version check** — compares local `manifest.json` vs server `gameVersion`, warns but never blocks
- **Multi-client window titles** — renames `SSOClient.exe` window to `Star Stable Online - <Character>` so you can tell instances apart
- **Minimize to tray** instead of closing
- **Dark mode + modern custom controls**
- **Streamer mode** — replaces character name, email and account ID with random aliases (`Aurora Starrider`, `***@***`, random ID) in UI + game window title. No re-login needed to toggle.
- **14 UI languages:** en, de, sv, fr, es, it, nl, pl, pt, fi, da, no, hu, ru — game language and launcher language are separate settings
- **Single-file Native AOT exe** — `dotnet publish -r win-x64` → one exe, no DLLs, fast startup

**Privacy built-in:**

- Password field masks saved passwords (`********`), never reveals them on Show-toggle
- Email is shown masked (`m***@***`) after login
- Saved account dropdown is numbered (`Account 1`, `Account 2`) in streamer mode
- `last-error.txt` for debugging, no telemetry

---

## 📸 Screenshots

> Add yours here:

```text
/screenshots/discover.png
/screenshots/account.png
/screenshots/settings-dark.png
```

---

## 🚀 Quick Start

1. Download the latest release (`SSOLauncher.exe`) or build it yourself (see below).
2. Place `SSOLauncher.exe` anywhere (e.g. next to your game, or in its own folder).
3. Start it, go to **Settings** → **Browse** and select your client folder:

   ```text
   ...\Star Stable Online\client
   ```

   This is the folder containing `SSOClient.exe`.
4. Go to **Account**, enter email + password, press **LOG IN**.
5. Press **PLAY**.

That's it. The game starts, the launcher minimizes itself.

### Requirements

- Windows 10 / 11 x64
- Installed Star Stable Online (Steam or standalone — you just need `SSOClient.exe`, `SSOGamelib.exe`, `HDiffPatch.exe`)
- No .NET install needed for the AOT release build (self-contained)

---

## 🔨 Build from source

You need the **.NET 8 SDK**. For Native AOT you also need **Visual Studio with "Desktop development with C++"** (for `link.exe`).

```bat
build.bat         :: Native AOT release -> single exe in bin\x64\Release\...\win-x64\publish\
build.bat run     :: build + start
build.bat debug   :: plain JIT Debug build (fast, no C++ needed)
```

Manual:

```bat
:: fast dev build
dotnet build -c Debug

:: release single-file
"C:\Program Files\Microsoft Visual Studio\...\vcvarsall.bat" x64
dotnet publish -r win-x64 -c Release
```

Output: `bin\x64\Release\net8.0-windows\win-x64\publish\SSOLauncher.exe` — single file, no DLLs needed.

---

## ⚙️ Settings & Files

All files live next to the exe:

| File | Content |
|------|---------|
| `settings.json` | Plain JSON: `InstallDir`, `Language`, `LauncherLanguage`, `VerifyFiles`, `RandomDevice`, `MinimizeToTray`, `DarkMode`, `StreamerMode`, window size |
| `profiles.dat` | DPAPI-encrypted (`CurrentUser` scope): email, password, refreshToken, autoLogin flag |
| `last-error.txt` | Last exception with stacktrace, for bug reports |
| `email.txt` (legacy) | Only if "Remember email" was used in old versions, auto-migrated |

Checkboxes explained:

- `Verify game files before start` — runs `SSOGamelib.exe --gameFilesEndpoint ... --gameVersion ... --gameDirectory ... --gameClientVariant client ...`
- `Random device ID` — logs in as unknown device. Warning: server will require email verification ("verification pending").
- `Save login securely` — stores profile in `profiles.dat`
- `Auto-login` — only one profile can have this, uses refresh token on next start

---

## 🔒 Security & Privacy

- Passwords and refresh tokens are encrypted with Windows DPAPI (`ProtectedData.Protect(..., CurrentUser)`). They cannot be decrypted on another PC or by another Windows user.
- Tokens are kept in memory only and refreshed automatically.
- The launcher talks only to:
  - `https://lb-pub.prod.starstable.com/api-gateway/1.0`
  - `https://launcher-proxy.starstable.com`
  - `https://launcher-release-prod.starstable.com/latest.yml`
  - News images from Star Stable CDN
- User-Agent spoofs Electron/Chromium (`... Chrome/132 Electron/34`) — required by the gateway integrity check, otherwise `401 integrity check failed`.
- This project contains no cracks, no game file patches, no bypasses. It uses the same public API as the official launcher.

If you share screenshots / streams: enable **Streamer mode** in Settings.

---

## 🧨 Troubleshooting

**"verification pending"**

> Check your inbox + spam for a Star Stable verification mail, click the link, press Login again. Happens especially with `Random device ID`.

**"verification required"**

> Email not confirmed, parental confirmation missing, or too many attempts. Log in at starstable.com once, wait 15-30 min.

**"SSOClient.exe exited immediately (code X)"**

> 1. Press LOGIN again, then PLAY quickly (queue token expired)
> 2. Kill old `SSOClient.exe` in Task Manager (only one instance)
> 3. Enable file check or update via official launcher (outdated client)

**"Old game version detected"**

> Local `manifest.json` ≠ server `gameVersion`. Update once via official launcher, or press Yes to start anyway.

**"Auto-login failed"**

> Refresh token was rotated (e.g. you logged in via official launcher/website). The launcher tries the saved password as fallback, otherwise just log in manually once.

Full error is always in `last-error.txt` next to the exe — include it in bug reports.

---

## 🗺️ Roadmap

- [ ] Auto-update check for SSOLauncher itself
- [ ] Horse / character avatars in sidebar
- [ ] Patch notes diff view
- [ ] Portable `settings.json` encryption option
- [ ] Linux / macOS support (blocked by SSOClient itself — Windows only)

PRs and issues welcome.

---

## 🤝 Contributing

1. Fork, create branch: `git checkout -b feat/my-feature`
2. `build.bat debug` for fast iteration
3. Keep Native AOT compatibility: no resx, no data binding, no TypeConverters, use `AppJsonContext` source-gen for JSON
4. Open PR with screenshot + `last-error.txt` if relevant

---

## 📄 License

GPL-3.0 — see [LICENSE](LICENSE).

Copyright (c) 2026 Tevvez

---

## 🙏 Credits

- Star Stable Entertainment AB for the game — again, this launcher is unofficial.
- Community testers and translators.
