namespace AutoSightseeingLog.Core.Localization;

internal static class L
{
    internal static class Common
    {
        public static readonly LocString Close = new("common.close", "Close");
        public static readonly LocString Cancel = new("common.cancel", "Cancel");
        public static readonly LocString Clear = new("common.clear", "Clear");
        public static readonly LocString SelectAll = new("common.selectAll", "Select all");
        public static readonly LocString Pause = new("common.pause", "Pause");
        public static readonly LocString Resume = new("common.resume", "Resume");
        public static readonly LocString StopRun = new("common.stopRun", "Stop the run");
        public static readonly LocString Working = new("common.working", "Working…");
        public static readonly LocString NoMatches = new("common.noMatches", "Nothing matches “{0}”.");
    }

    internal static class Shell
    {
        public static readonly LocString NavVistas = new("shell.nav.vistas", "Vistas");
        public static readonly LocString NavSettings = new("shell.nav.settings", "Settings");
        public static readonly LocString NavHistory = new("shell.nav.history", "History");
        public static readonly LocString NavPlugins = new("shell.nav.plugins", "Plugins");
        public static readonly LocString NavAbout = new("shell.nav.about", "About");
        public static readonly LocString StatusRunning = new("shell.status.running", "Running");
        public static readonly LocString StatusPaused = new("shell.status.paused", "Paused");
        public static readonly LocString StatusReady = new("shell.status.ready", "Ready");
        public static readonly LocString StatusPickVistas = new("shell.status.pickVistas", "Pick vistas");
        public static readonly LocString StatusAllDone = new("shell.status.allDone", "All done");
        public static readonly LocString StatusLocked = new("shell.status.locked", "Locked");
        public static readonly LocString StatusOffline = new("shell.status.offline", "Not logged in");
        public static readonly LocString StatusSetupNeeded = new("shell.status.setupNeeded", "Setup needed");
        public static readonly LocString StatusIdle = new("shell.status.idle", "Idle");
        public static readonly LocString Minimize = new("shell.minimize", "Minimize to the title strip");
        public static readonly LocString Restore = new("shell.restore", "Restore the window");
        public static readonly LocString ResumeBlocked = new("shell.resumeBlocked", "Resumes automatically once you leave the duty");
        public static readonly LocString GreetingMorning = new("shell.greeting.morning", "Good morning");
        public static readonly LocString GreetingAfternoon = new("shell.greeting.afternoon", "Good afternoon");
        public static readonly LocString GreetingEvening = new("shell.greeting.evening", "Good evening");
        public static readonly LocString GreetingNight = new("shell.greeting.night", "Late night");
    }

    internal static class Vistas
    {
        public static readonly LocString TitleSetupNeeded = new("vistas.title.setupNeeded", "Setup needed");
        public static readonly LocString DetailSetupNeeded = new("vistas.detail.setupNeeded", "Install the required plugins before your first run.");
        public static readonly LocString TitleOffline = new("vistas.title.offline", "Log in to begin");
        public static readonly LocString DetailOffline = new("vistas.detail.offline", "Your Sightseeing Log is read from your character once you are logged in.");
        public static readonly LocString TitlePick = new("vistas.title.pick", "Pick vistas to begin");
        public static readonly LocString DetailPick = new("vistas.detail.pick", "Tick vistas in the log below and they'll appear in your plan.");
        public static readonly LocString TitleAllDone = new("vistas.title.allDone", "All caught up");
        public static readonly LocString DetailAllDone = new("vistas.detail.allDone", "Every vista you picked is already in your log.");
        public static readonly LocString TitleLocked = new("vistas.title.locked", "Nothing unlocked yet");
        public static readonly LocString DetailLocked = new("vistas.detail.locked", "Every vista left in your plan is still locked. Hover one to see what opens it.");
        public static readonly LocString TitleReady = new("vistas.title.ready", "Ready to explore");
        public static readonly LocString DetailReady = new("vistas.detail.ready", "Everything's set. Press Start whenever you're ready.");
        public static readonly LocString TitleRunning = new("vistas.title.running", "Sightseeing");
        public static readonly LocString TitlePaused = new("vistas.title.paused", "Paused");
        public static readonly LocString DetailPausedInContent = new("vistas.detail.pausedInContent", "Resumes once you leave the duty");
        public static readonly LocString DetailPausedManual = new("vistas.detail.pausedManual", "Resume whenever you're ready");
        public static readonly LocString OpenPlugins = new("vistas.openPlugins", "Open plugins");
        public static readonly LocString NoRunsYet = new("vistas.noRunsYet", "No runs yet");
        public static readonly LocString StatsAppearHere = new("vistas.statsAppearHere", "your stats will appear here");
        public static readonly LocPlural LastRun = new("vistas.lastRun", "Last run  ·  {0} vista", "Last run  ·  {0} vistas");
    }

