using System.Reflection;
using System; using System.Diagnostics; using System.Drawing; using System.Drawing.Drawing2D;
using System.Runtime.InteropServices; using System.Windows.Forms; using System.IO; using System.Collections.Generic;

[assembly: AssemblyTitle("Station Keeper")]
[assembly: AssemblyProduct("Station Keeper")]
[assembly: AssemblyDescription("Keeps you from idling out by nudging the mouse only while Star Citizen is in focus.")]
[assembly: AssemblyCopyright("Copyright (c) 2026 FrittenFritz")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class N {
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct IN { public uint type; public MI mi; public uint p1, p2; }
  [StructLayout(LayoutKind.Sequential)] public struct LII { public uint cbSize; public uint dwTime; }
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, IN[] i, int s);
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII p);
  public static void Move(int dx){ IN[] a=new IN[1]; a[0].type=0; a[0].mi.dx=dx; a[0].mi.flags=1; SendInput(1,a,Marshal.SizeOf(typeof(IN))); }
  public static string Fg(){ uint p; GetWindowThreadProcessId(GetForegroundWindow(), out p); if(p==0) return null;
    try { return Process.GetProcessById((int)p).ProcessName; } catch { return null; } }
  // Milliseconds since the last keyboard/mouse input anywhere in the session (32-bit tick math, wrap-safe).
  public static long IdleMs(){ LII l=new LII(); l.cbSize=(uint)Marshal.SizeOf(typeof(LII)); if(!GetLastInputInfo(ref l)) return 0; return (long)unchecked((uint)Environment.TickCount-l.dwTime); }
}

// Short warning beep: three 880 Hz pulses, 16-bit mono PCM WAV generated in memory and scaled by volume.
static class Beep {
  public static void Play(int volPct){ if(volPct<=0) return;
    try { System.Media.SoundPlayer sp=new System.Media.SoundPlayer(new MemoryStream(Wav(volPct/100.0))); sp.Play(); } catch {} }
  static byte[] Wav(double vol){ int rate=22050; double[] seg={0.12,0.08,0.12,0.08,0.18}; int total=0; foreach(double d in seg) total+=(int)(d*rate);
    short[] s=new short[total]; int i=0; double amp=vol*vol*30000; // squared curve: slider feels more natural
    for(int k=0;k<seg.Length;k++){ int n=(int)(seg[k]*rate); for(int j=0;j<n;j++,i++){ if(k%2==1){ s[i]=0; continue; }
        double env=Math.Min(1.0,Math.Min(j,n-j)/(rate*0.008)); s[i]=(short)(amp*env*Math.Sin(2*Math.PI*880*j/rate)); } }
    MemoryStream ms=new MemoryStream(); BinaryWriter w=new BinaryWriter(ms); int bytes=total*2;
    w.Write(new char[]{'R','I','F','F'}); w.Write(36+bytes); w.Write(new char[]{'W','A','V','E','f','m','t',' '}); w.Write(16); w.Write((short)1); w.Write((short)1);
    w.Write(rate); w.Write(rate*2); w.Write((short)2); w.Write((short)16); w.Write(new char[]{'d','a','t','a'}); w.Write(bytes); foreach(short v in s) w.Write(v); w.Flush(); return ms.ToArray(); }
}

static class Cfg {
  static string Ini { get { return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"StationKeeper"),"settings.ini"); } }
  public static bool Top=false; public static int Volume=60, AfkTimeout=900, AfkGrace=60;
  public static void Load(){ try { foreach(string raw in File.ReadAllLines(Ini)){ string l=raw.Trim(); int e=l.IndexOf('='); if(e<1) continue; string k=l.Substring(0,e).Trim(), v=l.Substring(e+1).Trim(); int n;
      if(!int.TryParse(v,out n)) continue;
      if(k=="AlwaysOnTop") Top=n==1; else if(k=="WarningVolume") Volume=Math.Max(0,Math.Min(100,n));
      else if(k=="AfkTimeoutSeconds") AfkTimeout=Math.Max(120,n); else if(k=="AfkGraceSeconds") AfkGrace=Math.Max(0,n); } } catch {}
    if(AfkGrace>AfkTimeout-60) AfkGrace=Math.Max(0,AfkTimeout-60); }
  public static void Save(){ try { Directory.CreateDirectory(Path.GetDirectoryName(Ini));
      File.WriteAllText(Ini,"[StationKeeper]\r\nAlwaysOnTop="+(Top?"1":"0")+"\r\nWarningVolume="+Volume+"\r\nAfkTimeoutSeconds="+AfkTimeout+"\r\nAfkGraceSeconds="+AfkGrace+"\r\n"); } catch {} }
}

