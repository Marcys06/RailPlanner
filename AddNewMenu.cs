using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

public sealed class AddNewMenu : DrawableGameComponent
{
    readonly MainGame game;
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D pixel = null!;
    KeyboardState oldKeys;
    MouseState oldMouse;
    bool open;
    int selected;

    bool sectionWizard;
    int sectionField;
    int fromIndex;
    int toIndex;
    int sectionNumber;
    int trackCount = 2;
    readonly List<int> trackVmax = new();

    static readonly string[] Items = { "STATION", "LINE", "SECTION", "TRAIN" };

    public AddNewMenu(MainGame game) : base(game)
    {
        this.game = game;
        DrawOrder = 10000;
        UpdateOrder = 10000;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Game.Content.Load<SpriteFont>("DefaultFont");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        base.LoadContent();
    }

    public override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();
        bool P(Keys key) => keys.IsKeyDown(key) && !oldKeys.IsKeyDown(key);

        if (sectionWizard) UpdateSectionWizard(keys, mouse, P);
        else if (!open)
        {
            if (P(Keys.Insert) || P(Keys.F12)) { open = true; selected = 0; }
        }
        else
        {
            if (P(Keys.Escape)) open = false;
            else if (P(Keys.Up)) selected = (selected + Items.Length - 1) % Items.Length;
            else if (P(Keys.Down)) selected = (selected + 1) % Items.Length;
            else if (P(Keys.Enter)) Activate(selected);
            else if (mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
            {
                var r = MenuRect();
                for (var i = 0; i < Items.Length; i++)
                {
                    var item = new Rectangle(r.X + 28, r.Y + 78 + i * 58, r.Width - 56, 48);
                    if (item.Contains(mouse.Position)) { Activate(i); break; }
                }
            }
        }

        oldKeys = keys;
        oldMouse = mouse;
        base.Update(gameTime);
    }

    void Activate(int index)
    {
        selected = Math.Clamp(index, 0, Items.Length - 1);
        open = false;
        if (selected == 2) { OpenSectionWizard(); return; }
        if (selected == 0) Invoke("BeginAddStation");
        else if (selected == 1) Invoke("OpenLineEditor", null);
        else Invoke("OpenTrainDialog");
    }

    void OpenSectionWizard()
    {
        var project = Project;
        if (project == null || project.Stations.Count < 2) { Status("ADD AT LEAST TWO STATIONS FIRST"); return; }

        var line = SelectedLine(project) ?? project.Lines.FirstOrDefault();
        if (line == null)
        {
            line = new RailwayLine { Number = "LK001", Name = "New railway line" };
            project.Lines.Add(line);
        }

        fromIndex = 0;
        toIndex = 1;
        sectionNumber = line.Sections.Count + 1;
        trackCount = 2;
        trackVmax.Clear();
        EnsureVmax();
        sectionField = 0;
        sectionWizard = true;
        Status("ADD SECTION: SELECT TRACK COUNT AND VMAX");
    }

    void UpdateSectionWizard(KeyboardState keys, MouseState mouse, Func<Keys, bool> P)
    {
        var project = Project;
        if (project == null) { sectionWizard = false; return; }

        if (P(Keys.Escape)) { sectionWizard = false; Status("SECTION CREATION CANCELLED"); return; }
        if (P(Keys.Enter)) { SaveSection(project); return; }

        var max = 3 + trackCount;
        if (P(Keys.Tab)) { sectionField = (sectionField + 1) % (max + 1); return; }
        if (P(Keys.Up)) { sectionField = Math.Max(0, sectionField - 1); return; }
        if (P(Keys.Down)) { sectionField = Math.Min(max, sectionField + 1); return; }
        if (P(Keys.Left)) { ChangeField(project, -1); return; }
        if (P(Keys.Right)) { ChangeField(project, 1); return; }

        if (mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            var r = SectionRect();
            for (var i = 0; i <= max; i++)
            {
                var row = new Rectangle(r.X + 30, r.Y + 84 + i * 46, r.Width - 60, 38);
                if (row.Contains(mouse.Position)) { sectionField = i; ChangeField(project, 1); return; }
            }
            if (new Rectangle(r.X + 30, r.Bottom - 62, 220, 42).Contains(mouse.Position)) SaveSection(project);
            else if (new Rectangle(r.X + 270, r.Bottom - 62, 220, 42).Contains(mouse.Position))
            {
                sectionWizard = false;
                Status("SECTION CREATION CANCELLED");
            }
        }
    }

