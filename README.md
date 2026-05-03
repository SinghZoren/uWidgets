# uWidgets Desktop Widgets

uWidgets is a portable Windows desktop widget app built with .NET 8 and Avalonia. This fork keeps the original widget shell and adds a more complete set of desktop-first productivity and media widgets with a dark Apple-style glass UI.

The app is designed to be local-first. Widget layout/settings stay portable with the app, while newer widgets use shared local JSON stores so the compact desktop widgets and full app windows stay in sync.

## What Changed In This Version

This version adds and upgrades several major areas:

- New Pomodoro widget with focus/break sessions, session progress, responsive widget layouts, and optional custom MP3 alarm sound.
- Rebuilt Todo/Reminders into a full local-first Todo app plus synced desktop widget.
- Added Todo natural-language quick add, task details, categories, priorities, due dates/times, recurrence, subtasks, board view, calendar list, stats, search, keyboard shortcuts, and local JSON persistence.
- Added Obsidian-backed Notes behavior. The notes widget can show recent vault notes, create new Markdown notes, and open notes directly in Obsidian instead of using an internal editor.
- Added Spotify widget with OAuth login, playlist selection, playback controls, shuffle, progress, cover art carousel, Spotify Connect device handling, and fast optimistic cover-art updates on skip.
- Added global glassmorphism improvements: transparent widget backgrounds, blur level, opacity level, corner radius, accent color support, and more consistent dark translucent styling.
- Fixed several crashes and UI issues around Notes, Todo, Pomodoro sizing, transparent widget rendering, playlist counts, and Spotify playback controls.

## Widgets

### Clock

Original clock widgets are preserved, including analog, digital, and world-clock styles.

### Calendar

Month and day calendar widgets with the original compact desktop layout.

### Weather

Weather widgets remain available for forecast, temperature, UV index, sunrise/sunset, pressure, and air quality.

### Monitor

System monitor widgets for CPU, RAM, disk, network, and battery.

### Pomodoro

The Pomodoro widget is built for fast focus sessions on the desktop.

Features:

- Focus, short break, and long break phases.
- Start, reset, and skip controls.
- Completed focus session counter.
- Responsive layouts for small, medium, large, and extra large widgets.
- Optional custom MP3 alarm sound in the widget settings.
- Visual styling aligned with the glass widget system.

### Todo

The Todo widget is a compact view of the same local data used by the full Todo app.

Widget features:

- Quick add from the widget.
- Today, Upcoming, and Done sections.
- Checkbox completion directly from the desktop.
- Active/done counts.
- Open button for the full Todo app.
- Shared storage through `TodoData.json`.

Full Todo app features:

- Natural quick add, for example: `Finish report tomorrow 5pm #school !high`.
- Parser support for categories, priority, today/tomorrow/weekdays/next week, and common time formats.
- Today, Upcoming, All, Board, Calendar, and Stats views.
- Editable task details panel.
- Notes, due date, due time, priority, category, recurrence, status, and subtasks.
- Category manager with automatic `#tag` category creation.
- Recurring tasks that create the next occurrence when completed.
- Search across task title, notes, category, and subtask text.
- Keyboard shortcuts: Enter to quick add, Ctrl/Cmd+K to focus quick add, Delete to remove selected task when not typing, and Escape to clear/deselect.

### Notes And Obsidian

The Notes widget is now meant to work as an Obsidian companion.

Features:

- Panel tab for recent notes.
- Create a new note from the widget.
- Open notes directly in Obsidian using `obsidian://open`.
- Automatic vault detection from Obsidian config when possible.
- Manual vault path setting when auto-detection is not enough.
- Recent note previews pulled from Markdown files in the vault.

Notes can still store local widget state in `NotesData.json`, but the intended workflow is to keep your real writing in your Obsidian vault.

### Spotify

The Spotify widget uses Spotify Web API OAuth and Spotify Connect.

Features:

- Connect with a Spotify Developer app client ID.
- Current track title, artist, progress, and cover art.
- Previous/current/next cover art carousel.
- Play/pause, previous, next, and shuffle controls.
- Playlist selector that starts playback when changed.
- Playlist track counts.
- Optimistic cover-art updates so skipping feels instant.
- Open Spotify helper when no playback device is available.

Important Spotify notes:

