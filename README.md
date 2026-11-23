# SamReMake

A Windows desktop achievement workspace built on the Steam connection and data layer from [PaulCombal/SamRewritten](https://github.com/PaulCombal/SamRewritten). This is an independent derivative project, not an official upstream release.

## Features

- English by default. Change to Simplified Chinese in **Settings → Language**; the choice is saved locally.
- Library search, AppID entry, achievement search and state filters.
- Oldest/newest Steam unlock-time sorting. Unknown timestamps stay unknown and sort last. Local time is displayed.
- Achievement changes, integer/float statistic editing, JSON import/export and cancellable schedules.
- Integrated advanced workspace: friend timelines, relative-time replay, multi-game export/import/reset/unlock, idling, and snapshot history with restoration.
- Pre-write snapshots. A failed snapshot blocks the write. Restoration can restore states and statistics, but cannot recreate historical unlock timestamps.
- Read activity indicator, elapsed time, cancellation and bounded read timeouts. Writes are not force-cancelled.

The advanced workspace belongs to this application. It does not launch the SamRewritten graphical program. A bundled headless CLI engine handles Steam communication in the background.

## Usage

Start Steam and sign in. Run `SteamAchievementManager-Remake.exe` from the complete portable folder. Ordinary connections may show you as in-game. Choose a game, then select achievements with Ctrl/Shift. Every manual write is previewed before submission.

Open **Advanced workspace** for friend replay, multi-game operations, idling and snapshot restoration. Friend timelines require access to the source user's achievement data. Unknown source timestamps and achievements you already unlocked are excluded. Replay preserves relative intervals scaled to your chosen duration; Steam records the actual new unlock times.

Schedules run only while the app remains open. Closing the advanced workspace stops its replay and gracefully requests idle shutdown. If a write is already in flight, stopping the plan does not undo that write. History, snapshots and language preferences are stored under `%LOCALAPPDATA%/SteamAchievementManager-Remake`.

## Build

Requirements: Windows, Visual Studio with .NET desktop development and .NET Framework 4.8 targeting pack, MSVC, and Rust stable (>=1.95).

Run `build-remake.ps1` in a Developer PowerShell. The WPF frontend is in `desktop/Remake`. The Rust source at the repository root provides the headless engine. No submodule is required.

## Validation and status

This is a development preview. Release builds and tests cover timestamps, unknown-date sorting, command arguments, cancellation/timeouts, WPF read-state cleanup, serial schedules and friend replay planning. Steam library reads were verified locally. Actual achievement/stat writes, friend access and idling still require live integration validation; automated tests do not modify Steam accounts.

## Attribution and license

The frontend and combined distribution use **GPL-3.0-only**, see `LICENSE`. SamRewritten's source, author notices and license are preserved in this repository. Its inherited third-party attribution remains intact. The repository contains no separately copied original C# SAM project. Project naming does not imply affiliation with Valve or upstream maintainers.
