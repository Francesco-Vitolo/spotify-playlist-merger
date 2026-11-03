using SpotifyLikeAll.Api;
using SpotifyLikeAll.Components;
using SpotifyLikeAll.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<TokenStore>();
builder.Services.AddHttpClient<ISpotifyClient, SpotifyClient>(http =>
    http.BaseAddress = new Uri("https://api.spotify.com/v1/"));
builder.Services.AddScoped<PlaylistLoader>();

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