    internal static class Plan
    {
        public static readonly LocString Title = new("plan.title", "Plan");
        public static readonly LocString SentenceVisit = new("plan.sentence.visit", "Visit");
        public static readonly LocString SentenceThen = new("plan.sentence.then", "then");
        public static readonly LocString SentenceEnd = new("plan.sentence.end", ".");
        public static readonly LocString VistasNone = new("plan.vistasNone", "no vistas yet");
        public static readonly LocPlural VistasCount = new("plan.vistasCount", "{0} vista", "{0} vistas");
        public static readonly LocString Locked = new("plan.locked", "Stop the run to change the plan.");
        public static readonly LocString Hint = new("plan.hint", "The vistas you pick will appear here, grouped by expansion.");
        public static readonly LocString RemoveFromPlan = new("plan.removeFromPlan", "Remove from the plan");
        public static readonly LocString AfterStayToken = new("plan.after.stay.token", "stay where you are");
        public static readonly LocString AfterStayName = new("plan.after.stay.name", "Stay where you are");
        public static readonly LocString AfterStayDetail = new("plan.after.stay.detail", "Just stop. You're left standing at the last vista.");
        public static readonly LocString AfterInnToken = new("plan.after.inn.token", "return to the inn");
        public static readonly LocString AfterInnName = new("plan.after.inn.name", "Return to the inn");
        public static readonly LocString AfterInnDetail = new("plan.after.inn.detail", "Travel to your Grand Company city and enter the inn room.");
        public static readonly LocString AfterLogoutToken = new("plan.after.logout.token", "log out");
        public static readonly LocString AfterLogoutName = new("plan.after.logout.name", "Log out to title");
        public static readonly LocString AfterLogoutDetail = new("plan.after.logout.detail", "Log out to the title screen.");
        public static readonly LocString AfterCloseToken = new("plan.after.close.token", "close the game");
        public static readonly LocString AfterCloseName = new("plan.after.close.name", "Close the game");
        public static readonly LocString AfterCloseDetail = new("plan.after.close.detail", "Close FFXIV entirely (via XIVLauncher's /xlkill).");
        public static readonly LocString WhenDone = new("plan.whenDone", "When every vista is done");
    }

