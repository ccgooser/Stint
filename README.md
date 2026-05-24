# Stint

> *For the chronically multitasking.*

Stint is a lightweight, keyboard-driven time tracker for Windows that lives in the system tray and stays out of your way. It's built for people who juggle multiple concurrent tasks and need to track time without breaking their flow.

---

## What makes it different

Most time trackers make you click through a UI every time you want to log something. Stint uses a **command bar** approach — one keyboard shortcut brings up a small popup, and a handful of intuitive commands let you start, stop, and report on events without ever touching the mouse.

You can have multiple events running simultaneously, stop them by index, and generate reports — all from the same text field.

---

## Features

- **System tray app** — always running, never in the way
- **Global hotkey** — configurable shortcut (default `Alt+Shift+T`) to show/hide
- **Concurrent events** — track multiple tasks at the same time
- **Command bar** — keyboard-driven interface with intuitive command prefixes
- **Autocomplete** — title and category fields learn from your history
- **Dark theme** — easy on the eyes
- **HTML reports** — today, yesterday, this week, this month, full history
- **Edit & delete** — retrospectively fix start/stop times on any event
- **Auto-stop** — configurable maximum duration with a 50% warning notification
- **URL protocol** — `stint://` scheme for integration with external apps
- **Persistent storage** — SQLite database survives reboots
- **Retroactive entry** — log events that started in the past with `@HH:mm`

---

## Command Reference

All commands are entered in the **Action** field:

| Command | Description |
|---|---|
| `title` | Start a new event |
| `@HH:mm title` | Start an event with a retroactive start time |
| `#n` + Enter | Stop active event by row index |
| `!n` + Enter | Open edit form for active event by row index |
| `?today` | Report — today's totals by event and category |
| `?yesterday` | Report — yesterday's totals |
| `?week` | Report — this week's totals (Monday to today) |
| `?month` | Report — this month's totals |
| `?history` | Full log of every event ever recorded |
| `?open` | Open the reports folder in Explorer |
| `?help` | Open the full help page |

Double-clicking an active event row also opens the edit form.  
**Ctrl+Z** undoes an accidental stop, restoring the original start time.

---

## URL Protocol

Stint registers a `stint://` URL scheme for integration with external applications, scripts, and automation tools. The running instance receives URLs and acts on them immediately.

```
stint://show
stint://start?title=Meeting&category=DEV
stint://start?title=Meeting&category=DEV&time=09:30
stint://start?title=Meeting&guid=your-unique-id
stint://stop?id=42
stint://stop?guid=your-unique-id
stint://report?type=today
stint://edit?id=42
```

The optional `guid` parameter allows external apps to supply their own reference ID for events, enabling them to start and stop events without needing to track Stint's internal IDs.

URLs can be triggered from a browser address bar, `Win+R`, batch files, or PowerShell:

```powershell
Start-Process "stint://start?title=Daily+Standup&category=MEETINGS"
```

---

## Configuration

A `config.txt` file is created alongside the executable on first run. Delete it to regenerate defaults.

```ini
# Stint configuration

# Global hotkey — modifiers: Alt, Shift, Ctrl
hotkey=Alt+Shift+T

# Auto-stop threshold in minutes. Set to 'off' to disable.
max_duration_minutes=480

# Folder where HTML reports are saved
report_path=.\Reports
```

---

## Data

All event data is stored in a SQLite database:

```
%LOCALAPPDATA%\Stint\stint.db
```

The database survives app restarts and Windows reboots. Active events at shutdown are still running when you start the app again — intentionally, since your work didn't stop just because your computer did.

The database can be queried directly with any SQLite tool for custom reporting or integration.

---

## Customising Help

Drop a `help.html` file alongside the executable to customise the help page. The file should contain HTML body content only — no `<html>`, `<head>`, or `<body>` tags. The app wraps it in a matching dark-themed shell.

---

## Building from Source

**Requirements:**
- .NET 8 SDK
- Windows (WinForms, Windows-only by design)

```bash
git clone https://github.com/ccgooser/Stint.git
cd stint
dotnet restore
dotnet build
```

**Dependencies:**
- [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) — local database
- [DarkUI](https://www.nuget.org/packages/DarkUI) — dark theme controls

---

## Roadmap

- [ ] Voice companion app — button + microphone for hands-free time logging via the `stint://` protocol
- [ ] Shell command interface (`stint start "Meeting"`) as an alternative to the URL scheme
- [ ] Additional report types and date range filtering

---

## Licence

MIT — do what you like with it.

---

*Built with [Claude](https://claude.ai) in a single extended pair programming session. The command bar approach, URL protocol design, and voice companion concept are original — if you haven't seen a time tracker quite like this before, that's intentional.*