class AfkForm : Form {
  static Color BG=Color.FromArgb(10,15,26), PANEL=Color.FromArgb(14,22,38), CY=Color.FromArgb(0,216,255), DIM=Color.FromArgb(90,110,130), TX=Color.FromArgb(200,225,240), RED=Color.FromArgb(150,60,70),
    AMBER=Color.FromArgb(255,176,0), ALERT=Color.FromArgb(255,70,70);
  Font fT=Mk("Bahnschrift",11,FontStyle.Bold), fH=Mk("Bahnschrift",9,FontStyle.Regular), fB=Mk("Bahnschrift",14,FontStyle.Bold), fS=Mk("Bahnschrift",8,FontStyle.Regular), fM=Mk("Consolas",22,FontStyle.Bold), fL=Mk("Consolas",8.5f,FontStyle.Regular);
  bool on=false; int min, max, left; string fg="", last; Random rnd=new Random(); Timer t=new Timer();
  Rectangle tog=new Rectangle(20,50,340,62), close=new Rectangle(350,6,24,22); bool hoverClose;
  // AFK window panel
  const int AY=320; Rectangle slider=new Rectangle(90,AY+92,160,14), test=new Rectangle(302,AY+86,50,24), chk=new Rectangle(20,448,170,22);
  bool dragVol, warned, seenSc; DateTime lastAct=DateTime.Now; int afkLeft; string afkState="NO SESSION";
  static Font Mk(string n,float s,FontStyle st){ Font f=new Font(n,s,st); return f.Name==n?f:new Font("Segoe UI",s,st); }
  public AfkForm(int mn,int mx){ min=mn; max=mx; left=rnd.Next(min,max+1); Cfg.Load(); afkLeft=Cfg.AfkTimeout;
    Text="Station Keeper"; FormBorderStyle=FormBorderStyle.None; Size=new Size(380,484); BackColor=BG; StartPosition=FormStartPosition.CenterScreen; TopMost=Cfg.Top; DoubleBuffered=true; Icon=MakeIcon();
    last="["+DateTime.Now.ToString("HH:mm:ss")+"] System ready. Toggle to ACTIVE to start. No keys, no clicks.";
    t.Interval=1000; t.Tick+=Tick; t.Start(); Tick(null,null); }
  static string Ts(){ return "["+DateTime.Now.ToString("HH:mm:ss")+"] "; }
  void Tick(object s,EventArgs e){ fg=N.Fg()??""; bool sc=fg=="StarCitizen";
    if(on && --left<=0){
      if(sc){ N.Move(3); System.Threading.Thread.Sleep(120); N.Move(-3); last=Ts()+"Star Citizen in focus -> mouse nudged (3 px)."; }
      else last=Ts()+"Star Citizen not in focus -> nothing sent.";
      left=rnd.Next(min,max+1); }
    UpdateAfk(sc); Invalidate(); }
  // AFK window: last activity = last input seen while SC is the foreground window (includes nudges, which are real input to SC).
  void UpdateAfk(bool sc){ DateTime now=DateTime.Now;
    if(sc){ if(!seenSc){ seenSc=true; lastAct=now; }
      DateTime a=now.AddMilliseconds(-N.IdleMs()); if(a>lastAct) lastAct=a; }
    if(!seenSc){ afkState="NO SESSION"; afkLeft=Cfg.AfkTimeout; return; }
    double idle=(now-lastAct).TotalSeconds; int rem=(int)Math.Ceiling(Cfg.AfkTimeout-idle); if(rem<0) rem=0;
    bool counting=!sc || idle>=Cfg.AfkGrace;
    if(!counting){ afkState="ACTIVE IN GAME"; afkLeft=Cfg.AfkTimeout; warned=false; return; }
    afkLeft=rem; afkState=rem==0?"WINDOW EXPIRED":(sc?"IDLE IN GAME":"TABBED OUT");
    if(rem<=60 && rem>0 && !warned){ warned=true; Beep.Play(Cfg.Volume); last=Ts()+"AFK warning: 1 minute left in the SC idle window."; }
    if(rem>60) warned=false; }
  void SetVol(int x){ int v=(int)Math.Round((x-slider.X)*100.0/slider.Width); Cfg.Volume=Math.Max(0,Math.Min(100,v)); Invalidate(); }
  protected override void OnMouseMove(MouseEventArgs e){ if(dragVol){ SetVol(e.X); return; }
    bool h=close.Contains(e.Location); if(h!=hoverClose){hoverClose=h;Invalidate();}
    Rectangle sh=Rectangle.Inflate(slider,4,6);
    Cursor=(tog.Contains(e.Location)||chk.Contains(e.Location)||test.Contains(e.Location)||sh.Contains(e.Location)||h)?Cursors.Hand:Cursors.Default; }
  protected override void OnMouseUp(MouseEventArgs e){ if(dragVol){ dragVol=false; Cfg.Save(); } }
  protected override void OnMouseDown(MouseEventArgs e){ if(e.Button!=MouseButtons.Left) return;
    if(close.Contains(e.Location)){ Close(); return; }
    if(Rectangle.Inflate(slider,4,6).Contains(e.Location)){ dragVol=true; SetVol(e.X); return; }
    if(test.Contains(e.Location)){ Beep.Play(Cfg.Volume); last=Ts()+"Warning tone test at "+Cfg.Volume+" %."; Invalidate(); return; }
    if(chk.Contains(e.Location)){ TopMost=!TopMost; Cfg.Top=TopMost; Cfg.Save(); Invalidate(); return; }
    if(tog.Contains(e.Location)){ on=!on; if(on) left=rnd.Next(min,max+1); last=Ts()+"Guard "+(on?"ACTIVATED.":"DEACTIVATED."); Invalidate(); return; }
    N.ReleaseCapture(); N.SendMessage(Handle,0xA1,2,0); }
  static GraphicsPath RR(float x,float y,float w,float h,float r){ GraphicsPath g=new GraphicsPath(); g.AddArc(x,y,r,r,180,90); g.AddArc(x+w-r,y,r,r,270,90); g.AddArc(x+w-r,y+h-r,r,r,0,90); g.AddArc(x,y+h-r,r,r,90,90); g.CloseFigure(); return g; }
  protected override void OnPaint(PaintEventArgs e){ Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    Brush cb=new SolidBrush(CY), db=new SolidBrush(DIM), tb=new SolidBrush(TX);
    using(Pen p=new Pen(CY,1)){ g.DrawRectangle(p,0,0,Width-1,Height-1); g.DrawLine(p,0,34,Width,34); }
    using(Pen p=new Pen(CY,3)){ g.DrawLine(p,0,0,18,0); g.DrawLine(p,0,0,0,18); g.DrawLine(p,Width-1,Height-19,Width-1,Height-1); g.DrawLine(p,Width-19,Height-1,Width-1,Height-1); }
    g.DrawString("S T A T I O N   K E E P E R",fT,cb,12,8);
    g.DrawString("\u2715",fT,hoverClose?cb:db,354,7);
    int ox=tog.X, oy=tog.Y; Color col=on?CY:RED;
    if(on) for(int i=6;i>=1;i--) using(GraphicsPath gp=RR(ox+8-i,oy+8-i,110+2*i,44+2*i,44+2*i)) using(Brush b=new SolidBrush(Color.FromArgb(14,0,216,255))) g.FillPath(b,gp);
    using(GraphicsPath gp=RR(ox+8,oy+8,110,44,44)){ using(Brush b=new SolidBrush(PANEL)) g.FillPath(b,gp); using(Pen p=new Pen(col,2)) g.DrawPath(p,gp); }
    using(Brush b=new SolidBrush(col)){ g.FillEllipse(b,ox+(on?78:14),oy+14,32,32); g.DrawString(on?"A C T I V E":"I N A C T I V E",fB,b,ox+135,oy+10); }
    g.DrawString("CLICK TO TOGGLE",fS,db,ox+137,oy+38);
    int sx=20, sy=124; Rectangle sr=new Rectangle(sx,sy,340,186);
    using(Brush b=new SolidBrush(PANEL)) g.FillRectangle(b,sr);
    using(Pen p=new Pen(Color.FromArgb(80,0,216,255))) g.DrawRectangle(p,sx,sy,339,185);
    g.FillRectangle(cb,sx,sy,4,18); g.DrawString("S Y S T E M   S T A T U S",fH,cb,sx+10,sy+2);
    g.DrawString("NEXT PULSE",fS,db,sx+10,sy+30);
    g.DrawString(on?string.Format("{0:00}:{1:00}",left/60,left%60):"--:--",fM,on?cb:db,sx+8,sy+44);
    bool sc=fg=="StarCitizen";
    g.DrawString("TARGET WINDOW",fS,db,sx+190,sy+30);
    using(Brush b=new SolidBrush(sc?CY:RED)) g.FillEllipse(b,sx+190,sy+52,10,10);
    g.DrawString(sc?"STAR CITIZEN":"NOT IN FOCUS",fH,tb,sx+205,sy+47);
    g.DrawString("INTERVAL  "+min+"-"+max+" S",fS,db,sx+190,sy+72);
    using(Pen p=new Pen(Color.FromArgb(60,0,216,255))) g.DrawLine(p,sx+10,sy+100,sx+330,sy+100);
    g.DrawString("LAST EVENT",fS,db,sx+10,sy+106);
    g.DrawString(last,fL,tb,new RectangleF(sx+10,sy+124,320,56));
    // --- SC AFK window panel ---
    int ay=AY; bool counting=afkState=="IDLE IN GAME"||afkState=="TABBED OUT"||afkState=="WINDOW EXPIRED";
    Color ac=!counting?DIM:(afkLeft<=60?ALERT:(afkLeft<=180?AMBER:CY));
    using(Brush b=new SolidBrush(PANEL)) g.FillRectangle(b,sx,ay,340,118);
    using(Pen p=new Pen(Color.FromArgb(80,ac))) g.DrawRectangle(p,sx,ay,339,117);
    using(Brush b=new SolidBrush(counting?ac:CY)) { g.FillRectangle(b,sx,ay,4,18); g.DrawString("S C   A F K   W I N D O W",fH,b,sx+10,ay+2); }
    g.DrawString("TIME TO KICK",fS,db,sx+10,ay+30);
    using(Brush b=new SolidBrush(ac)) g.DrawString(string.Format("{0:00}:{1:00}",afkLeft/60,afkLeft%60),fM,b,sx+8,ay+44);
    g.DrawString("STATE",fS,db,sx+190,ay+30);
    using(Brush b=new SolidBrush(counting?ac:(seenSc?CY:DIM))) g.FillEllipse(b,sx+190,ay+52,10,10);
    g.DrawString(afkState,fH,tb,sx+205,ay+47);
    g.DrawString((Cfg.AfkTimeout/60)+" MIN  //  WARN 1:00",fS,db,sx+190,ay+66);
    float frac=Math.Max(0f,Math.Min(1f,afkLeft/(float)Cfg.AfkTimeout));
    using(Brush b=new SolidBrush(Color.FromArgb(40,ac))) g.FillRectangle(b,sx+10,ay+78,320,3);
    using(Brush b=new SolidBrush(ac)) g.FillRectangle(b,sx+10,ay+78,320*frac,3);
    g.DrawString("WARN VOL",fS,db,sx+10,ay+93);
    using(Brush b=new SolidBrush(Color.FromArgb(50,0,216,255))) g.FillRectangle(b,slider.X,slider.Y+5,slider.Width,4);
    float kx=slider.X+slider.Width*Cfg.Volume/100f;
    g.FillRectangle(cb,slider.X,slider.Y+5,kx-slider.X,4);
    g.FillRectangle(cb,kx-3,slider.Y-1,6,16);
    g.DrawString(Cfg.Volume+" %",fS,tb,slider.Right+8,ay+93);
    using(Pen p=new Pen(CY,1)) g.DrawRectangle(p,test);
    g.DrawString("TEST",fS,cb,test.X+11,test.Y+5);
    using(Pen p=new Pen(TopMost?CY:DIM,1.5f)) g.DrawRectangle(p,chk.X,chk.Y+3,14,14);
    if(TopMost){ using(Brush b=new SolidBrush(Color.FromArgb(40,0,216,255))) g.FillRectangle(b,chk.X+1,chk.Y+4,13,13);
      using(Pen p=new Pen(CY,2.2f)) g.DrawLines(p,new PointF[]{ new PointF(chk.X+3,chk.Y+10), new PointF(chk.X+6,chk.Y+14), new PointF(chk.X+12,chk.Y+6) }); }
    g.DrawString("A L W A Y S   O N   T O P",fS,TopMost?cb:db,chk.X+22,chk.Y+4); }
  public static Icon MakeIcon(){ Bitmap b=new Bitmap(32,32); using(Graphics g=Graphics.FromImage(b)){ g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
      using(Brush br=new SolidBrush(BG)) g.FillEllipse(br,1,1,30,30); using(Pen p=new Pen(CY,2)) g.DrawEllipse(p,2,2,28,28); using(Brush br=new SolidBrush(CY)) g.FillEllipse(br,11,11,10,10); }
    return Icon.FromHandle(b.GetHicon()); }
}
static class Program {
  [STAThread] static void Main(string[] a){ int mn=180, mx=300; string shot=null;
    for(int i=0;i<a.Length-1;i++){ if(a[i]=="--min") int.TryParse(a[i+1],out mn); if(a[i]=="--max") int.TryParse(a[i+1],out mx); if(a[i]=="--screenshot") shot=a[i+1]; }
    if(a.Length>0 && a[0]=="--make-icon"){ using(var fs=File.Create(a[1])) AfkForm.MakeIcon().Save(fs); return; }
    if(mn<1) mn=1; if(mx<mn) mx=mn;
    Application.EnableVisualStyles(); AfkForm f=new AfkForm(mn,mx);
    if(shot!=null){ Timer t2=new Timer(); t2.Interval=1200; t2.Tick+=delegate{ t2.Stop(); Bitmap b=new Bitmap(f.Width,f.Height); f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height)); b.Save(shot); f.Close(); }; t2.Start(); }
    Application.Run(f); }
}
