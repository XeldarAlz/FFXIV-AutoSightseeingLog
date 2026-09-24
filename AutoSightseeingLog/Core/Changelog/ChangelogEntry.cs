using AutoSightseeingLog.Core.Localization;

namespace AutoSightseeingLog.Core.Changelog;

internal readonly record struct ChangelogEntry(string Version, string Date, LocString[] Highlights);
