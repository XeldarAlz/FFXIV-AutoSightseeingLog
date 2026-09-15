# Third-party notices

Auto Sightseeing Log reads every vista, its position, emote, and time window from the game's own sheets. Two kinds of facts the game data does not carry come from the project below and live in `AutoSightseeingLog/Core/Vistas/VistaData.cs`. Only those facts are shipped; the source code they were taken from is not redistributed.

## TouristMod

- Source: https://github.com/alydevs/Tourist, files `TouristMod/Weathers.cs` and `TouristMod/Windows/MainWindow.cs` at commit `a5d53632a3c848012ef6f0a14c05322a12777846`. TouristMod is alydevs' modification of Tourist by Anna Clemens, maintained by foophoof.
- Used:
  - The weather each A Realm Reborn vista asks for (log numbers 1 to 80), stored as a bit mask of Weather sheet row ids.
  - A reachable approach point for 33 vistas whose log position sits where the pathfinder cannot go.
  - Which vistas are reached indoors, from a ledge, through an NPC, or by a jump puzzle, rewritten as short categories.
- License: European Union Public Licence v. 1.2 (EUPL-1.2). The EUPL's compatibility clause (Article 5, with its Appendix listing the GNU Affero General Public License v. 3) lets these facts be distributed as part of this AGPL-3.0-or-later project. Full licence text: https://interoperable-europe.ec.europa.eu/collection/eupl/eupl-text-eupl-12
