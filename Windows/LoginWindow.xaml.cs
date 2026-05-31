using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace ClaudeUsageTray.Windows;

public partial class LoginWindow : Window
{
	private DispatcherTimer? _cookieTimer;
	private readonly CoreWebView2Environment? _environment;
	private bool _loginHandled;

	public event EventHandler? LoginSucceeded;

	public LoginWindow(CoreWebView2Environment? environment = null)
	{
		InitializeComponent();
		_environment = environment;
		Loaded += async (_, _) => await OnLoadedAsync();
	}

	private async Task OnLoadedAsync()
	{
		await WebView2Control.EnsureCoreWebView2Async(_environment);
		WebView2Control.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
		StartCookiePolling();
		WebView2Control.CoreWebView2.Navigate("https://claude.ai/login");
	}

	private void StartCookiePolling()
	{
		_cookieTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_cookieTimer.Tick += async (_, _) =>
		{
			if (_loginHandled) return;
			try
			{
				var cookies = await WebView2Control.CoreWebView2.CookieManager
					.GetCookiesAsync("https://claude.ai");
				var session = cookies.FirstOrDefault(c => c.Name == "sessionKey");
				if (session?.Value is { Length: > 0 })
					CompleteLogin();
			}
			catch { }
		};
		_cookieTimer.Start();
	}

	private void CompleteLogin()
	{
		if (_loginHandled) return;
		_loginHandled = true;
		_cookieTimer?.Stop();
		LoginSucceeded?.Invoke(this, EventArgs.Empty);
		Close();
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		_cookieTimer?.Stop();
		try { WebView2Control?.Dispose(); } catch { }
	}
}
