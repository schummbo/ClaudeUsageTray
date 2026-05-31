using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageTray.Services;

public class SettingsService
{
	private static readonly string _path = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
		"ClaudeUsageTray",
		"settings.json");

	public string? OrganizationId { get; set; }

	public void Load()
	{
		try
		{
			if (!File.Exists(_path))
				return;

			var json = File.ReadAllText(_path);
			var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("organizationId", out var prop))
				OrganizationId = prop.GetString();
		}
		catch
		{
			// Silently ignore corrupted or missing files on first run
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
		var json = JsonSerializer.Serialize(new { organizationId = OrganizationId });
		File.WriteAllText(_path, json);
	}
}
