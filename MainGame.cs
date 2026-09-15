using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

public sealed class MainGame : Game
{
    readonly GraphicsDeviceManager graphics;
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D pixel = null!;

    RailProject project = new();
    AnalysisResult analysis = new();
    string mode = "SELECT";
    string status = "READY";

    Guid? selectedStation, selectedTrain, selectedLine, selectedSection, pendingFrom;
    Vector2 camera = new(80, 70);
    float zoom = .7f;
    MouseState oldMouse;
    KeyboardState oldKeys;
    int nextStation = 1, nextTrain = 1;

    bool stationDialog;
    Guid? pendingNewStationId;
    Station draftStation = new();
    bool lineDialog;
    RailwayLine draftLine = new();
    bool sectionDialog;
    RailSection draftSection = new();
    Guid sectionEditLineId;

    bool trainDialog;
    bool stationPicker;
    int stationPickerRow;
    int stationPickerIndex;
    TrainRun draftTrain = new();
    int draftField, draftRow;
    bool editingText;

    public MainGame()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1440,
            PreferredBackBufferHeight = 900
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "RailPlanner";
        Window.TextInput += OnTextInput;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Content.Load<SpriteFont>("DefaultFont");
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
    }

    void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (stationDialog)
        {
            if (e.Character == '\b') { BackspaceDialogField(); return; }
            if (e.Character == '\r' || e.Character == '\n') return;
            if (!char.IsControl(e.Character)) AppendDialogField(e.Character);
            return;
        }

        if (lineDialog)
        {
            if (e.Character == '\b') { BackspaceDialogField(); return; }
            if (e.Character == '\r' || e.Character == '\n') return;
            if (!char.IsControl(e.Character)) AppendDialogField(e.Character);
            return;
        }

        if (sectionDialog)
        {
            if (e.Character == '\b') { BackspaceDialogField(); return; }
            if (e.Character == '\r' || e.Character == '\n') return;
            if (!char.IsControl(e.Character)) AppendDialogField(e.Character);
            return;
        }

        if (!trainDialog || !editingText) return;
        if (e.Character == '\b') { BackspaceTrainField(); return; }
        if (e.Character == '\r' || e.Character == '\n') return;
        if (!char.IsControl(e.Character)) AppendTrainField(e.Character);
    }

    protected override void Update(GameTime time)
    {
        var k = Keyboard.GetState();
        var m = Mouse.GetState();
        bool P(Keys x) => k.IsKeyDown(x) && !oldKeys.IsKeyDown(x);

        if (stationDialog || lineDialog || sectionDialog)
        {
            UpdateDataDialog(k, m, P);
            oldKeys = k;
            oldMouse = m;
            base.Update(time);
            return;
        }

        if (trainDialog)
        {
            UpdateTrainDialog(k, m, P);
            oldKeys = k;
            oldMouse = m;
            base.Update(time);
            return;
        }

        if (P(Keys.F1)) mode = "SELECT";
        if (P(Keys.F2)) BeginAddStation();
        if (P(Keys.F3)) { mode = "SECTION"; pendingFrom = null; status = "CLICK TWO STATIONS"; }
        if (P(Keys.F4)) OpenTrainDialog();
        if (P(Keys.F8)) { mode = "LINE VIEW"; status = "LINE MENU"; }
        if (P(Keys.F9)) OpenStationEditor();
        if (P(Keys.F10)) ChangeStationTracks(1);
        if (P(Keys.F11)) ChangeStationTracks(-1);
        if (P(Keys.F5)) Save();
        if (P(Keys.F6)) LoadProject();
        if (P(Keys.F7))
        {
            analysis = TimetableAnalyzer.Analyze(project);
            status = $"ANALYSIS: {analysis.Conflicts.Count} CONFLICTS / {analysis.Warnings.Count} WARNINGS";
        }
        if (P(Keys.D) && selectedTrain.HasValue) OpenTrainDuplicate();

        if (P(Keys.L)) OpenLineEditor(null);
        if (P(Keys.E))
        {
            if (selectedSection.HasValue) OpenSectionEditor();
            else if (selectedLine.HasValue) OpenLineEditor(selectedLine);
            else if (selectedStation.HasValue) OpenStationEditor();
        }

        if (P(Keys.N) && selectedLine.HasValue) OpenSectionEditor(null, true);
        if (P(Keys.Delete)) Delete();

        if (P(Keys.Escape))
        {
            mode = "SELECT";
            pendingFrom = null;
            selectedSection = null;
        }

        if (P(Keys.OemPlus) || P(Keys.Add))
            zoom = MathHelper.Clamp(zoom * 1.1f, .25f, 3f);
        if (P(Keys.OemMinus) || P(Keys.Subtract))
            zoom = MathHelper.Clamp(zoom / 1.1f, .25f, 3f);

        camera += new Vector2(
            (k.IsKeyDown(Keys.Left) ? 5 : 0) - (k.IsKeyDown(Keys.Right) ? 5 : 0),
            (k.IsKeyDown(Keys.Up) ? 5 : 0) - (k.IsKeyDown(Keys.Down) ? 5 : 0));

        if (m.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            if (m.X < 280 && m.Y >= 54) ClickSidebar(new Vector2(m.X, m.Y));
            else if (m.X >= 280 && m.Y >= 54) Click(new Vector2(m.X, m.Y));
        }

        oldKeys = k;
        oldMouse = m;
        base.Update(time);
    }

    void BeginAddStation()
    {
        mode = "STATION";
        status = "CLICK MAP TO ADD STATION";
    }

    void OpenStationEditor()
    {
        if (selectedStation is not Guid id)
        {
            status = "SELECT A STATION FIRST";
            return;
        }

        var source = project.Stations.FirstOrDefault(x => x.Id == id);
        if (source == null) return;

        draftStation = JsonSerializer.Deserialize<Station>(
            JsonSerializer.Serialize(source, ProjectJson.Options), ProjectJson.Options)!;
        pendingNewStationId = null;
        stationDialog = true;
        editingText = false;
        draftField = 1;
        mode = "STATION EDIT";
        status = "EDIT STATION";
    }

    void OpenLineEditor(Guid? id)
    {
        if (id is Guid lineId)
        {
            var source = project.Lines.FirstOrDefault(x => x.Id == lineId);
            if (source == null) return;
            draftLine = JsonSerializer.Deserialize<RailwayLine>(
                JsonSerializer.Serialize(source, ProjectJson.Options), ProjectJson.Options)!;
        }
        else
        {
            draftLine = new RailwayLine
            {
                Number = $"LK{project.Lines.Count + 1:000}",
                Name = "New railway line"
            };
        }

        lineDialog = true;
        editingText = false;
        draftField = 0;
        mode = "LINE EDIT";
        status = id.HasValue ? "EDIT LINE" : "ADD LINE";
    }

    void OpenSectionEditor(Guid? lineId = null, bool forceNew = false)
    {
        RailwayLine? line = null;

        if (lineId.HasValue)
            line = project.Lines.FirstOrDefault(x => x.Id == lineId.Value);
        else if (selectedLine.HasValue)
            line = project.Lines.FirstOrDefault(x => x.Id == selectedLine.Value);

        if (line == null)
        {
            status = "SELECT A LINE FIRST";
            return;
        }

        if (!forceNew && selectedSection is Guid sectionId && line.Sections.Any(x => x.Id == sectionId))
        {
            var source = line.Sections.First(x => x.Id == sectionId);
            draftSection = JsonSerializer.Deserialize<RailSection>(
                JsonSerializer.Serialize(source, ProjectJson.Options), ProjectJson.Options)!;
        }
        else
        {
            var a = project.Stations.FirstOrDefault();
            var b = project.Stations.Skip(1).FirstOrDefault() ?? a;
            draftSection = new RailSection
            {
                Number = line.Sections.Count + 1,
                FromStationId = a?.Id ?? Guid.Empty,
                ToStationId = b?.Id ?? Guid.Empty,
                Geometry = a != null && b != null
                    ? new() { new(a.X, a.Y), new(b.X, b.Y) }
                    : new()
            };
            draftSection.Tracks.Add(new RailTrack { Name = "1", Direction = TrackDirection.Forward });
            draftSection.Tracks.Add(new RailTrack { Name = "2", Direction = TrackDirection.Reverse });
            selectedSection = null;
        }

        sectionEditLineId = line.Id;
        sectionDialog = true;
        editingText = false;
        draftField = 0;
        mode = "SECTION EDIT";
        status = selectedSection.HasValue ? "EDIT SECTION" : "ADD SECTION";
    }

    void UpdateDataDialog(KeyboardState k, MouseState m, Func<Keys, bool> P)
    {
        if (editingText)
        {
            if (P(Keys.Escape) || P(Keys.Enter)) editingText = false;
            return;
        }

        if (P(Keys.Escape))
        {
            CancelDataDialogs();
            return;
        }

        if (P(Keys.Enter))
        {
            if (stationDialog) SaveStationDialog();
            else if (lineDialog) SaveLineDialog();
            else SaveSectionDialog();
            return;
        }

        if (P(Keys.Tab))
        {
            var max = stationDialog ? 3 : lineDialog ? 1 : 4;
            draftField = (draftField + 1) % (max + 1);
            return;
        }

        if (P(Keys.Left)) CycleDialogField(-1);
        if (P(Keys.Right)) CycleDialogField(1);

        if (P(Keys.Space))
        {
            if (stationDialog && (draftField == 0 || draftField == 1)) editingText = true;
            if (lineDialog && (draftField == 0 || draftField == 1)) editingText = true;
            if (sectionDialog && (draftField >= 2 && draftField <= 4)) editingText = true;
        }

        if (m.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            if (stationDialog) ClickStationDialog(m);
            else if (lineDialog) ClickLineDialog(m);
            else ClickSectionDialog(m);
        }
    }

    void CycleDialogField(int dir)
    {
        if (stationDialog)
        {
            if (draftField == 2)
                draftStation.Tracks = Math.Clamp(draftStation.Tracks + dir, 1, 20);
            else if (draftField == 3)
                draftStation.Type = (StationType)(((int)draftStation.Type + dir + 5) % 5);
            return;
        }

        if (lineDialog) return;

        if (sectionDialog)
        {
            if (draftField == 0)
            {
                draftSection.FromStationId = CycleStationId(draftSection.FromStationId, dir);
                RefreshSectionGeometry();
            }
            else if (draftField == 1)
            {
                draftSection.ToStationId = CycleStationId(draftSection.ToStationId, dir);
                RefreshSectionGeometry();
            }
            else if (draftField == 3)
            {
                SetTrackName(0, CycleTrackName(GetTrackName(0), dir));
            }
            else if (draftField == 4)
            {
                SetTrackName(1, CycleTrackName(GetTrackName(1), dir));
            }
        }
    }

    Guid CycleStationId(Guid current, int dir)
    {
        var stations = project.Stations;
        if (stations.Count == 0) return Guid.Empty;
        var i = stations.FindIndex(x => x.Id == current);
        if (i < 0) i = 0;
        i = (i + dir + stations.Count) % stations.Count;
        return stations[i].Id;
    }

    void RefreshSectionGeometry()
    {
        var a = project.Stations.FirstOrDefault(x => x.Id == draftSection.FromStationId);
        var b = project.Stations.FirstOrDefault(x => x.Id == draftSection.ToStationId);
        if (a == null || b == null) return;
        draftSection.Geometry = new() { new(a.X, a.Y), new(b.X, b.Y) };
    }

    string GetTrackName(int index)
    {
        var track = draftSection.Tracks
            .Where(x => x.Direction == (index == 0 ? TrackDirection.Forward : TrackDirection.Reverse))
            .FirstOrDefault();
        return track?.Name ?? (index == 0 ? "1" : "2");
    }

    string CycleTrackName(string current, int dir)
    {
        var values = new[] { "1", "2", "3", "4", "A", "B", "C" };
        var i = Array.IndexOf(values, current);
        if (i < 0) i = 0;
        return values[(i + dir + values.Length) % values.Length];
    }

    void SetTrackName(int index, string name)
    {
        var direction = index == 0 ? TrackDirection.Forward : TrackDirection.Reverse;
        var track = draftSection.Tracks.FirstOrDefault(x => x.Direction == direction);
        if (track == null)
        {
            draftSection.Tracks.Add(new RailTrack { Name = name, Direction = direction });
        }
        else track.Name = name;
    }

    void ClickStationDialog(MouseState m)
    {
        if (m.X >= 500 && m.X <= 1050 && m.Y >= 180 && m.Y < 235)
        {
            draftField = 0;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 1050 && m.Y >= 235 && m.Y < 290)
        {
            draftField = 1;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 700 && m.Y >= 290 && m.Y < 345)
            draftField = 2;
        else if (m.X >= 700 && m.X <= 1050 && m.Y >= 290 && m.Y < 345)
            draftField = 3;
        else if (m.X >= 500 && m.X <= 760 && m.Y >= 700 && m.Y < 750)
            SaveStationDialog();
        else if (m.X >= 780 && m.X <= 1040 && m.Y >= 700 && m.Y < 750)
            CancelDataDialogs();
    }

    void ClickLineDialog(MouseState m)
    {
        if (m.X >= 500 && m.X <= 1050 && m.Y >= 200 && m.Y < 255)
        {
            draftField = 0;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 1050 && m.Y >= 255 && m.Y < 310)
        {
            draftField = 1;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 760 && m.Y >= 700 && m.Y < 750)
            SaveLineDialog();
        else if (m.X >= 780 && m.X <= 1040 && m.Y >= 700 && m.Y < 750)
            CancelDataDialogs();
    }

    void ClickSectionDialog(MouseState m)
    {
        if (m.X >= 500 && m.X <= 1040 && m.Y >= 190 && m.Y < 245)
        {
            draftField = 0;
            CycleDialogField(1);
        }
        else if (m.X >= 500 && m.X <= 1040 && m.Y >= 245 && m.Y < 300)
        {
            draftField = 1;
            CycleDialogField(1);
        }
        else if (m.X >= 500 && m.X <= 1040 && m.Y >= 300 && m.Y < 355)
        {
            draftField = 2;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 1040 && m.Y >= 355 && m.Y < 410)
        {
            draftField = 3;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 1040 && m.Y >= 410 && m.Y < 465)
        {
            draftField = 4;
            editingText = true;
        }
        else if (m.X >= 500 && m.X <= 760 && m.Y >= 700 && m.Y < 750)
            SaveSectionDialog();
        else if (m.X >= 780 && m.X <= 1040 && m.Y >= 700 && m.Y < 750)
            CancelDataDialogs();
    }

    void AppendDialogField(char c)
    {
        if (stationDialog)
        {
            if (draftField == 0 && draftStation.Code.Length < 12) draftStation.Code += c;
            else if (draftField == 1 && draftStation.Name.Length < 60) draftStation.Name += c;
        }
        else if (lineDialog)
        {
            if (draftField == 0 && draftLine.Number.Length < 20) draftLine.Number += c;
            else if (draftField == 1 && draftLine.Name.Length < 80) draftLine.Name += c;
        }
        else if (sectionDialog)
        {
            if (draftField == 2 && char.IsDigit(c))
            {
                if (int.TryParse(draftSection.Number.ToString() + c, out var n))
                    draftSection.Number = n;
            }
            else if (draftField == 3) SetTrackName(0, GetTrackName(0) + c);
            else if (draftField == 4) SetTrackName(1, GetTrackName(1) + c);
        }
    }

    void BackspaceDialogField()
    {
        if (stationDialog)
        {
            if (draftField == 0 && draftStation.Code.Length > 0) draftStation.Code = draftStation.Code[..^1];
            else if (draftField == 1 && draftStation.Name.Length > 0) draftStation.Name = draftStation.Name[..^1];
        }
        else if (lineDialog)
        {
            if (draftField == 0 && draftLine.Number.Length > 0) draftLine.Number = draftLine.Number[..^1];
            else if (draftField == 1 && draftLine.Name.Length > 0) draftLine.Name = draftLine.Name[..^1];
        }
        else if (sectionDialog)
        {
            if (draftField == 2)
            {
                var s = draftSection.Number.ToString();
                if (s.Length > 1 && int.TryParse(s[..^1], out var n)) draftSection.Number = n;
            }
            else if (draftField == 3) SetTrackName(0, TrimLast(GetTrackName(0)));
            else if (draftField == 4) SetTrackName(1, TrimLast(GetTrackName(1)));
        }
    }

    static string TrimLast(string value) => value.Length == 0 ? value : value[..^1];

    void SaveStationDialog()
    {
        if (string.IsNullOrWhiteSpace(draftStation.Code) || string.IsNullOrWhiteSpace(draftStation.Name))
        {
            status = "STATION NEEDS CODE AND NAME";
            return;
        }

        var existing = project.Stations.FirstOrDefault(x => x.Id == draftStation.Id);
        if (existing == null)
        {
            draftStation.Id = Guid.NewGuid();
            project.Stations.Add(draftStation);
            selectedStation = draftStation.Id;
            nextStation++;
            status = $"ADDED {draftStation.Code}";
        }
        else
        {
            var idx = project.Stations.IndexOf(existing);
            project.Stations[idx] = draftStation;
            selectedStation = draftStation.Id;
            status = $"SAVED {draftStation.Code}";
        }

        stationDialog = false;
        pendingNewStationId = null;
        editingText = false;
        mode = "SELECT";
    }

    void SaveLineDialog()
    {
        if (string.IsNullOrWhiteSpace(draftLine.Number) || string.IsNullOrWhiteSpace(draftLine.Name))
        {
            status = "LINE NEEDS NUMBER AND NAME";
            return;
        }

        var existing = project.Lines.FirstOrDefault(x => x.Id == draftLine.Id);
        if (existing == null)
        {
            draftLine.Id = Guid.NewGuid();
            project.Lines.Add(draftLine);
        }
        else
        {
            var idx = project.Lines.IndexOf(existing);
            project.Lines[idx] = draftLine;
        }

        selectedLine = draftLine.Id;
        selectedSection = null;
        lineDialog = false;
        editingText = false;
        mode = "LINE VIEW";
        status = $"SAVED {draftLine.Number}";
    }

    void SaveSectionDialog()
    {
        var line = project.Lines.FirstOrDefault(x => x.Id == sectionEditLineId);
        var a = project.Stations.FirstOrDefault(x => x.Id == draftSection.FromStationId);
        var b = project.Stations.FirstOrDefault(x => x.Id == draftSection.ToStationId);

        if (line == null || a == null || b == null || a.Id == b.Id)
        {
            status = "SECTION NEEDS TWO DIFFERENT STATIONS";
            return;
        }

        RefreshSectionGeometry();

        var existing = line.Sections.FirstOrDefault(x => x.Id == draftSection.Id);
        if (existing == null)
            line.Sections.Add(draftSection);
        else
            line.Sections[line.Sections.IndexOf(existing)] = draftSection;

        selectedLine = line.Id;
        selectedSection = draftSection.Id;
        sectionDialog = false;
        editingText = false;
        mode = "LINE VIEW";
        status = $"SAVED {line.Number}|{draftSection.Number}";
    }

    void CancelDataDialogs()
    {
        if (stationDialog && pendingNewStationId is Guid newId)
            project.Stations.RemoveAll(x => x.Id == newId);

        stationDialog = lineDialog = sectionDialog = false;
        pendingNewStationId = null;
        editingText = false;
        mode = "SELECT";
        status = "EDIT CANCELLED";
    }

    void OpenTrainDialog()
    {
        draftTrain = new TrainRun
        {
            Number = $"IC {nextTrain:000}",
            Name = "New run",
            CommercialLineId = project.CommercialLines.FirstOrDefault()?.Id
        };

        if (project.Stations.Count > 0)
        {
            draftTrain.Timetable.Add(new TimetableEntry
            {
                StationId = project.Stations[0].Id,
                Kind = StopKind.Origin,
                Arrival = "--:--",
                Departure = "--:--"
            });

            draftTrain.Timetable.Add(new TimetableEntry
            {
                StationId = project.Stations.Count > 1 ? project.Stations[1].Id : project.Stations[0].Id,
                Kind = StopKind.Destination,
                Arrival = "--:--",
                Departure = "--:--"
            });
        }

        draftRow = 0;
        draftField = 0;
        editingText = false;
        stationPicker = false;
        trainDialog = true;
        mode = "TRAIN EDITOR";
        status = "ADD TRAIN";
    }

    void OpenTrainDuplicate()
    {
        var t = project.Trains.FirstOrDefault(x => x.Id == selectedTrain);
        if (t == null) return;

        draftTrain = JsonSerializer.Deserialize<TrainRun>(
            JsonSerializer.Serialize(t, ProjectJson.Options), ProjectJson.Options)!;
        draftTrain.Id = Guid.NewGuid();
        draftTrain.Number = t.Number + "-COPY";
        draftRow = 0;
        draftField = 0;
        editingText = false;
        stationPicker = false;
        trainDialog = true;
        mode = "TRAIN EDITOR";
        status = "TRAIN EDITOR - DUPLICATE";
    }

    void UpdateTrainDialog(KeyboardState k, MouseState m, Func<Keys, bool> P)
    {
        if (stationPicker)
        {
            UpdateStationPicker(k, m, P);
            return;
        }

        if (editingText)
        {
            if (P(Keys.Escape) || P(Keys.Enter)) editingText = false;
            return;
        }

        if (P(Keys.Escape))
        {
            trainDialog = false;
            status = "TRAIN EDITOR CANCELLED";
            mode = "SELECT";
            return;
        }

        if (P(Keys.Enter))
        {
            SaveDraftTrain();
            return;
        }

        if (P(Keys.Tab))
        {
            draftField++;
            if (draftField >= 3 + draftTrain.Timetable.Count * 5) draftField = 0;
            if (draftField >= 3)
                draftRow = Math.Clamp((draftField - 3) / 5, 0, Math.Max(0, draftTrain.Timetable.Count - 1));
            return;
        }

        if (P(Keys.A) || P(Keys.Insert))
        {
            AddDraftRow();
            return;
        }

        if (P(Keys.Delete) && draftTrain.Timetable.Count > 0)
        {
            draftTrain.Timetable.RemoveAt(Math.Clamp(draftRow, 0, draftTrain.Timetable.Count - 1));
            draftRow = Math.Clamp(draftRow, 0, Math.Max(0, draftTrain.Timetable.Count - 1));
            NormalizeDraftKinds();
            return;
        }

        if (P(Keys.Up))
        {
            draftRow = Math.Max(0, draftRow - 1);
            return;
        }

        if (P(Keys.Down))
        {
            draftRow = Math.Min(Math.Max(0, draftTrain.Timetable.Count - 1), draftRow + 1);
            return;
        }

        if (P(Keys.Left)) { CycleTrainField(-1); return; }
        if (P(Keys.Right)) { CycleTrainField(1); return; }

        if (P(Keys.Space))
        {
            if (draftField == 0 || draftField == 1 || draftField >= 3)
            {
                var n = draftField >= 3 ? draftField - 3 : -1;
                var col = n >= 0 ? n % 5 : -1;
                editingText = draftField < 2 || col is 2 or 3;
            }
            return;
        }

        if (m.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            if (m.X >= 400 && m.X <= 1050 && m.Y >= 110 && m.Y < 165)
            {
                draftField = 0;
                editingText = true;
                return;
            }

            if (m.X >= 400 && m.X <= 1050 && m.Y >= 165 && m.Y < 220)
            {
                draftField = 1;
                editingText = true;
                return;
            }

            if (m.X >= 500 && m.X <= 1050 && m.Y >= 220 && m.Y < 270)
            {
                draftField = 2;
                CycleCommercialLine(1);
                return;
            }

            if (m.Y >= 325 && m.Y < 325 + draftTrain.Timetable.Count * 44)
            {
                var row = Math.Clamp((m.Y - 325) / 44, 0, draftTrain.Timetable.Count - 1);
                draftRow = row;

                if (m.X >= 390 && m.X < 545)
                {
                    stationPickerRow = row;
                    OpenStationPicker(row);
                }
                else if (m.X >= 545 && m.X < 635)
                {
                    draftField = 3 + row * 5;
                    CycleTrainField(1);
                }
                else if (m.X >= 635 && m.X < 720)
                {
                    draftField = 4 + row * 5;
                    editingText = true;
                }
                else if (m.X >= 720 && m.X < 805)
                {
                    draftField = 5 + row * 5;
                    editingText = true;
                }
                else if (m.X >= 805 && m.X < 900)
                {
                    draftField = 6 + row * 5;
                    CycleTrainField(1);
                }
                return;
            }

            if (m.X >= 390 && m.X <= 570 && m.Y >= 750 && m.Y < 800)
            {
                AddDraftRow();
                return;
            }

            if (m.X >= 580 && m.X <= 735 && m.Y >= 750 && m.Y < 800)
            {
                if (draftTrain.Timetable.Count > 0)
                {
                    draftTrain.Timetable.RemoveAt(Math.Clamp(draftRow, 0, draftTrain.Timetable.Count - 1));
                    draftRow = Math.Clamp(draftRow, 0, Math.Max(0, draftTrain.Timetable.Count - 1));
                    NormalizeDraftKinds();
                }
                return;
            }

            if (m.X >= 745 && m.X <= 900 && m.Y >= 750 && m.Y < 800)
            {
                SaveDraftTrain();
                return;
            }

            if (m.X >= 910 && m.X <= 1060 && m.Y >= 750 && m.Y < 800)
            {
                trainDialog = false;
                status = "TRAIN EDITOR CANCELLED";
                mode = "SELECT";
            }
        }
    }

    void OpenStationPicker(int row)
    {
        if (project.Stations.Count == 0)
        {
            status = "ADD STATIONS FIRST";
            return;
        }

        stationPicker = true;
        stationPickerIndex = Math.Max(0,
            project.Stations.FindIndex(x => x.Id == draftTrain.Timetable[row].StationId));
        status = "SELECT STATION";
    }

    void UpdateStationPicker(KeyboardState k, MouseState m, Func<Keys, bool> P)
    {
        if (P(Keys.Escape))
        {
            stationPicker = false;
            status = "TRAIN EDITOR";
            return;
        }

        if (P(Keys.Up))
            stationPickerIndex = Math.Max(0, stationPickerIndex - 1);
        if (P(Keys.Down))
            stationPickerIndex = Math.Min(project.Stations.Count - 1, stationPickerIndex + 1);

        if (P(Keys.Enter))
        {
            draftTrain.Timetable[stationPickerRow].StationId = project.Stations[stationPickerIndex].Id;
            stationPicker = false;
            status = "STATION SELECTED";
            return;
        }

        if (m.LeftButton == ButtonState.Pressed && oldMouse.LeftButton == ButtonState.Released)
        {
            if (m.X >= 590 && m.X <= 1035 && m.Y >= 225 && m.Y < 685)
            {
                var i = (m.Y - 225) / 44;
                if (i >= 0 && i < project.Stations.Count)
                {
                    stationPickerIndex = i;
                    draftTrain.Timetable[stationPickerRow].StationId = project.Stations[i].Id;
                    stationPicker = false;
                    status = "STATION SELECTED";
                }
            }
        }
    }

    string CommercialLineText()
    {
        if (draftTrain.CommercialLineId is not Guid id) return "NONE";
        var line = project.CommercialLines.FirstOrDefault(x => x.Id == id);
        return line == null ? "NONE" : $"{line.Code} {line.Name}";
    }

    void CycleCommercialLine(int dir)
    {
        var values = new List<Guid?> { null };
        values.AddRange(project.CommercialLines.Select(x => (Guid?)x.Id));
        var current = values.IndexOf(draftTrain.CommercialLineId);
        if (current < 0) current = 0;
        draftTrain.CommercialLineId = values[(current + dir + values.Count) % values.Count];
    }

    void CycleTrainField(int dir)
    {
        if (draftField == 2)
        {
            CycleCommercialLine(dir);
            return;
        }

        if (draftField < 3) return;

        var n = draftField - 3;
        var row = n / 5;
        var col = n % 5;
        if (row >= draftTrain.Timetable.Count) return;

        var e = draftTrain.Timetable[row];

        if (col == 0)
        {
            OpenStationPicker(row);
        }
        else if (col == 1)
        {
            e.Kind = (StopKind)(((int)e.Kind + dir + 4) % 4);
            NormalizeDraftKinds();
        }
        else if (col == 2 || col == 3)
        {
            editingText = true;
        }
        else if (col == 4)
        {
            var tracks = AllTracksForStation(e.StationId);
            if (tracks.Count == 0) { e.TrackId = null; return; }

            var i = e.TrackId.HasValue
                ? Math.Max(0, tracks.FindIndex(t => t.Id == e.TrackId.Value))
                : -1;
            i = (i + dir + tracks.Count + 1) % (tracks.Count + 1);
            e.TrackId = i < 0 ? null : tracks[i].Id;
        }
    }

    void AddDraftRow()
    {
        if (project.Stations.Count == 0)
        {
            status = "ADD STATIONS FIRST";
            return;
        }

        var stationIndex = Math.Min(
            project.Stations.Count - 1,
            Math.Max(0, draftTrain.Timetable.Count));

        draftTrain.Timetable.Add(new TimetableEntry
        {
            StationId = project.Stations[stationIndex].Id,
            Kind = StopKind.Stop,
            Arrival = "--:--",
            Departure = "--:--"
        });

        draftRow = draftTrain.Timetable.Count - 1;
        NormalizeDraftKinds();
        draftField = 3 + draftRow * 5;
    }

    void NormalizeDraftKinds()
    {
        for (var i = 0; i < draftTrain.Timetable.Count; i++)
        {
            if (i == 0) draftTrain.Timetable[i].Kind = StopKind.Origin;
            else if (i == draftTrain.Timetable.Count - 1) draftTrain.Timetable[i].Kind = StopKind.Destination;
            else if (draftTrain.Timetable[i].Kind is StopKind.Origin or StopKind.Destination)
                draftTrain.Timetable[i].Kind = StopKind.Stop;
        }
    }

    List<RailTrack> AllTracksForStation(Guid stationId)
    {
        var result = new List<RailTrack>();
        foreach (var line in project.Lines)
        foreach (var sec in line.Sections)
        if (sec.FromStationId == stationId || sec.ToStationId == stationId)
        foreach (var t in sec.Tracks)
        if (!result.Any(x => x.Id == t.Id))
            result.Add(t);
        return result;
    }

    void AppendTrainField(char c)
    {
        if (draftField == 0) draftTrain.Number += c;
        else if (draftField == 1) draftTrain.Name += c;
        else
        {
            var n = draftField - 3;
            if (n < 0) return;
            var row = n / 5;
            var col = n % 5;
            if (row >= draftTrain.Timetable.Count) return;
            if (col == 2) draftTrain.Timetable[row].Arrival += c;
            else if (col == 3) draftTrain.Timetable[row].Departure += c;
        }
    }

    void BackspaceTrainField()
    {
        if (draftField == 0 && draftTrain.Number.Length > 0)
            draftTrain.Number = draftTrain.Number[..^1];
        else if (draftField == 1 && draftTrain.Name.Length > 0)
            draftTrain.Name = draftTrain.Name[..^1];
        else if (draftField >= 3)
        {
            var n = draftField - 3;
            var row = n / 5;
            var col = n % 5;
            if (row >= draftTrain.Timetable.Count) return;
            if (col == 2 && draftTrain.Timetable[row].Arrival.Length > 0)
                draftTrain.Timetable[row].Arrival = draftTrain.Timetable[row].Arrival[..^1];
            if (col == 3 && draftTrain.Timetable[row].Departure.Length > 0)
                draftTrain.Timetable[row].Departure = draftTrain.Timetable[row].Departure[..^1];
        }
    }

    void SaveDraftTrain()
    {
        NormalizeDraftKinds();

        if (string.IsNullOrWhiteSpace(draftTrain.Number) || draftTrain.Timetable.Count < 2)
        {
            status = "TRAIN NEEDS NUMBER AND TWO TIMETABLE POINTS";
            return;
        }

        var existing = project.Trains.FirstOrDefault(t => t.Id == draftTrain.Id);
        if (existing == null)
        {
            draftTrain.Id = Guid.NewGuid();
            project.Trains.Add(draftTrain);
            nextTrain++;
        }
        else
        {
            var idx = project.Trains.IndexOf(existing);
            project.Trains[idx] = draftTrain;
        }

        selectedTrain = draftTrain.Id;
        trainDialog = false;
        stationPicker = false;
        mode = "SELECT";
        status = $"SAVED {draftTrain.Number}";
    }

    void ChangeStationTracks(int delta)
    {
        if (selectedStation is not Guid id)
        {
            status = "SELECT A STATION FIRST";
            return;
        }

        var s = project.Stations.FirstOrDefault(x => x.Id == id);
        if (s == null) return;

        var value = Math.Clamp(s.Tracks + delta, 1, 20);
        if (value == s.Tracks)
        {
            status = value == 20 ? "MAXIMUM TRACKS: 20" : "MINIMUM TRACKS: 1";
            return;
        }

        s.Tracks = value;
        status = $"{s.Code}: {s.Tracks} TRACKS";
    }

    void ClickSidebar(Vector2 p)
    {
        var y = 104;

        foreach (var line in project.Lines)
        {
            if (p.Y >= y && p.Y < y + 24)
            {
                selectedLine = line.Id;
                selectedSection = null;
                mode = "LINE VIEW";
                status = $"LINE {line.Number} SELECTED";
                return;
            }

            y += 26;

            if (selectedLine == line.Id)
            {
                var route = LineRoute(line);
                if (route.Count > 0) y += 30;

                foreach (var sec in line.Sections)
                {
                    if (p.Y >= y && p.Y < y + 34)
                    {
                        selectedLine = line.Id;
                        selectedSection = sec.Id;
                        mode = "LINE VIEW";
                        status = $"SELECTED {line.Number}|{sec.Number}";
                        return;
                    }
                    y += 36;
                }
            }

            y += 8;
        }

        if (p.Y >= 820 && p.Y < 850)
        {
            OpenLineEditor(null);
            return;
        }

        if (p.Y >= 850 && p.Y < 880 && selectedLine.HasValue)
        {
            OpenSectionEditor(null, true);
            return;
        }

        if (p.Y >= 880 && selectedLine.HasValue)
        {
            if (selectedSection.HasValue) OpenSectionEditor();
            else OpenLineEditor(selectedLine);
        }
    }

    void Click(Vector2 screen)
    {
        var p = ScreenToMap(screen);
        var s = project.Stations.FirstOrDefault(
            x => Vector2.Distance(new(x.X, x.Y), p) < 18 / zoom);

        if (mode == "STATION" && s == null)
        {
            var ns = new Station
            {
                Id = Guid.NewGuid(),
                Name = $"Station {nextStation}",
                Code = $"N{nextStation:00}",
                X = (int)p.X,
                Y = (int)p.Y,
                Tracks = 2,
                Type = StationType.Local
            };
            nextStation++;
            project.Stations.Add(ns);
            selectedStation = ns.Id;
            draftStation = JsonSerializer.Deserialize<Station>(
                JsonSerializer.Serialize(ns, ProjectJson.Options), ProjectJson.Options)!;
            pendingNewStationId = ns.Id;
            stationDialog = true;
            editingText = false;
            draftField = 1;
            mode = "STATION EDIT";
            status = "ENTER STATION DETAILS";
            return;
        }

        if (s == null)
        {
            selectedStation = null;
            selectedTrain = null;
            return;
        }

        selectedStation = s.Id;

        if (mode == "SECTION")
        {
            if (pendingFrom == null)
            {
                pendingFrom = s.Id;
                status = $"FROM {s.Code}: SELECT DESTINATION";
            }
            else if (pendingFrom != s.Id)
            {
                AddSection(pendingFrom.Value, s.Id);
                pendingFrom = null;
                mode = "SELECT";
            }
        }
    }

    void AddSection(Guid a, Guid b)
    {
        var line = project.Lines.FirstOrDefault();
        if (line == null)
        {
            line = new RailwayLine { Number = "LK001", Name = "Wroclaw - Klodzko" };
            project.Lines.Add(line);
        }

        if (line.Sections.Any(x =>
            (x.FromStationId == a && x.ToStationId == b) ||
            (x.FromStationId == b && x.ToStationId == a)))
        {
            status = "SECTION EXISTS";
            return;
        }

        var A = project.Stations.First(x => x.Id == a);
        var B = project.Stations.First(x => x.Id == b);

        var sec = new RailSection
        {
            Number = line.Sections.Count + 1,
            FromStationId = a,
            ToStationId = b,
            Geometry = new() { new(A.X, A.Y), new(B.X, B.Y) }
        };
        sec.Tracks.Add(new RailTrack { Name = "1", Direction = TrackDirection.Forward });
        sec.Tracks.Add(new RailTrack { Name = "2", Direction = TrackDirection.Reverse });
        line.Sections.Add(sec);

        status = $"ADDED {line.Number}|{sec.Number} {A.Code} -> {B.Code}";
        selectedLine = line.Id;
        selectedSection = sec.Id;
    }

    void Delete()
    {
        if (selectedSection is Guid sectionId && selectedLine is Guid lineId)
        {
            var line = project.Lines.FirstOrDefault(x => x.Id == lineId);
            if (line != null)
            {
                line.Sections.RemoveAll(x => x.Id == sectionId);
                selectedSection = null;
                status = "SECTION DELETED";
                return;
            }
        }

        if (selectedStation is Guid stationId)
        {
            project.Stations.RemoveAll(x => x.Id == stationId);
            foreach (var line in project.Lines)
                line.Sections.RemoveAll(x => x.FromStationId == stationId || x.ToStationId == stationId);
            selectedStation = null;
            status = "STATION DELETED";
        }
        else if (selectedLine is Guid lineId2)
        {
            project.Lines.RemoveAll(x => x.Id == lineId2);
            selectedLine = null;
            selectedSection = null;
            status = "LINE DELETED";
        }
        else if (selectedTrain is Guid trainId)
        {
            project.Trains.RemoveAll(x => x.Id == trainId);
            selectedTrain = null;
            status = "TRAIN DELETED";
        }
    }

    void Save()
    {
        File.WriteAllText("project.railplanner",
            JsonSerializer.Serialize(project, ProjectJson.Options));
        status = "SAVED project.railplanner";
    }

    void LoadProject()
    {
        if (!File.Exists("project.railplanner"))
        {
            status = "NO PROJECT FILE";
            return;
        }

        project = JsonSerializer.Deserialize<RailProject>(
            File.ReadAllText("project.railplanner"), ProjectJson.Options) ?? new();

        selectedStation = selectedTrain = selectedLine = selectedSection = null;
        status = "LOADED project.railplanner";
    }

    Vector2 ScreenToMap(Vector2 p) =>
        (p - new Vector2(280, 54) - camera) / zoom;

    Vector2 MapToScreen(Vector2 p) =>
        p * zoom + new Vector2(280, 54) + camera;

    protected override void Draw(GameTime t)
    {
        GraphicsDevice.Clear(new Color(9, 12, 16));
        batch.Begin(samplerState: SamplerState.PointClamp);

        Rect(new(0, 0, 1440, 54), new(18, 22, 28));
        Text("RAILPLANNER", new(18, 15), Color.White, 1);
        Text("F1 SELECT  F2 STATION  F3 SECTION  F4 TRAIN  F5 SAVE  F6 LOAD  F7 ANALYZE  F8 LINES  E EDIT  L ADD LINE",
            new(250, 18), Color.LightGray, .52f);

        DrawMap();
        Sidebar();

        if (stationDialog) DrawStationDialog();
        if (lineDialog) DrawLineDialog();
        if (sectionDialog) DrawSectionDialog();
        if (trainDialog) DrawTrainDialog();

        batch.End();
        base.Draw(t);
    }

    void DrawMap()
    {
        Rect(new(0, 54, 1440, 846), new(11, 15, 20));

        for (int x = -1000; x < 2000; x += 50)
        {
            var p = MapToScreen(new(x, 0));
            Rect(new((int)p.X, 54, 1, 846), new(25, 31, 38));
        }

        for (int y = -1000; y < 2000; y += 50)
        {
            var p = MapToScreen(new(0, y));
            Rect(new(280, (int)p.Y, 1160, 1), new(25, 31, 38));
        }

        foreach (var l in project.Lines)
        foreach (var s in l.Sections)
        {
            var q = s.Geometry.Select(x => MapToScreen(new(x.X, x.Y))).ToList();
            var selected = selectedSection == s.Id;

            for (int i = 0; i < q.Count - 1; i++)
                Line(q[i], q[i + 1],
                    selected ? Color.White : new Color(115, 125, 135),
                    selected ? 4 : 2);

            if (q.Count > 1)
                Text($"{l.Number}|{s.Number}",
                    (q[0] + q[^1]) / 2 - new Vector2(0, 16),
                    selected ? Color.White : new Color(130, 145, 155),
                    selected ? .7f : .55f);
        }

        foreach (var s in project.Stations)
        {
            var p = MapToScreen(new(s.X, s.Y));
            bool sel = selectedStation == s.Id;
            Rect(new((int)p.X - 4, (int)p.Y - 4, sel ? 12 : 8, sel ? 12 : 8),
                sel ? Color.White : new Color(220, 220, 220));
            Text(s.Code, p + new Vector2(8, -8), Color.LightGray, .65f);
        }
    }

    void Sidebar()
    {
        Rect(new(0, 54, 280, 846), new(16, 20, 25));
        Text("LINES", new(18, 74), Color.White, .82f);

        var y = 104;

        foreach (var line in project.Lines)
        {
            var active = selectedLine == line.Id;
            Text($"{line.Number}  {line.Name}", new(18, y),
                active ? Color.White : Color.LightGray, .7f);
            y += 26;

            if (active)
            {
                var route = LineRoute(line);
                if (route.Count > 0)
                {
                    Text(string.Join(" - ", route.Select(x => x.Code)),
                        new(28, y), new Color(130, 145, 155), .52f);
                    y += 30;
                }

                foreach (var sec in line.Sections)
                {
                    var secActive = selectedSection == sec.Id;
                    var from = project.Stations.FirstOrDefault(x => x.Id == sec.FromStationId)?.Code ?? "?";
                    var to = project.Stations.FirstOrDefault(x => x.Id == sec.ToStationId)?.Code ?? "?";
                    Text($"{line.Number}|{sec.Number}", new(28, y),
                        secActive ? Color.White : new Color(150, 165, 175), .62f);
                    Text($"{from} - {to}", new(105, y),
                        secActive ? Color.White : new Color(115, 130, 140), .55f);
                    y += 36;
                }
            }

            y += 8;
        }

        var ny = Math.Max(y + 20, 600);
        Text("NETWORK", new(18, ny), Color.White, .72f);
        Text($"STATIONS {project.Stations.Count}", new(18, ny + 30), Color.LightGray, .65f);
        Text($"LINES    {project.Lines.Count}", new(18, ny + 52), Color.LightGray, .65f);
        Text($"TRAINS   {project.Trains.Count}", new(18, ny + 74), Color.LightGray, .65f);
        Text("MODE " + mode, new(18, ny + 106), Color.White, .65f);
        Text(status, new(18, ny + 132),
            status.Contains("CONFLICT") ? Color.OrangeRed : new Color(150, 190, 205), .5f);

        Text("ANALYSIS", new(18, ny + 172), Color.White, .72f);
        Text($"CONFLICTS {analysis.Conflicts.Count}", new(18, ny + 202),
            analysis.Conflicts.Count > 0 ? Color.OrangeRed : Color.LightGray, .65f);
        Text($"WARNINGS  {analysis.Warnings.Count}", new(18, ny + 224),
            analysis.Warnings.Count > 0 ? Color.Gold : Color.LightGray, .65f);

        if (selectedStation is Guid id)
        {
            var s = project.Stations.FirstOrDefault(x => x.Id == id);
            if (s != null)
            {
                Text(s.Name, new(18, ny + 264), Color.White, .72f);
                Text($"CODE {s.Code}", new(18, ny + 292), Color.LightGray, .6f);
                Text($"TRACKS {s.Tracks}", new(18, ny + 314), Color.LightGray, .6f);
                Text($"TYPE {s.Type}", new(18, ny + 336), Color.LightGray, .6f);
            }
        }

        if (selectedTrain is Guid tid)
        {
            var tr = project.Trains.FirstOrDefault(x => x.Id == tid);
            if (tr != null)
            {
                Text(tr.Number, new(18, ny + 264), Color.White, .72f);
                Text(tr.Name, new(18, ny + 292), Color.LightGray, .6f);
                Text("D = DUPLICATE / EDIT", new(18, ny + 320), Color.LightGray, .6f);
            }
        }

        var listY = Math.Max(560, Math.Min(ny + 260, 700));
        Text("TRAIN LIST", new(18, listY), Color.White, .62f);
        var trainDrawY = listY + 24;
        foreach (var train in project.Trains.Take(6))
        {
            Text(train.Number, new(18, trainDrawY),
                selectedTrain == train.Id ? Color.White : Color.LightGray, .52f);
            trainDrawY += 20;
        }

        Rect(new(12, 820, 256, 24), new(25, 34, 43));
        Rect(new(12, 848, 256, 24), new(25, 34, 43));
        Rect(new(12, 876, 256, 18), new(25, 34, 43));
        Text("+ ADD LINE  [L]", new(22, 824), Color.White, .55f);
        Text("+ ADD SEGMENT [N]", new(22, 852), Color.White, .52f);
        Text("E EDIT   DELETE REMOVE", new(22, 879), Color.LightGray, .48f);
    }

    void DrawStationDialog()
    {
        Modal("ADD / EDIT STATION", 350, 110, 760, 660);
        Text("Station details", new(390, 150), Color.White, .75f);
        Field("CODE", draftStation.Code, 390, 190, draftField == 0, editingText && draftField == 0);
        Field("NAME", draftStation.Name, 390, 245, draftField == 1, editingText && draftField == 1);
        Field("TRACKS", draftStation.Tracks.ToString(), 390, 300, draftField == 2, false);
        Field("TYPE", draftStation.Type.ToString().ToUpper(), 390, 355, draftField == 3, false);
        Text("Left/Right changes TRACKS and TYPE.", new(390, 420), Color.Gray, .52f);
        Text("Space / click edits CODE and NAME.", new(390, 442), Color.Gray, .52f);
        Button("[ SAVE ]", 500, 700, 260, 50);
        Button("[ CANCEL ]", 780, 700, 260, 50);
    }

    void DrawLineDialog()
    {
        Modal("ADD / EDIT RAILWAY LINE", 350, 110, 760, 660);
        Text("Infrastructure line", new(390, 160), Color.White, .75f);
        Field("NUMBER", draftLine.Number, 390, 205, draftField == 0, editingText && draftField == 0);
        Field("NAME", draftLine.Name, 390, 260, draftField == 1, editingText && draftField == 1);
        Text("A line is the parent of its railway sections.", new(390, 340), Color.Gray, .55f);
        Text("Segments are edited separately and keep NUMBER|SEGMENT.", new(390, 364), Color.Gray, .55f);
        Button("[ SAVE ]", 500, 700, 260, 50);
        Button("[ CANCEL ]", 780, 700, 260, 50);
    }

    void DrawSectionDialog()
    {
        Modal("ADD / EDIT RAILWAY SECTION", 350, 110, 760, 660);
        Text("Concrete infrastructure segment", new(390, 155), Color.White, .72f);
        var from = project.Stations.FirstOrDefault(x => x.Id == draftSection.FromStationId)?.Code ?? "?";
        var to = project.Stations.FirstOrDefault(x => x.Id == draftSection.ToStationId)?.Code ?? "?";
        Field("FROM", from, 390, 195, draftField == 0, false);
        Field("TO", to, 390, 250, draftField == 1, false);
        Field("NUMBER", draftSection.Number.ToString(), 390, 305, draftField == 2, editingText && draftField == 2);
        Field("FORWARD TRACK", GetTrackName(0), 390, 360, draftField == 3, editingText && draftField == 3);
        Field("REVERSE TRACK", GetTrackName(1), 390, 415, draftField == 4, editingText && draftField == 4);
        Text("Click FROM/TO to cycle stations. Track names are editable.", new(390, 480), Color.Gray, .52f);
        Button("[ SAVE ]", 500, 700, 260, 50);
        Button("[ CANCEL ]", 780, 700, 260, 50);
    }

    void DrawTrainDialog()
    {
        Modal("ADD / EDIT TRAIN", 350, 70, 760, 770);

        Field("NUMBER", draftTrain.Number, 390, 125, draftField == 0, editingText && draftField == 0);
        Field("NAME", draftTrain.Name, 390, 180, draftField == 1, editingText && draftField == 1);

        Text("COMMERCIAL LINE", new(390, 235), Color.Gray, .5f);
        Text(CommercialLineText(), new(545, 235), draftField == 2 ? Color.White : Color.LightGray, .58f);
        Text("Left/Right changes commercial line.", new(390, 258), Color.Gray, .46f);
        Text("TIMETABLE", new(390, 282), Color.White, .72f);
        Text("#", new(390, 307), Color.Gray, .5f);
        Text("STATION", new(420, 307), Color.Gray, .5f);
        Text("TYPE", new(545, 307), Color.Gray, .5f);
        Text("ARR", new(635, 307), Color.Gray, .5f);
        Text("DEP", new(720, 307), Color.Gray, .5f);
        Text("TRACK", new(805, 307), Color.Gray, .5f);

        for (var i = 0; i < draftTrain.Timetable.Count; i++)
        {
            var e = draftTrain.Timetable[i];
            var y = 329 + i * 44;
            var active = i == draftRow;
            var st = project.Stations.FirstOrDefault(s => s.Id == e.StationId);
            var c = active ? Color.White : Color.LightGray;

            Rect(new(385, y - 4, 680, 38), active ? new Color(35, 45, 55) : new Color(27, 34, 42));
            Text((i + 1).ToString(), new(390, y), Color.Gray, .5f);
            Text(st?.Code ?? "?", new(420, y), c, .58f);
            Text(e.Kind.ToString().ToUpper(), new(545, y), c, .48f);
            Text(e.Arrival + (active && draftField == 4 + i * 5 && editingText ? "_" : ""), new(635, y), c, .52f);
            Text(e.Departure + (active && draftField == 5 + i * 5 && editingText ? "_" : ""), new(720, y), c, .52f);
            Text(TrackText(e), new(805, y), c, .5f);
        }

        Text("Click STATION to choose from all stations.", new(390, 705), Color.Gray, .5f);
        Text("A / INSERT adds point. DELETE removes point. PASS is supported.", new(390, 727), Color.Gray, .5f);

        Button("[ + ADD STATION ]", 390, 750, 180, 50);
        Button("[ REMOVE ]", 580, 750, 155, 50);
        Button("[ SAVE ]", 745, 750, 155, 50);
        Button("[ CANCEL ]", 910, 750, 155, 50);

        if (stationPicker) DrawStationPicker();
    }

    void DrawStationPicker()
    {
        Rect(new(570, 150, 500, 550), new(4, 7, 10));
        Rect(new(585, 165, 470, 520), new(24, 31, 39));
        Text("SELECT STATION", new(610, 185), Color.White, .78f);
        Text("Enter selects, Esc closes", new(610, 208), Color.Gray, .5f);

        for (var i = 0; i < project.Stations.Count && i < 10; i++)
        {
            var s = project.Stations[i];
            var y = 235 + i * 44;
            if (i == stationPickerIndex) Rect(new(600, y - 5, 430, 36), new Color(48, 60, 72));
            Text($"{s.Code}  {s.Name}", new(615, y), i == stationPickerIndex ? Color.White : Color.LightGray, .55f);
        }
    }

    void Modal(string title, int x, int y, int w, int h)
    {
        Rect(new(x - 8, y - 8, w + 16, h + 16), new(3, 5, 8));
        Rect(new(x, y, w, h), new(22, 28, 36));
        Text(title, new(x + 40, y + 24), Color.White, .95f);
    }

    void Field(string label, string value, int x, int y, bool active, bool edit)
    {
        Text(label, new(x, y), Color.Gray, .52f);
        Text(value + (edit ? "_" : ""), new(x + 130, y),
            active ? Color.White : Color.LightGray, .62f);
    }

    void Button(string label, int x, int y, int w, int h)
    {
        Rect(new(x, y, w, h), new Color(31, 41, 50));
        Text(label, new(x + 12, y + 16), Color.White, .58f);
    }

    string TrackText(TimetableEntry e)
    {
        if (e.TrackId is not Guid id) return "AUTO";
        foreach (var l in project.Lines)
        foreach (var s in l.Sections)
        {
            var t = s.Tracks.FirstOrDefault(x => x.Id == id);
            if (t != null) return t.Name;
        }
        return "AUTO";
    }

    List<Station> LineRoute(RailwayLine line)
    {
        var result = new List<Station>();

        foreach (var sec in line.Sections)
        {
            var a = project.Stations.FirstOrDefault(x => x.Id == sec.FromStationId);
            var b = project.Stations.FirstOrDefault(x => x.Id == sec.ToStationId);
            if (a == null || b == null) continue;

            if (result.Count == 0) result.Add(a);
            else if (result[^1].Id != a.Id) result.Add(a);

            if (result[^1].Id != b.Id) result.Add(b);
        }

        return result;
    }

    void Text(string s, Vector2 p, Color c, float z) =>
        batch.DrawString(font, s, p, c, 0, Vector2.Zero, z, SpriteEffects.None, 0);

    void Rect(Rectangle r, Color c) => batch.Draw(pixel, r, c);

    void Line(Vector2 a, Vector2 b, Color c, int w)
    {
        var d = b - a;
        batch.Draw(pixel, a, null, c, MathF.Atan2(d.Y, d.X),
            Vector2.Zero, new Vector2(d.Length(), w), SpriteEffects.None, 0);
    }
}
