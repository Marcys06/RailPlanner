using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

public sealed class MainGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private Texture2D pixel = null!;

    private RailProject project = null!;
    private SimulationResult result = new();
    private TimeSpan simTime;
    private bool running;
    private double speed = 60;
    private string status = "READY";
    private readonly string projectFile = "project.railplanner";
    private KeyboardState oldKeys;
    private int selectedTrain;

    private const int Width = 1440;
    private const int Height = 900;
    private const int Left = 18;
    private const int Right = 1422;

    public MainGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Width,
            PreferredBackBufferHeight = Height
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Window.Title = "RailPlanner - 24h Railway Simulation";
    }

    protected override void Initialize()
    {
        project = DemoProject.Create();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Content.Load<SpriteFont>("DefaultFont");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        bool pressed(Keys key) => keys.IsKeyDown(key) && !oldKeys.IsKeyDown(key);

        if (pressed(Keys.Escape)) Exit();

        if (pressed(Keys.Space))
        {
            running = !running;
            status = running ? "SIMULATION RUNNING" : "PAUSED";
        }

        if (pressed(Keys.Enter) || pressed(Keys.F7))
            Run24Hours();

        if (pressed(Keys.R))
        {
            simTime = TimeSpan.Zero;
            running = false;
            result = new SimulationResult();
            status = "SIMULATION RESET";
        }

        if (pressed(Keys.Add) || pressed(Keys.OemPlus))
        {
            speed = Math.Min(3600, speed * 2);
            status = $"SPEED x{speed:0}";
        }

        if (pressed(Keys.Subtract) || pressed(Keys.OemMinus))
        {
            speed = Math.Max(1, speed / 2);
            status = $"SPEED x{speed:0}";
        }

        if (pressed(Keys.F5)) Save();
        if (pressed(Keys.F6)) Load();
        if (pressed(Keys.F8)) status = BuildReport();

        // Train and route editing:
        if (pressed(Keys.A)) AddTrain();
        if (pressed(Keys.Delete) || pressed(Keys.Back))
            DeleteSelectedTrain();
        if (pressed(Keys.Up))
            SelectTrain(-1);
        if (pressed(Keys.Down))
            SelectTrain(1);
        if (pressed(Keys.N))
            AddStationToSelectedRoute();
        if (pressed(Keys.P))
            ToggleSelectedStationStop();
        if (pressed(Keys.D1) || pressed(Keys.NumPad1)) AssignTrack(1);
        if (pressed(Keys.D2) || pressed(Keys.NumPad2)) AssignTrack(2);
        if (pressed(Keys.D3) || pressed(Keys.NumPad3)) AssignTrack(3);
        if (pressed(Keys.D4) || pressed(Keys.NumPad4)) AssignTrack(4);
        if (pressed(Keys.D5) || pressed(Keys.NumPad5)) AssignTrack(5);
        if (pressed(Keys.D6) || pressed(Keys.NumPad6)) AssignTrack(6);
        if (pressed(Keys.D7) || pressed(Keys.NumPad7)) AssignTrack(7);
        if (pressed(Keys.D8) || pressed(Keys.NumPad8)) AssignTrack(8);
        if (pressed(Keys.D9) || pressed(Keys.NumPad9)) AssignTrack(9);

        if (running)
        {
            simTime += TimeSpan.FromSeconds(gameTime.ElapsedGameTime.TotalSeconds * speed);
            if (simTime >= TimeSpan.FromHours(24))
            {
                simTime = TimeSpan.FromHours(24);
                running = false;
                result = new SimulationEngine().Run(project);
                status = "24H SIMULATION COMPLETE";
            }
        }

        oldKeys = keys;
        base.Update(gameTime);
    }

    private void Run24Hours()
    {
        running = false;
        simTime = TimeSpan.FromHours(24);
        result = new SimulationEngine().Run(project);
        status = $"24H COMPLETE: {result.Conflicts.Count} CONFLICTS / {result.DelayedTrains} DELAYED";
    }

    private TrainRun? SelectedTrain =>
        project.Trains.Count == 0 ? null :
        project.Trains[Math.Clamp(selectedTrain, 0, project.Trains.Count - 1)];

    private void SelectTrain(int delta)
    {
        if (project.Trains.Count == 0) return;
        selectedTrain = Math.Clamp(selectedTrain + delta, 0, project.Trains.Count - 1);
        var train = SelectedTrain!;
        status = $"SELECTED {train.Number}";
    }

    private void AddTrain()
    {
        if (project.Stations.Count < 2 || project.Carriers.Count == 0)
        {
            status = "NEED 2 STATIONS AND 1 OPERATOR";
            return;
        }

        var carrier = project.Carriers.First();
        var stations = project.Stations;
        var number = $"R {project.Trains.Count + 101:000}";

        var train = new TrainRun
        {
            Number = number,
            Name = "New train",
            CarrierId = carrier.Id,
            Timetable =
            {
                new TimetableEntry
                {
                    StationId = stations[0].Id,
                    Kind = StopKind.Origin,
                    Departure = "08:00"
                },
                new TimetableEntry
                {
                    StationId = stations[1].Id,
                    Kind = StopKind.Destination,
                    Arrival = "09:00"
                }
            }
        };

        project.Trains.Add(train);
        selectedTrain = project.Trains.Count - 1;
        result = new SimulationResult();
        status = $"ADDED {number} | N=ADD STATION";
    }

    private void DeleteSelectedTrain()
    {
        var train = SelectedTrain;
        if (train is null)
        {
            status = "NO TRAIN SELECTED";
            return;
        }

        var number = train.Number;
        project.Trains.RemoveAt(selectedTrain);
        selectedTrain = Math.Clamp(selectedTrain, 0, Math.Max(0, project.Trains.Count - 1));
        result = new SimulationResult();
        status = $"DELETED {number}";
    }

    private void AddStationToSelectedRoute()
    {
        var train = SelectedTrain;
        if (train is null)
        {
            status = "NO TRAIN SELECTED";
            return;
        }

        var used = train.Timetable.Select(x => x.StationId).ToHashSet();
        var next = project.Stations.FirstOrDefault(x => !used.Contains(x.Id));
        if (next is null)
        {
            status = "ALL STATIONS ALREADY IN ROUTE";
            return;
        }

        var destination = train.Timetable[^1];
        var previous = train.Timetable.Count >= 2 ? train.Timetable[^2] : train.Timetable[^1];
        var sectionToNext = FindSectionBetween(previous.StationId, next.Id);
        var sectionToDestination = FindSectionBetween(next.Id, destination.StationId);

        if (sectionToNext is null || sectionToDestination is null)
        {
            status = $"NO CONTINUOUS ROUTE VIA {next.Code}";
            return;
        }

        var fromTime = ParseTime(previous.Departure) ?? ParseTime(previous.Arrival) ?? TimeSpan.FromHours(8);
        var destinationTime = ParseTime(destination.Arrival) ?? TimeSpan.FromHours(10);
        if (destinationTime <= fromTime)
            destinationTime = fromTime.Add(TimeSpan.FromHours(1));

        var midpoint = fromTime + TimeSpan.FromMinutes(
            Math.Max(5, (destinationTime - fromTime).TotalMinutes / 2));

        train.Timetable.Insert(train.Timetable.Count - 1, new TimetableEntry
        {
            StationId = next.Id,
            Kind = StopKind.Stop,
            Arrival = FormatTime(midpoint),
            Departure = FormatTime(midpoint)
        });

        result = new SimulationResult();
        status = $"ROUTE + {next.Code} (STOP)";
    }

    private void ToggleSelectedStationStop()
    {
        var train = SelectedTrain;
        if (train is null || train.Timetable.Count < 3)
        {
            status = "ROUTE NEEDS AN INTERMEDIATE STATION";
            return;
        }

        var entry = train.Timetable[^2];
        if (entry.Kind == StopKind.Stop)
        {
            entry.Kind = StopKind.Pass;
            entry.Departure = entry.Arrival;
            status = $"PASS THROUGH {StationCode(entry.StationId)}";
        }
        else if (entry.Kind == StopKind.Pass)
        {
            entry.Kind = StopKind.Stop;
            entry.Departure = entry.Arrival;
            status = $"STOP AT {StationCode(entry.StationId)}";
        }
        else
        {
            status = "SELECTED ENTRY CANNOT BE CHANGED";
        }

        result = new SimulationResult();
    }

    private void AssignTrack(int trackNumber)
    {
        var train = SelectedTrain;
        if (train is null || train.Timetable.Count < 2)
        {
            status = "NO TRAIN ROUTE";
            return;
        }

        // Assign the track to the last segment before the last intermediate station.
        var entryIndex = train.Timetable.Count - 2;
        var entry = train.Timetable[entryIndex];
        var previous = train.Timetable[entryIndex - 1];
        var section = FindSectionBetween(previous.StationId, entry.StationId);

        if (section is null)
        {
            status = $"NO SECTION {StationCode(previous.StationId)} -> {StationCode(entry.StationId)}";
            return;
        }

        var track = section.Tracks.FirstOrDefault(x => x.Name == trackNumber.ToString());
        if (track is null)
        {
            status = $"TRACK {trackNumber} NOT AVAILABLE";
            return;
        }

        entry.TrackId = track.Id;
        status = $"TRACK {track.Name} -> {StationCode(entry.StationId)}";
        result = new SimulationResult();
    }

    private void Save()
    {
        File.WriteAllText(projectFile, JsonSerializer.Serialize(project, ProjectJson.Options));
        status = $"SAVED {projectFile}";
    }

    private void Load()
    {
        if (!File.Exists(projectFile))
        {
            status = "PROJECT FILE NOT FOUND";
            return;
        }

        project = JsonSerializer.Deserialize<RailProject>(
            File.ReadAllText(projectFile), ProjectJson.Options) ?? DemoProject.Create();

        selectedTrain = Math.Clamp(selectedTrain, 0, Math.Max(0, project.Trains.Count - 1));
        result = new SimulationResult();
        simTime = TimeSpan.Zero;
        running = false;
        status = $"LOADED {projectFile}";
    }

    private string BuildReport()
    {
        if (result.Trains.Count == 0)
            return "NO SIMULATION RESULT - PRESS ENTER";

        var worst = result.Trains.OrderByDescending(x => x.Delay).FirstOrDefault();
        return $"TRAINS {result.Trains.Count} | DELAYED {result.DelayedTrains} | " +
               $"AVG {result.AverageDelayMinutes:0.0} MIN | TOTAL {result.TotalDelayMinutes:0} MIN | " +
               $"CONFLICTS {result.Conflicts.Count} | WORST {(worst?.TrainNumber ?? "-")} {(worst?.Delay.TotalMinutes ?? 0):0} MIN";
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(245, 245, 245));
        batch.Begin(samplerState: SamplerState.PointClamp);

        DrawTopBar();
        DrawTrainTable();
        DrawConflictTable();
        DrawRouteTable();
        DrawFooter();

        batch.End();
        base.Draw(gameTime);
    }

    private void DrawTopBar()
    {
        Rect(new Rectangle(0, 0, Width, 54), new Color(35, 35, 35));
        Text("RAILPLANNER", new Vector2(18, 9), Color.White, .9f);
        Text("24H SIMULATION", new Vector2(18, 32), Color.LightGray, .42f);

        Text($"TIME {FormatTime(simTime)}", new Vector2(300, 15), Color.White, .7f);
        Text(running ? "RUNNING" : "PAUSED", new Vector2(470, 15),
            running ? Color.LightGreen : Color.LightGray, .65f);
        Text($"SPEED x{speed:0}", new Vector2(590, 15), Color.White, .65f);

        Text("[A] ADD  [DEL] DELETE  [↑↓] SELECT  [N] STATION  [P] STOP/PASS  [1-9] TOR",
            new Vector2(760, 9), Color.LightGray, .42f);
        Text("[SPACE] START/PAUSE  [ENTER] 24H  [R] RESET  [F5] SAVE  [F6] LOAD",
            new Vector2(760, 29), Color.LightGray, .42f);
    }

    private void DrawTrainTable()
    {
        const int top = 68;
        const int header = 34;
        const int row = 30;

        Text("TRAINS", new Vector2(Left, top), Color.Black, .72f);
        Text($"{project.Trains.Count} entries", new Vector2(1220, top + 4), Color.DimGray, .45f);

        var y = top + 27;
        DrawTableHeader(new Rectangle(Left, y, Right - Left, header),
            "NR", "OPERATOR", "NAZWA", "OD", "DO", "ODJAZD", "PRZYJAZD", "OPÓŹN.", "STATUS");

        y += header;
        for (var i = 0; i < project.Trains.Take(9).Count(); i++)
        {
            var train = project.Trains[i];
            var carrier = project.Carriers.FirstOrDefault(c => c.Id == train.CarrierId);
            var first = train.Timetable.FirstOrDefault();
            var last = train.Timetable.LastOrDefault();
            var from = StationCode(first?.StationId);
            var to = StationCode(last?.StationId);
            var trainResult = result.Trains.FirstOrDefault(x => x.TrainId == train.Id);
            var delay = trainResult?.Delay ?? TimeSpan.Zero;
            var scheduledDeparture = first?.Departure ?? "--:--";
            var scheduledArrival = last?.Arrival ?? last?.Departure ?? "--:--";
            var actualArrival = trainResult == null ? scheduledArrival : FormatTime(trainResult.ActualArrival);

            var selected = i == selectedTrain;
            DrawTableRow(new Rectangle(Left, y, Right - Left, row),
                train.Number,
                carrier?.Code ?? "--",
                train.Name,
                from,
                to,
                scheduledDeparture,
                actualArrival,
                delay == TimeSpan.Zero ? "0 min" : $"+{delay.TotalMinutes:0} min",
                selected ? "SELECTED" : trainResult == null ? "PLAN" : delay > TimeSpan.Zero ? "DELAY" : "OK",
                delay > TimeSpan.Zero || selected);

            y += row;
        }
    }

    private void DrawConflictTable()
    {
        const int top = 392;
        Text("CONFLICTS", new Vector2(Left, top), Color.Black, .68f);
        Text($"{result.Conflicts.Count}", new Vector2(1220, top + 4),
            result.Conflicts.Count == 0 ? Color.DarkGreen : Color.DarkRed, .5f);

        var y = top + 27;
        DrawTableHeader(new Rectangle(Left, y, Right - Left, 30),
            "ID", "TYP", "NR A", "NR B", "TOR", "OD", "DO", "OPIS");

        y += 30;
        if (result.Conflicts.Count == 0)
        {
            DrawTableRow(new Rectangle(Left, y, Right - Left, 25),
                "-", "NONE", "-", "-", "-", "-", "-", "NO CONFLICTS", false);
            return;
        }

        foreach (var conflict in result.Conflicts.Take(6))
        {
            var a = project.Trains.FirstOrDefault(t => t.Id == conflict.TrainA)?.Number ?? "--";
            var b = project.Trains.FirstOrDefault(t => t.Id == conflict.TrainB)?.Number ?? "--";

            DrawTableRow(new Rectangle(Left, y, Right - Left, 25),
                conflict.Id,
                conflict.Type,
                a,
                b,
                TrackName(conflict.TrackId),
                FormatTime(conflict.Start),
                FormatTime(conflict.End),
                conflict.Message,
                true);

            y += 25;
        }
    }

    private void DrawRouteTable()
    {
        const int top = 665;
        Text("SELECTED TRAIN ROUTE", new Vector2(Left, top), Color.Black, .68f);

        var train = SelectedTrain;
        if (train is null)
        {
            Text("NO TRAIN - [A] ADD", new Vector2(Left, top + 30), Color.DimGray, .5f);
            return;
        }

        Text($"{train.Number} | [N] add station | [P] STOP/PASS na last stacji intermediate | [1-9] assign track",
            new Vector2(370, top + 4), Color.DimGray, .42f);

        var y = top + 27;
        DrawTableHeader(new Rectangle(Left, y, Right - Left, 30),
            "LP", "STATION", "TYP", "PRZYJAZD", "ODJAZD", "TOR", "SECTION");

        y += 30;
        for (var i = 0; i < train.Timetable.Take(7).Count(); i++)
        {
            var entry = train.Timetable[i];
            var section = i < train.Timetable.Count - 1
                ? FindSectionBetween(entry.StationId, train.Timetable[i + 1].StationId)
                : null;

            DrawTableRow(new Rectangle(Left, y, Right - Left, 25),
                (i + 1).ToString(),
                StationCode(entry.StationId),
                entry.Kind.ToString().ToUpperInvariant(),
                entry.Arrival,
                entry.Departure,
                entry.TrackId is Guid id ? TrackName(id) : "--",
                section?.Number.ToString() ?? "--",
                false);

            y += 25;
        }
    }

    private void DrawFooter()
    {
        Rect(new Rectangle(0, 865, Width, 35), new Color(35, 35, 35));
        Text($"STATUS: {status}", new Vector2(18, 875), Color.White, .45f);
        Text($"Trains: {project.Trains.Count} | Stations: {project.Stations.Count} | " +
             $"Sections: {project.Lines.SelectMany(x => x.Sections).Count()} | " +
             $"Delayed: {result.DelayedTrains} | Conflicts: {result.Conflicts.Count}",
            new Vector2(620, 875), Color.LightGray, .42f);
    }

    private void DrawTableHeader(Rectangle area, params string[] cells)
    {
        Rect(area, new Color(65, 65, 65));
        var widths = GetColumnWidths(cells.Length);
        var x = area.X;

        for (var i = 0; i < cells.Length; i++)
        {
            Text(cells[i], new Vector2(x + 7, area.Y + 8), Color.White, .42f);
            x += widths[i];
        }
    }

    private void DrawTableRow(Rectangle area, string c1, string c2, string c3, string c4,
        string c5, string c6, string c7, bool warning)
        => DrawTableRow(area, new[] { c1, c2, c3, c4, c5, c6, c7 }, warning);

    private void DrawTableRow(Rectangle area, string c1, string c2, string c3, string c4,
        string c5, string c6, string c7, string c8, bool warning)
        => DrawTableRow(area, new[] { c1, c2, c3, c4, c5, c6, c7, c8 }, warning);

    private void DrawTableRow(Rectangle area, string c1, string c2, string c3, string c4,
        string c5, string c6, string c7, string c8, string c9, bool warning)
        => DrawTableRow(area, new[] { c1, c2, c3, c4, c5, c6, c7, c8, c9 }, warning);

    private void DrawTableRow(Rectangle area, string[] cells, bool warning)
    {
        Rect(area, warning ? new Color(255, 238, 220) : Color.White);
        Border(area, new Color(205, 205, 205));

        var widths = GetColumnWidths(cells.Length);
        var x = area.X;

        for (var i = 0; i < cells.Length; i++)
        {
            var text = Clip(cells[i], widths[i] - 12);
            Text(text, new Vector2(x + 7, area.Y + Math.Max(4, (area.Height - 18) / 2)),
                warning && i == cells.Length - 1 ? Color.DarkRed : Color.Black, .42f);
            x += widths[i];
        }
    }

    private static int[] GetColumnWidths(int count)
    {
        return count switch
        {
            9 => new[] { 90, 90, 180, 80, 80, 95, 105, 85, 130 },
            8 => new[] { 70, 150, 100, 100, 80, 80, 100, 600 },
            7 => new[] { 70, 170, 120, 110, 110, 100, 600 },
            _ => Enumerable.Repeat((Right - Left) / Math.Max(1, count), count).ToArray()
        };
    }

    private string StationCode(Guid? id)
        => id is Guid value
            ? project.Stations.FirstOrDefault(x => x.Id == value)?.Code ?? "--"
            : "--";

    private string TrackName(Guid id)
        => project.Lines.SelectMany(x => x.Sections)
            .SelectMany(x => x.Tracks)
            .FirstOrDefault(x => x.Id == id)?.Name ?? "--";

    private RailSection? FindSection(Guid id)
        => project.Lines.SelectMany(x => x.Sections).FirstOrDefault(x => x.Id == id);

    private RailSection? FindSectionBetween(Guid from, Guid to)
        => project.Lines.SelectMany(x => x.Sections).FirstOrDefault(x =>
            (x.FromStationId == from && x.ToStationId == to) ||
            (x.FromStationId == to && x.ToStationId == from));

    private static TimeSpan? ParseTime(string? value)
        => TimeSpan.TryParse(value, out var time) ? time : null;

    private static string FormatTime(TimeSpan time)
    {
        var totalHours = (int)time.TotalHours;
        return $"{totalHours:00}:{time.Minutes:00}";
    }

    private static string Clip(string value, int width)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var max = Math.Max(1, width / 8);
        return value.Length <= max ? value : value[..Math.Max(0, max - 3)] + "...";
    }

    private void Border(Rectangle r, Color color)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, 1), color);
        Rect(new Rectangle(r.X, r.Bottom - 1, r.Width, 1), color);
        Rect(new Rectangle(r.X, r.Y, 1, r.Height), color);
        Rect(new Rectangle(r.Right - 1, r.Y, 1, r.Height), color);
    }

    private void Rect(Rectangle r, Color color) => batch.Draw(pixel, r, color);

    private void Text(string value, Vector2 position, Color color, float scale)
        => batch.DrawString(font, value, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
}

