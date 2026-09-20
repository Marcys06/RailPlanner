# RailPlanner

RailPlanner is a compact OpenTTD-inspired railway network and timetable simulator.

The current direction is a 24-hour discrete simulation, not a real-time railway dispatcher. A project contains infrastructure, carriers and train runs. The simulation calculates actual track occupancy, propagates waiting time caused by occupied tracks, detects scheduled conflicts and produces 24-hour statistics.

## Current MVP

- Railway network: stations, lines, sections and directional tracks.
- Multiple carriers and train runs.
- Timetable points with arrival/departure times and optional track selection.
- 24-hour simulation engine.
- Track occupancy and delay propagation.
- Scheduled overlap detection.
- Section utilization and delay statistics.
- Save/load of the complete project as project.railplanner.
- MonoGame/DesktopGL dashboard based on the existing RailPlanner foundation.
- Demo scenario loaded automatically on startup.

## Controls

| Key | Action |
|---|---|
| Space | Start / pause simulation |
| Enter | Run the complete 24 hours immediately |
| R | Reset simulation |
| + / - | Change simulation speed |
| T | Add a demo train |
| F5 | Save project |
| F6 | Load project |
| F7 | Run complete 24 hours |
| F8 | Show result summary |

## Simulation model

The simulation runs from 00:00 to 24:00.

A train occupies a directional track from its scheduled departure until the scheduled arrival. If the selected track is still occupied by another simulated train, the train waits until the resource becomes free. That waiting time propagates to later timetable points.

The first version intentionally keeps the model small. Signals, block sections, route locking, switches, acceleration/braking curves, platform conflicts and passenger demand are not part of the core yet.

## Data model

RailProject -> Carrier, Station, RailwayLine -> RailSection -> RailTrack, TrainRun -> TimetableEntry.

SimulationResult -> OccupancyInterval, SimulationConflict, TrainResult, SectionUtilization.

## Build

Requires the .NET 9 SDK.

Run dotnet restore and then dotnet run from the repository root.

The project keeps MonoGame/DesktopGL because it is suitable for an OpenTTD-like desktop simulation view.
