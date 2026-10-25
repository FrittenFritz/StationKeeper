# Changelog

## [1.3.1] - 2026-10-25
### Fixed
- Phantom system input reset the PULSE to STANDBY, so pulses never fired on some PCs. Windows' generic "last input" time (`GetLastInputInfo`) is updated by other software every 60 s even with nobody at the PC. Keyboard/mouse activity now comes from low-level hooks that count only hardware events; injected input and Station Keeper's own pulses (tagged) are ignored. The AFK window uses the same real-input source.
- Joystick/HOTAS: any axis movement, button or hat (POV) counts as input.

### Changed
- PULSE is a small relative mouse move (15 counts right in 3 steps, ~100 ms pause, same back); fixes wrong INPUT struct size that made earlier pulses fail silently on 64-bit. `SendInput` results are now checked: LAST EVENT shows "Mouse pulse sent." or "Pulse failed (err N)".
- LAST EVENT shows why the PULSE went back to STANDBY, e.g. `STANDBY: keyboard/mouse input` or `STANDBY: joystick input`.
- AFK window hold: while the PULSE is ARMED and the game is in focus, the AFK window is always held at full time (greyed, no countdown, no warning tone, state HELD BY PULSE), as in 1.3.0. When the hold ends (PULSE OFF or the game loses focus), it counts from your last real input or the last pulse instead of restarting at a full window.
- The window title and the version label in the header are now centered exactly, horizontally and vertically.
- The PULSE switch and its OFF/ARMED label are red when OFF and green when ARMED.
- Fixed a once-per-second game stutter while Station Keeper was open: keyboard/mouse hooks now run on their own thread, and only connected joysticks are polled, in the background.
- The version number (e.g. v1.3.1) is shown small and dimmed in the header, right of the title, and in the tray icon tooltip.

## [1.3.0] - 2026-10-25
### Added
- MINI mode: a **MINI** checkbox next to Always on top shrinks the same window to a compact, semi-transparent, always-on-top overlay with the AFK window countdown and a PULSE status dot (cyan: ARMED, red: OFF).
  - Drag the unlocked mini window anywhere with the mouse.
  - The lock icon fixes it in place and makes it click-through. The mini window never takes focus from the game (`WS_EX_NOACTIVATE`, no activation when switching views).
  - Tray icon with **Show / Unlock mini / Normal view / Exit**, because a locked window ignores clicks. Right-click the unlocked mini window for the same menu.
  - Mini state, lock, position and opacity are saved (`MiniMode`, `MiniLocked`, `MiniX`, `MiniY`, `MiniOpacity`). The saved position is clamped to a visible screen on load.
- Mini opacity slider (10-100 %) in the normal window, applied live. The mouse wheel over the unlocked mini window changes it too.
- PULSE line in the mini window while the PULSE is ARMED: NEXT PULSE countdown or STANDBY, amber GAME NOT IN FOCUS / red GAME NOT RUNNING when the pulse would have no effect. The line is hidden with PULSE OFF.
- `--screenshot-mini` flag. `--screenshot` renders now use the built-in defaults instead of your settings.ini.

### Changed
- PULSE idle delay: the PULSE countdown only starts after `AfkGraceSeconds` without real input and shows **STANDBY** until then.
  - Real input (mouse, keyboard, joystick/HOTAS) resets it to STANDBY. Once you are idle again, it starts with a fresh random interval.
  - Station Keeper's own pulses are ignored as input.
  - Safety check: if there was real input in the last 2 s, the pulse is skipped and the timer goes back to STANDBY.
- The AFK window is held at full time while the PULSE is ARMED and the game is in focus: shown greyed, no countdown and no warning tone (state HELD BY PULSE). It resumes from a fresh full window when the PULSE is OFF or the game loses focus.

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