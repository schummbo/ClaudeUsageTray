using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Shapes;
using ClaudeUsageTray.Models;

namespace ClaudeUsageTray.Windows;

public partial class UsagePopup : Window
{
	public UsagePopup()
	{
		InitializeComponent();
		Deactivated += (_, _) => Hide();
	}

	public void Update(UsageData data)
	{
		var sessionPct = data.FiveHour?.Utilization ?? 0;
		var weeklyPct = data.SevenDay?.Utilization ?? 0;

		DonutCanvas.Children.Clear();

		// Draw donut arcs
		DrawDonutRings(sessionPct, weeklyPct);

		// Update centre text
		SessionPctText.Text = $"{(int)sessionPct}%";
		WeeklyPctText.Text = $"{(int)weeklyPct}%";

		// Centre reset time and legend reset times
		var sessionReset = FormatSessionReset(data.FiveHour?.ResetsAt);
		SessionResetText.Text = sessionReset;
		LegendSessionResetText.Text = sessionReset;
		WeeklyResetText.Text = FormatWeeklyReset(data.SevenDay?.ResetsAt);
	}

	private void DrawDonutRings(double sessionPct, double weeklyPct)
	{
		var centre = new System.Windows.Point(100, 100);
		const double outerRadius = 90;
		const double innerRadius = 65;

		// Draw background rings (light grey)
		DrawArcPath(centre, outerRadius, 0, 359.9, System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
		DrawArcPath(centre, innerRadius, 0, 359.9, System.Windows.Media.Color.FromArgb(34, 255, 255, 255));

		// Draw coloured arcs
		var outerColor = ThresholdColor(weeklyPct, System.Windows.Media.Color.FromArgb(255, 59, 130, 246));
		var innerColor = ThresholdColor(sessionPct, System.Windows.Media.Color.FromArgb(255, 163, 230, 53));

		if (weeklyPct > 0)
			DrawArcPath(centre, outerRadius, -90, weeklyPct / 100.0 * 360.0, outerColor);
		if (sessionPct > 0)
			DrawArcPath(centre, innerRadius, -90, sessionPct / 100.0 * 360.0, innerColor);

		// Keep legend dot in sync with the threshold color
		SessionLegendDot.Fill = new System.Windows.Media.SolidColorBrush(innerColor);
	}

	private void DrawArcPath(System.Windows.Point centre, double radius, double startAngleDeg, double sweepAngleDeg, System.Windows.Media.Color strokeColor)
	{
		// Clamp sweep to avoid degeneracy at 360°
		if (sweepAngleDeg >= 360)
			sweepAngleDeg = 359.9;

		var startPoint = ArcPoint(centre, radius, startAngleDeg);
		var endPoint = ArcPoint(centre, radius, startAngleDeg + sweepAngleDeg);

		var isLargeArc = sweepAngleDeg > 180;

		var arc = new ArcSegment
		{
			Point = endPoint,
			Size = new System.Windows.Size(radius, radius),
			RotationAngle = 0,
			IsLargeArc = isLargeArc,
			SweepDirection = SweepDirection.Clockwise
		};

		var pathFigure = new PathFigure { StartPoint = startPoint };
		pathFigure.Segments.Add(arc);

		var geometry = new PathGeometry();
		geometry.Figures.Add(pathFigure);

		var path = new Path
		{
			Data = geometry,
			Stroke = new SolidColorBrush(strokeColor),
			StrokeThickness = 16,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		};

		DonutCanvas.Children.Add(path);
	}

	private static System.Windows.Point ArcPoint(System.Windows.Point centre, double radius, double angleDeg)
	{
		var rad = angleDeg * Math.PI / 180.0;
		return new System.Windows.Point(centre.X + radius * Math.Sin(rad),
						centre.Y - radius * Math.Cos(rad));
	}

	private static System.Windows.Media.Color ThresholdColor(double pct, System.Windows.Media.Color normalColor)
	{
		return pct >= 90 ? System.Windows.Media.Color.FromArgb(255, 239, 68, 68) :
			   pct >= 75 ? System.Windows.Media.Color.FromArgb(255, 245, 158, 11) :
			   normalColor;
	}

	private static string FormatSessionReset(DateTimeOffset? resetsAt)
	{
		if (resetsAt == null)
			return "";
		var remaining = resetsAt.Value.ToUniversalTime() - DateTimeOffset.UtcNow;
		if (remaining.TotalSeconds < 0)
			remaining = TimeSpan.Zero;
		var hours = (int)remaining.TotalHours;
		var minutes = remaining.Minutes;
		return $"resets in {hours}h {minutes}m";
	}

	private static string FormatWeeklyReset(DateTimeOffset? resetsAt)
	{
		if (resetsAt == null)
			return "";
		var local = resetsAt.Value.ToLocalTime();
		return $"resets {local:ddd h:mm tt}";
	}

	public void ShowAtCursor()
	{
		// Ensure window has been shown at least once so PresentationSource is available
		if (!IsLoaded)
		{
			Show();
		}

		var cursorPos = System.Windows.Forms.Cursor.Position;
		var source = PresentationSource.FromVisual(this);
		var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
		var dipPos = transform.Transform(new System.Windows.Point(cursorPos.X, cursorPos.Y));

		var workArea = SystemParameters.WorkArea;
		Left = Math.Clamp(dipPos.X - Width / 2, workArea.Left, workArea.Right - Width);
		Top = Math.Clamp(dipPos.Y - Height - 8, workArea.Top, workArea.Bottom - Height);

		if (!IsVisible)
			Show();
		Activate();
	}

	protected override void OnDeactivated(EventArgs e)
	{
		base.OnDeactivated(e);
		Hide();
	}
}
