using SpotifyLikeAll.Api;
using SpotifyLikeAll.Auth;
using SpotifyLikeAll.Components;
using SpotifyLikeAll.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<SpotifyOptions>(builder.Configuration.GetSection(SpotifyOptions.Section));
builder.Services.AddHttpClient(SpotifyAuthService.HttpClientName);
builder.Services.AddSingleton<RefreshTokenStore>();
builder.Services.AddSingleton<SpotifyAuthService>();

builder.Services.AddHttpClient<ISpotifyClient, SpotifyClient>(http =>
    http.BaseAddress = new Uri("https://api.spotify.com/v1/"));
builder.Services.AddScoped<PlaylistLoader>();
builder.Services.AddSingleton<LikedLog>();
builder.Services.AddSingleton<IgnoredPlaylistsStore>();
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<TopTracksService>();

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapSpotifyAuth();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
