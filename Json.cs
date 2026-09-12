using System.Text.Json.Serialization;

namespace SSOLauncher;

// Request bodies sent to the SSO API. Kept as concrete types (instead of anonymous
// objects) so serialization can go through the source-generated context below -
// required for Native AOT, where reflection-based JsonSerializer isn't safe.
internal sealed class LoginRequestBody
{
    public LoginCredentials Credentials { get; set; } = new();
}

internal sealed class LoginCredentials
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

internal sealed class RefreshRequestBody
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
}

internal sealed class InitializeUserBody
{
    public string DeviceId { get; set; } = "";
    public string LauncherVersion { get; set; } = "";
    public string LauncherPlatform { get; set; } = "desktop";
    public string ClientOsRelease { get; set; } = "";
    public string BrowserFamily { get; set; } = "Electron";
}

// On-disk shape of profiles.dat (encrypted with DPAPI, see ProfileStore).
internal sealed class ProfileFileDto
{
    public ProfileEntryDto[] Profiles { get; set; } = Array.Empty<ProfileEntryDto>();
}

internal sealed class ProfileEntryDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public bool AutoLogin { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(LauncherSettings))]
[JsonSerializable(typeof(ProfileFileDto))]
[JsonSerializable(typeof(LoginRequestBody))]
[JsonSerializable(typeof(RefreshRequestBody))]
[JsonSerializable(typeof(InitializeUserBody))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