    internal static class Library
    {
        public static readonly LocString Title = new("library.title", "Sightseeing Log");
        public static readonly LocString SelectedSummary = new("library.selected", "{0} of {1} selected");
        public static readonly LocString RecordedSummary = new("library.recorded", "{0} of {1} in your log");
        public static readonly LocString LockedRunning = new("library.lockedRunning", "Stop the run to change your vistas.");
        public static readonly LocString LockedQuest = new("library.lockedQuest", "Locked: complete “{0}” to unlock this vista.");
        public static readonly LocString LockedFirstLog = new("library.lockedFirstLog", "Locked: record the first 20 vistas, then speak to {0} in {1}.");
        public static readonly LocString StatusDone = new("library.status.done", "done");
        public static readonly LocString StatusOpen = new("library.status.open", "open");
        public static readonly LocString StatusLeft = new("library.status.left", "{0} left");
        public static readonly LocString StatusIn = new("library.status.in", "in {0}");
        public static readonly LocString StatusNoWindow = new("library.status.noWindow", "not soon");
        public static readonly LocString StatusLocked = new("library.status.locked", "locked");
        public static readonly LocString TooltipEmote = new("library.tooltip.emote", "Emote: {0}");
        public static readonly LocString TooltipTime = new("library.tooltip.time", "Eorzea time: {0} to {1}");
        public static readonly LocString TooltipWeather = new("library.tooltip.weather", "Weather: {0}");
        public static readonly LocString TooltipOpenFor = new("library.tooltip.openFor", "Open now, for {0} more.");
        public static readonly LocString TooltipOpenAlways = new("library.tooltip.openAlways", "Open at any hour, in any weather.");
        public static readonly LocString TooltipOpensIn = new("library.tooltip.opensIn", "Opens in {0}.");
        public static readonly LocString TooltipNoWindow = new("library.tooltip.noWindow", "No window in the next few days.");
        public static readonly LocString TooltipDone = new("library.tooltip.done", "Already in your log.");
        public static readonly LocString TooltipJumpPuzzle = new("library.tooltip.jumpPuzzle", "Reached by a jump puzzle, so the run leaves this one to you.");
        public static readonly LocString TooltipNpcGate = new("library.tooltip.npcGate", "An NPC opens the way to this spot.");
        public static readonly LocString TooltipIndoors = new("library.tooltip.indoors", "The last stretch is indoors, on foot.");
        public static readonly LocString TooltipLedge = new("library.tooltip.ledge", "Recorded from a ledge next to the marker.");
        public static readonly LocString NoVistas = new("library.noVistas", "No vistas found in game data.");
        public static readonly LocString NotLoggedIn = new("library.notLoggedIn", "Log in to read your Sightseeing Log.");
        public static readonly LocString ExpansionArr = new("library.expansion.arr", "A Realm Reborn");
        public static readonly LocString ExpansionHw = new("library.expansion.hw", "Heavensward");
        public static readonly LocString ExpansionSb = new("library.expansion.sb", "Stormblood");
        public static readonly LocString ExpansionShb = new("library.expansion.shb", "Shadowbringers");
        public static readonly LocString ExpansionEw = new("library.expansion.ew", "Endwalker");
        public static readonly LocString ExpansionDt = new("library.expansion.dt", "Dawntrail");
    }

    internal static class Tour
    {
        public static readonly LocString Start = new("tour.start", "START");
        public static readonly LocString Stop = new("tour.stop", "STOP");
        public static readonly LocString PauseCaps = new("tour.pause", "PAUSE");
        public static readonly LocString ResumeCaps = new("tour.resume", "RESUME");
        public static readonly LocString InContent = new("tour.inContent", "in content");
        public static readonly LocString ReasonInstall = new("tour.reason.install", "install the required plugins");
        public static readonly LocString ReasonOffline = new("tour.reason.offline", "log in to a character");
        public static readonly LocString ReasonPick = new("tour.reason.pick", "pick at least one vista");
        public static readonly LocString ReasonAllDone = new("tour.reason.allDone", "every vista you picked is in your log");
        public static readonly LocString ReasonLocked = new("tour.reason.locked", "every vista left is still locked");
        public static readonly LocString StartSub = new("tour.startSub", "{0}  ·  {1}");
        public static readonly LocPlural OpenNow = new("tour.openNow", "{0} open now", "{0} open now");
        public static readonly LocString StateRunning = new("tour.state.running", "running");
        public static readonly LocString StatePaused = new("tour.state.paused", "paused");
        public static readonly LocString StopSub = new("tour.stopSub", "{0} · {1}");
    }