internal static class DemoProject
{
    public static RailProject Create()
    {
        var p = new RailProject();

        var kd = new Carrier { Code = "KD", Name = "Koleje Dolnoslaskie" };
        var ic = new Carrier { Code = "IC", Name = "PKP Intercity" };
        p.Carriers.AddRange(new[] { kd, ic });

        var wro = new Station { Code = "WRO", Name = "Wroclaw", Tracks = 6, Type = StationType.Main };
        var ola = new Station { Code = "OLA", Name = "Olawa", Tracks = 3, Type = StationType.Local };
        var brz = new Station { Code = "BRZ", Name = "Brzeg", Tracks = 3, Type = StationType.Local };
        var leg = new Station { Code = "LEG", Name = "Legnica", Tracks = 4, Type = StationType.Main };
        p.Stations.AddRange(new[] { wro, ola, brz, leg });

        var line = new RailwayLine { Number = "LK001", Name = "Demo corridor" };
        line.Sections.Add(Section(1, wro, ola, 25, 2));
        line.Sections.Add(Section(2, ola, brz, 30, 1));
        line.Sections.Add(Section(3, brz, leg, 45, 2));
        p.Lines.Add(line);

        p.Trains.Add(Train("KD 101", "Regional", kd, wro, ola, brz, leg, "06:00", "06:25", "06:55", "07:40"));
        p.Trains.Add(Train("IC 201", "InterCity", ic, wro, ola, brz, leg, "06:15", "06:38", "07:05", "07:45"));
        p.Trains.Add(Train("KD 103", "Regional", kd, leg, brz, ola, wro, "07:00", "07:40", "08:10", "08:40"));
        p.Trains.Add(Train("IC 203", "InterCity", ic, wro, ola, brz, leg, "07:20", "07:45", "08:15", "08:55"));

        return p;
    }

