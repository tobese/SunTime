using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SunTime.Services;

namespace SunTime.Pages;

public sealed partial class LoginPage : Page
{
    private readonly ApiClient _api = ApiClient.Instance;

    public LoginPage()
    {
        this.InitializeComponent();
    }

    private async void OnGoogleSignIn(object sender, RoutedEventArgs e)
    {
        await SignInWithProviderAsync("Google");
    }

    private async void OnAppleSignIn(object sender, RoutedEventArgs e)
    {
        await SignInWithProviderAsync("Apple");
    }

    private async Task SignInWithProviderAsync(string provider)
    {
        try
        {
            GoogleSignInButton.IsEnabled = false;
            AppleSignInButton.IsEnabled = false;
            StatusText.Text = $"Signing in with {provider}…";

            // Use WebAuthenticationBroker to open the OIDC popup.
            // The redirect URI must be same-origin for WASM.
            var redirectUri = Windows.Security.Authentication.Web.WebAuthenticationBroker
                .GetCurrentApplicationCallbackUri().OriginalString;

            // Build the authorization URL for the provider.
            // In production, use a proper OIDC discovery endpoint.
            // For now, this shows the integration point.
            var authorizeUrl = provider switch
            {
                "Google" => $"https://accounts.google.com/o/oauth2/v2/auth?client_id=YOUR_GOOGLE_CLIENT_ID&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=id_token&scope=openid%20email%20profile&nonce={Guid.NewGuid()}",
                "Apple" => $"https://appleid.apple.com/auth/authorize?client_id=YOUR_APPLE_SERVICE_ID&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=id_token&scope=name%20email&nonce={Guid.NewGuid()}",
                _ => throw new ArgumentException($"Unknown provider: {provider}")
            };

            var result = await Windows.Security.Authentication.Web.WebAuthenticationBroker
                .AuthenticateAsync(
                    Windows.Security.Authentication.Web.WebAuthenticationOptions.None,
                    new Uri(authorizeUrl));

            if (result.ResponseStatus != Windows.Security.Authentication.Web.WebAuthenticationStatus.Success)
            {
                StatusText.Text = "Sign-in cancelled.";
                return;
            }

            // Extract the id_token from the response URL fragment
            var responseData = result.ResponseData;
            if (string.IsNullOrEmpty(responseData))
            {
                StatusText.Text = "No response data received.";
                return;
            }

            var fragment = new Uri(responseData).Fragment.TrimStart('#');
            var idToken = ParseFragmentValue(fragment, "id_token");

            if (string.IsNullOrEmpty(idToken))
            {
                StatusText.Text = "No identity token received.";
                return;
            }

            // Exchange the token with our backend
            var authResponse = await _api.ExternalLoginAsync(provider, idToken);
            if (authResponse == null)
            {
                StatusText.Text = "Login failed.";
                return;
            }

            // Navigate based on user status
            NavigateByStatus(authResponse.User.Status);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            GoogleSignInButton.IsEnabled = true;
            AppleSignInButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Parse a single value from an "a=b&amp;c=d" URL fragment/query string.
    /// Avoids a dependency on <c>System.Web.HttpUtility</c> so the code compiles
    /// cleanly on every SunTime TFM (WASM and Android).
    /// </summary>
    private static string? ParseFragmentValue(string fragment, string key)
    {
        if (string.IsNullOrEmpty(fragment)) return null;

        foreach (var pair in fragment.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var k = Uri.UnescapeDataString(pair.Substring(0, eq));
            if (string.Equals(k, key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
        }

        return null;
    }

    private void NavigateByStatus(string status)
    {
        var frame = this.Frame;
        switch (status)
        {
            case "Active":
                frame.Navigate(typeof(MainPage));
                frame.BackStack.Clear();
                break;
            case "PendingApproval":
                frame.Navigate(typeof(PendingApprovalPage));
                frame.BackStack.Clear();
                break;
            default:
                StatusText.Text = $"Account status: {status}. Contact an administrator.";
                break;
        }
    }
}
