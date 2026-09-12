using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SSOLauncher;

internal sealed class SsoApiClient : IDisposable
{
    public const string ApiEndpoint = "https://launcher-proxy.starstable.com";
    public const string GatewayEndpoint = "https://lb-pub.prod.starstable.com/api-gateway/1.0";
    private const string UpdateFeedUrl = "https://launcher-release-prod.starstable.com/latest.yml";
    private const string FallbackLauncherVersion = "2.59.0";
    private string? _launcherVersion;

    // Muss wie Electron/Chromium aussehen, sonst blockt das Gateway mit
    // 401 "failed to verify origin: integrity check failed".
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36 Electron/34.0.0";

    private readonly HttpClient _http;
    private readonly string _deviceId;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? SessionId { get; private set; }

    // Wird aufgerufen, sobald ein neues Refresh-Token reinkommt (Login/Refresh),
    // damit die UI es persistent speichern kann.
    public Func<string, Task>? OnRefreshTokenChanged { get; set; }

    private bool _refreshing;
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    public SsoApiClient(string deviceId)
    {
        _deviceId = deviceId;
        var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
        _http = new HttpClient(handler);
        _http.DefaultRequestHeaders.Add("Device-ID", deviceId);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE,de;q=0.9,en;q=0.8");
    }

    public void Dispose() => _http.Dispose();

    private async Task<JsonNode> PostAsync(string url, object? body, bool withAuth, CancellationToken ct)
    {
        if (withAuth) await EnsureValidAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (withAuth && AccessToken != null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (body != null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body, body.GetType(), AppJsonContext.Default),
                Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        string text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"POST {url} -> {(int)res.StatusCode} {text}");
        return JsonNode.Parse(text) ?? new JsonObject();
    }

