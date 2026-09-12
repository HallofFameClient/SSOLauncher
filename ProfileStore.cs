using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SSOLauncher;

internal sealed record SsoProfile(string Email, string Password, string RefreshToken = "", bool AutoLogin = false);

// Mehrere Accounts, DPAPI-verschlüsselt (nur dieser Windows-Benutzer kann lesen).
// Migriert alte Formate (credentials.dat, email.txt) beim ersten Laden.
internal static class ProfileStore
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "profiles.dat");
    private static string LegacyPath => Path.Combine(AppContext.BaseDirectory, "credentials.dat");
    private static string LegacyEmailPath => Path.Combine(AppContext.BaseDirectory, "email.txt");

    public static List<SsoProfile> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                byte[] enc = File.ReadAllBytes(FilePath);
                byte[] plain = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
                var list = new List<SsoProfile>();
                foreach (var el in doc.RootElement.GetProperty("profiles").EnumerateArray())
                {
                    string email = el.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
                    string pass = el.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
                    string refresh = el.TryGetProperty("refreshToken", out var r) ? r.GetString() ?? "" : "";
                    bool auto = el.TryGetProperty("autoLogin", out var a) && a.ValueKind == JsonValueKind.True;
                    if (string.IsNullOrEmpty(email)) continue;
                    if (string.IsNullOrEmpty(pass) && string.IsNullOrEmpty(refresh)) continue;
                    list.Add(new SsoProfile(email, pass, refresh, auto));
                }
                return list;
            }
            // Migration: altes credentials.dat / email.txt übernehmen
            var migrated = new List<SsoProfile>();
            try
            {
                if (File.Exists(LegacyPath))
                {
                    byte[] enc = File.ReadAllBytes(LegacyPath);
                    byte[] plain = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                    using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
                    string email = doc.RootElement.GetProperty("email").GetString() ?? "";
                    string pass = doc.RootElement.GetProperty("password").GetString() ?? "";
                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(pass))
                        migrated.Add(new SsoProfile(email, pass));
                    File.Delete(LegacyPath);
                }
            }
            catch { }
            if (migrated.Count > 0) { Save(migrated); return migrated; }
        }
        catch { }
        return new List<SsoProfile>();
    }

    public static void Save(List<SsoProfile> profiles)
    {
        var dto = new ProfileFileDto
        {
            Profiles = profiles.Select(p => new ProfileEntryDto
            {
                Email = p.Email, Password = p.Password, RefreshToken = p.RefreshToken, AutoLogin = p.AutoLogin
            }).ToArray()
        };
        string json = JsonSerializer.Serialize(dto, AppJsonContext.Default.ProfileFileDto);
        byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, enc);
    }

    public static string? LegacyEmail()
    {
        try
        {
            if (File.Exists(LegacyEmailPath))
            {
                string mail = File.ReadAllText(LegacyEmailPath).Trim();
                if (!string.IsNullOrEmpty(mail)) return mail;
            }
        }
        catch { }
        return null;
    }
}
