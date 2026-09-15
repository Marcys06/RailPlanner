using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RailPlanner;

public sealed class MainGame : Game
{
    readonly GraphicsDeviceManager graphics; SpriteBatch batch = null!; SpriteFont font = null!; Texture2D pixel = null!;
    RailProject project = new(); AnalysisResult analysis = new(); string mode = "SELECT", status = "READY";
    Guid? selectedStation, selectedTrain, pendingFrom; Vector2 camera = new(80,70); float zoom=.7f; MouseState oldMouse; KeyboardState oldKeys; int nextStation=1,nextTrain=1;
    public MainGame(){graphics=new GraphicsDeviceManager(this){PreferredBackBufferWidth=1440,PreferredBackBufferHeight=900};Content.RootDirectory="Content";IsMouseVisible=true;Window.Title="RailPlanner";}
    protected override void LoadContent(){batch=new SpriteBatch(GraphicsDevice);font=Content.Load<SpriteFont>("DefaultFont");pixel=new Texture2D(GraphicsDevice,1,1);pixel.SetData(new[]{Color.White});}
    protected override void Update(GameTime time)
    {
        var k=Keyboard.GetState();var m=Mouse.GetState();bool P(Keys x)=>k.IsKeyDown(x)&&!oldKeys.IsKeyDown(x);
        if(P(Keys.F1))mode="SELECT";if(P(Keys.F2)){mode="STATION";status="CLICK MAP TO ADD STATION";}if(P(Keys.F3)){mode="SECTION";pendingFrom=null;status="CLICK TWO STATIONS";}if(P(Keys.F4)){mode="TRAIN";status="CLICK A STATION TO CREATE A RUN";}
        if(P(Keys.F5))Save();if(P(Keys.F6))LoadProject();if(P(Keys.F7)){analysis=TimetableAnalyzer.Analyze(project);status=$"ANALYSIS: {analysis.Conflicts.Count} CONFLICTS / {analysis.Warnings.Count} WARNINGS";}
        if(P(Keys.D)&&selectedTrain.HasValue)Duplicate();if(P(Keys.Delete))Delete();if(P(Keys.Escape)){mode="SELECT";pendingFrom=null;}if(P(Keys.OemPlus)||P(Keys.Add))zoom=MathHelper.Clamp(zoom*1.1f,.25f,3f);if(P(Keys.OemMinus)||P(Keys.Subtract))zoom=MathHelper.Clamp(zoom/1.1f,.25f,3f);
        camera+=new Vector2((k.IsKeyDown(Keys.Left)?5:0)-(k.IsKeyDown(Keys.Right)?5:0),(k.IsKeyDown(Keys.Up)?5:0)-(k.IsKeyDown(Keys.Down)?5:0));
        if(m.LeftButton==ButtonState.Pressed&&oldMouse.LeftButton==ButtonState.Released&&m.X>=280&&m.Y>=54)Click(new Vector2(m.X,m.Y));oldKeys=k;oldMouse=m;base.Update(time);
    }
    void Click(Vector2 screen)
    {
        var p=ScreenToMap(screen);var s=project.Stations.FirstOrDefault(x=>Vector2.Distance(new(x.X,x.Y),p)<18/zoom);
        if(mode=="STATION"&&s==null){s=new Station{Name=$"Station {nextStation}",Code=$"N{nextStation:00}",X=(int)p.X,Y=(int)p.Y};nextStation++;project.Stations.Add(s);selectedStation=s.Id;mode="SELECT";status=$"ADDED {s.Code}";return;}
        if(s==null){selectedStation=null;selectedTrain=null;return;}selectedStation=s.Id;
        if(mode=="SECTION"){if(pendingFrom==null){pendingFrom=s.Id;status=$"FROM {s.Code}: SELECT DESTINATION";}else if(pendingFrom!=s.Id){AddSection(pendingFrom.Value,s.Id);pendingFrom=null;mode="SELECT";}}else if(mode=="TRAIN"){AddTrain(s.Id);mode="SELECT";}
    }
    void AddSection(Guid a,Guid b){var line=project.Lines.FirstOrDefault()??new RailwayLine{Number="LK001",Name="Railway line"};if(!project.Lines.Contains(line))project.Lines.Add(line);if(line.Sections.Any(x=>(x.FromStationId==a&&x.ToStationId==b)||(x.FromStationId==b&&x.ToStationId==a))){status="SECTION EXISTS";return;}var A=project.Stations.First(x=>x.Id==a);var B=project.Stations.First(x=>x.Id==b);var sec=new RailSection{FromStationId=a,ToStationId=b,Geometry=new(){new(A.X,A.Y),new(B.X,B.Y)}};sec.Tracks.Add(new RailTrack{Name="1",Direction=TrackDirection.Forward});sec.Tracks.Add(new RailTrack{Name="2",Direction=TrackDirection.Reverse});line.Sections.Add(sec);status=$"ADDED {line.Number} {A.Code} -> {B.Code}";}
    void AddTrain(Guid station){var other=project.Stations.FirstOrDefault(x=>x.Id!=station);if(other==null){status="ADD TWO STATIONS FIRST";return;}var t=new TrainRun{Number=$"IC {nextTrain++:000}",Name="New run"};t.Timetable.Add(new TimetableEntry{StationId=station,Kind=StopKind.Origin,Departure="08:00"});t.Timetable.Add(new TimetableEntry{StationId=other.Id,Kind=StopKind.Destination,Arrival="08:30"});project.Trains.Add(t);selectedTrain=t.Id;status=$"ADDED {t.Number} - D DUPLICATES";}
    void Duplicate(){var t=project.Trains.FirstOrDefault(x=>x.Id==selectedTrain);if(t==null)return;var c=JsonSerializer.Deserialize<TrainRun>(JsonSerializer.Serialize(t,ProjectJson.Options),ProjectJson.Options)!;c.Id=Guid.NewGuid();c.Number=t.Number+"-COPY";project.Trains.Add(c);selectedTrain=c.Id;status="TRAIN DUPLICATED";}
    void Delete(){if(selectedStation is Guid s){project.Stations.RemoveAll(x=>x.Id==s);selectedStation=null;status="STATION DELETED";}else if(selectedTrain is Guid t){project.Trains.RemoveAll(x=>x.Id==t);selectedTrain=null;status="TRAIN DELETED";}}
    void Save(){File.WriteAllText("project.railplanner",JsonSerializer.Serialize(project,ProjectJson.Options));status="SAVED project.railplanner";}void LoadProject(){if(!File.Exists("project.railplanner")){status="NO PROJECT FILE";return;}project=JsonSerializer.Deserialize<RailProject>(File.ReadAllText("project.railplanner"),ProjectJson.Options)??new();status="LOADED project.railplanner";}
    Vector2 ScreenToMap(Vector2 p)=>(p-new Vector2(280,54)-camera)/zoom;Vector2 MapToScreen(Vector2 p)=>p*zoom+new Vector2(280,54)+camera;
    protected override void Draw(GameTime t){GraphicsDevice.Clear(new Color(9,12,16));batch.Begin(samplerState:SamplerState.PointClamp);Rect(new(0,0,1440,54),new(18,22,28));Text("RAILPLANNER",new(18,15),Color.White,1);Text("F1 SELECT  F2 STATION  F3 SECTION  F4 TRAIN  F5 SAVE  F6 LOAD  F7 ANALYZE",new(250,18),Color.LightGray,.7f);DrawMap();Sidebar();batch.End();base.Draw(t);}
    void DrawMap(){Rect(new(0,54,1440,846),new(11,15,20));for(int x=-1000;x<2000;x+=50){var p=MapToScreen(new(x,0));Rect(new((int)p.X,54,1,846),new(25,31,38));}for(int y=-1000;y<2000;y+=50){var p=MapToScreen(new(0,y));Rect(new(280,(int)p.Y,1160,1),new(25,31,38));}foreach(var l in project.Lines)foreach(var s in l.Sections){var q=s.Geometry.Select(x=>MapToScreen(new(x.X,x.Y))).ToList();for(int i=0;i<q.Count-1;i++)Line(q[i],q[i+1],new(115,125,135),2);if(q.Count>1)Text(l.Number,(q[0]+q[^1])/2-new Vector2(0,16),new(130,145,155),.6f);}foreach(var s in project.Stations){var p=MapToScreen(new(s.X,s.Y));bool sel=selectedStation==s.Id;Rect(new((int)p.X-4,(int)p.Y-4,sel?12:8,sel?12:8),sel?Color.White:new(220,220,220));Text(s.Code,p+new Vector2(8,-8),Color.LightGray,.65f);}}
    void Sidebar(){Rect(new(0,54,280,846),new(16,20,25));Text("NETWORK",new(18,74),Color.White,.82f);Text($"STATIONS {project.Stations.Count}",new(18,105),Color.LightGray,.7f);Text($"LINES    {project.Lines.Count}",new(18,128),Color.LightGray,.7f);Text($"TRAINS   {project.Trains.Count}",new(18,151),Color.LightGray,.7f);Text("MODE "+mode,new(18,190),Color.White,.7f);Text(status,new(18,218),status.Contains("CONFLICT")?Color.OrangeRed:new(150,190,205),.62f);Text("ANALYSIS",new(18,265),Color.White,.82f);Text($"CONFLICTS {analysis.Conflicts.Count}",new(18,295),analysis.Conflicts.Count>0?Color.OrangeRed:Color.LightGray,.7f);Text($"WARNINGS  {analysis.Warnings.Count}",new(18,318),analysis.Warnings.Count>0?Color.Gold:Color.LightGray,.7f);if(selectedStation is Guid id){var s=project.Stations.FirstOrDefault(x=>x.Id==id);if(s!=null){Text(s.Name,new(18,365),Color.White,.8f);Text($"CODE {s.Code}",new(18,394),Color.LightGray,.68f);Text($"TRACKS {s.Tracks}",new(18,416),Color.LightGray,.68f);Text($"TYPE {s.Type}",new(18,438),Color.LightGray,.68f);}}if(selectedTrain is Guid tid){var tr=project.Trains.FirstOrDefault(x=>x.Id==tid);if(tr!=null){Text(tr.Number,new(18,365),Color.White,.82f);Text(tr.Name,new(18,395),Color.LightGray,.68f);Text("D = DUPLICATE",new(18,425),Color.LightGray,.68f);}}Text("DELETE REMOVE   ESC CANCEL",new(18,870),Color.Gray,.6f);}
    void Text(string s,Vector2 p,Color c,float z)=>batch.DrawString(font,s,p,c,0,Vector2.Zero,z,SpriteEffects.None,0);void Rect(Rectangle r,Color c)=>batch.Draw(pixel,r,c);void Line(Vector2 a,Vector2 b,Color c,int w){var d=b-a;batch.Draw(pixel,a,null,c,MathF.Atan2(d.Y,d.X),Vector2.Zero,new Vector2(d.Length(),w),SpriteEffects.None,0);}
}