    private async Task<JsonNode> GetAsync(string url, bool withAuth, CancellationToken ct)
    {
        if (withAuth) await EnsureValidAccessTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (withAuth && AccessToken != null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);

        using var res = await _http.SendAsync(req, ct);
        string text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"GET {url} -> {(int)res.StatusCode} {text}");
        return JsonNode.Parse(text) ?? new JsonObject();
    }

    public async Task LoginAsync(string email, string password, CancellationToken ct)
    {
        JsonNode json;
        try
        {
            json = await PostAsync($"{GatewayEndpoint}/session/create",
                new LoginRequestBody { Credentials = new LoginCredentials { Email = email, Password = password } },
                withAuth: false, ct);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("invalid credentials"))
        {
            throw new Exception("Login failed: wrong email or password.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("verification pending"))
        {
            throw new Exception("Login blocked: verification is pending for this account (\"verification pending\").\n"
                + "What to do:\n"
                + "1) Check your inbox (incl. spam): Star Stable probably just sent a verification mail -> click the link.\n"
                + "2) Then press \"Login\" again.\n"
                + "3) No mail? Log in at starstable.com and finish verification / request a new mail.\n"
                + "Details in last-error.txt.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("verification required"))
        {
            throw new Exception("Login blocked: the account requires verification (\"verification required\").\n"
                + "This comes from the Star Stable server, not the launcher. Possible reasons:\n"
                + "1) Email not confirmed yet -> check inbox, click the confirmation link.\n"
                + "2) For minors: confirm the parent email.\n"
                + "3) Too many attempts / unusual login -> wait 15-30 min, then try the official launcher or website.\n"
                + "Full server response is in last-error.txt next to this exe.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("integrity check"))
        {
            throw new Exception("Login blocked (integrity check). Please update the launcher / report this.");
        }

        AccessToken = json["access_token"]?.GetValue<string>();
        RefreshToken = json["refresh_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(AccessToken))
            throw new Exception("Login failed: no access_token received.");

        SessionId = DecodeSessionId(AccessToken);
        if (string.IsNullOrEmpty(SessionId))
            throw new Exception("Login failed: no session_id found in token.");
        if (OnRefreshTokenChanged != null && !string.IsNullOrEmpty(RefreshToken))
            await OnRefreshTokenChanged(RefreshToken);
    }

    // Auto-Login: neue Tokens per Refresh-Token holen (wie offizieller Launcher).
    public async Task RefreshSessionAsync(string refreshToken, CancellationToken ct)
    {
        JsonNode json;
        try
        {
            json = await PostAsync($"{GatewayEndpoint}/session/refresh",
                new RefreshRequestBody { RefreshToken = refreshToken }, withAuth: false, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception("Auto-login failed (refresh token invalid/expired). Please log in manually."
                + "\nDetails: " + ex.Message);
        }
        AttachTokens(json["access_token"]?.GetValue<string>(), json["refresh_token"]?.GetValue<string>());
        if (string.IsNullOrEmpty(AccessToken))
            throw new Exception("Auto-login failed: no access_token received.");
        if (OnRefreshTokenChanged != null && !string.IsNullOrEmpty(RefreshToken))
            await OnRefreshTokenChanged(RefreshToken);
    }

    // Bereits vorhandene Tokens übernehmen (z.B. nach Refresh).
    public void AttachTokens(string? accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        if (!string.IsNullOrEmpty(refreshToken)) RefreshToken = refreshToken;
        SessionId = string.IsNullOrEmpty(accessToken) ? null : DecodeSessionId(accessToken);
    }

    // Wie offizieller Launcher (ensureValidAccessToken): läuft das Token in <5 Min ab,
    // still refreshen. Wird automatisch vor jedem authentifizierten Request geprüft.
    public async Task EnsureValidAccessTokenAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(AccessToken) || string.IsNullOrEmpty(RefreshToken)) return;
        DateTime exp = JwtExpiry(AccessToken);
        if (exp == DateTime.MinValue || exp - DateTime.UtcNow > RefreshMargin) return;
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            JsonNode json = await PostAsync($"{GatewayEndpoint}/session/refresh",
                new RefreshRequestBody { RefreshToken = RefreshToken! }, withAuth: false, ct);
            AttachTokens(json["access_token"]?.GetValue<string>(), json["refresh_token"]?.GetValue<string>());
            if (OnRefreshTokenChanged != null && !string.IsNullOrEmpty(RefreshToken))
                await OnRefreshTokenChanged(RefreshToken);
        }
        finally { _refreshing = false; }
    }

    private static DateTime JwtExpiry(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return DateTime.MinValue;
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
            var node = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            long exp = node?["exp"]?.GetValue<long>() ?? 0;
            if (exp <= 0) return DateTime.MinValue;
            return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
        }
        catch { return DateTime.MinValue; }
    }

    // Holt die aktuellste Launcher-Version automatisch vom Update-Feed (latest.yml).
    // Fallback: hartcodierte Version, damit nie blockiert wird.
    public async Task<string> GetLauncherVersionAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_launcherVersion)) return _launcherVersion;
        try
        {
            using var res = await _http.GetAsync(UpdateFeedUrl, ct);
            if (res.IsSuccessStatusCode)
            {
                string yml = await res.Content.ReadAsStringAsync(ct);
                foreach (var line in yml.Split('\n'))
                {
                    string t = line.Trim();
                    if (t.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = t.Substring("version:".Length).Trim().Trim('\'', '"');
                        if (System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+"))
                        {
                            _launcherVersion = v;
                            return v;
                        }
                    }
                }
            }
        }
        catch { /* ignorieren, Fallback nutzen */ }
        _launcherVersion = FallbackLauncherVersion;
        return _launcherVersion;
    }

    public async Task<JsonNode> InitializeUserAsync(CancellationToken ct)
    {
        string launcherVersion = await GetLauncherVersionAsync(ct);
        var body = new InitializeUserBody
        {
            DeviceId = _deviceId,
            LauncherVersion = launcherVersion,
            ClientOsRelease = Environment.OSVersion.ToString(),
        };
        return await PostAsync($"{ApiEndpoint}/launcher/auth/initialize", body, withAuth: true, ct);
    }

    public async Task<JsonNode> GetRemoteConfigAsync(CancellationToken ct)
    {
        // Offizieller Launcher ruft das ohne Auth-Refresh auf, mit Token geht es aber auch.
        try
        {
            return await GetAsync($"{ApiEndpoint}/launcher/config", withAuth: true, ct);
        }
        catch
        {
            return await GetAsync($"{ApiEndpoint}/launcher/config", withAuth: false, ct);
        }
    }

    public async Task<JsonNode> GetGameServerDataAsync(CancellationToken ct)
    {
        // -> enthält u.a. gameVersion + updateInProgress (für SSOGamelib.exe)
        return await GetAsync($"{ApiEndpoint}/launcher/game-server/token", withAuth: true, ct);
    }

    public async Task<JsonNode> GetStarCoinsAsync(CancellationToken ct)
    {
        return await GetAsync($"{ApiEndpoint}/launcher/star-coins/token", withAuth: true, ct);
    }

    public async Task<JsonNode> GetPendingHorsesAsync(CancellationToken ct)
    {
        return await GetAsync($"{GatewayEndpoint}/horses/pending?claimed=false", withAuth: true, ct);
    }

    public async Task<JsonNode> GetNewsAsync(string language, int first, CancellationToken ct)
    {
        string url = $"{ApiEndpoint}/launcher/news/desktop?languageCode={Uri.EscapeDataString(language)}&first={first}&skip=0";
        return await GetAsync(url, withAuth: AccessToken != null, ct);
    }

    public async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken ct)
    {
        try
        {
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadAsByteArrayAsync(ct);
        }
        catch { return null; }
    }

    // Echte Form: {"articles":[{"headline","preamble","heroImage":{"responsiveImage":{"src"}}}, ...]}
    public static List<(string Title, string Text, string Image)> ParseNews(JsonNode? root, int max)
    {
        var out_ = new List<(string, string, string)>();
        try
        {
            var arr = FindFirstArray(root);
            if (arr == null) return out_;
            foreach (var item in arr.Take(max))
            {
                string title = Pick(item, "headline", "title", "name") ?? "(no title)";
                string text = Pick(item, "preamble", "summary", "excerpt", "description", "body") ?? "";
                if (text.Length > 140) text = text[..137] + "...";
                string image = FindImageUrl(item) ?? "";
                out_.Add((title, text, image));
            }
        }
        catch { }
        return out_;
    }

    private static JsonArray? FindFirstArray(JsonNode? node)
    {
        if (node is JsonArray arr) return arr;
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                var r = FindFirstArray(kv.Value);
                if (r != null) return r;
            }
        }
        return null;
    }

    private static string? Pick(JsonNode? item, params string[] keys)
    {
        if (item is not JsonObject obj) return null;
        foreach (var k in keys)
        {
            if (obj.TryGetPropertyValue(k, out var v))
            {
                string? s = ScalarString(v);
                if (!string.IsNullOrEmpty(s)) return s.Trim();
            }
        }
        return null;
    }

    // Rekursive Bildsuche: findet z.B. heroImage.responsiveImage.src
    private static string? FindImageUrl(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // Direkte Treffer zuerst
            foreach (var k in new[] { "src", "url", "href", "image", "imageUrl", "thumbnail", "cover" })
            {
                if (obj.TryGetPropertyValue(k, out var v))
                {
                    if (v is JsonValue)
                    {
                        string? s = ScalarString(v);
                        if (!string.IsNullOrEmpty(s) && s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            return s;
                    }
                }
            }
            foreach (var kv in obj)
            {
                string? r = FindImageUrl(kv.Value);
                if (r != null) return r;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                string? r = FindImageUrl(item);
                if (r != null) return r;
            }
        }
        return null;
    }

    public async Task<(bool Passed, int Position, string? QueueToken)> CreateQueuePositionAsync(CancellationToken ct)
    {
        // Wie offizieller Launcher: POST ohne Body
        var json = await PostAsync($"{ApiEndpoint}/launcher/login-queue/v2/desktop/token",
            null, withAuth: true, ct);
        return (
            json["passedTheQueue"]?.GetValue<bool>() ?? false,
            json["queuePosition"]?.GetValue<int>() ?? 0,
            json["queueToken"]?.GetValue<string>()
        );
    }

    public async Task<(bool Passed, int Position)> CheckQueuePositionAsync(CancellationToken ct)
    {
        var json = await GetAsync($"{ApiEndpoint}/launcher/login-queue/token", withAuth: true, ct);
        return (
            json["passedTheQueue"]?.GetValue<bool>() ?? false,
            json["queuePosition"]?.GetValue<int>() ?? 0
        );
    }

    public async Task DeleteQueueTokenAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiEndpoint}/launcher/login-queue/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            using var res = await _http.SendAsync(req);
        }
        catch { /* ignorieren */ }
    }

    public static string? DecodeSessionId(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
            var node = JsonNode.Parse(Encoding.UTF8.GetString(bytes));
            return node?["session_id"]?.GetValue<string>();
        }
        catch { return null; }
    }

    public static string GetString(JsonNode? root, params string[] names)
    {
        if (root == null) return "";
        foreach (var n in names)
        {
            JsonNode? v = root is JsonObject obj && obj.TryGetPropertyValue(n, out var found) ? found : null;
            string? s = ScalarString(v);
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return "";
    }

    // Nur Skalare (string/number/bool) – Objekte/Arrays werden ignoriert,
    // damit nie JSON ("{...}") als Text zurückkommt.
    internal static string? ScalarString(JsonNode? v)
    {
        if (v is JsonValue jv)
        {
            try
            {
                if (jv.TryGetValue<string>(out var s) && !string.IsNullOrEmpty(s)) return s;
                if (jv.TryGetValue<long>(out var l)) return l.ToString();
                if (jv.TryGetValue<bool>(out var b)) return b ? "true" : "false";
                if (jv.TryGetValue<double>(out var d)) return d.ToString();
            }
            catch { }
        }
        return null;
    }
}