    private static RailSection Section(int number, Station a, Station b, double km, int tracks)
    {
        var s = new RailSection
        {
            Number = number,
            FromStationId = a.Id,
            ToStationId = b.Id,
            LengthKm = km
        };

        for (var i = 0; i < tracks; i++)
        {
            s.Tracks.Add(new RailTrack
            {
                Name = (i + 1).ToString(),
                Direction = i % 2 == 0 ? TrackDirection.Forward : TrackDirection.Reverse,
                Vmax = 120
            });
        }

        return s;
    }

    private static TrainRun Train(
        string number, string name, Carrier carrier,
        Station a, Station b, Station c, Station d,
        string t0, string t1, string t2, string t3)
        => new()
        {
            Number = number,
            Name = name,
            CarrierId = carrier.Id,
            Timetable =
            {
                new TimetableEntry { StationId = a.Id, Kind = StopKind.Origin, Departure = t0 },
                new TimetableEntry { StationId = b.Id, Kind = StopKind.Stop, Arrival = t1, Departure = t1 },
                new TimetableEntry { StationId = c.Id, Kind = StopKind.Stop, Arrival = t2, Departure = t2 },
                new TimetableEntry { StationId = d.Id, Kind = StopKind.Destination, Arrival = t3 }
            }
        };
}
