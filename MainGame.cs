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
    private string projectFile = "project.railplanner";
    private KeyboardState oldKeys;
    private MouseState oldMouse;

    public MainGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1440,
            PreferredBackBufferHeight = 900
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
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
        var mouse = Mouse.GetState();
        bool pressed(Keys key) => keys.IsKeyDown(key) && !oldKeys.IsKeyDown(key);

        if (pressed(Keys.Space))
        {
            running = !running;
            status = running ? "SIMULATION RUNNING" : "PAUSED";
        }

        if (pressed(Keys.Enter))
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
            status = $"SPEED {speed:0} SIM MIN / REAL SEC";
        }

        if (pressed(Keys.Subtract) || pressed(Keys.OemMinus))
        {
            speed = Math.Max(1, speed / 2);
            status = $"SPEED {speed:0} SIM MIN / REAL SEC";
        }

        if (pressed(Keys.F5)) Save();
        if (pressed(Keys.F6)) Load();
        if (pressed(Keys.F7)) Run24Hours();
        if (pressed(Keys.F8)) status = BuildReport();
        if (pressed(Keys.T)) AddDemoTrain();

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

        if (mouse.LeftButton == ButtonState.Pressed &&
            oldMouse.LeftButton == ButtonState.Released &&
            mouse.X > 300)
        {
            var clicked = project.Stations.FirstOrDefault(s =>
                Vector2.Distance(new Vector2(s.X + 300, s.Y + 90), mouse.Position.ToVector2()) < 18);
            if (clicked != null) status = $"{clicked.Code} - {clicked.Name} / {clicked.Tracks} tracks";
        }

        oldKeys = keys;
        oldMouse = mouse;
        base.Update(gameTime);
    }

    private void Run24Hours()
    {
        running = false;
        simTime = TimeSpan.FromHours(24);
        result = new SimulationEngine().Run(project);
        status = $"24H COMPLETE: {result.Conflicts.Count} CONFLICTS / {result.DelayedTrains} DELAYED";
    }

    private void AddDemoTrain()
    {
        if (project.Stations.Count < 2) return;
        var carrier = project.Carriers.First();
        var a = project.Stations[0];
        var b = project.Stations[^1];
        var number = $"R {project.Trains.Count + 101:000}";

        project.Trains.Add(new TrainRun
        {
            Number = number,
            Name = "Generated regional",
            CarrierId = carrier.Id,
            Timetable = new()
            {
                new TimetableEntry { StationId = a.Id, Kind = StopKind.Origin, Departure = "08:00" },
                new TimetableEntry { StationId = b.Id, Kind = StopKind.Destination, Arrival = "10:00" }
            }
        });

        status = $"ADDED {number}. Press ENTER to simulate.";
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

        result = new SimulationResult();
        simTime = TimeSpan.Zero;
        running = false;
        status = $"LOADED {projectFile}";
    }

    private string BuildReport()
    {
        if (result.Trains.Count == 0) return "NO SIMULATION RESULT - PRESS ENTER";

        var worst = result.Trains.OrderByDescending(x => x.Delay).FirstOrDefault();
        return $"TRAINS {result.Trains.Count} | DELAYED {result.DelayedTrains} | " +
               $"AVG DELAY {result.AverageDelayMinutes:0.0} MIN | " +
               $"TOTAL DELAY {result.TotalDelayMinutes:0} MIN | " +
               $"CONFLICTS {result.Conflicts.Count} | " +
               $"WORST {(worst?.TrainNumber ?? "-")} {(worst?.Delay.TotalMinutes ?? 0):0} MIN";
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 15, 19));
        batch.Begin(samplerState: SamplerState.PointClamp);

        DrawHeader();
        DrawMap();
        DrawSidebar();
        DrawTimeline();

        batch.End();
        base.Draw(gameTime);
    }

    private void DrawHeader()
    {
        Rect(new Rectangle(0, 0, 1440, 64), new Color(24, 29, 36));
        Text("RAILPLANNER", new Vector2(22, 14), Color.White, 1.15f);
        Text("24H RAILWAY SIMULATION", new Vector2(22, 39), Color.Gray, .48f);
        Text($"TIME {simTime:hh\\:mm\\:ss}", new Vector2(350, 20), Color.White, .75f);
        Text(running ? "RUNNING" : "PAUSED", new Vector2(500, 20), running ? Color.LightGreen : Color.Gray, .7f);
        Text($"x{speed:0}", new Vector2(610, 20), Color.LightGray, .65f);
    }

    private void DrawMap()
    {
        Rect(new Rectangle(0, 64, 1080, 720), new Color(18, 23, 28));

        foreach (var line in project.Lines)
        foreach (var section in line.Sections)
        {
            var a = project.Stations.FirstOrDefault(s => s.Id == section.FromStationId);
            var b = project.Stations.FirstOrDefault(s => s.Id == section.ToStationId);
            if (a == null || b == null) continue;
            DrawLine(
                new Vector2(a.X + 300, a.Y + 90),
                new Vector2(b.X + 300, b.Y + 90),
                Color.DarkGray, 7);
        }

        foreach (var station in project.Stations)
        {
            var p = new Vector2(station.X + 300, station.Y + 90);
            Rect(new Rectangle((int)p.X - 10, (int)p.Y - 10, 20, 20), Color.White);
            Text(station.Code, p + new Vector2(-14, 15), Color.LightGray, .52f);
        }

        foreach (var occupancy in result.Occupancies)
        {
            if (simTime < occupancy.ActualStart || simTime > occupancy.ActualEnd) continue;
            var section = project.Lines.SelectMany(x => x.Sections).FirstOrDefault(x => x.Id == occupancy.SectionId);
            if (section == null) continue;
            var a = project.Stations.FirstOrDefault(s => s.Id == section.FromStationId);
            var b = project.Stations.FirstOrDefault(s => s.Id == section.ToStationId);
            if (a == null || b == null) continue;

            var span = occupancy.ActualEnd - occupancy.ActualStart;
            var progress = span.TotalSeconds <= 0 ? 1 : (simTime - occupancy.ActualStart).TotalSeconds / span.TotalSeconds;
            progress = Math.Clamp(progress, 0, 1);
            var pos = Vector2.Lerp(
                new Vector2(a.X + 300, a.Y + 90),
                new Vector2(b.X + 300, b.Y + 90),
                (float)progress);
            Rect(new Rectangle((int)pos.X - 7, (int)pos.Y - 7, 14, 14), Color.Orange);
            Text(occupancy.TrainNumber, pos + new Vector2(10, -8), Color.Orange, .5f);
        }
    }

    private void DrawSidebar()
    {
        Rect(new Rectangle(1080, 64, 360, 720), new Color(22, 27, 33));
        Text("SIMULATION", new Vector2(1100, 84), Color.White, .8f);
        Text("SPACE   start / pause", new Vector2(1100, 120), Color.LightGray, .52f);
        Text("ENTER   run full 24h", new Vector2(1100, 145), Color.LightGray, .52f);
        Text("R       reset", new Vector2(1100, 170), Color.LightGray, .52f);
        Text("+ / -   simulation speed", new Vector2(1100, 195), Color.LightGray, .52f);
        Text("T       add demo train", new Vector2(1100, 220), Color.LightGray, .52f);
        Text("F5/F6   save / load", new Vector2(1100, 245), Color.LightGray, .52f);
        Text("F8      report", new Vector2(1100, 270), Color.LightGray, .52f);

        Text("NETWORK", new Vector2(1100, 315), Color.White, .8f);
        Text($"Stations      {project.Stations.Count}", new Vector2(1100, 350), Color.LightGray, .58f);
        Text($"Sections      {project.Lines.SelectMany(x => x.Sections).Count()}", new Vector2(1100, 375), Color.LightGray, .58f);
        Text($"Trains        {project.Trains.Count}", new Vector2(1100, 400), Color.LightGray, .58f);
        Text($"Carriers      {project.Carriers.Count}", new Vector2(1100, 425), Color.LightGray, .58f);

        Text("RESULT", new Vector2(1100, 470), Color.White, .8f);
        Text($"Conflicts     {result.Conflicts.Count}", new Vector2(1100, 505), result.Conflicts.Count == 0 ? Color.LightGreen : Color.OrangeRed, .58f);
        Text($"Delayed       {result.DelayedTrains}", new Vector2(1100, 530), Color.LightGray, .58f);
        Text($"Avg delay     {result.AverageDelayMinutes:0.0} min", new Vector2(1100, 555), Color.LightGray, .58f);
        Text($"Total delay   {result.TotalDelayMinutes:0} min", new Vector2(1100, 580), Color.LightGray, .58f);

        Text("STATUS", new Vector2(1100, 630), Color.White, .8f);
        Text(Wrap(status, 42), new Vector2(1100, 665), Color.LightGray, .5f);
    }

    private void DrawTimeline()
    {
        Rect(new Rectangle(0, 784, 1440, 116), new Color(14, 18, 22));
        Text("24H", new Vector2(20, 804), Color.Gray, .5f);
        for (var h = 0; h <= 24; h += 2)
        {
            var x = 70 + h * 52;
            Rect(new Rectangle(x, 828, 1, 38), Color.DarkGray);
            Text($"{h:00}", new Vector2(x - 8, 870), Color.Gray, .45f);
        }
        var markerX = 70 + (float)(simTime.TotalHours * 52);
        Rect(new Rectangle((int)markerX, 818, 2, 50), Color.Orange);
    }

    private void DrawLine(Vector2 a, Vector2 b, Color color, int width)
    {
        var delta = b - a;
        var length = delta.Length();
        var angle = (float)Math.Atan2(delta.Y, delta.X);
        batch.Draw(pixel, a, null, color, angle, Vector2.Zero,
            new Vector2(length, width), SpriteEffects.None, 0);
    }

    private void Rect(Rectangle r, Color color) => batch.Draw(pixel, r, color);

    private void Text(string value, Vector2 position, Color color, float scale)
        => batch.DrawString(font, value, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);

    private static string Wrap(string value, int width)
    {
        if (value.Length <= width) return value;
        return value[..width] + "...";
    }
}