    internal static class Run
    {
        public static readonly LocString PhaseReading = new("run.phase.reading", "Reading your log");
        public static readonly LocString PhaseTravelling = new("run.phase.travelling", "Travelling");
        public static readonly LocString PhaseWaiting = new("run.phase.waiting", "Waiting for the window");
        public static readonly LocString PhaseEmoting = new("run.phase.emoting", "Taking in the view");
        public static readonly LocString PhaseFinishing = new("run.phase.finishing", "Finishing up");
        public static readonly LocString PhaseStandingBy = new("run.phase.standingBy", "Standing by");
        public static readonly LocString PhaseReady = new("run.phase.ready", "Ready");
        public static readonly LocString PhasePaused = new("run.phase.paused", "Paused");
        public static readonly LocString PhasePausedInContent = new("run.phase.pausedInContent", "Paused (in content)");
        public static readonly LocString UpNext = new("run.upNext", "Up next");
        public static readonly LocString NothingLeft = new("run.nothingLeft", "Nothing left in your plan.");
        public static readonly LocString QueueFirst = new("run.queueFirst", "Vistas show up here once the log is read.");
        public static readonly LocPlural InPlay = new("run.inPlay", "{1}  ·  {0} vista in play", "{1}  ·  {0} vistas in play");
        public static readonly LocString SomewhereElse = new("run.somewhereElse", "Somewhere else");
        public static readonly LocString TileVistas = new("run.tile.vistas", "Vistas");
        public static readonly LocString TileZones = new("run.tile.zones", "Zones");
        public static readonly LocString TileOpen = new("run.tile.open", "Open now");
        public static readonly LocString TileElapsed = new("run.tile.elapsed", "Elapsed");
        public static readonly LocString GoalOf = new("run.goal.of", "/ {0}");
        public static readonly LocPlural VistasToGo = new("run.goal.vistasToGo", "{0} vista to go", "{0} vistas to go");
        public static readonly LocString AllRecorded = new("run.goal.allRecorded", "every vista recorded");
    }

    internal static class History
    {
        public static readonly LocString Title = new("history.title", "History");
        public static readonly LocString Empty = new("history.empty", "Your finished runs will show up here.");
        public static readonly LocPlural Summary = new("history.summary", "{0} recorded run  ·  {1} sightseeing  ·  {2} vistas/h average", "{0} recorded runs  ·  {1} sightseeing  ·  {2} vistas/h average");
        public static readonly LocString TileRuns = new("history.tile.runs", "Runs");
        public static readonly LocString TileVistas = new("history.tile.vistas", "Vistas");
        public static readonly LocString TileZones = new("history.tile.zones", "Zones");
        public static readonly LocString TileTime = new("history.tile.time", "Time");
        public static readonly LocString NoRuns = new("history.noRuns", "No runs recorded yet. Finish (or stop) a tour and it'll show up here.");
        public static readonly LocString VistasPerRun = new("history.vistasPerRun", "Vistas per run");
        public static readonly LocString RecentRuns = new("history.recentRuns", "Recent runs");
        public static readonly LocPlural ChartRange = new("history.chartRange", "last {0} run  ·  oldest to newest", "last {0} runs  ·  oldest to newest");
        public static readonly LocString ChartPeak = new("history.chartPeak", "peak {0}");
        public static readonly LocString ChartTooltip = new("history.chartTooltip", "{0}  ·  {1} vistas  ·  {2}");
        public static readonly LocString TooltipRate = new("history.tooltip.rate", "Rate: {0} vistas/h");
        public static readonly LocString TooltipVistas = new("history.tooltip.vistas", "Vistas: {0}");
        public static readonly LocString JustNow = new("history.time.justNow", "just now");
        public static readonly LocString MinutesAgo = new("history.time.minutesAgo", "{0}m ago");
        public static readonly LocString HoursAgo = new("history.time.hoursAgo", "{0}h ago");
        public static readonly LocString DaysAgo = new("history.time.daysAgo", "{0}d ago");
        public static readonly LocString ClearHistory = new("history.clear", "Clear history");
        public static readonly LocString ClearQuestion = new("history.clearQuestion", "Delete all recorded runs?");
        public static readonly LocString ClearYes = new("history.clearYes", "Yes, clear");
    }

