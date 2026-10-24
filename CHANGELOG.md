# Changelog

## [1.2.0] - 2026-10-24
### Added
- Pulse-ineffective warning: a blinking HUD banner while the PULSE is ARMED but the game is not in the foreground (amber: GAME NOT IN FOCUS, red: GAME NOT RUNNING), so you know the next pulse will have no effect. Shows a neutral NO ALERTS slot otherwise. No sound.
- `--screenshot-armed` flag for rendering the armed state with `--screenshot`.

### Changed
- The top toggle is now **ARMED / OFF** and only switches the randomized mouse PULSE (nudges are sent only while Star Citizen is in the foreground).
- PULSE OFF is a monitor-only mode: no input is generated, while the AFK window countdown and warning tone keep running.
- The AFK window state shows HELD BY PULSE while the pulse is armed and the game is in focus.
- UI and log texts say "the game" / GAME instead of SC; the panel is now called AFK WINDOW.
- README explains both parts and adds a Monitor-only mode section.

### Fixed
- Text in the warning banner and the TEST button is now centered.
- The window size is fixed (380x524); the warning banner uses a reserved slot instead of growing the window.
## [1.1.0] - 2026-10-24
### Added
- AFK window panel: a countdown of Star Citizen's idle-kick window (default 15 min, `AfkTimeoutSeconds`). It counts from the last input while the game was in the foreground, appears after 1 min idle in the game (`AfkGraceSeconds`) or immediately when tabbed out, and turns amber at 3:00 and red at 1:00.
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