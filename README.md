# SamReMake

**English** | [简体中文](README.zh-CN.md)

A Windows desktop application for browsing and managing Steam achievements and statistics, based on [SamRewritten](https://github.com/PaulCombal/SamRewritten). It adds a native desktop workspace with searchable lists, real unlock-time sorting, scheduling, friend-timeline replay, backups and language settings.

This is an independent development preview. It is not an official release of SamRewritten and is not affiliated with Valve.

## Getting started

1. Start the Steam desktop client and sign in to the account you want to use.
2. Extract the **complete portable package**, keeping its `engine` folder beside `SteamAchievementManager-Remake.exe`.
3. Run `SteamAchievementManager-Remake.exe` and click **Load Steam library**, or enter an AppID and click **Open AppID**.
4. Choose a game. Open any feature directly from the top toolbar, grouped as **Game progress**, **Automation** and **Data management**.

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

The app previews the proposed changes, saves a snapshot and submits the changes after confirmation. Items that are protected or already in the requested state are excluded. If Steam rejects an item, refresh to inspect its actual state.

Relocking and unlocking again cannot preserve or recreate an earlier unlock timestamp.

### Refresh, read progress and cancellation

**Refresh F5** reloads the selected game's achievements and statistics. The bottom status area identifies the current stage, displays an animated activity bar and shows elapsed waiting time.

Steam does not provide a reliable completion percentage for these reads, so the activity bar intentionally shows no percentage. **Cancel read** stops the active read request. Selecting another game cancels the previous read before starting the next one.

Ordinary reads have a 25-second limit per request. Advanced friend-timeline reads may take up to 45 seconds; multi-game exports may take up to 120 seconds. Achievements and statistics are separate requests. Cancelling a read does not undo a completed write, and in-flight writes are not forcibly interrupted.

### Statistics editing

Open **Statistics**, select a row, enter a new value and click **Save selected stat**. This edits an exposed integer or floating-point game statistic, such as a counter defined by the game.

The table shows the API ID, current value and access restrictions. The app checks numeric input, protected fields, increase-only restrictions and reported ranges where applicable. Use a period for decimal values. A snapshot is saved before submission, and Steam can still reject a value.

### Single-game export and import

- **Export progress** saves the selected game's achievement states and statistics to a JSON file. Use it to inspect or keep a portable copy of the current progress data.
- **Import progress** reads a compatible JSON export and applies **only the currently selected AppID**. It previews the operation and saves a snapshot first.

Importing/restoring progress is about achievement states and statistic values. It cannot restore historical unlock timestamps. Protected fields may be skipped or refused by Steam.

### Interval-based schedules

In **Achievements**, select locked achievements, then open **Schedule**. Set an interval from **1 to 86400 seconds** and choose **Preview and start**.

The queue waits before the first item and between subsequent items, submitting one unlock request at a time. This is useful for spreading a selection of unlocks over time. Steam request time is added to the waits, so the interval is not an exact wall-clock completion guarantee.

**Stop pending tasks** cancels future queue items. A request already being submitted may still finish. A failure stops subsequent items. Keep the app open: queues are not resumed or caught up automatically after closing. Stop the queue before switching games or refreshing.

## Automation and data tools

All tools are direct pages in the main window—there is no advanced-workspace submenu or extra window. **Game progress** contains Achievements and Statistics; **Automation** contains Schedule, Friend replay and Idle; **Data management** contains Batch operations and History / restore. Steam communication uses the bundled background engine.

### Batch operations across games

Enter AppIDs separated by spaces or commas in **Batch operations**.

| Action | What it does |
| --- | --- |
| Export selected apps | Writes a single JSON export containing the entered AppIDs. |
| Import backup file | Imports the apps contained in the selected JSON file. The file determines the target apps; the AppID textbox does not filter this action. |
| Unlock all achievements | Unlocks the achievements exposed by each entered app, subject to Steam permissions. |
| Reset achievements and stats | Resets both achievements **and statistics** for the entered apps. This is broader than relocking selected achievements. |

Writes are confirmed and snapshots are saved first. Operations across games are not a single transaction: cancellation or failure can leave earlier games already processed. Cancellation stops future work; it does not roll back completed requests.

### Friend timelines and relative-time replay

Select the target game in the sidebar, then choose **Automation → Friend replay** in the top toolbar. Both are direct navigation buttons, not nested menus.

1. Click **Load friends** and select a friend, or enter a SteamID64 directly.
2. Set a replay duration of **1–10080 minutes**.
3. Click **Read and preview timeline** to see eligible achievements and their relative waits.
4. Review the plan, then choose **Start previewed plan**.

This feature reproduces the *relative spacing* of another user's known unlock times, scaled to your chosen duration. Already-unlocked target achievements, protected items and source achievements with missing timestamps are excluded. Friend data must be accessible to the signed-in Steam user.

The first eligible item has no initial delay. Equal source times have no gap; a single eligible item starts immediately. Processing time can extend the actual run. Steam records the new unlock times when the requests execute—this does **not** copy historical dates into your account.

### Idling

**Idle → Start idling** keeps the current AppID connected as in-game without starting the game's executable. It does not unlock achievements by itself.

Use **Stop idling** to request a clean shutdown. Closing the application also requests idle shutdown; wait for cleanup if Steam is slow to respond. Do not rely on graceful cleanup after forcibly terminating the process or shutting down Windows.

### History, snapshots and restoration

**Data management → History / restore** lists snapshots saved before progress-changing operations. Each entry identifies its time, AppID and reason.

Select an entry and choose **Restore selected snapshot** to import that game's earlier achievement states and statistic values. Restoration requires confirmation and first saves another snapshot of the current state. If a required backup fails, the associated write is not submitted.

Snapshots cannot restore the original achievement unlock timestamps. Entries created before snapshot support was added may exist in the text audit log without a restorable snapshot.

## Settings and local files

**Settings → Language** changes the UI between English and Simplified Chinese and remembers your selection. The navigation and tool labels update without resetting page input or active sessions.

User files are stored under:

```text
%LOCALAPPDATA%/SteamAchievementManager-Remake/
├── language.txt       Saved interface language
├── history.jsonl      Audit entries for main-workspace changes
└── snapshots/         Progress JSON files and snapshot metadata
```

These files are local user data, not part of the source repository. Keep copies of snapshots you want to retain when moving to another computer.

## Build and test

Requirements: Windows, Visual Studio with **.NET desktop development**, the **.NET Framework 4.8 targeting pack**, MSVC, and Rust stable **1.95 or later**.

From a Developer PowerShell at the repository root:

```powershell
.\build-remake.ps1
.\tests\run.ps1
```

The portable output is written to `dist/SteamAchievementManager-Remake`. The WPF frontend lives in `desktop/Remake`; the Rust source at the repository root supplies the headless engine. No submodule is required.

This remains a development preview. Builds and automated tests cover timestamps, sorting, command arguments, read cancellation/timeouts, language resources, UI read-state cleanup, serial schedules and friend replay planning. Actual Steam achievement/statistic writes, friend access and idling still require live integration validation; automated tests do not modify Steam accounts.

## Attribution and license

Based on [PaulCombal/SamRewritten](https://github.com/PaulCombal/SamRewritten). The original author notices and inherited third-party attribution are retained. See [UPSTREAM.md](UPSTREAM.md) and the [upstream README](docs/UPSTREAM-README.md) for provenance.

The frontend and combined distribution use **GPL-3.0-only**; see [LICENSE](LICENSE). SamReMake is an independent derivative project, not an official upstream release.
