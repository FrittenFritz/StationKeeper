using System.Reflection;
using System; using System.Diagnostics; using System.Drawing; using System.Drawing.Drawing2D;
using System.Runtime.InteropServices; using System.Windows.Forms;

[assembly: AssemblyTitle("Station Keeper")]
[assembly: AssemblyProduct("Station Keeper")]
[assembly: AssemblyDescription("Keeps you from idling out by nudging the mouse only while Star Citizen is in focus.")]
[assembly: AssemblyCopyright("Copyright (c) 2026 FrittenFritz")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class N {
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct IN { public uint type; public MI mi; public uint p1, p2; }
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern uint SendInput(uint n, IN[] i, int s);
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
  public static void Move(int dx){ IN[] a=new IN[1]; a[0].type=0; a[0].mi.dx=dx; a[0].mi.flags=1; SendInput(1,a,Marshal.SizeOf(typeof(IN))); }
  public static string Fg(){ uint p; GetWindowThreadProcessId(GetForegroundWindow(), out p); if(p==0) return null;
    try { return Process.GetProcessById((int)p).ProcessName; } catch { return null; } }
}
class AfkForm : Form {
  static Color BG=Color.FromArgb(10,15,26), PANEL=Color.FromArgb(14,22,38), CY=Color.FromArgb(0,216,255), DIM=Color.FromArgb(90,110,130), TX=Color.FromArgb(200,225,240), RED=Color.FromArgb(150,60,70);
  Font fT=Mk("Bahnschrift",11,FontStyle.Bold), fH=Mk("Bahnschrift",9,FontStyle.Regular), fB=Mk("Bahnschrift",14,FontStyle.Bold), fS=Mk("Bahnschrift",8,FontStyle.Regular), fM=Mk("Consolas",22,FontStyle.Bold), fL=Mk("Consolas",8.5f,FontStyle.Regular);
  bool on=false; int min, max, left; string fg="", last; Random rnd=new Random(); Timer t=new Timer();
  Rectangle tog=new Rectangle(20,50,340,62), close=new Rectangle(350,6,24,22); bool hoverClose; Rectangle chk=new Rectangle(20,320,170,22);
  static string Ini { get { return System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"StationKeeper"),"settings.ini"); } }
  static bool LoadTop(){ try { foreach(string l in System.IO.File.ReadAllLines(Ini)) if(l.Trim()=="AlwaysOnTop=1") return true; } catch {} return false; }
  static void SaveTop(bool v){ try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Ini)); System.IO.File.WriteAllText(Ini,"[StationKeeper]\r\nAlwaysOnTop="+(v?"1":"0")+"\r\n"); } catch {} }
  static Font Mk(string n,float s,FontStyle st){ Font f=new Font(n,s,st); return f.Name==n?f:new Font("Segoe UI",s,st); }
  public AfkForm(int mn,int mx){ min=mn; max=mx; left=rnd.Next(min,max+1);
    Text="Station Keeper"; FormBorderStyle=FormBorderStyle.None; Size=new Size(380,356); BackColor=BG; StartPosition=FormStartPosition.CenterScreen; TopMost=LoadTop(); DoubleBuffered=true; Icon=MakeIcon();
    last="["+DateTime.Now.ToString("HH:mm:ss")+"] System ready. Toggle to ACTIVE to start. No keys, no clicks.";
    t.Interval=1000; t.Tick+=Tick; t.Start(); }
  void Tick(object s,EventArgs e){ fg=N.Fg()??"";
    if(on && --left<=0){ string ts="["+DateTime.Now.ToString("HH:mm:ss")+"] ";
      if(fg=="StarCitizen"){ N.Move(3); System.Threading.Thread.Sleep(120); N.Move(-3); last=ts+"Star Citizen in focus -> mouse nudged (3 px)."; }
      else last=ts+"Star Citizen not in focus -> nothing sent.";
      left=rnd.Next(min,max+1); }
    Invalidate(); }
  protected override void OnMouseMove(MouseEventArgs e){ bool h=close.Contains(e.Location); if(h!=hoverClose){hoverClose=h;Invalidate();} Cursor=(tog.Contains(e.Location)||chk.Contains(e.Location)||h)?Cursors.Hand:Cursors.Default; }
  protected override void OnMouseDown(MouseEventArgs e){ if(e.Button!=MouseButtons.Left) return;
    if(close.Contains(e.Location)){ Close(); return; }
    if(chk.Contains(e.Location)){ TopMost=!TopMost; SaveTop(TopMost); Invalidate(); return; }
    if(tog.Contains(e.Location)){ on=!on; if(on) left=rnd.Next(min,max+1); last="["+DateTime.Now.ToString("HH:mm:ss")+"] Guard "+(on?"ACTIVATED.":"DEACTIVATED."); Invalidate(); return; }
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
    if(a.Length>0 && a[0]=="--make-icon"){ using(var fs=System.IO.File.Create(a[1])) AfkForm.MakeIcon().Save(fs); return; }
    if(mn<1) mn=1; if(mx<mn) mx=mn;
    Application.EnableVisualStyles(); AfkForm f=new AfkForm(mn,mx);
    if(shot!=null){ Timer t2=new Timer(); t2.Interval=1200; t2.Tick+=delegate{ t2.Stop(); Bitmap b=new Bitmap(f.Width,f.Height); f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height)); b.Save(shot); f.Close(); }; t2.Start(); }
    Application.Run(f); }
}


