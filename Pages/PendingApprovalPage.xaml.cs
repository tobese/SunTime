using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SunTime.Services;

namespace SunTime.Pages;

public sealed partial class PendingApprovalPage : Page
{
    private readonly ApiClient _api = ApiClient.Instance;

    public PendingApprovalPage()
    {
        this.InitializeComponent();
    }

    private void OnSignOut(object sender, RoutedEventArgs e)
    {
        _api.Logout();
        Frame.Navigate(typeof(LoginPage));
        // Clear back stack so the user cannot navigate back into the app
        Frame.BackStack.Clear();
    }
}
