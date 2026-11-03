using System.Security.Cryptography;

namespace SpotifyLikeAll.Auth;

public static class AuthEndpoints
{
    private const string StateCookie = "spotify_auth_state";

    public static void MapSpotifyAuth(this WebApplication app)
    {
        // Step 1: send the browser to Spotify's consent page.
        app.MapGet("/login", (HttpContext context, SpotifyAuthService auth) =>
        {
            if (!auth.IsConfigured)
                return Results.Redirect("/");

            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            context.Response.Cookies.Append(StateCookie, state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true,
            });

            return Results.Redirect(auth.BuildAuthorizeUrl(state));
        });

        // Step 2: Spotify redirects back here with ?code=...&state=...
        app.MapGet("/callback", async (
            HttpContext context,
            SpotifyAuthService auth,
            string? code,
            string? state,
            string? error,
            CancellationToken ct) =>
        {
            var expectedState = context.Request.Cookies[StateCookie];
            context.Response.Cookies.Delete(StateCookie);

            if (error is not null)
                return Results.Redirect("/?loginError=" + Uri.EscapeDataString(error));

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != expectedState)
                return Results.Redirect("/?loginError=invalid_state");

            try
            {
                await auth.CompleteLoginAsync(code, ct);
            }
            catch (SpotifyAuthException ex)
            {
                return Results.Redirect("/?loginError=" + Uri.EscapeDataString(ex.Message));
            }

            return Results.Redirect("/");
        });

        app.MapGet("/logout", async (SpotifyAuthService auth, Services.AppState state) =>
        {
            await auth.LogoutAsync();
            state.Clear();
            return Results.Redirect("/");
        });
    }
}
