using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SunTime.Services;

/// <summary>
/// HTTP client for the SunTime backend API.
///
/// Exposes auth (external/password/me/refresh) and user-management endpoints.
/// A shared instance is used across pages via <see cref="Instance"/> so that the
/// JWT and the currently-signed-in user survive page navigations.
/// </summary>
public class ApiClient
{
    /// <summary>
    /// Shared instance accessible from all pages.
    /// </summary>
    public static ApiClient Instance { get; } = new();

    private readonly HttpClient _http;
    private string? _token;
    public UserResponse? CurrentUser { get; private set; }

    public ApiClient()
    {
        _http = new HttpClient
        {
            // When served by nginx, the app and API share the same origin.
            // nginx reverse-proxies /api/* to the backend container.
            // For local dev without Docker, change this to the API's address.
            BaseAddress = new Uri(GetBaseUri())
        };
    }

    private static string GetBaseUri()
    {
#if __WASM__
        // In WASM, use the current page origin so /api/* goes through nginx
        return "";
#else
        return "http://localhost:5179";
#endif
    }

    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    // --- Auth ---

    public async Task<AuthResponse?> ExternalLoginAsync(string provider, string idToken)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login",
            new { Provider = provider, IdToken = idToken });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (result != null)
        {
            SetToken(result.Token);
            CurrentUser = result.User;
        }
        return result;
    }

    public async Task<AuthResponse?> PasswordLoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/auth/login/password",
            new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (result != null)
        {
            SetToken(result.Token);
            CurrentUser = result.User;
        }
        return result;
    }

    public void Logout()
    {
        ClearToken();
        CurrentUser = null;
    }

    public async Task<UserResponse?> GetMeAsync()
    {
        var response = await _http.GetAsync("/api/auth/me");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    // --- User Management ---

    public async Task<List<UserResponse>> GetUsersAsync(string? status = null)
    {
        var url = "/api/users" + (status != null ? $"?status={status}" : "");
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UserResponse>>() ?? new List<UserResponse>();
    }

    public async Task<UserResponse?> ApproveUserAsync(string userId, bool approved)
    {
        var response = await _http.PostAsJsonAsync($"/api/users/{userId}/approve",
            new { Approved = approved });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse?> ChangeRoleAsync(string userId, string role)
    {
        var response = await _http.PostAsJsonAsync($"/api/users/{userId}/role",
            new { Role = role });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }
}

// Mirror DTOs from the server for deserialization
public partial record AuthResponse(string Token, UserResponse User);

public partial record UserResponse(
    string Id,
    string? Email,
    string? DisplayName,
    string? ExternalProvider,
    string Role,
    string Status,
    DateTime CreatedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt
);
