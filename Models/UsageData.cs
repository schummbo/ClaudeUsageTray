using System.Text.Json.Serialization;

namespace ClaudeUsageTray.Models;

public record UsagePeriod(
	[property: JsonPropertyName("utilization")] double? Utilization,
	[property: JsonPropertyName("resets_at")] DateTimeOffset? ResetsAt);

public record UsageData(
	[property: JsonPropertyName("five_hour")] UsagePeriod? FiveHour,
	[property: JsonPropertyName("seven_day")] UsagePeriod? SevenDay);
