using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Spotify.Models;

namespace Spotify.Services;

public static class SpotifyWebApi
{
    private const string ApiBase = "https://api.spotify.com/v1";
    private static readonly HttpClient Http = new();

    public static async Task<SpotifyPlaybackModel> GetPlaybackAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ApiBase}/me/player", accessToken, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return SpotifyPlaybackModel.Empty;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Could not read Spotify playback.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("item", out var item) || item.ValueKind == JsonValueKind.Null)
            return SpotifyPlaybackModel.Empty;

        var id = GetString(item, "id");
        var uri = GetString(item, "uri");
        var title = GetString(item, "name", "Nothing playing");
        var durationMs = GetInt(item, "duration_ms");
        var progressMs = GetInt(root, "progress_ms");
        var isPlaying = GetBool(root, "is_playing");
        var shuffleOn = GetBool(root, "shuffle_state");
        var album = item.TryGetProperty("album", out var albumElement) ? albumElement : default;
        var albumName = album.ValueKind == JsonValueKind.Object ? GetString(album, "name") : "";
        var imageUrl = album.ValueKind == JsonValueKind.Object ? FirstImage(album) : "";
        var artists = Artists(item);
        var deviceName = root.TryGetProperty("device", out var device) ? GetString(device, "name") : "";

        return new SpotifyPlaybackModel(
            id,
            uri,
            title,
            string.IsNullOrWhiteSpace(artists) ? "Unknown artist" : artists,
            albumName,
            imageUrl,
            isPlaying,
            shuffleOn,
            Math.Clamp(progressMs, 0, durationMs),
            durationMs,
            deviceName);
    }

    public static async Task<IReadOnlyList<SpotifyPlaylistModel>> GetPlaylistsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var playlists = new List<SpotifyPlaylistModel>();
        var url = $"{ApiBase}/me/playlists?limit=50";

        while (!string.IsNullOrWhiteSpace(url) && playlists.Count < 150)
        {
            using var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response, json, "Could not load Spotify playlists.");

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                break;

            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var trackCount = item.TryGetProperty("tracks", out var tracks)
                    ? GetInt(tracks, "total")
                    : 0;
                if (trackCount <= 0)
                    trackCount = await GetPlaylistTrackCountAsync(accessToken, id, cancellationToken);

                var owner = item.TryGetProperty("owner", out var ownerElement)
                    ? GetString(ownerElement, "display_name")
                    : "";

                playlists.Add(new SpotifyPlaylistModel(
                    id,
                    GetString(item, "name", "Playlist"),
                    FirstImage(item),
                    trackCount,
                    owner));
            }

            url = GetString(root, "next");
        }

        return playlists;
    }

    public static async Task<IReadOnlyList<SpotifyDeviceModel>> GetDevicesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ApiBase}/me/player/devices", accessToken, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Could not load Spotify devices.");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
            return [];

        return devices
            .EnumerateArray()
            .Select(device => new SpotifyDeviceModel(
                GetString(device, "id"),
                GetString(device, "name", "Spotify device"),
                GetString(device, "type"),
                GetBool(device, "is_active"),
                GetBool(device, "is_restricted")))
            .Where(device => !string.IsNullOrWhiteSpace(device.Id) && !device.IsRestricted)
            .ToList();
    }

    public static async Task<SpotifyTrackPreviewModel> GetNextTrackAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{ApiBase}/me/player/queue", accessToken, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Could not load Spotify queue.");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("queue", out var queue) || queue.ValueKind != JsonValueKind.Array)
            return SpotifyTrackPreviewModel.Empty;

        foreach (var item in queue.EnumerateArray())
        {
            var preview = ToTrackPreview(item);
            if (!string.IsNullOrWhiteSpace(preview.Id))
                return preview;
        }

        return SpotifyTrackPreviewModel.Empty;
    }

    public static async Task<IReadOnlyList<SpotifyTrackPreviewModel>> GetPlaylistTracksAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken = default)
    {
        var tracks = new List<SpotifyTrackPreviewModel>();
        var fields = Uri.EscapeDataString("items(track(id,name,artists(name),album(images))),next,total");
        var offset = 0;

        while (offset < 500)
        {
            using var response = await SendAsync(
                HttpMethod.Get,
                $"{ApiBase}/playlists/{playlistId}/tracks?limit=50&offset={offset}&fields={fields}",
                accessToken,
                null,
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response, json, "Could not load Spotify playlist tracks.");

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                break;

            var loaded = 0;
            foreach (var item in items.EnumerateArray())
            {
                loaded++;
                if (!item.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object)
                    continue;

                var preview = ToTrackPreview(track);
                if (!string.IsNullOrWhiteSpace(preview.Id))
                    tracks.Add(preview);
            }

            offset += loaded;
            var total = GetInt(root, "total");
            if (loaded == 0 ||
                offset >= total ||
                !root.TryGetProperty("next", out var next) ||
                next.ValueKind == JsonValueKind.Null)
                break;
        }

        return tracks;
    }

    public static Task TransferPlaybackAsync(string accessToken, string deviceId, bool play = false, CancellationToken cancellationToken = default) =>
        SendCommandAsync(
            HttpMethod.Put,
            $"{ApiBase}/me/player",
            accessToken,
            $$"""{"device_ids":["{{deviceId}}"],"play":{{play.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

    public static Task PlayPlaylistAsync(string accessToken, string playlistId, string? deviceId = null, CancellationToken cancellationToken = default) =>
        SendCommandAsync(
            HttpMethod.Put,
            WithDevice($"{ApiBase}/me/player/play", deviceId),
            accessToken,
            $$"""{"context_uri":"spotify:playlist:{{playlistId}}"}""",
            cancellationToken);

    public static Task ResumeAsync(string accessToken, string? deviceId = null, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Put, WithDevice($"{ApiBase}/me/player/play", deviceId), accessToken, "{}", cancellationToken);

    public static Task PauseAsync(string accessToken, string? deviceId = null, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Put, WithDevice($"{ApiBase}/me/player/pause", deviceId), accessToken, null, cancellationToken);

    public static Task NextAsync(string accessToken, string? deviceId = null, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, WithDevice($"{ApiBase}/me/player/next", deviceId), accessToken, null, cancellationToken);

    public static Task PreviousAsync(string accessToken, string? deviceId = null, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, WithDevice($"{ApiBase}/me/player/previous", deviceId), accessToken, null, cancellationToken);

    public static Task SetShuffleAsync(string accessToken, bool state, string? deviceId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiBase}/me/player/shuffle?state={state.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(deviceId))
            url += $"&device_id={Uri.EscapeDataString(deviceId)}";

        return SendCommandAsync(HttpMethod.Put, url, accessToken, null, cancellationToken);
    }

    private static async Task SendCommandAsync(
        HttpMethod method,
        string url,
        string accessToken,
        string? json,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, url, accessToken, json, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "Spotify command failed. Open Spotify on a device and make sure your account has Premium.");
    }

    private static async Task<int> GetPlaylistTrackCountAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"{ApiBase}/playlists/{playlistId}/tracks?limit=1&fields=total",
            accessToken,
            null,
            cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return 0;

        using var document = JsonDocument.Parse(json);
        return GetInt(document.RootElement, "total");
    }

    private static string WithDevice(string url, string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId)
            ? url
            : $"{url}?device_id={Uri.EscapeDataString(deviceId)}";

    private static async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        string? json,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (json != null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return await Http.SendAsync(request, cancellationToken);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string json, string fallback)
    {
        if (response.IsSuccessStatusCode)
            return;

        throw new InvalidOperationException(ParseSpotifyError(json, fallback));
    }

    private static string ParseSpotifyError(string json, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
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

    private static string FirstImage(JsonElement element)
    {
        if (!element.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var image in images.EnumerateArray())
        {
            var url = GetString(image, "url");
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        return "";
    }

    private static string Artists(JsonElement item)
    {
        if (!item.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
            return "";

        return string.Join(", ", artists
            .EnumerateArray()
            .Select(artist => GetString(artist, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static SpotifyTrackPreviewModel ToTrackPreview(JsonElement item)
    {
        var id = GetString(item, "id");
        if (string.IsNullOrWhiteSpace(id))
            return SpotifyTrackPreviewModel.Empty;

        var title = GetString(item, "name", "Next track");
        var artists = Artists(item);
        var imageUrl = "";

        if (item.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
            imageUrl = FirstImage(album);
        else
            imageUrl = FirstImage(item);

        return new SpotifyTrackPreviewModel(
            id,
            title,
            string.IsNullOrWhiteSpace(artists) ? "Unknown artist" : artists,
            imageUrl);
    }

    private static string GetString(JsonElement element, string propertyName, string fallback = "") =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;

    private static int GetInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : 0;

    private static bool GetBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
}
