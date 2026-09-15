## What

<!-- One or two sentences on what this PR changes. -->

## Why

<!-- The motivating problem, linked issue, or user-visible behavior this fixes. -->

Closes #

## How to test

<!--
Minimum steps a reviewer can run to verify the change. Testing usually means ticking one vista you haven't logged yet, pressing Start, and watching the plugin travel there, wait for its window, and log it end-to-end. Make sure the dependency listed in /asl deps is installed first. For UI-only changes, describe what to click.
-->

## Checklist

- [ ] `dotnet build -c Release` passes
- [ ] Verified in-game on at least one vista in the affected expansion
- [ ] If this changes user-visible behavior, README is updated
- [ ] If this touches the automation loop, relevant `[AutoSightseeingLog]` log lines make the sequence auditable
