# RailPlanner

RailPlanner is a static railway-network planner for OpenTTD projects. It is intentionally not a real-time train simulator: the application stores a schematic network and timetable data, then checks the timetable for infrastructure conflicts.

The UI follows the dark, technical dispatcher style of RailDispatchMono and is built with MonoGame/DesktopGL on .NET 9.

The application UI and project documentation use English and ASCII-only interface text so the default SpriteFont does not depend on locale-specific characters.

## Current MVP

- Large logical map (default 1000 x 1000).
- Stations with code, name, track count and type.
- Railway lines (`LK...`) with numbered segments such as `LK001|1`, `LK001|2`, `LK001|3`.
- A railway line represents the complete route, while each segment connects two consecutive stations.
- Example structure: `LK001 Wroclaw - Klodzko` -> `LK001|1 Wroclaw - Iwiny` -> `LK001|2 Iwiny - Smardzow` -> `...` -> `LK001|N ... - Klodzko`.
- Schematic section geometry stored as map points.
- Commercial lines (`IC`, `R`, etc.) separated from infrastructure lines.
- Individual train runs with static arrival/departure times.
- `PASS` timetable semantics are represented by `StopKind.Pass` and do not require a stop.
- Optional explicit track selection for each timetable departure.
- Duplicate train runs with `D`.
- Static conflict analysis with `OK / WARNING / CONFLICT` style counters.
- Informational station track-load calculation; station occupancy does not create conflicts.
- One project file: `project.railplanner` (JSON).

## Line browser

Press `F8` to open the line view. Click a railway line in the left panel to expand it and see its ordered segments. Each segment has its own identifier in the form `LINE|SEGMENT`, for example:

```text
LK001  Wroclaw - Klodzko
  Wroclaw - Iwiny
  LK001|1  Wroclaw - Iwiny
  LK001|2  Iwiny - Smardzow
  LK001|3  Smardzow - ...
  LK001|4  ... - Klodzko
```

Selecting a segment highlights that exact section on the schematic map. The full line remains the parent object; segments are the infrastructure units used for routing and analysis.

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
| F8 | Open line browser |
| `+` / `-` | Zoom |
| Arrow keys | Pan map |
| D | Duplicate selected train |
| Delete | Delete selected station/train |
| Esc | Cancel current operation |

## Build

Requires the .NET 9 SDK. From the repository root:

```text
dotnet tool restore
dotnet restore
dotnet run
```

The project uses the same MonoGame/DesktopGL family as RailDispatchMono rather than introducing a web stack.

## Timetable model

A train occupies a directional track from the departure time at station A until the arrival time at station B. Conflicts are reported when two runs overlap on the same track. A train can pass a station without stopping; `StopKind.Pass` keeps the point in its timetable while distinguishing it from a scheduled stop.
