using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Windows;

namespace ClaudeUsageTray.Services;

public class SessionExpiredException : Exception { }

public class ClaudeApiService : IDisposable
{
	private readonly SettingsService _settings;
	private WebView2Window? _webView2Window;

	public CoreWebView2Environment? WebView2Environment { get; private set; }

	public ClaudeApiService(SettingsService settingsService)
	{
		_settings = settingsService;
	}

	public async Task InitializeAsync()
	{
		_webView2Window = new WebView2Window();
		_webView2Window.Show();
		_webView2Window.Hide();

		var userDataFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"ClaudeUsageTray",
			"WebView2");
		WebView2Environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
		await _webView2Window.WebView.EnsureCoreWebView2Async(WebView2Environment);
	}

	public async Task<UsageData?> GetUsageAsync()
	{
		if (string.IsNullOrEmpty(_settings.OrganizationId))
			return null;

		var url = $"https://claude.ai/api/organizations/{_settings.OrganizationId}/usage";
		var json = await NavigateAndReadAsync(url);
		if (json == "__UNAUTHORIZED__") throw new SessionExpiredException();
		if (json == null) return null;

		try
		{
			return JsonSerializer.Deserialize<UsageData>(json);
		}
		catch
		{
			return null;
		}
	}

	public async Task<string?> GetOrDiscoverOrgIdAsync()
	{
		if (!string.IsNullOrEmpty(_settings.OrganizationId))
			return _settings.OrganizationId;

		var json = await NavigateAndReadAsync("https://claude.ai/api/organizations");
		if (json == null || json == "__UNAUTHORIZED__")
			return null;

		try
		{
			var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Array)
				return null;

			// Prefer: team org with chat → any org with chat → first org
			JsonElement? teamWithChat = null;
			JsonElement? firstWithChat = null;
			JsonElement? firstAny = null;

			foreach (var item in root.EnumerateArray())
			{
				if (firstAny == null) firstAny = item;
				if (HasChatCapability(item))
				{
					if (firstWithChat == null) firstWithChat = item;
					if (item.TryGetProperty("raven_type", out var rt) && rt.GetString() == "team")
					{
						teamWithChat = item;
						break;
					}
				}
			}

			var selected = teamWithChat ?? firstWithChat ?? firstAny;
			if (selected == null) return null;

			if (!selected.Value.TryGetProperty("uuid", out var uuidProp))
				return null;

			var uuid = uuidProp.GetString();
			if (string.IsNullOrEmpty(uuid)) return null;

			_settings.OrganizationId = uuid;
			_settings.Save();
			return uuid;
		}
		catch
		{
			return null;
		}
	}

	public void Dispose()
	{
		try { _webView2Window?.WebView?.Dispose(); } catch { }
		try { _webView2Window?.Close(); } catch { }
	}

	public async Task ClearSessionAsync()
	{
		if (_webView2Window?.WebView?.CoreWebView2 == null)
			return;

		_webView2Window.WebView.CoreWebView2.CookieManager.DeleteAllCookies();
		_settings.OrganizationId = null;
		_settings.Save();
	}

	// Navigate the hidden browser directly to the API URL and read the JSON from the
	// page body. Top-level navigations carry all cookies (including SameSite=Lax),
	// and JSON endpoints don't do JS redirects that would disrupt ExecuteScriptAsync.
	private async Task<string?> NavigateAndReadAsync(string url)
	{
		if (_webView2Window?.WebView?.CoreWebView2 == null) return null;

		var tcs = new TaskCompletionSource<bool>();
		void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs e)
		{
			_webView2Window!.WebView.CoreWebView2.NavigationCompleted -= OnNav;
			tcs.TrySetResult(e.IsSuccess);
		}
		_webView2Window.WebView.CoreWebView2.NavigationCompleted += OnNav;
		_webView2Window.WebView.CoreWebView2.Navigate(url);

		var success = await tcs.Task;
		if (!success) return null;

		// If we were redirected to a login/auth page the session has expired.
		var finalUrl = _webView2Window.WebView.CoreWebView2.Source ?? "";
		if (finalUrl.Contains("/login") || finalUrl.Contains("/auth"))
			return "__UNAUTHORIZED__";

		try
		{
			// Chromium renders JSON responses as a page with the text in <body>.
			var result = await _webView2Window.WebView.ExecuteScriptAsync(
				"document.body.innerText");
			if (result == null || result == "null") return null;
			if (!result.StartsWith('"')) return null;
			return JsonSerializer.Deserialize<string>(result);
		}
		catch
		{
			return null;
		}
	}

	private static bool HasChatCapability(JsonElement org)
	{
		if (!org.TryGetProperty("capabilities", out var caps) ||
			caps.ValueKind != JsonValueKind.Array)
			return false;

		foreach (var cap in caps.EnumerateArray())
		{
			if (cap.GetString() == "chat")
				return true;
		}
		return false;
	}
}
