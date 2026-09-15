using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

/// <summary>
/// Central ADD NEW chooser plus a dedicated railway-section wizard.
/// </summary>
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
    int sectionNumber = 1;
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
        bool Pressed(Keys key) => keys.IsKeyDown(key) && !oldKeys.IsKeyDown(key);

        if (sectionWizard)
        {
            UpdateSectionWizard(keys, mouse, Pressed);
        }
        else if (!open)
        {
            if (Pressed(Keys.Insert) || Pressed(Keys.F12))
            {
                open = true;
                selected = 0;
            }
        }
        else
        {
            if (Pressed(Keys.Escape))
            {
                open = false;
            }
            else if (Pressed(Keys.Up))
            {
                selected = (selected + Items.Length - 1) % Items.Length;
            }
            else if (Pressed(Keys.Down))
            {
                selected = (selected + 1) % Items.Length;
            }
            else if (Pressed(Keys.Enter))
            {
                Activate(selected);
            }
            else if (mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
            {
                var rect = MenuRect();
                for (var i = 0; i < Items.Length; i++)
                {
                    var item = new Rectangle(rect.X + 28, rect.Y + 78 + i * 58, rect.Width - 56, 48);
                    if (item.Contains(mouse.Position))
                    {
                        Activate(i);
                        break;
                    }
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

        if (selected == 2)
        {
            open = false;
            OpenSectionWizard();
            return;
        }

        open = false;
        switch (selected)
        {
            case 0:
                Invoke("BeginAddStation");
                break;
            case 1:
                Invoke("OpenLineEditor", null);
                break;
            case 3:
                Invoke("OpenTrainDialog");
                break;
        }
    }

    void OpenSectionWizard()
    {
        var project = GetProject();
        if (project == null || project.Stations.Count < 2)
        {
            SetStatus("ADD AT LEAST TWO STATIONS FIRST");
            return;
        }

        var line = GetSelectedLine(project) ?? project.Lines.FirstOrDefault();
        if (line == null)
        {
            line = new RailwayLine { Number = "LK001", Name = "New railway line" };
            project.Lines.Add(line);
        }

        fromIndex = 0;
        toIndex = Math.Min(1, project.Stations.Count - 1);
        sectionNumber = line.Sections.Count + 1;
        trackCount = 2;
        trackVmax.Clear();
        EnsureTrackVmax();
        sectionField = 0;
        sectionWizard = true;
        SetStatus("ADD SECTION: SELECT TRACKS AND VMAX");
    }

    void UpdateSectionWizard(KeyboardState keys, MouseState mouse, Func<Keys, bool> Pressed)
    {
        var project = GetProject();
        if (project == null) { sectionWizard = false; return; }

        if (Pressed(Keys.Escape))
        {
            sectionWizard = false;
            SetStatus("SECTION CREATION CANCELLED");
            return;
        }

        if (Pressed(Keys.Enter))
        {
            SaveSectionWizard(project);
            return;
        }

        var maxField = 3 + trackCount;
        if (Pressed(Keys.Tab))
        {
            sectionField = (sectionField + 1) % (maxField + 1);
            return;
        }

        if (Pressed(Keys.Up))
        {
            sectionField = Math.Max(0, sectionField - 1);
            return;
        }

        if (Pressed(Keys.Down))
        {
            sectionField = Math.Min(maxField, sectionField + 1);
            return;
        }

        if (Pressed(Keys.Left))
        {
            ChangeSectionField(project, -1);
            return;
        }

        if (Pressed(Keys.Right))
        {
            ChangeSectionField(project, 1);
            return;
        }

        if (mouse.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            var r = SectionWizardRect();
            for (var i = 0; i <= maxField; i++)
            {
                var y = r.Y + 92 + i * 46;
                var row = new Rectangle(r.X + 30, y - 8, r.Width - 60, 38);
                if (row.Contains(mouse.Position))
                {
                    sectionField = i;
                    ChangeSectionField(project, 1);
                    return;
                }
            }

            var save = new Rectangle(r.X + 30, r.Bottom - 62, 220, 42);
            var cancel = new Rectangle(r.X + 270, r.Bottom - 62, 220, 42);
            if (save.Contains(mouse.Position)) SaveSectionWizard(project);
            else if (cancel.Contains(mouse.Position))
            {
                sectionWizard = false;
                SetStatus("SECTION CREATION CANCELLED");
            }
        }
    }

    void ChangeSectionField(RailProject project, int dir)
    {
        EnsureTrackVmax();

        if (sectionField == 0)
        {
            fromIndex = CycleIndex(fromIndex, project.Stations.Count, dir);
            if (fromIndex == toIndex && project.Stations.Count > 1)
                toIndex = CycleIndex(toIndex, project.Stations.Count, dir);
        }
        else if (sectionField == 1)
        {
            toIndex = CycleIndex(toIndex, project.Stations.Count, dir);
            if (toIndex == fromIndex && project.Stations.Count > 1)
                toIndex = CycleIndex(toIndex, project.Stations.Count, dir);
        }
        else if (sectionField == 2)
        {
            sectionNumber = Math.Max(1, sectionNumber + dir);
        }
        else if (sectionField == 3)
        {
            trackCount = Math.Clamp(trackCount + dir, 1, 8);
            EnsureTrackVmax();
        }
        else
        {
            var index = sectionField - 4;
            if (index >= 0 && index < trackCount)
                trackVmax[index] = Math.Clamp(trackVmax[index] + dir * 10, 20, 300);
        }
    }

    static int CycleIndex(int current, int count, int dir)
    {
        if (count <= 0) return 0;
        return (current + dir + count) % count;
    }

    void EnsureTrackVmax()
    {
        while (trackVmax.Count < trackCount) trackVmax.Add(120);
        while (trackVmax.Count > trackCount) trackVmax.RemoveAt(trackVmax.Count - 1);
    }

    void SaveSectionWizard(RailProject project)
    {
        if (project.Stations.Count < 2) return;
        var line = GetSelectedLine(project) ?? project.Lines.FirstOrDefault();
        if (line == null) return;

        var a = project.Stations[Math.Clamp(fromIndex, 0, project.Stations.Count - 1)];
        var b = project.Stations[Math.Clamp(toIndex, 0, project.Stations.Count - 1)];
        if (a.Id == b.Id)
        {
            SetStatus("SECTION NEEDS TWO DIFFERENT STATIONS");
            return;
        }

        if (line.Sections.Any(x =>
            (x.FromStationId == a.Id && x.ToStationId == b.Id) ||
            (x.FromStationId == b.Id && x.ToStationId == a.Id)))
        {
            SetStatus("SECTION EXISTS");
            return;
        }

        EnsureTrackVmax();
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
            var direction = i < forwardCount ? TrackDirection.Forward : TrackDirection.Reverse;
            section.Tracks.Add(new RailTrack
            {
                Name = (i + 1).ToString(),
                Direction = direction,
                Vmax = trackVmax[i]
            });
        }

        line.Sections.Add(section);
        SetNullableGuidField("selectedLine", line.Id);
        SetNullableGuidField("selectedSection", section.Id);
        SetStatus($"ADDED {line.Number}|{section.Number} {a.Code} -> {b.Code} ({trackCount} TRACKS)");
        sectionWizard = false;
    }

    RailwayLine? GetSelectedLine(RailProject project)
    {
        var value = GetField("selectedLine");
        if (value is Guid id) return project.Lines.FirstOrDefault(x => x.Id == id);
        if (value is Guid? nullable && nullable.HasValue) return project.Lines.FirstOrDefault(x => x.Id == nullable.Value);
        return null;
    }

    RailProject? GetProject()
        => GetField("project") as RailProject;

    object? GetField(string name)
        => typeof(MainGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game);

    void SetNullableGuidField(string name, Guid value)
        => typeof(MainGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, (Guid?)value);

    void SetStatus(string value)
    {
        typeof(MainGame).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(game, value);
    }

    void Invoke(string method, params object?[] args)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var candidates = typeof(MainGame).GetMethods(flags)
            .Where(x => x.Name == method)
            .ToArray();

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (parameters.Length != args.Length) continue;
            try
            {
                candidate.Invoke(game, args);
                return;
            }
            catch (ArgumentException)
            {
            }
        }
    }

    Rectangle MenuRect()
    {
        const int width = 560;
        const int height = 390;
        return new Rectangle(
            (GraphicsDevice.Viewport.Width - width) / 2,
            (GraphicsDevice.Viewport.Height - height) / 2,
            width,
            height);
    }

    Rectangle SectionWizardRect()
    {
        const int width = 620;
        const int height = 650;
        return new Rectangle(
            (GraphicsDevice.Viewport.Width - width) / 2,
            (GraphicsDevice.Viewport.Height - height) / 2,
            width,
            height);
    }

    public override void Draw(GameTime gameTime)
    {
        if (!open && !sectionWizard) return;

        batch.Begin(samplerState: SamplerState.PointClamp);
        Rect(new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(0, 0, 0, 120));

        if (sectionWizard) DrawSectionWizard();
        else DrawMenu();

        batch.End();
    }

    void DrawMenu()
    {
        var r = MenuRect();
        Rect(r, new Color(18, 22, 28));
        Border(r, new Color(90, 100, 115));

        Text("ADD NEW", new Vector2(r.X + 28, r.Y + 22), Color.White, 1.15f);
        Text("SELECT OBJECT TYPE", new Vector2(r.X + 28, r.Y + 50), Color.LightGray, .65f);

        for (var i = 0; i < Items.Length; i++)
        {
            var item = new Rectangle(r.X + 28, r.Y + 78 + i * 58, r.Width - 56, 48);
            var active = i == selected;
            Rect(item, active ? new Color(48, 56, 68) : new Color(28, 33, 40));
            Border(item, active ? new Color(160, 175, 195) : new Color(65, 72, 82));
            Text(Items[i], new Vector2(item.X + 16, item.Y + 14), active ? Color.White : Color.LightGray, .8f);
        }

        Text("UP / DOWN  SELECT    ENTER  OPEN    ESC  CANCEL", new Vector2(r.X + 28, r.Bottom - 27), Color.Gray, .55f);
    }

    void DrawSectionWizard()
    {
        var project = GetProject();
        if (project == null || project.Stations.Count == 0) return;
        EnsureTrackVmax();
        var r = SectionWizardRect();

        Rect(r, new Color(18, 22, 28));
        Border(r, new Color(90, 100, 115));
        Text("ADD RAILWAY SECTION", new Vector2(r.X + 30, r.Y + 22), Color.White, .95f);
        Text("Choose stations, track count and Vmax for every track.", new Vector2(r.X + 30, r.Y + 50), Color.LightGray, .55f);

        var from = project.Stations[Math.Clamp(fromIndex, 0, project.Stations.Count - 1)];
        var to = project.Stations[Math.Clamp(toIndex, 0, project.Stations.Count - 1)];
        Field("FROM", from.Code + "  " + from.Name, r.X + 30, r.Y + 92, sectionField == 0);
        Field("TO", to.Code + "  " + to.Name, r.X + 30, r.Y + 138, sectionField == 1);
        Field("SEGMENT", sectionNumber.ToString(), r.X + 30, r.Y + 184, sectionField == 2);
        Field("TRACK COUNT", trackCount.ToString(), r.X + 30, r.Y + 230, sectionField == 3);

        Text("TRACK", new Vector2(r.X + 30, r.Y + 278), Color.Gray, .5f);
        Text("DIRECTION", new Vector2(r.X + 130, r.Y + 278), Color.Gray, .5f);
        Text("VMAX", new Vector2(r.X + 340, r.Y + 278), Color.Gray, .5f);

        for (var i = 0; i < trackCount; i++)
        {
            var y = r.Y + 302 + i * 40;
            var active = sectionField == 4 + i;
            Rect(new Rectangle(r.X + 25, y - 5, r.Width - 50, 32), active ? new Color(48, 56, 68) : new Color(28, 33, 40));
            Text((i + 1).ToString(), new Vector2(r.X + 38, y + 2), active ? Color.White : Color.LightGray, .58f);
            var direction = i < (trackCount + 1) / 2 ? "FORWARD" : "REVERSE";
            Text(direction, new Vector2(r.X + 130, y + 2), active ? Color.White : Color.LightGray, .55f);
            Text(trackVmax[i] + " km/h", new Vector2(r.X + 340, y + 2), active ? Color.White : Color.LightGray, .55f);
        }

        Text("LEFT / RIGHT changes selected value. Vmax step: 10 km/h.", new Vector2(r.X + 30, r.Bottom - 105), Color.Gray, .5f);
        Text("TAB / UP / DOWN selects fields. ENTER saves. ESC cancels.", new Vector2(r.X + 30, r.Bottom - 83), Color.Gray, .5f);
        Button("[ SAVE ]", r.X + 30, r.Bottom - 62, 220, 42);
        Button("[ CANCEL ]", r.X + 270, r.Bottom - 62, 220, 42);
    }

    void Field(string label, string value, int x, int y, bool active)
    {
        Text(label, new Vector2(x, y), Color.Gray, .48f);
        Text(value, new Vector2(x + 130, y), active ? Color.White : Color.LightGray, .58f);
    }

    void Button(string label, int x, int y, int w, int h)
    {
        Rect(new Rectangle(x, y, w, h), new Color(31, 41, 50));
        Text(label, new Vector2(x + 12, y + 13), Color.White, .58f);
    }

    void Rect(Rectangle rectangle, Color color) => batch.Draw(pixel, rectangle, color);

    void Border(Rectangle r, Color color)
    {
        Rect(new Rectangle(r.X, r.Y, r.Width, 2), color);
        Rect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), color);
        Rect(new Rectangle(r.X, r.Y, 2, r.Height), color);
        Rect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), color);
    }

    void Text(string value, Vector2 position, Color color, float scale)
        => batch.DrawString(font, value, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
}
