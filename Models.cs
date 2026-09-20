using System.Text.Json;
using System.Text.Json.Serialization;

namespace RailPlanner;

public enum StationType { Halt, Local, Junction, Main, Terminal }
public enum StopKind { Origin, Stop, Pass, Destination }
public enum TrackDirection { Forward, Reverse }
public enum TrainState { Planned, Running, WaitingForTrack, AtStation, Finished }

public sealed class RailProject
{
    public string Name { get; set; } = "RailPlanner simulation";
    public int MapWidth { get; set; } = 1200;
    public int MapHeight { get; set; } = 700;
    public List<Carrier> Carriers { get; set; } = new();
    public List<Station> Stations { get; set; } = new();
    public List<RailwayLine> Lines { get; set; } = new();
    public List<TrainRun> Trains { get; set; } = new();
}

public sealed class Carrier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "OP";
    public string Name { get; set; } = "Operator";
}

public sealed class Station
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "NEW";
    public string Name { get; set; } = "New station";
    public int X { get; set; }
    public int Y { get; set; }
    public int Tracks { get; set; } = 2;
    public StationType Type { get; set; } = StationType.Local;
}

public sealed class RailwayLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "LK001";
    public string Name { get; set; } = "New line";
    public List<RailSection> Sections { get; set; } = new();
}

public sealed class RailSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public Guid FromStationId { get; set; }
    public Guid ToStationId { get; set; }
    public double LengthKm { get; set; } = 10;
    public List<RailTrack> Tracks { get; set; } = new();
}

public sealed class RailTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "1";
    public TrackDirection Direction { get; set; } = TrackDirection.Forward;
    public int Vmax { get; set; } = 120;
}

public sealed class TrainRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = "R 001";
    public string Name { get; set; } = "Regional";
    public Guid CarrierId { get; set; }
    public List<TimetableEntry> Timetable { get; set; } = new();
}

public sealed class TimetableEntry
{
    public Guid StationId { get; set; }
    public StopKind Kind { get; set; } = StopKind.Stop;
    public string Arrival { get; set; } = "--:--";
    public string Departure { get; set; } = "--:--";
    public Guid? TrackId { get; set; }
}

public sealed record OccupancyInterval(
    Guid TrackId,
    Guid TrainId,
    string TrainNumber,
    TimeSpan ScheduledStart,
    TimeSpan ScheduledEnd,
    TimeSpan ActualStart,
    TimeSpan ActualEnd,
    Guid SectionId);

public sealed record SimulationConflict(
    string Id,
    string Type,
    Guid SectionId,
    Guid TrackId,
    Guid TrainA,
    Guid TrainB,
    TimeSpan Start,
    TimeSpan End,
    string Message);

public sealed record TrainResult(
    Guid TrainId,
    string TrainNumber,
    TimeSpan ScheduledArrival,
    TimeSpan ActualArrival,
    TimeSpan Delay);

public sealed class SimulationResult
{
    public TimeSpan Duration { get; set; }
    public List<OccupancyInterval> Occupancies { get; } = new();
    public List<SimulationConflict> Conflicts { get; } = new();
    public List<TrainResult> Trains { get; } = new();
    public Dictionary<Guid, double> SectionUtilization { get; } = new();
    public double TotalDelayMinutes => Trains.Sum(x => Math.Max(0, x.Delay.TotalMinutes));
    public double AverageDelayMinutes => Trains.Count == 0 ? 0 : TotalDelayMinutes / Trains.Count;
    public int DelayedTrains => Trains.Count(x => x.Delay > TimeSpan.Zero);
}

public static class ProjectJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
