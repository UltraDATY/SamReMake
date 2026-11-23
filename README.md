# SamReMake

**English** | [简体中文](README.zh-CN.md)

A Windows desktop application for browsing and managing Steam achievements and statistics, based on [SamRewritten](https://github.com/PaulCombal/SamRewritten). It adds a native desktop workspace with searchable lists, real unlock-time sorting, single-game import/export and language settings.

This is an independent development preview. It is not an official release of SamRewritten and is not affiliated with Valve.

## Getting started

1. Start the Steam desktop client and sign in to the account you want to use.
2. Extract the **complete portable package**, keeping its `engine` folder beside `SteamAchievementManager-Remake.exe`.
3. Run `SteamAchievementManager-Remake.exe` and click **Load Steam library**, or enter an AppID and click **Open AppID**.
4. Choose a game. Open **Achievements** or **Statistics** directly from the top toolbar.

The interface starts in English unless you have already saved another language. Use **Settings → Language** in the **top-left toolbar** to switch between English and Simplified Chinese.

Ordinary Steam connections are used; Steam may show your account as in-game while an app is connected.

## What each feature does

### Game library and AppID entry

- **Load Steam library** retrieves the games/apps reported for the signed-in Steam account, so you can select a game without finding its ID manually.
- **Find a game / AppID** filters the loaded library by game name or AppID. This searches the local list; it does not search the Steam store.
- **Open AppID** opens a specific Steam app directly. For example, the number in a store URL such as `/app/105600/` is an AppID. Entering an ID does not grant ownership or guarantee that the app exposes achievements or statistics.

### Achievement browsing and search

The achievement table shows the name, description, current locked/unlocked state, reported unlock time and global unlock percentage when available.

**Search achievements** matches names, descriptions and achievement API IDs. The state selector shows **All states**, **Unlocked** or **Locked**. Combine search and filtering to find a specific achievement or review what remains locked.

Game-provided names and descriptions are separate from the application's interface language; changing the UI language does not translate game data.

### Sorting and actual unlock times

Use the order selector to sort by:

| Order | Useful for |
| --- | --- |
| Newest first | Reviewing recently unlocked achievements. |
| Oldest first | Following the chronological order recorded by Steam. |
| Name A–Z | Finding achievements by name. |
| Global rate ↓ | Comparing commonly unlocked achievements with rarer ones. |

Times come from Steam's reported timestamps and are displayed in your local time zone. An unlocked achievement with no usable timestamp is shown as **Unknown**; locked achievements show a dash. Unknown times stay last in both time-sort directions.

The app does not invent missing timestamps or substitute the current clock. A timestamp is what Steam currently reports, not proof of the first time an achievement was ever legitimately earned.

### Unlocking and relocking selected achievements

Use **Ctrl** or **Shift** to select multiple achievement rows, then choose **Unlock selected** or **Relock selected**.

The app previews the proposed changes and submits them after confirmation. Items that are protected or already in the requested state are excluded. If Steam rejects an item, refresh to inspect its actual state.

Relocking and unlocking again cannot preserve or recreate an earlier unlock timestamp.

### Refresh, read progress and cancellation

**Refresh F5** reloads the selected game's achievements and statistics. The bottom status area identifies the current stage, displays an animated activity bar and shows elapsed waiting time.

Steam does not provide a reliable completion percentage for these reads, so the activity bar intentionally shows no percentage. **Cancel read** stops the active read request. Selecting another game cancels the previous read before starting the next one.

Ordinary reads have a 25-second limit per request. Achievements and statistics are separate requests. Cancelling a read does not undo a completed write, and in-flight writes are not forcibly interrupted.

### Statistics editing

Open **Statistics**, select a row, enter a new value and click **Save selected stat**. This edits an exposed integer or floating-point game statistic, such as a counter defined by the game.

The table shows the API ID, current value and access restrictions. The app checks numeric input, protected fields, increase-only restrictions and reported ranges where applicable. Use a period for decimal values. Steam can still reject a value.

### Single-game export and import

- **Export progress** saves the selected game's achievement states and statistics to a JSON file. Use it to inspect or keep a portable copy of the current progress data.
- **Import progress** reads a compatible JSON export and applies **only the currently selected AppID**. It previews the operation before submission.

Importing/restoring progress is about achievement states and statistic values. It cannot restore historical unlock timestamps. Protected fields may be skipped or refused by Steam.

## Settings and local files

Use **Settings → Language** in the top-left toolbar to switch between English and Simplified Chinese. Your selection is saved in:

```text
%LOCALAPPDATA%/SteamAchievementManager-Remake/language.txt
```

The desktop application no longer includes schedules, friend replay, idling, cross-game batch operations or history/restoration. It no longer creates automatic snapshots or audit entries. Existing `history.jsonl` and `snapshots/` files from earlier versions are left untouched; the single-game JSON import/export buttons remain available for manually saved progress files.

## Build and test

Requirements: Windows, Visual Studio with **.NET desktop development**, the **.NET Framework 4.8 targeting pack**, MSVC, and Rust stable **1.95 or later**.

From a Developer PowerShell at the repository root:

```powershell
.\build-remake.ps1
.\tests\run.ps1
```

The portable output is written to `dist/SteamAchievementManager-Remake`. The WPF frontend lives in `desktop/Remake`; the Rust source at the repository root supplies the headless engine. No submodule is required.

This remains a development preview. Builds and automated tests cover timestamps, sorting, command arguments, read cancellation/timeouts, language resources, UI read-state cleanup, the retained two-page navigation. Actual Steam achievement/statistic writes still require live integration validation; automated tests do not modify Steam accounts.

## Attribution and license

Based on [PaulCombal/SamRewritten](https://github.com/PaulCombal/SamRewritten). The original author notices and inherited third-party attribution are retained. See [UPSTREAM.md](UPSTREAM.md) and the [upstream README](docs/UPSTREAM-README.md) for provenance.

The frontend and combined distribution use **GPL-3.0-only**; see [LICENSE](LICENSE). SamReMake is an independent derivative project, not an official upstream release.

## Release downloads

Download a Windows portable ZIP from [GitHub Releases](https://github.com/UltraDATY/SamReMake/releases). Each release includes the complete executable package, its matching source archive, and a SHA-256 checksum. Historical releases are rebuilt from the exact commit stated in their release notes.

Every push to `master` runs the Windows release workflow. After the build and automated tests pass, it publishes a development-preview release with a ZIP asset; documentation updates are included. A failed build or test does not publish a package. The release version is `0.3.<workflow run number>`. A maintainer can also run the workflow manually.
