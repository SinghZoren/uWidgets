using System.Text.Json;
using ReactiveUI;
using Spotify.Models;
using Spotify.Services;
using uWidgets.Core.Interfaces;

namespace Spotify.ViewModels;

public class SpotifySettingsViewModel(IWidgetLayoutProvider widgetLayoutProvider) : ReactiveObject
{
    private SpotifyModel model = (widgetLayoutProvider.Get().GetModel<SpotifyModel>() ?? new SpotifyModel()).Normalize();
    private bool isBusy;
    private string statusText = "";

    public string ClientId
    {
        get => model.ClientId;
        set => UpdateModel(model with { ClientId = value });
    }

    public string RedirectUri
    {
        get => model.RedirectUri;
        set => UpdateModel(model with { RedirectUri = value });
    }

    public bool IsConnected => model.IsConnected;
    public bool IsConfigured => model.IsConfigured;

    public bool IsBusy
    {
        get => isBusy;
        private set => this.RaiseAndSetIfChanged(ref isBusy, value);
    }

    public string StatusText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(statusText))
                return statusText;
            if (model.IsConnected)
                return "Connected to Spotify";
            return model.IsConfigured
                ? "Ready to connect"
                : "Paste a Spotify app client ID";
        }
        private set => this.RaiseAndSetIfChanged(ref statusText, value);
    }

    public async Task ConnectAsync()
    {
        if (!model.IsConfigured || IsBusy)
            return;

        IsBusy = true;
        try
        {
            StatusText = "Waiting for Spotify login...";
            var token = await SpotifyOAuthService.AuthorizeAsync(model.ClientId, model.RedirectUri);
            UpdateModel(model with
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAtUtc = token.ExpiresAtUtc
            });
            StatusText = "Connected to Spotify";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseAll();
        }
    }

    public void Disconnect()
    {
        UpdateModel(model with
        {
            AccessToken = "",
            RefreshToken = "",
            ExpiresAtUtc = null,
            SelectedPlaylistId = "",
            SelectedPlaylistName = ""
        });
        StatusText = "Disconnected";
    }

    private void UpdateModel(SpotifyModel newModel)
    {
        model = newModel.Normalize();
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });
        RaiseAll();
    }

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(ClientId));
        this.RaisePropertyChanged(nameof(RedirectUri));
        this.RaisePropertyChanged(nameof(IsConnected));
        this.RaisePropertyChanged(nameof(IsConfigured));
        this.RaisePropertyChanged(nameof(StatusText));
    }
}