    void ChangeField(RailProject project, int dir)
    {
        EnsureVmax();
        if (sectionField == 0)
        {
            fromIndex = Cycle(fromIndex, project.Stations.Count, dir);
            if (fromIndex == toIndex) toIndex = Cycle(toIndex, project.Stations.Count, dir);
        }
        else if (sectionField == 1)
        {
            toIndex = Cycle(toIndex, project.Stations.Count, dir);
            if (toIndex == fromIndex) toIndex = Cycle(toIndex, project.Stations.Count, dir);
        }
        else if (sectionField == 2) sectionNumber = Math.Max(1, sectionNumber + dir);
        else if (sectionField == 3)
        {
            trackCount = Math.Clamp(trackCount + dir, 1, 8);
            EnsureVmax();
        }
        else
        {
            var i = sectionField - 4;
            if (i >= 0 && i < trackCount) trackVmax[i] = Math.Clamp(trackVmax[i] + dir * 10, 20, 300);
        }
    }

    static int Cycle(int value, int count, int dir)
        => count <= 0 ? 0 : (value + dir + count) % count;

    void EnsureVmax()
    {
        while (trackVmax.Count < trackCount) trackVmax.Add(120);
        while (trackVmax.Count > trackCount) trackVmax.RemoveAt(trackVmax.Count - 1);
    }

    void SaveSection(RailProject project)
    {
        var line = SelectedLine(project) ?? project.Lines.FirstOrDefault();
        if (line == null || project.Stations.Count < 2) return;

        var a = project.Stations[Math.Clamp(fromIndex, 0, project.Stations.Count - 1)];
        var b = project.Stations[Math.Clamp(toIndex, 0, project.Stations.Count - 1)];
        if (a.Id == b.Id) { Status("SECTION NEEDS TWO DIFFERENT STATIONS"); return; }
        if (line.Sections.Any(x => (x.FromStationId == a.Id && x.ToStationId == b.Id) || (x.FromStationId == b.Id && x.ToStationId == a.Id)))
        { Status("SECTION EXISTS"); return; }

        EnsureVmax();
        var section = new RailSection
        {
            Number = sectionNumber,
            FromStationId = a.Id,
            ToStationId = b.Id,
            Geometry = new() { new(a.X, a.Y), new(b.X, b.Y) }
        };

        var forwardCount = (trackCount + 1) / 2;
        for (var i = 0; i < trackCount; i++)
        {
            section.Tracks.Add(new RailTrack
            {
                Name = (i + 1).ToString(),
                Direction = i < forwardCount ? TrackDirection.Forward : TrackDirection.Reverse,
                Vmax = trackVmax[i]
            });
        }

        line.Sections.Add(section);
        SetGuid("selectedLine", line.Id);
        SetGuid("selectedSection", section.Id);
        sectionWizard = false;
        Status($"ADDED {line.Number}|{section.Number} {a.Code} -> {b.Code} ({trackCount} TRACKS)");
    }

    RailProject? Project => Get("project") as RailProject;

    RailwayLine? SelectedLine(RailProject project)
    {
        var value = Get("selectedLine");
        return value is Guid id ? project.Lines.FirstOrDefault(x => x.Id == id) : null;
    }

    object? Get(string name)
        => typeof(MainGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game);

    void SetGuid(string name, Guid value)
        => typeof(MainGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, (Guid?)value);