internal static class DemoProject
{
    public static RailProject Create()
    {
        var p = new RailProject();

        var kd = new Carrier { Code = "KD", Name = "Koleje Dolnoslaskie" };
        var ic = new Carrier { Code = "IC", Name = "PKP Intercity" };
        p.Carriers.AddRange(new[] { kd, ic });

        var wro = new Station { Code = "WRO", Name = "Wroclaw", X = 80, Y = 260, Tracks = 6, Type = StationType.Main };
        var ola = new Station { Code = "OLA", Name = "Olawa", X = 300, Y = 180, Tracks = 3, Type = StationType.Local };
        var brz = new Station { Code = "BRZ", Name = "Brzeg", X = 520, Y = 250, Tracks = 3, Type = StationType.Local };
        var leg = new Station { Code = "LEG", Name = "Legnica", X = 820, Y = 130, Tracks = 4, Type = StationType.Main };
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
                Vmax = i == 0 ? 120 : 120
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
            Timetable = new()
            {
                new TimetableEntry { StationId = a.Id, Kind = StopKind.Origin, Departure = t0 },
                new TimetableEntry { StationId = b.Id, Kind = StopKind.Stop, Arrival = t1, Departure = t1 },
                new TimetableEntry { StationId = c.Id, Kind = StopKind.Stop, Arrival = t2, Departure = t2 },
                new TimetableEntry { StationId = d.Id, Kind = StopKind.Destination, Arrival = t3 }
            }
        };
}
