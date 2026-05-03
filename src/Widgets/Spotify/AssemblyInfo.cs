using System.Reflection;
using Spotify.Locales;
using Spotify.Models;
using Spotify.Views.Settings;
using uWidgets.Core.Models.Attributes;
using SpotifyWidget = Spotify.Views.Spotify;

[assembly: AssemblyCompany("zoren")]
[assembly: AssemblyVersion("1.0.0")]

[assembly: WidgetInfo(typeof(SpotifyWidget), typeof(SpotifyModel), typeof(SpotifySettings), "Spotify_Title", "Spotify_Subtitle")]
[assembly: Locale(typeof(Locale), "Spotify", "M12,2 A10,10 0 1,0 12,22 A10,10 0 1,0 12,2 Z M7.7,9.4 C10.7,8.4 14.1,8.7 16.7,10.2 L15.9,11.7 C13.7,10.4 10.8,10.1 8.2,11 Z M8.2,12.5 C10.4,11.9 12.7,12.1 14.8,13.2 L14.1,14.5 C12.4,13.6 10.4,13.4 8.6,13.9 Z M8.7,15.3 C10.2,15 11.7,15.1 13.1,15.8 L12.5,17 C11.4,16.5 10.2,16.4 9,16.7 Z")]
