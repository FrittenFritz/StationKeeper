# Changelog

## [1.1.0] - 2026-10-24
### Added
- SC AFK window panel: a countdown of Star Citizen's idle-kick window (default 15 min, `AfkTimeoutSeconds`). It counts from the last input while the game was in the foreground, appears after 1 min idle in the game (`AfkGraceSeconds`) or immediately when tabbed out, and turns amber at 3:00 and red at 1:00.
- Warning tone once per countdown at 1:00 left, generated in memory (no sound files).
- Volume slider (0-100 %, `WarningVolume`) and a TEST button.

### Changed
- Window is taller to fit the new panel; settings.ini now stores all keys.

## [1.0.0] - 2026-10-24
### Added
- First release.
- Borderless HUD-style window with ACTIVE/INACTIVE toggle, starting inactive.
- Mouse nudge of 3 px and back at a random 180-300 s interval, sent only while Star Citizen is in the foreground.
- `--min` / `--max` command-line arguments.
- "Always on top" option, saved in `%APPDATA%\StationKeeper\settings.ini`.