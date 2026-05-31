using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace ClaudeUsageTray.Windows;

public partial class WebView2Window : Window
{
	public WebView2 WebView => WebView2Control;

	public WebView2Window()
	{
		InitializeComponent();
	}
}
