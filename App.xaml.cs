using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Services;
using ClaudeUsageTray.Windows;
using ClaudeUsageTray.Helpers;
using Hardcodet.Wpf.TaskbarNotification;

namespace ClaudeUsageTray;

public partial class App : System.Windows.Application
{
	private SettingsService? _settings;
	private ClaudeApiService? _api;
	private UsagePopup? _popup;
	private DispatcherTimer? _pollTimer;
	private System.Drawing.Icon? _previousIcon;
	private UsageData? _latestUsageData;
	private bool _loginWindowOpen;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyIcon(IntPtr hIcon);

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		InitializeAsync();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_pollTimer?.Stop();

		try { _popup?.Close(); } catch { }

		if (_previousIcon != null)
		{
			try { DestroyIcon(_previousIcon.Handle); } catch { }
		}

		_api?.Dispose();

		// Dispose the TaskbarIcon so the tray entry is removed and the
		// Hardcodet message pump shuts down cleanly.
		try { ((TaskbarIcon)FindResource("TrayIcon")).Dispose(); } catch { }

		base.OnExit(e);
	}

	private async void InitializeAsync()
	{
		try
		{
			_settings = new SettingsService();
			_settings.Load();

			_api = new ClaudeApiService(_settings);
			await _api.InitializeAsync();

			if (string.IsNullOrEmpty(_settings.OrganizationId))
			{
				ShowLoginWindow(afterLogin: async () =>
				{
					await _api.GetOrDiscoverOrgIdAsync();
					ContinueAfterLogin();
				});
			}
			else
			{
				ContinueAfterLogin();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show($"Failed to initialize: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			Shutdown();
		}
	}

	private void ShowLoginWindow(Func<Task> afterLogin)
	{
		if (_loginWindowOpen) return;
		_loginWindowOpen = true;

		var login = new LoginWindow(_api!.WebView2Environment);
		login.LoginSucceeded += async (_, _) =>
		{
			_loginWindowOpen = false;
			await afterLogin();
		};
		login.Closed += (_, _) => _loginWindowOpen = false;
		login.Show();
	}

	private void ContinueAfterLogin()
	{
		_popup = new UsagePopup();
		var tray = (TaskbarIcon)FindResource("TrayIcon");

		_previousIcon = TrayIconGenerator.Generate(0, 0);
		tray.Icon = _previousIcon;

		var menu = tray.ContextMenu!;
		((System.Windows.Controls.MenuItem)menu.Items[0]).Click += async (_, _) => await RefreshAsync();
		((System.Windows.Controls.MenuItem)menu.Items[1]).Click += async (_, _) => await LogOutAsync();
		((System.Windows.Controls.MenuItem)menu.Items[3]).Click += (_, _) => Shutdown();

		tray.TrayLeftMouseDown += TrayIcon_LeftClick;

		_pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
		_pollTimer.Tick += async (_, _) => await RefreshAsync();
		_pollTimer.Start();

		_ = RefreshAsync();
	}

	private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
	{
		if (_popup!.IsVisible)
		{
			_popup.Hide();
		}
		else
		{
			if (_latestUsageData != null)
			{
				_popup.Update(_latestUsageData);
			}
			_popup.ShowAtCursor();
		}
	}

	private async Task RefreshAsync()
	{
		try
		{
			var usage = await _api!.GetUsageAsync();
			if (usage == null) return; // API error (not auth) — silently skip, retry on next poll

			_latestUsageData = usage;

			// Update tray icon
			var sessionPct = usage.FiveHour?.Utilization ?? 0;
			var weeklyPct = usage.SevenDay?.Utilization ?? 0;

			var newIcon = TrayIconGenerator.Generate(sessionPct, weeklyPct);
			var tray = (TaskbarIcon)FindResource("TrayIcon");

			if (_previousIcon != null)
			{
				try { DestroyIcon(_previousIcon.Handle); } catch { }
			}

			tray.Icon = newIcon;
			tray.ToolTipText = $"Session: {(int)sessionPct}%  Weekly: {(int)weeklyPct}%";
			_previousIcon = newIcon;

			// Update popup if visible
			if (_popup!.IsVisible)
			{
				_popup.Update(usage);
			}
		}
		catch (SessionExpiredException)
		{
			ShowLoginWindow(afterLogin: () => RefreshAsync());
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Poll failed: {ex}");
		}
	}

	private async Task LogOutAsync()
	{
		await _api!.ClearSessionAsync();
		_popup!.Hide();
		ShowLoginWindow(afterLogin: async () =>
		{
			await _api.GetOrDiscoverOrgIdAsync();
			await RefreshAsync();
		});
	}
}
