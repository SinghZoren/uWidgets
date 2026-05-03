using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spotify.Models;

namespace Spotify.Services;

public static class SpotifyOAuthService
{
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string Scopes = "user-read-playback-state user-read-currently-playing user-modify-playback-state playlist-read-private playlist-read-collaborative";
    private static readonly HttpClient Http = new();

    public static async Task<SpotifyTokenResponse> AuthorizeAsync(
        string clientId,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Spotify client ID is required.");

        redirectUri = NormalizeRedirectUri(redirectUri);
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = CreateCodeVerifier()[..24];
        var authUrl = BuildAuthorizeUrl(clientId, redirectUri, challenge, state);

        using var listener = CreateListener(redirectUri);
        listener.Start();

        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);
        var code = context.Request.QueryString["code"];
        var returnedState = context.Request.QueryString["state"];
        var error = context.Request.QueryString["error"];

        await WriteBrowserResponseAsync(context.Response, string.IsNullOrWhiteSpace(error));

        if (!string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException($"Spotify authorization failed: {error}");

        if (string.IsNullOrWhiteSpace(code) || returnedState != state)
            throw new InvalidOperationException("Spotify authorization response was invalid.");

        return await ExchangeCodeAsync(clientId, redirectUri, code, verifier, cancellationToken);
    }

    public static async Task<SpotifyTokenResponse> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Spotify refresh needs a client ID and refresh token.");

        var body = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        using var response = await Http.PostAsync(TokenUrl, new FormUrlEncodedContent(body), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ParseSpotifyError(json, "Spotify token refresh failed."));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 3600;
        var nextRefreshToken = root.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString() ?? refreshToken
            : refreshToken;

        return new SpotifyTokenResponse(
            accessToken,
            nextRefreshToken,
            DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 30)));
    }

    private static async Task<SpotifyTokenResponse> ExchangeCodeAsync(
        string clientId,
        string redirectUri,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier
        };

        using var response = await Http.PostAsync(TokenUrl, new FormUrlEncodedContent(body), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ParseSpotifyError(json, "Spotify authorization token exchange failed."));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var refreshToken = root.GetProperty("refresh_token").GetString() ?? "";
        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 3600;

        return new SpotifyTokenResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 30)));
    }

    private static string BuildAuthorizeUrl(string clientId, string redirectUri, string challenge, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = challenge,
            ["scope"] = Scopes,
            ["state"] = state
        };

        return $"{AuthorizeUrl}?{string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"))}";
    }

    private static HttpListener CreateListener(string redirectUri)
    {
        var uri = new Uri(redirectUri);
        var prefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath.TrimEnd('/')}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        return listener;
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, bool success)
    {
        var message = success
            ? "Spotify connected. You can close this tab and return to uWidgets."
            : "Spotify connection failed. You can close this tab and return to uWidgets.";
        var bytes = Encoding.UTF8.GetBytes($"<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;background:#111;color:#ddd;padding:32px\"><h2>{message}</h2></body></html>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static string NormalizeRedirectUri(string redirectUri)
    {
        redirectUri = string.IsNullOrWhiteSpace(redirectUri)
            ? "http://127.0.0.1:55432/callback/"
            : redirectUri.Trim();

        return redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";
    }

    private static string CreateCodeVerifier()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string ParseSpotifyError(string json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("error_description", out var description))
                return description.GetString() ?? fallback;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? fallback;
                if (error.TryGetProperty("message", out var message))
                    return message.GetString() ?? fallback;
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
