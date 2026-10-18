# Contributing

Thanks for taking an interest. This is a small solo project, but PRs are welcome and I'll review them.

## Quick start

```bash
git clone --recurse-submodules https://github.com/XeldarAlz/FFXIV-AutoSightseeingLog.git
cd FFXIV-AutoSightseeingLog
dotnet build AutoSightseeingLog.sln -c Release
```

You need the .NET 10 SDK. The plugin requires Dalamud at runtime; CI pulls a Dalamud dev build automatically and that's enough to compile. See `.github/workflows/release.yml` if you want to reproduce CI locally.

Load the built plugin via `/xlsettings` -> **Experimental** -> **Dev Plugin Locations**, pointing at `AutoSightseeingLog/bin/Release/AutoSightseeingLog.dll`.

Run the jump controller regression checks with `dotnet run --project tests/JumpController.Tests -c Release`. They compile the production decision and trajectory code without Dalamud, replay Kugane movement traces, and exercise missed takeoffs, falls, guarded landing corrections, recovery bounds and calibrated launches at different frame rates. Held-out overshoot traces check range predictions learned from earlier jumps, including the first roof miss after observing a capped-speed jump. Recorded upper-roof flights check downhill startup, run-up reversals and exclusion of failed manual attempts from the route. The leg 44 regression checks the failed startup, rejection of the lower lip as a correction target, precise takeoff alignment and teleport destination checks. Roof recovery tests use synthetic collision surfaces to check slopes, gaps, obstacles, stacked floors and checkpoint selection. These checks do not simulate the game's collision geometry or execute teleports; a full in-game climb is still required.

## Project layout

- `AutoSightseeingLog/Core/`: vista data, the log reader, Eorzea time and weather, the automation state machine.
- `AutoSightseeingLog/Windows/`: ImGui main window, settings, dependencies.
- `AutoSightseeingLog/`: plugin entry points, config, command wiring.
- `ECommons/`: submodule, shared Dalamud helpers. Don't patch this directly; upstream it.

Keep logic small and direct. This plugin has one job.

## Before you open a PR

1. `dotnet build -c Release` cleanly.
2. Test in-game on at least one vista per expansion you touched. Positions, time windows, and weather rules differ between expansions, so a fix that works for Dawntrail may not work for A Realm Reborn.
3. Keep the diff focused. One concern per PR.
4. Match the existing style. No heavy abstractions "for later."
5. If your change affects what a user sees or types (commands, window layout, settings), update the README.
6. If you used AI beyond autocomplete, say which level in the PR description. It is one line, and [AI-USAGE.md](AI-USAGE.md) explains the level names and why I ask.

## Good first issues

Check the tracker for anything labeled `good first issue`. Vista-specific quirks (a spot the plugin can't reach, a vista that never records, a wrong time window) are usually the lowest-friction way to help: name the vista by its log number, attach a log of what the plugin did vs. what should have happened, and a fix is usually a small change.

## Security

Please don't file public issues for security problems; see [SECURITY.md](SECURITY.md).

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Be decent.

## License

By contributing, you agree your contributions are licensed under AGPL-3.0-or-later with the additional terms in [NOTICE](NOTICE), the same as the project.
