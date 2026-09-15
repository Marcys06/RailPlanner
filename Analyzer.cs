namespace RailPlanner;

public static class TimetableAnalyzer
{
    public static AnalysisResult Analyze(RailProject project)
    {
        var result = new AnalysisResult(); var intervals = new List<OccupancyInterval>(); var stations = project.Stations.ToDictionary(s => s.Id);
        foreach (var train in project.Trains)
        {
            if (train.Timetable.Count < 2) { result.Warnings.Add($"{train.Number}: fewer than two timetable points."); continue; }
            for (var i = 0; i < train.Timetable.Count - 1; i++)
            {
                var a = train.Timetable[i]; var b = train.Timetable[i + 1];
                if (!stations.TryGetValue(a.StationId, out var sa) || !stations.TryGetValue(b.StationId, out var sb)) { result.Warnings.Add($"{train.Number}: unknown station in timetable."); continue; }
                var start = Parse(a.Departure, a.Arrival); var end = Parse(b.Arrival, b.Departure);
                if (start is null || end is null) { result.Warnings.Add($"{train.Number}: invalid time {sa.Code}->{sb.Code}."); continue; }
                if (end <= start) end = end.Value.Add(TimeSpan.FromDays(1));
                var section = FindSection(project, sa.Id, sb.Id);
                if (section is null) { result.Warnings.Add($"{train.Number}: no railway section {sa.Code}->{sb.Code}."); continue; }
                var direction = section.FromStationId == sa.Id ? TrackDirection.Forward : TrackDirection.Reverse;
                var tracks = section.Tracks.Where(t => t.Direction == direction).ToList();
                var selected = a.TrackId.HasValue ? tracks.FirstOrDefault(t => t.Id == a.TrackId.Value) : tracks.FirstOrDefault();
                if (selected is null) { result.Warnings.Add($"{train.Number}: no valid track for {sa.Code}->{sb.Code}."); continue; }
                intervals.Add(new OccupancyInterval(selected.Id, train.Id, train.Number, start.Value, end.Value, section.Id));
            }
        }
        foreach (var group in intervals.GroupBy(i => i.TrackId))
        {
            var ordered = group.OrderBy(i => i.Start).ToList();
            for (var i = 0; i < ordered.Count; i++) for (var j = i + 1; j < ordered.Count; j++)
            {
                if (ordered[j].Start >= ordered[i].End) break; if (ordered[i].TrainId == ordered[j].TrainId) continue;
                var start = ordered[i].Start > ordered[j].Start ? ordered[i].Start : ordered[j].Start; var end = ordered[i].End < ordered[j].End ? ordered[i].End : ordered[j].End;
                result.Conflicts.Add(new Conflict($"C{result.Conflicts.Count + 1:000}", $"{ordered[i].TrainNumber} overlaps {ordered[j].TrainNumber} for {(end - start).TotalMinutes:0} min.", group.Key, ordered[i].TrainId, ordered[j].TrainId, start, end));
            }
        }
        foreach (var station in project.Stations)
        {
            var events = new List<(TimeSpan Time, int Delta)>();
            foreach (var train in project.Trains) foreach (var e in train.Timetable.Where(e => e.StationId == station.Id && e.Kind == StopKind.Stop))
            {
                var arrival = Parse(e.Arrival, e.Departure); var departure = Parse(e.Departure, e.Arrival);
                if (arrival is not null) events.Add((arrival.Value, 1)); if (departure is not null) events.Add((departure.Value, -1));
            }
            var used = 0; var max = 0; foreach (var e in events.OrderBy(e => e.Time).ThenBy(e => e.Delta)) { used = Math.Max(0, used + e.Delta); max = Math.Max(max, used); }
            result.StationLoads.Add(new StationLoad(station.Id, max, station.Tracks));
        }
        return result;
    }
    private static TimeSpan? Parse(string primary, string fallback) { if (TimeSpan.TryParse(primary, out var value)) return value; if (TimeSpan.TryParse(fallback, out value)) return value; return null; }
    public static RailSection? FindSection(RailProject project, Guid from, Guid to)
    {
        foreach (var line in project.Lines) foreach (var section in line.Sections)
            if ((section.FromStationId == from && section.ToStationId == to) || (section.FromStationId == to && section.ToStationId == from)) return section;
        return null;
    }
}
