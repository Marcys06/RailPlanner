using System.Text.Json.Serialization;

namespace RailPlanner;

public enum StationType { Halt, Local, Junction, Main, Terminal }
public enum StopKind { Origin, Stop, Pass, Destination }
public enum TrackDirection { Forward, Reverse }

public sealed class RailProject
{
    public string Name { get; set; } = "New project";
    public int MapWidth { get; set; } = 1000;
    public int MapHeight { get; set; } = 1000;
    public List<Station> Stations { get; set; } = new();
    public List<RailwayLine> Lines { get; set; } = new();
    public List<CommercialLine> CommercialLines { get; set; } = new();
    public List<TrainRun> Trains { get; set; } = new();
}
public sealed class Station
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Code { get; set; } = "NEW"; public string Name { get; set; } = "New station";
    public int X { get; set; } public int Y { get; set; } public int Tracks { get; set; } = 2; public StationType Type { get; set; } = StationType.Local;
}
public sealed class RailwayLine
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Number { get; set; } = "LK000"; public string Name { get; set; } = "New line"; public List<RailSection> Sections { get; set; } = new();
}
public sealed class RailSection
{
    public Guid Id { get; set; } = Guid.NewGuid(); public int Number { get; set; }
    public Guid FromStationId { get; set; } public Guid ToStationId { get; set; }
    public List<RailTrack> Tracks { get; set; } = new(); public List<MapPoint> Geometry { get; set; } = new();
}
public sealed class RailTrack
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "1";
    public TrackDirection Direction { get; set; } = TrackDirection.Forward;
    public int Vmax { get; set; } = 120;
}
public sealed class MapPoint
{
    public int X { get; set; } public int Y { get; set; } public MapPoint() { } public MapPoint(int x, int y) { X = x; Y = y; }
}
public sealed class CommercialLine
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Code { get; set; } = "IC"; public string Name { get; set; } = "InterCity";
}
public sealed class TrainRun
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Number { get; set; } = "IC 001"; public string Name { get; set; } = "New run";
    public Guid? CommercialLineId { get; set; } public List<TimetableEntry> Timetable { get; set; } = new();
}
public sealed class TimetableEntry
{
    public Guid StationId { get; set; }
    public StopKind Kind { get; set; } = StopKind.Stop;
    public string Arrival { get; set; } = "--:--";
    public string Departure { get; set; } = "--:--";
    public Guid? TrackId { get; set; }
}
public sealed record OccupancyInterval(Guid TrackId, Guid TrainId, string TrainNumber, TimeSpan Start, TimeSpan End, Guid SectionId);
public sealed record Conflict(string Number, string Message, Guid TrackId, Guid TrainA, Guid TrainB, TimeSpan Start, TimeSpan End);
public sealed record StationLoad(Guid StationId, int MaximumUsed, int Capacity);
public sealed class AnalysisResult
{
    public List<Conflict> Conflicts { get; } = new(); public List<StationLoad> StationLoads { get; } = new(); public List<string> Warnings { get; } = new();
}
public static class ProjectJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
}
