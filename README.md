# RailPlanner

RailPlanner is a static railway-network planner for OpenTTD projects. It is intentionally not a real-time train simulator: the application stores a schematic network and timetable data, then checks the timetable for infrastructure conflicts.

The UI follows the dark, technical dispatcher style of RailDispatchMono and is built with MonoGame/DesktopGL on .NET 9.

## Current MVP

- Large logical map (default 1000 x 1000).
- Stations with code, name, track count and type.
- Railway lines (`LK...`) with sections and directional tracks.
- Schematic section geometry stored as map points.
- Commercial lines (`IC`, `R`, etc.) separated from infrastructure lines.
- Individual train runs with static arrival/departure times.
- `PASS` timetable semantics are represented by `StopKind.Pass` and do not require a stop.
- Optional explicit track selection for each timetable departure.
- Duplicate train runs with `D`.
- Static conflict analysis with `OK / WARNING / CONFLICT` style counters.
- Informational station track-load calculation; station occupancy does not create conflicts.
- One project file: `project.railplanner` (JSON).

## Controls

| Key | Action |
|---|---|
| F1 | Select mode |
| F2 | Add station |
| F3 | Create railway section between two stations |
| F4 | Create a test train run |
| F5 | Save `project.railplanner` |
| F6 | Load `project.railplanner` |
| F7 | Analyze timetable |
| `+` / `-` | Zoom |
| Arrow keys | Pan map |
| D | Duplicate selected train |
| Delete | Delete selected station/train |
| Esc | Cancel current operation |

## Build

Requires the .NET 9 SDK. From the repository root:

```text
dotnet restore
dotnet run
```

The project uses the same MonoGame/DesktopGL family as RailDispatchMono rather than introducing a web stack.

## Timetable model

A train occupies a directional track from the departure time at station A until the arrival time at station B. Conflicts are reported when two runs overlap on the same track. A train can pass a station without stopping; `StopKind.Pass` keeps the point in its timetable while distinguishing it from a scheduled stop.
