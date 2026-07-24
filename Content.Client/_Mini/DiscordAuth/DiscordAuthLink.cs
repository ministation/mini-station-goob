using Content.Shared._CorvaxGoob.CCCVars;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Client._Mini.DiscordAuth;

/// <summary>
/// Opens the Discord auth page for the local player. Shared by lobby / escape / donate UI.
/// </summary>
public static class DiscordAuthLink
{
    /// <summary>
    /// Fallback when the replicated CVar is missing (misconfigured server / late replicate).
    /// </summary>
    public const string FallbackApiUrl = "http://85.192.49.3:5001";

    public static bool TryOpen(IConfigurationManager cfg, IPlayerManager players, IUriOpener uriOpener)
    {
        var userId = players.LocalSession?.UserId;
        if (userId is null)
        {
            Logger.WarningS("discord_auth", "Cannot open auth: no local session");
            return false;
        }

        var apiUrl = cfg.GetCVar(CCCVars.DiscordAuthApiUrl);
        if (string.IsNullOrWhiteSpace(apiUrl))
            apiUrl = FallbackApiUrl;

        apiUrl = apiUrl.TrimEnd('/');
        var requestUrl = $"{apiUrl}/login/{userId.Value}";

        // Client sandbox forbids System.UriKind / Uri.TryCreate — validate with a simple prefix check.
        if (!IsHttpUrl(requestUrl))
        {
            Logger.ErrorS("discord_auth", $"Invalid auth URL: {requestUrl}");
            return false;
        }

        try
        {
            uriOpener.OpenUri(requestUrl);
            return true;
        }
        catch (Exception e)
        {
            Logger.ErrorS("discord_auth", $"Failed to open auth URL: {e}");
            return false;
        }
    }

    private static bool IsHttpUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