- Playback control requires a Spotify Premium account.
- Spotify must have an available Spotify Connect device. Open the Spotify desktop app, mobile app, or web player at least once so the API has a device to target.
- Add this redirect URI to your Spotify Developer app: `http://127.0.0.1:55432/callback/`.
- The default widget redirect URI includes the trailing slash. Keep it exactly the same in Spotify settings and widget settings.

## Local Data

The app is portable and stores data near the app folder/build output.

Common generated files:

- `layout.json`: widget layout and per-widget settings.
- `TodoData.json`: shared Todo app/widget data.
- `NotesData.json`: local notes metadata.

These files are intentionally ignored by Git because they are user state, not source code.

## Requirements

- Windows 8.1 or later.
- .NET 8 SDK for development.
- .NET 8 Runtime for running framework-dependent builds.
- Spotify Premium for Spotify playback controls.
- Obsidian installed if you want direct Obsidian note opening.

## Build From Source

Restore dependencies:

```powershell
dotnet restore src\uWidgets.sln
```

Build the solution:

```powershell
dotnet build src\uWidgets.sln --no-restore -m:1 -v minimal
```

Recommended development run after building the solution:

```powershell
.\src\uWidgets\bin\Debug\net8.0\uWidgets.exe
```

You can also run the main project directly, but build the full solution first so the dynamically loaded widget DLLs are present:

```powershell
dotnet run --project src\uWidgets\uWidgets.csproj
```

## Publish A Local Release

Build the solution in Release first. This generates the dynamically loaded widget DLLs:

```powershell
dotnet build src\uWidgets.sln -c Release --no-restore -m:1 -v minimal
```

Framework-dependent single-file publish for the main app:

```powershell
dotnet publish src\uWidgets\uWidgets.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\uWidgets
```

Copy the widget plugin folder into the publish folder:

```powershell
Copy-Item -Path src\uWidgets\bin\Release\net8.0\Widgets -Destination dist\uWidgets\Widgets -Recurse -Force
```

Self-contained publish for the main app:

```powershell
dotnet publish src\uWidgets\uWidgets.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\uWidgets-self-contained
```

Copy the widget plugin folder into the self-contained publish folder:

```powershell
Copy-Item -Path src\uWidgets\bin\Release\net8.0\Widgets -Destination dist\uWidgets-self-contained\Widgets -Recurse -Force
```

After publishing, launch:

```powershell
dist\uWidgets\uWidgets.exe
```

## Spotify Setup

1. Go to the Spotify Developer Dashboard and create or reuse an app.
2. Copy the app client ID into the Spotify widget settings.
3. Add this redirect URI in the Spotify app settings:

```text
http://127.0.0.1:55432/callback/
```

4. Save the Spotify app settings.
5. In uWidgets, open Spotify widget settings and click `Connect Spotify`.
6. Keep Spotify open on at least one device so playback commands have a target.

## Obsidian Setup

1. Install Obsidian.
2. Open the vault you want the widget to use.
3. Add the Notes widget.
4. If auto-detection does not find the right vault, set the vault path in Notes widget settings.
5. Use the widget panel to create or open notes. Notes open directly in Obsidian.

## Development Notes

- Widgets live in `src\Widgets`.
- The main app shell lives in `src\uWidgets`.
- Shared app contracts live in `src\uWidgets.Core`.
- Todo business logic is split into parser, selectors, storage, store, and widget bridge services under `src\Widgets\Reminders\Services`.
- Notes Obsidian integration lives under `src\Widgets\Notes\Services`.
- Spotify OAuth and Web API code lives under `src\Widgets\Spotify\Services`.

If a build fails because a widget DLL is locked, close the running app first:

```powershell
Get-Process -Name uWidgets -ErrorAction SilentlyContinue | Stop-Process -Force
```

Then rebuild.

## Release Checklist

Before pushing or publishing a release:

1. Build with `dotnet build src\uWidgets.sln --no-restore -m:1 -v minimal`.
2. Launch `src\uWidgets\bin\Debug\net8.0\uWidgets.exe`.
3. Smoke-test Todo quick add, Notes Obsidian open, Pomodoro alarm settings, and Spotify playback controls.
4. Confirm `layout.json`, `TodoData.json`, and `NotesData.json` are not staged.
5. Publish into `dist\` only when you are ready to package a release.

## License And Upstream

This project is based on uWidgets by creewick. Keep the original license terms from `LICENSE.txt` when redistributing.