    internal static class Plugins
    {
        public static readonly LocString Title = new("plugins.title", "Plugins");
        public static readonly LocString AllInstalled = new("plugins.allInstalled", "All required plugins are installed and loaded.");
        public static readonly LocPlural Missing = new("plugins.missing", "{0} required plugin is missing.", "{0} required plugins are missing.");
        public static readonly LocString Required = new("plugins.required", "Required");
        public static readonly LocString Optional = new("plugins.optional", "Optional");
        public static readonly LocString Installed = new("plugins.installed", "Installed");
        public static readonly LocString Install = new("plugins.install", "Install");
        public static readonly LocString Installing = new("plugins.installing", "Installing…");
        public static readonly LocString RepoHint = new("plugins.repoHint", "Repo: {0}\nLeft-click to open repo URL · right-click to copy");
        public static readonly LocString Footer = new("plugins.footer",
            "Install adds the plugin's source repository to Dalamud and queues an install. If one-click install fails (URL drift, network), right-click a plugin name to copy its repo URL and add it manually via /xlsettings -> Experimental -> Custom Plugin Repositories.");
        public static readonly LocString PurposeVnavmesh = new("plugins.purpose.vnavmesh", "Pathfinding, flying, and movement to every vista.");
    }

    internal static class About
    {
        public static readonly LocString Connect = new("about.connect", "Connect");
        public static readonly LocString SupportTitle = new("about.support.title", "Made with care");
        public static readonly LocString SupportBody = new("about.support.body", "I build and maintain this in my spare time. If it has helped you, a Patreon membership lets me keep improving it. No pressure, and thank you for being here.");
        public static readonly LocString SupportButton = new("about.support.button", "Support on Patreon");
        public static readonly LocString PatreonHint = new("about.support.hint", "Open Patreon · right-click to copy");
        public static readonly LocString LinkHint = new("about.linkHint", "Click to open · right-click to copy");
        public static readonly LocString MadeBy = new("about.madeBy", "Made by {0}");
        public static readonly LocString Version = new("about.version", "v {0}");
        public static readonly LocString LinkGitHub = new("about.link.github", "GitHub");
        public static readonly LocString LinkDiscord = new("about.link.discord", "Discord");
        public static readonly LocString LinkDiscussions = new("about.link.discussions", "Discussions");
        public static readonly LocString LinkBug = new("about.link.bug", "Report a bug");
        public static readonly LocString LinkMore = new("about.link.more", "More plugins");
        public static readonly LocString LinkSecurity = new("about.link.security", "Security");
        public static readonly LocString ReminderTitle = new("about.reminder.title", "A little reminder");
        public static readonly LocString FactsTitle = new("about.facts.title", "Did you know?");
        public static readonly LocString QuotesTitle = new("about.quotes.title", "Words to live by");
        public static readonly LocString JokesTitle = new("about.jokes.title", "Just for fun");

        public static readonly LocString[] Reminders =
        [
            new("about.reminder.1", "Been at it a while? Roll your shoulders and take one slow breath."),
            new("about.reminder.2", "Hydration check. When did you last drink some water?"),
            new("about.reminder.3", "Blink a few times and let your eyes rest for a moment."),
            new("about.reminder.4", "Stand up, stretch, and shake out your hands. Future you says thanks."),
            new("about.reminder.5", "Sit up and settle in comfortably. Your back will thank you later."),
            new("about.reminder.6", "Remember to eat something today. You matter more than any score."),
            new("about.reminder.7", "Eyes feel tired? Look at something far away for twenty seconds."),
            new("about.reminder.8", "Whatever you're chasing, you're allowed to take a break whenever."),
            new("about.reminder.9", "You're doing great. Be a little kinder to yourself today."),
            new("about.reminder.10", "A glass of water and a quick stretch can reset a long session."),
            new("about.reminder.11", "Unclench your jaw and drop your shoulders. There you go."),
            new("about.reminder.12", "Rest is part of the journey too. Step away whenever you need to."),
        ];

        public static readonly LocString[] Facts =
        [
            new("about.facts.1", "Honey never spoils. Jars over 3,000 years old have been found still edible."),
            new("about.facts.2", "Octopuses have three hearts and blue blood."),
            new("about.facts.3", "A day on Venus is longer than a whole year on Venus."),
            new("about.facts.4", "Bananas are berries, but strawberries aren't."),
            new("about.facts.5", "There are more possible chess games than atoms in the observable universe."),
            new("about.facts.6", "Sharks have been around longer than trees have."),
            new("about.facts.7", "A group of flamingos is called a flamboyance."),
            new("about.facts.8", "Honeybees can recognize individual human faces."),
            new("about.facts.9", "Wombat droppings are cube shaped."),
            new("about.facts.10", "The Eiffel Tower can grow over 15 cm taller on a hot day."),
            new("about.facts.11", "Hot water can sometimes freeze faster than cold water."),
            new("about.facts.12", "A bolt of lightning is roughly five times hotter than the surface of the Sun."),
        ];

