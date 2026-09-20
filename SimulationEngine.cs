namespace RailPlanner;

public sealed class SimulationEngine
{
    public SimulationResult Run(RailProject project, TimeSpan duration = default)
    {
        if (duration == default) duration = TimeSpan.FromHours(24);
        var result = new SimulationResult { Duration = duration };
        var sections = project.Lines.SelectMany(x => x.Sections).ToList();
        var occupied = new Dictionary<Guid, List<OccupancyInterval>>();

        foreach (var train in project.Trains.OrderBy(t => Parse(t.Timetable.FirstOrDefault()?.Departure) ?? TimeSpan.Zero))
        {
            if (train.Timetable.Count < 2) continue;
            SimulateTrain(train, sections, occupied, result, duration);
        }

        foreach (var list in occupied.Values)
            result.Occupancies.AddRange(list);

        foreach (var section in sections)
        {
            var used = result.Occupancies.Where(x => x.SectionId == section.Id)
                .Sum(x => Math.Max(0, (x.ActualEnd - x.ActualStart).TotalMinutes));
            result.SectionUtilization[section.Id] =
                duration.TotalMinutes == 0 ? 0 : Math.Min(100, used / duration.TotalMinutes * 100);
        }

        DetectConflicts(result);
        return result;
    }

    private static void SimulateTrain(
        TrainRun train,
        List<RailSection> sections,
        Dictionary<Guid, List<OccupancyInterval>> occupied,
        SimulationResult result,
        TimeSpan duration)
    {
        var current = Parse(train.Timetable[0].Departure) ?? TimeSpan.Zero;
        var scheduledFinal = Parse(train.Timetable[^1].Arrival) ??
                             Parse(train.Timetable[^1].Departure) ?? current;

        for (var i = 0; i < train.Timetable.Count - 1; i++)
        {
            var from = train.Timetable[i];
            var to = train.Timetable[i + 1];
            var scheduledStart = Parse(from.Departure) ?? Parse(from.Arrival);
            var scheduledEnd = Parse(to.Arrival) ?? Parse(to.Departure);
            if (scheduledStart is null || scheduledEnd is null) continue;

            var travel = scheduledEnd.Value <= scheduledStart.Value
                ? scheduledEnd.Value.Add(TimeSpan.FromDays(1)) - scheduledStart.Value
                : scheduledEnd.Value - scheduledStart.Value;

            var section = sections.FirstOrDefault(x =>
                (x.FromStationId == from.StationId && x.ToStationId == to.StationId) ||
                (x.FromStationId == to.StationId && x.ToStationId == from.StationId));
            if (section is null) continue;

            var direction = section.FromStationId == from.StationId
                ? TrackDirection.Forward
                : TrackDirection.Reverse;

            var track = from.TrackId is Guid requested
                ? section.Tracks.FirstOrDefault(x => x.Id == requested)
                : section.Tracks.FirstOrDefault(x => x.Direction == direction);

            if (track is null) continue;

            var requestedStart = current > scheduledStart.Value ? current : scheduledStart.Value;
            var actualStart = FindTrackFreeAt(occupied, track.Id, requestedStart, travel);
            var actualEnd = actualStart + travel;

            if (actualStart >= duration) break;

            var interval = new OccupancyInterval(
                track.Id, train.Id, train.Number,
                scheduledStart.Value, scheduledStart.Value + travel,
                actualStart, actualEnd, section.Id);

            if (!occupied.TryGetValue(track.Id, out var list))
                occupied[track.Id] = list = new List<OccupancyInterval>();

            list.Add(interval);
            current = actualEnd;
        }

        result.Trains.Add(new TrainResult(
            train.Id,
            train.Number,
            scheduledFinal,
            current,
            current - scheduledFinal));
    }

    private static TimeSpan FindTrackFreeAt(
        Dictionary<Guid, List<OccupancyInterval>> occupied,
        Guid trackId,
        TimeSpan requested,
        TimeSpan travel)
    {
        if (!occupied.TryGetValue(trackId, out var list))
            return requested;

        var candidate = requested;
        foreach (var item in list.OrderBy(x => x.ActualStart))
        {
            if (candidate + travel <= item.ActualStart) break;
            if (candidate >= item.ActualEnd) continue;
            candidate = item.ActualEnd;
        }
        return candidate;
    }

    private static void DetectConflicts(SimulationResult result)
    {
        var id = 1;
        foreach (var group in result.Occupancies.GroupBy(x => x.TrackId))
        {
            var ordered = group.OrderBy(x => x.ScheduledStart).ToList();
            for (var i = 0; i < ordered.Count; i++)
            for (var j = i + 1; j < ordered.Count; j++)
            {
                var a = ordered[i];
                var b = ordered[j];
                if (a.TrainId == b.TrainId) continue;

                var start = a.ScheduledStart > b.ScheduledStart ? a.ScheduledStart : b.ScheduledStart;
                var end = a.ScheduledEnd < b.ScheduledEnd ? a.ScheduledEnd : b.ScheduledEnd;

                if (start < end)
                {
                    result.Conflicts.Add(new SimulationConflict(
                        $"C{id++:0000}",
                        "SCHEDULED_OVERLAP",
                        a.SectionId,
                        a.TrackId,
                        a.TrainId,
                        b.TrainId,
                        start,
                        end,
                        $"{a.TrainNumber} and {b.TrainNumber} are scheduled on the same track."));
                }
            }
        }
    }

    private static TimeSpan? Parse(string? value)
        => TimeSpan.TryParse(value, out var time) ? time : null;
}