    void Status(string value)
        => typeof(MainGame).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, value);

    void Invoke(string method, params object?[] args)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var candidate in typeof(MainGame).GetMethods(flags).Where(x => x.Name == method))
        {
            if (candidate.GetParameters().Length != args.Length) continue;
            try { candidate.Invoke(game, args); return; }
            catch (ArgumentException) { }
        }
    }

    Rectangle MenuRect() => new(
        (GraphicsDevice.Viewport.Width - 560) / 2,
        (GraphicsDevice.Viewport.Height - 390) / 2,
        560, 390);

    Rectangle SectionRect() => new(
        (GraphicsDevice.Viewport.Width - 620) / 2,
        (GraphicsDevice.Viewport.Height - 650) / 2,
        620, 650);

    public override void Draw(GameTime gameTime)
    {
        if (!open && !sectionWizard) return;
        batch.Begin(samplerState: SamplerState.PointClamp);
        Rect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 120));
        if (sectionWizard) DrawSectionWizard(); else DrawMenu();
        batch.End();
    }

    void DrawMenu()
    {
        var r = MenuRect();
        Rect(r, new Color(18, 22, 28)); Border(r, new Color(90, 100, 115));
        Text("ADD NEW", new(r.X + 28, r.Y + 22), Color.White, 1.15f);
        Text("SELECT OBJECT TYPE", new(r.X + 28, r.Y + 50), Color.LightGray, .65f);
        for (var i = 0; i < Items.Length; i++)
        {
            var item = new Rectangle(r.X + 28, r.Y + 78 + i * 58, r.Width - 56, 48);
            var active = i == selected;
            Rect(item, active ? new Color(48, 56, 68) : new Color(28, 33, 40));
            Border(item, active ? new Color(160, 175, 195) : new Color(65, 72, 82));
            Text(Items[i], new(item.X + 16, item.Y + 14), active ? Color.White : Color.LightGray, .8f);
        }
        Text("UP / DOWN SELECT   ENTER OPEN   ESC CANCEL", new(r.X + 28, r.Bottom - 27), Color.Gray, .55f);
    }

    void DrawSectionWizard()
    {
        var project = Project;
        if (project == null || project.Stations.Count < 2) return;
        EnsureVmax();
        var r = SectionRect();
        Rect(r, new Color(18, 22, 28)); Border(r, new Color(90, 100, 115));
        Text("ADD RAILWAY SECTION", new(r.X + 30, r.Y + 22), Color.White, .95f);
        Text("Choose stations, track count and Vmax for every track.", new(r.X + 30, r.Y + 50), Color.LightGray, .55f);

        var from = project.Stations[fromIndex];
        var to = project.Stations[toIndex];
        Field("FROM", from.Code + "  " + from.Name, r.X + 30, r.Y + 92, sectionField == 0);
        Field("TO", to.Code + "  " + to.Name, r.X + 30, r.Y + 138, sectionField == 1);
        Field("SEGMENT", sectionNumber.ToString(), r.X + 30, r.Y + 184, sectionField == 2);
        Field("TRACK COUNT", trackCount.ToString(), r.X + 30, r.Y + 230, sectionField == 3);

        Text("TRACK", new(r.X + 30, r.Y + 278), Color.Gray, .5f);
        Text("DIRECTION", new(r.X + 130, r.Y + 278), Color.Gray, .5f);
        Text("VMAX", new(r.X + 340, r.Y + 278), Color.Gray, .5f);
        for (var i = 0; i < trackCount; i++)
        {
            var y = r.Y + 302 + i * 40;
            var active = sectionField == 4 + i;
            Rect(new(r.X + 25, y - 5, r.Width - 50, 32), active ? new Color(48, 56, 68) : new Color(28, 33, 40));
            Text((i + 1).ToString(), new(r.X + 38, y + 2), active ? Color.White : Color.LightGray, .58f);
            Text(i < (trackCount + 1) / 2 ? "FORWARD" : "REVERSE", new(r.X + 130, y + 2), active ? Color.White : Color.LightGray, .55f);
            Text(trackVmax[i] + " km/h", new(r.X + 340, y + 2), active ? Color.White : Color.LightGray, .55f);
        }
        Text("LEFT / RIGHT changes selected value. Vmax step: 10 km/h.", new(r.X + 30, r.Bottom - 105), Color.Gray, .5f);
        Text("TAB / UP / DOWN selects fields. ENTER saves. ESC cancels.", new(r.X + 30, r.Bottom - 83), Color.Gray, .5f);
        Button("[ SAVE ]", r.X + 30, r.Bottom - 62, 220, 42);
        Button("[ CANCEL ]", r.X + 270, r.Bottom - 62, 220, 42);
    }

    void Field(string label, string value, int x, int y, bool active)
    {
        Text(label, new(x, y), Color.Gray, .48f);
        Text(value, new(x + 130, y), active ? Color.White : Color.LightGray, .58f);
    }

    void Button(string label, int x, int y, int w, int h)
    {
        Rect(new(x, y, w, h), new Color(31, 41, 50));
        Text(label, new(x + 12, y + 13), Color.White, .58f);
    }

    void Rect(Rectangle r, Color c) => batch.Draw(pixel, r, c);

    void Border(Rectangle r, Color c)
    {
        Rect(new(r.X, r.Y, r.Width, 2), c); Rect(new(r.X, r.Bottom - 2, r.Width, 2), c);
        Rect(new(r.X, r.Y, 2, r.Height), c); Rect(new(r.Right - 2, r.Y, 2, r.Height), c);
    }

    void Text(string value, Vector2 position, Color color, float scale)
        => batch.DrawString(font, value, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
}