        public static readonly LocString[] Quotes =
        [
            new("about.quotes.1", "Done is better than perfect. You can always polish later."),
            new("about.quotes.2", "Small steps every day add up to surprising distances."),
            new("about.quotes.3", "Comparison is the thief of joy. Run your own race."),
            new("about.quotes.4", "Progress, not perfection."),
            new("about.quotes.5", "You don't have to be great to start, but you have to start to be great."),
            new("about.quotes.6", "Be patient with yourself. Growth takes time."),
            new("about.quotes.7", "The best time to begin was yesterday. The second best is right now."),
            new("about.quotes.8", "Celebrate the small wins. They count too."),
            new("about.quotes.9", "Slow progress is still progress."),
            new("about.quotes.10", "Your only real competition is who you were yesterday."),
        ];

        public static readonly LocString[] Jokes =
        [
            new("about.jokes.1", "Why don't scientists trust atoms? Because they make up everything."),
            new("about.jokes.2", "I would tell you a chemistry joke, but I know I wouldn't get a reaction."),
            new("about.jokes.3", "Why did the scarecrow win an award? He was outstanding in his field."),
            new("about.jokes.4", "I'm reading a book about anti-gravity. It's impossible to put down."),
            new("about.jokes.5", "Why don't skeletons fight each other? They don't have the guts."),
            new("about.jokes.6", "What do you call fake spaghetti? An impasta."),
            new("about.jokes.7", "Why did the bicycle fall over? It was two tired."),
            new("about.jokes.8", "What do you call cheese that isn't yours? Nacho cheese."),
            new("about.jokes.9", "I'm on a seafood diet. I see food, and I eat it."),
            new("about.jokes.10", "I only know 25 letters of the alphabet. I don't know y."),
        ];
    }

    internal static class Settings
    {
        public static readonly LocString Title = new("settings.title", "Settings");
        public static readonly LocString Language = new("settings.language", "Language");
        public static readonly LocString LanguageHelp = new("settings.languageHelp", "The language of this plugin's windows. Zone and vista names always follow the game client.");
        public static readonly LocString SearchHint = new("settings.searchHint", "Search...");

        public static readonly LocString CatGeneral = new("settings.cat.general", "General");
        public static readonly LocString CatGeneralSub = new("settings.cat.generalSub", "Window and behavior preferences.");

        public static readonly LocString GeneralWindow = new("settings.general.window", "Window");
        public static readonly LocString OpenOnLogin = new("settings.general.openOnLogin", "Open on login");
        public static readonly LocString OpenOnLoginHelp = new("settings.general.openOnLoginHelp", "Pop the main window automatically the next time you log in.");
        public static readonly LocString GeneralBehavior = new("settings.general.behavior", "Behavior");
        public static readonly LocString AutoPause = new("settings.general.autoPause", "Auto-pause in content");
        public static readonly LocString AutoPauseHelp = new("settings.general.autoPauseHelp", "Pause the run while you are inside a duty, trial, raid, or any other instanced content, then resume it once you are back outside. Your plan and session stats are kept.");
        public static readonly LocString AskAtHardSpots = new("settings.general.askAtHardSpots", "Ask me at hard spots");
        public static readonly LocString AskAtHardSpotsHelp = new("settings.general.askAtHardSpotsHelp", "Some vistas sit on a small object the character has to be jumped onto. When the plugin cannot get onto a spot by itself, it marks the spot in the world and waits up to a minute for you to stand inside it, then carries on. Turn this off for unattended runs.");
    }

    internal static class Plugin
    {
        public static readonly LocString CommandHelp = new("plugin.commandHelp", "Toggle the Auto Sightseeing Log window. /asl config | stats | deps | about | pause (pause or resume the run) | goto <number> (travel to a vista without logging it; goto stop cancels) | logdump (write the Sightseeing Log state to the plugin log).");
        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /asl.");
    }
}
