using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClaudeUsageTray.Helpers;

public static class TrayIconGenerator
{
	/// <summary>
	/// Generates a 32x32 system tray icon representing usage state via two concentric arcs.
	/// </summary>
	/// <param name="sessionPct">Session (five-hour) usage percentage (0-100)</param>
	/// <param name="weeklyPct">Weekly (seven-day) usage percentage (0-100)</param>
	/// <returns>A System.Drawing.Icon that can be assigned to the tray icon.
	/// The caller must call DestroyIcon(icon.Handle) after the icon is replaced to avoid GDI handle leaks.
	/// Note: System.Drawing.Icon.Handle is not directly accessible; track the IntPtr returned by GetHicon() separately.</returns>
	public static Icon Generate(double sessionPct, double weeklyPct)
	{
		// Clamp percentages to 0–100
		sessionPct = Math.Clamp(sessionPct, 0, 100);
		weeklyPct = Math.Clamp(weeklyPct, 0, 100);

		var bitmap = new Bitmap(32, 32);
		using (var g = Graphics.FromImage(bitmap))
		{
			g.Clear(Color.Transparent);
			g.SmoothingMode = SmoothingMode.AntiAlias;

			// Draw outer (weekly) ring
			DrawRing(g, 4, 4, 24, 24,   // bounding box
					 ColorTranslator.FromHtml("#3b82f6"), weeklyPct);

			// Draw inner (session) ring
			DrawRing(g, 9, 9, 14, 14,   // bounding box
					 ColorTranslator.FromHtml("#a3e635"), sessionPct);
		}

		// Convert to Icon
		IntPtr hIcon = bitmap.GetHicon();
		var icon = Icon.FromHandle(hIcon);
		bitmap.Dispose();
		// Note: caller is responsible for DestroyIcon(hIcon) after the icon is replaced
		return icon;
	}

	private static void DrawRing(Graphics g, int x, int y, int width, int height,
								 Color normalColor, double percentage)
	{
		// Draw background ring (light grey)
		var bgPen = new Pen(Color.FromArgb(64, 255, 255, 255), 4) {
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		g.DrawArc(bgPen, x, y, width, height, -90f, 360f);

		// Draw coloured arc
		var color = ThresholdColor(percentage, normalColor);
		var pen = new Pen(color, 4) {
			StartCap = LineCap.Round,
			EndCap = LineCap.Round
		};
		var sweepAngle = (float)(percentage / 100.0 * 360.0);
		g.DrawArc(pen, x, y, width, height, -90f, sweepAngle);

		bgPen.Dispose();
		pen.Dispose();
	}

	private static Color ThresholdColor(double pct, Color normalColor)
	{
		return pct >= 90 ? ColorTranslator.FromHtml("#ef4444") :
			   pct >= 75 ? ColorTranslator.FromHtml("#f59e0b") :
			   normalColor;
	}
}
