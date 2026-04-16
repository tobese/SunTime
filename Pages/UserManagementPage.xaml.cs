using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SunTime.Services;

namespace SunTime.Pages;

public sealed partial class UserManagementPage : Page
{
    private readonly ApiClient _api = ApiClient.Instance;
    private string? _currentFilter;

    public UserManagementPage()
    {
        this.InitializeComponent();
        _ = LoadUsersAsync();
    }

    private async Task LoadUsersAsync(string? statusFilter = null)
    {
        _currentFilter = statusFilter;
        try
        {
            var users = await _api.GetUsersAsync(statusFilter);
            UserList.ItemsSource = users;
        }
        catch (Exception ex)
        {
            // Show error inline if the API call fails
            UserList.ItemsSource = null;
            System.Diagnostics.Debug.WriteLine($"LoadUsers failed: {ex.Message}");
        }
    }

    private void OnFilterAll(object sender, RoutedEventArgs e) => _ = LoadUsersAsync();
    private void OnFilterPending(object sender, RoutedEventArgs e) => _ = LoadUsersAsync("PendingApproval");
    private void OnFilterActive(object sender, RoutedEventArgs e) => _ = LoadUsersAsync("Active");

    private async void OnApproveUser(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string userId)
        {
            try
            {
                await _api.ApproveUserAsync(userId, approved: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Approve failed: {ex.Message}");
            }
            await LoadUsersAsync(_currentFilter);
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(SunTime.MainPage));
        }
    }
}
