using System.Reflection;
using System; using System.Diagnostics; using System.Drawing; using System.Drawing.Drawing2D;
using System.Runtime.InteropServices; using System.Windows.Forms; using System.IO; using System.Collections.Generic;

[assembly: AssemblyTitle("Station Keeper")]
[assembly: AssemblyProduct("Station Keeper")]
[assembly: AssemblyDescription("AFK window countdown with warning tone, plus an optional PULSE (small mouse move) only while Star Citizen is in focus.")]
[assembly: AssemblyCopyright("Copyright (c) 2026 FrittenFritz")]
[assembly: AssemblyVersion("1.3.1.0")]
[assembly: AssemblyFileVersion("1.3.1.0")]
[assembly: AssemblyInformationalVersion("1.3.1")]

static class N {
  // Win32 INPUT = type + union { MOUSEINPUT, KEYBDINPUT }. Size must be 40 bytes (x64) / 28 bytes (x86), otherwise SendInput fails.
  [StructLayout(LayoutKind.Sequential)] public struct MI { public int dx, dy; public uint data, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] public struct KI { public ushort vk, scan; public uint flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] public struct UN { [FieldOffset(0)] public MI mi; [FieldOffset(0)] public KI ki; }
  [StructLayout(LayoutKind.Sequential)] public struct IN { public uint type; public UN u; }
  public static readonly int INSZ=Marshal.SizeOf(typeof(IN)); public static readonly bool INOK=INSZ==(IntPtr.Size==8?40:28);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint n, IN[] i, int s);
  [DllImport("user32.dll")] public static extern bool ReleaseCapture();
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, int w, int l);
  public const int WS_EX_LAYERED=0x80000, WS_EX_TRANSPARENT=0x20, WS_EX_NOACTIVATE=0x08000000, WM_MOUSEACTIVATE=0x21, MA_NOACTIVATE=3;
  [StructLayout(LayoutKind.Sequential)] struct JIX { public uint size, flags, x, y, z, r, u, v, buttons, bnum, pov, r1, r2; }
  [DllImport("winmm.dll")] static extern int joyGetPosEx(int id, ref JIX j);
  // --- Real input detection ---
  // Keyboard/mouse: low-level hooks on their own background thread (own message loop), so a busy UI thread never delays system input. Only hardware events count: injected events
  // (LLKHF_INJECTED / LLMHF_INJECTED) and our own SendInput (dwExtraInfo == MAGIC) are ignored.
  // GetLastInputInfo is NOT used: other software and HID side-signals update it (e.g. a 60 s phantom tick).
  public const long MAGIC=0x534B5031; // "SKP1"
  delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] struct KBLL { public uint vk, scan, flags, time; public IntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct MSLL { public int x, y; public uint data, flags, time; public IntPtr extra; }
  [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id, HookProc f, IntPtr mod, uint thread);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
  static HookProc kbProc=KbHook, msProc=MsHook; static IntPtr hKb, hMs;
  static volatile int kmTick=Environment.TickCount; static volatile int joyTick=Environment.TickCount;
  public static string LastSrc="keyboard/mouse";
  // Hooks only store a timestamp: no allocation, no logging, so input to the game is never delayed.
  static IntPtr KbHook(int c, IntPtr w, IntPtr l){ if(c>=0){ KBLL k=(KBLL)Marshal.PtrToStructure(l,typeof(KBLL)); if((k.flags&0x10)==0 && k.extra.ToInt64()!=MAGIC){ kmTick=Environment.TickCount; } } return CallNextHookEx(IntPtr.Zero,c,w,l); }
  static IntPtr MsHook(int c, IntPtr w, IntPtr l){ if(c>=0){ MSLL m=(MSLL)Marshal.PtrToStructure(l,typeof(MSLL)); if((m.flags&0x1)==0 && m.extra.ToInt64()!=MAGIC){ kmTick=Environment.TickCount; } } return CallNextHookEx(IntPtr.Zero,c,w,l); }
  [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr h; public uint m; public IntPtr w, l; public uint t; public int x, y; }
  [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint a, uint b);
  [DllImport("user32.dll")] static extern bool PostThreadMessage(uint id, uint m, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  static volatile uint hookTid; static System.Threading.Thread hookThr;
  // Starts the hook thread; returns true when both hooks are installed.
  public static bool Hook(){ bool ok=false; var ready=new System.Threading.ManualResetEvent(false);
    hookThr=new System.Threading.Thread(delegate(){ hookTid=GetCurrentThreadId(); IntPtr mod=GetModuleHandle(null);
      hKb=SetWindowsHookEx(13,kbProc,mod,0); hMs=SetWindowsHookEx(14,msProc,mod,0); ok=hKb!=IntPtr.Zero && hMs!=IntPtr.Zero; ready.Set();
      MSG m; while(GetMessage(out m,IntPtr.Zero,0,0)>0){}
      if(hKb!=IntPtr.Zero) UnhookWindowsHookEx(hKb); if(hMs!=IntPtr.Zero) UnhookWindowsHookEx(hMs); hKb=hMs=IntPtr.Zero; });
    hookThr.IsBackground=true; hookThr.Priority=System.Threading.ThreadPriority.Highest; hookThr.Start(); ready.WaitOne(3000); return ok; }
  public static void Unhook(){ if(hookThr==null) return; PostThreadMessage(hookTid,0x0012,IntPtr.Zero,IntPtr.Zero); hookThr.Join(1000); hookThr=null; } // WM_QUIT
  // Joystick/HOTAS (winmm, IDs 0-15): any axis change, button or POV change counts immediately. New devices only set a baseline.
  // Runs on its own background thread: querying an unplugged/hidden ID can take 40+ ms, so only connected IDs are polled each second.
  // Full re-scan of all 16 IDs at start, every 30 s and after WM_DEVICECHANGE (JoyRescan()).
  static uint[][] jBase=new uint[16][]; static bool[] jOn=new bool[16]; static volatile bool jRescan=true;
  public static void JoyRescan(){ jRescan=true; }
  public static void StartJoy(){ var th=new System.Threading.Thread(delegate(){ int lastScan=0;
      while(true){ if(jRescan || Since(lastScan)>=30000){ jRescan=false; lastScan=Environment.TickCount;
          for(int i=0;i<16;i++){ JIX j=new JIX(); j.size=(uint)Marshal.SizeOf(typeof(JIX)); j.flags=0xFF; bool c=joyGetPosEx(i,ref j)==0; if(c && !jOn[i]) jBase[i]=null; jOn[i]=c; } }
        PollJoy(); System.Threading.Thread.Sleep(1000); } });
    th.IsBackground=true; th.Start(); }
  static void PollJoy(){ for(int i=0;i<16;i++){ if(!jOn[i]) continue; JIX j=new JIX(); j.size=(uint)Marshal.SizeOf(typeof(JIX)); j.flags=0xFF;
      if(joyGetPosEx(i,ref j)!=0){ jBase[i]=null; jOn[i]=false; continue; }
      uint[] c={j.x,j.y,j.z,j.r,j.u,j.v,j.buttons,j.pov}; uint[] b=jBase[i];
      if(b==null){ jBase[i]=c; continue; }
      bool hit=false;
      for(int k=0;k<6;k++){ if(c[k]!=b[k]){ hit=true; b[k]=c[k]; } }
      if(c[6]!=b[6]||c[7]!=b[7]){ hit=true; b[6]=c[6]; b[7]=c[7]; }
      if(hit) joyTick=Environment.TickCount; } }
  static int Since(int t){ return unchecked(Environment.TickCount-t); }
  // Call once per tick: returns ms since the last real input (joystick results come from the background poller) and sets LastSrc.
  public static long RealIdleMs(){ int a=Since(kmTick), b=Since(joyTick); LastSrc=b<a?"joystick":"keyboard/mouse"; return Math.Min(a,b); }
  // Re-read (used for the 2 s pre-fire check right after RealIdleMs).
  public static long RealIdleNowMs(){ return Math.Min(Since(kmTick),Since(joyTick)); }
  // PULSE input: small relative mouse move, 15 counts right in 3 steps (~10 ms apart), ~100 ms pause, same back. Tagged with MAGIC.
  // Returns 0 on success, otherwise the Win32 error code (-1 = wrong INPUT size).
  static int Mv(int dx){ IN[] a=new IN[1]; a[0].type=0; a[0].u.mi.dx=dx; a[0].u.mi.flags=1; a[0].u.mi.extra=(IntPtr)MAGIC; // MOUSEEVENTF_MOVE (relative)
    if(SendInput(1,a,INSZ)!=1){ int e=Marshal.GetLastWin32Error(); return e==0?-1:e; } return 0; }
  public static int MousePulse(){ if(!INOK) return -1; int e;
    for(int i=0;i<3;i++){ if((e=Mv(5))!=0) return e; System.Threading.Thread.Sleep(10); }
    System.Threading.Thread.Sleep(100);
    for(int i=0;i<3;i++){ if((e=Mv(-5))!=0) return e; System.Threading.Thread.Sleep(10); }
    return 0; }
  // Foreground process name, looked up only when the foreground PID changes.
  static uint fgPid; static string fgName;
  public static string Fg(){ uint p; GetWindowThreadProcessId(GetForegroundWindow(), out p); if(p==0) return null; if(p==fgPid) return fgName;
    string n=null; try { using(Process pr=Process.GetProcessById((int)p)) n=pr.ProcessName; } catch {} fgPid=p; fgName=n; return n; }
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
  public static bool Top=false, Mini=false, MiniLocked=false; public static int Volume=60, AfkTimeout=900, AfkGrace=60, MiniX=-1, MiniY=-1, MiniOpacity=70;
  public static bool NoLoad; // --screenshot renders use built-in defaults, never the user's settings.ini
  public static void Load(){ if(NoLoad) return; try { foreach(string raw in File.ReadAllLines(Ini)){ string l=raw.Trim(); int e=l.IndexOf('='); if(e<1) continue; string k=l.Substring(0,e).Trim(), v=l.Substring(e+1).Trim(); int n;
      if(!int.TryParse(v,out n)) continue;
      if(k=="AlwaysOnTop") Top=n==1; else if(k=="WarningVolume") Volume=Math.Max(0,Math.Min(100,n));
      else if(k=="AfkTimeoutSeconds") AfkTimeout=Math.Max(120,n); else if(k=="AfkGraceSeconds") AfkGrace=Math.Max(0,n);
      else if(k=="MiniMode") Mini=n==1; else if(k=="MiniLocked") MiniLocked=n==1; else if(k=="MiniX") MiniX=n; else if(k=="MiniY") MiniY=n;
      else if(k=="MiniOpacity") MiniOpacity=Math.Max(10,Math.Min(100,n)); } } catch {}
    if(AfkGrace>AfkTimeout-60) AfkGrace=Math.Max(0,AfkTimeout-60); }
  public static void Save(){ if(NoLoad) return; try { Directory.CreateDirectory(Path.GetDirectoryName(Ini));
      File.WriteAllText(Ini,"[StationKeeper]\r\nAlwaysOnTop="+(Top?"1":"0")+"\r\nWarningVolume="+Volume+"\r\nAfkTimeoutSeconds="+AfkTimeout+"\r\nAfkGraceSeconds="+AfkGrace+
        "\r\nMiniMode="+(Mini?"1":"0")+"\r\nMiniLocked="+(MiniLocked?"1":"0")+"\r\nMiniX="+MiniX+"\r\nMiniY="+MiniY+"\r\nMiniOpacity="+MiniOpacity+"\r\n"); } catch {} }
}

class AfkForm : Form {
  // Version label: single source = AssemblyInformationalVersion (three-part), via Application.ProductVersion.
  static readonly string VER="v"+Application.ProductVersion; static readonly Color VDIM=Color.FromArgb(70,88,106);
  static Color BG=Color.FromArgb(10,15,26), PANEL=Color.FromArgb(14,22,38), CY=Color.FromArgb(0,216,255), DIM=Color.FromArgb(90,110,130), TX=Color.FromArgb(200,225,240), RED=Color.FromArgb(150,60,70),
    AMBER=Color.FromArgb(255,176,0), ALERT=Color.FromArgb(255,70,70), GREEN=Color.FromArgb(50,220,130); // GREEN: PULSE switch + label when ARMED
  Font fT=Mk("Bahnschrift",11,FontStyle.Bold), fH=Mk("Bahnschrift",9,FontStyle.Regular), fB=Mk("Bahnschrift",14,FontStyle.Bold), fS=Mk("Bahnschrift",8,FontStyle.Regular), fV=Mk("Bahnschrift",7,FontStyle.Regular), fM=Mk("Consolas",22,FontStyle.Bold), fL=Mk("Consolas",8.5f,FontStyle.Regular), fMini=Mk("Consolas",17,FontStyle.Bold);
  bool on=false; int min, max, left; string fg="", last; Random rnd=new Random(); Timer t=new Timer();
  Rectangle tog=new Rectangle(20,50,340,62), close=new Rectangle(350,6,24,22); bool hoverClose;
  // AFK window panel
  const TextFormatFlags CF=TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine; const int WH=40; int wo=WH; bool warnOn; int AY { get { return 320+wo; } } Rectangle slider { get { return new Rectangle(90,AY+92,160,14); } } Rectangle test { get { return new Rectangle(302,AY+86,50,24); } } Rectangle chk { get { return new Rectangle(20,448+wo,170,22); } } Rectangle mchk { get { return new Rectangle(196,448+wo,52,22); } } Rectangle opSl { get { return new Rectangle(256,448+wo+4,64,14); } } bool dragOp;
  // Mini mode: compact, semi-transparent always-on-top timer. Locked = click-through and never activates.
  static readonly Size NORMAL=new Size(380,484+WH), MINI=new Size(170,50), MINI_P=new Size(170,66);
  Size CurMini { get { return on?MINI_P:MINI; } } Rectangle lockR=new Rectangle(144,15,18,20); Point normalLoc; bool normalLocSet; NotifyIcon tray; MenuItem miUnlock, miNormal;
  int scChk; volatile bool pulseBusy; bool scRunning, standby=true, pulsed; int blink; long ri; DateTime lastPulse=DateTime.MinValue;
  bool dragVol, warned, seenSc; DateTime lastAct=DateTime.Now; int afkLeft; string afkState="NO SESSION";
  static Font Mk(string n,float s,FontStyle st){ Font f=new Font(n,s,st); return f.Name==n?f:new Font("Segoe UI",s,st); }
  public AfkForm(int mn,int mx){ min=mn; max=mx; left=rnd.Next(min,max+1); Cfg.Load(); afkLeft=Cfg.AfkTimeout;
    Text="Station Keeper"; FormBorderStyle=FormBorderStyle.None; Size=new Size(380,484+WH); BackColor=BG; StartPosition=FormStartPosition.CenterScreen; TopMost=Cfg.Top; DoubleBuffered=true; Icon=MakeIcon();
    BuildTray(); if(Cfg.Mini) ApplyMini(true,false);
    last="["+DateTime.Now.ToString("HH:mm:ss")+"] System ready. PULSE OFF: monitor-only, no input sent. Click to ARM the pulse.";
    if(!N.Hook()) last=Ts()+"Input hooks unavailable: keyboard/mouse activity is not detected."; N.StartJoy();
    t.Interval=1000; t.Tick+=Tick; t.Start(); Tick(null,null); }
  string PT(){ return standby?"STANDBY":string.Format("{0:00}:{1:00}",left/60,left%60); }
  static string Ts(){ return "["+DateTime.Now.ToString("HH:mm:ss")+"] "; }
  void Tick(object s,EventArgs e){ fg=Cfg.NoLoad?"":(N.Fg()??""); /* renders: always not-in-focus */ bool sc=fg=="StarCitizen"; blink++;
    if(!sc){ if(--scChk<=0){ scChk=5; try { Process[] ps=Process.GetProcessesByName("StarCitizen"); scRunning=ps.Length>0; foreach(Process p in ps) p.Dispose(); } catch { scRunning=false; } } } else { scRunning=true; scChk=0; } // process search at most every 5 s
    SetWarn(on && !sc);
    // PULSE only counts down after AfkGrace s without real input; any real input -> STANDBY with reset.
    ri=N.RealIdleMs();
    if(on && ri<Cfg.AfkGrace*1000L){ if(!standby) last=Ts()+"STANDBY: "+N.LastSrc+" input. PULSE countdown reset."; standby=true; }
    else if(on && standby){ standby=false; left=rnd.Next(min,max+1); }
    else if(on && --left<=0){
      if(N.RealIdleNowMs()<2000){ standby=true; last=Ts()+"STANDBY: "+N.LastSrc+" input just before the pulse -> pulse skipped."; }
      else if(sc && !pulseBusy){ pulseBusy=true; System.Threading.ThreadPool.QueueUserWorkItem(delegate{ int pe=N.MousePulse(); // pulse (~160 ms incl. pauses) off the UI thread
          try { BeginInvoke((MethodInvoker)delegate{ pulseBusy=false; if(pe==0){ lastPulse=DateTime.Now; pulsed=true; last=Ts()+"Mouse pulse sent."; } else last=Ts()+"Pulse failed (err "+pe+")."; Invalidate(); }); } catch { pulseBusy=false; } }); }
      else last=Ts()+"Star Citizen not in focus -> nothing sent.";
      left=rnd.Next(min,max+1); }
    UpdateAfk(sc); FitMini(); Invalidate(); }
  // Mini grows by one line while PULSE is armed; keep it on screen.
  void FitMini(){ if(!Cfg.Mini || Size==CurMini) return; Size=CurMini; Location=ClampMini(Location); }
  void SetOp(int x){ int v=(int)Math.Round(10+(x-opSl.X)*90.0/opSl.Width); SetOpacity(v); }
  void SetOpacity(int v){ Cfg.MiniOpacity=Math.Max(10,Math.Min(100,v)); if(Cfg.Mini) Opacity=Math.Min(Cfg.MiniOpacity,99)/100.0; Invalidate(); }
  protected override void OnMouseWheel(MouseEventArgs e){ if(Cfg.Mini && !Cfg.MiniLocked){ SetOpacity(Cfg.MiniOpacity+(e.Delta>0?5:-5)); Cfg.Save(); } base.OnMouseWheel(e); }
  protected override bool ShowWithoutActivation { get { return Cfg.Mini; } }
  protected override CreateParams CreateParams { get { CreateParams cp=base.CreateParams;
      if(Cfg.Mini){ cp.ExStyle|=N.WS_EX_NOACTIVATE|N.WS_EX_LAYERED; if(Cfg.MiniLocked) cp.ExStyle|=N.WS_EX_TRANSPARENT; }
      return cp; } }
  protected override void WndProc(ref Message m){ if(m.Msg==0x0219) N.JoyRescan(); /* WM_DEVICECHANGE */ if(Cfg.Mini && m.Msg==N.WM_MOUSEACTIVATE){ m.Result=(IntPtr)N.MA_NOACTIVATE; return; } base.WndProc(ref m); }
  void BuildTray(){ ContextMenu cm=new ContextMenu();
    MenuItem miShow=new MenuItem("Show",delegate{ Visible=true; if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal; if(!Cfg.Mini) Activate(); });
    miUnlock=new MenuItem("Unlock mini",delegate{ SetLocked(false); });
    miNormal=new MenuItem("Normal view",delegate{ ApplyMini(false,true); });
    cm.MenuItems.Add(miShow); cm.MenuItems.Add(miUnlock); cm.MenuItems.Add(miNormal); cm.MenuItems.Add("-"); cm.MenuItems.Add(new MenuItem("Exit",delegate{ Close(); }));
    cm.Popup+=delegate{ miUnlock.Enabled=Cfg.Mini&&Cfg.MiniLocked; miNormal.Enabled=Cfg.Mini; };
    tray=new NotifyIcon(); tray.Icon=Icon; tray.Text="Station Keeper "+VER; tray.ContextMenu=cm; tray.Visible=true; }
  protected override void OnFormClosed(FormClosedEventArgs e){ N.Unhook(); if(tray!=null){ tray.Visible=false; tray.Dispose(); } base.OnFormClosed(e); }
  // Switch view without ever activating the window (Size/Location/TopMost/UpdateStyles use SWP_NOACTIVATE).
  void ApplyMini(bool mini,bool save){
    if(mini){ if(!normalLocSet && IsHandleCreated){ normalLoc=Location; normalLocSet=true; } else if(!normalLocSet && StartPosition==FormStartPosition.CenterScreen){ Rectangle wa=Screen.PrimaryScreen.WorkingArea; normalLoc=new Point(wa.X+(wa.Width-NORMAL.Width)/2,wa.Y+(wa.Height-NORMAL.Height)/2); normalLocSet=true; }
      Cfg.Mini=true; StartPosition=FormStartPosition.Manual; Size=CurMini; Location=ClampMini(new Point(Cfg.MiniX,Cfg.MiniY));
      Opacity=Math.Min(Cfg.MiniOpacity,99)/100.0; TopMost=true; }
    else { if(Cfg.Mini){ Cfg.MiniX=Location.X; Cfg.MiniY=Location.Y; } Cfg.Mini=false; Opacity=1.0; Size=NORMAL; TopMost=Cfg.Top;
      if(normalLocSet) Location=normalLoc; }
    if(IsHandleCreated) UpdateStyles(); if(save) Cfg.Save(); Invalidate(); }
  void SetLocked(bool l){ Cfg.MiniLocked=l; if(Cfg.Mini){ Cfg.MiniX=Location.X; Cfg.MiniY=Location.Y; } if(IsHandleCreated) UpdateStyles(); Cfg.Save(); Invalidate(); }
  // Default top-right of the primary screen; otherwise keep the whole mini window inside the nearest screen's working area.
  Point ClampMini(Point p){ Size MINI=CurMini; Rectangle wa=Screen.PrimaryScreen.WorkingArea;
    if(p.X==-1 && p.Y==-1) return new Point(wa.Right-MINI.Width-20,wa.Top+20);
    wa=Screen.FromRectangle(new Rectangle(p,MINI)).WorkingArea;
    return new Point(Math.Max(wa.Left,Math.Min(p.X,wa.Right-MINI.Width)),Math.Max(wa.Top,Math.Min(p.Y,wa.Bottom-MINI.Height))); }
  void PaintMini(Graphics g){ bool counting=afkState=="IDLE IN GAME"||afkState=="TABBED OUT"||afkState=="WINDOW EXPIRED";
    Color ac=!counting?DIM:(afkLeft<=60?ALERT:(afkLeft<=180?AMBER:CY));
    using(Pen p=new Pen(Color.FromArgb(140,0,216,255),1)) g.DrawRectangle(p,0,0,Width-1,Height-1);
    using(Pen p=new Pen(CY,3)){ g.DrawLine(p,0,0,12,0); g.DrawLine(p,0,0,0,12); g.DrawLine(p,Width-1,Height-13,Width-1,Height-1); g.DrawLine(p,Width-13,Height-1,Width-1,Height-1); }
    using(Brush b=new SolidBrush(on?GREEN:RED)) g.FillEllipse(b,10,20,10,10);
    using(Brush b=new SolidBrush(DIM)) g.DrawString(on?"AFK WINDOW  //  ARMED":"AFK WINDOW  //  OFF",fS,b,26,3);
    using(Brush b=new SolidBrush(ac)) g.DrawString(string.Format("{0:00}:{1:00}",afkLeft/60,afkLeft%60),fMini,b,24,15);
    if(on){ bool sc=fg=="StarCitizen"; Color pc=sc?CY:(scRunning?AMBER:ALERT);
      using(Pen p=new Pen(Color.FromArgb(60,0,216,255))) g.DrawLine(p,8,48,Width-9,48);
      using(Brush b=new SolidBrush(pc)) g.DrawString((sc?"NEXT PULSE  ":(scRunning?"GAME NOT IN FOCUS  ":"GAME NOT RUNNING  "))+PT(),fS,b,8,50); }
    Color lc=Cfg.MiniLocked?CY:DIM; int lx=lockR.X, ly=lockR.Y;
    using(Pen p=new Pen(lc,2f)){ if(Cfg.MiniLocked) g.DrawArc(p,lx+3,ly,12,14,180,180); else g.DrawArc(p,lx+7,ly-3,12,14,180,180);
      g.DrawLine(p,lx+3,ly+7,lx+3,ly+9); if(Cfg.MiniLocked) g.DrawLine(p,lx+15,ly+7,lx+15,ly+9); }
    using(Brush b=new SolidBrush(lc)) g.FillRectangle(b,lx+1,ly+9,16,11);
    using(Brush b=new SolidBrush(BG)) g.FillRectangle(b,lx+8,ly+12,2,5); }

  // AFK window: last activity = last REAL input (hooks/joystick, see N) while the game is in the foreground,
  // or the last pulse actually sent to the game (a pulse is input to the game).
  void UpdateAfk(bool sc){ DateTime now=DateTime.Now;
    if(sc){ if(!seenSc){ seenSc=true; lastAct=now; }
      DateTime a=now.AddMilliseconds(-ri); if(a>lastAct) lastAct=a; }
    if(pulsed && lastPulse>lastAct) lastAct=lastPulse;
    if(!seenSc){ afkState="NO SESSION"; afkLeft=Cfg.AfkTimeout; return; }
    // PULSE ARMED + game in the foreground: AFK window held at full time (greyed). When the hold ends it counts from lastAct.
    if(on && sc){ afkState="HELD BY PULSE"; afkLeft=Cfg.AfkTimeout; warned=false; return; }
    double idle=(now-lastAct).TotalSeconds; int rem=(int)Math.Ceiling(Cfg.AfkTimeout-idle); if(rem<0) rem=0;
    bool counting=!sc || idle>=Cfg.AfkGrace;
    if(!counting){ afkState="ACTIVE IN GAME"; afkLeft=Cfg.AfkTimeout; warned=false; return; }
    afkLeft=rem; afkState=rem==0?"WINDOW EXPIRED":(sc?"IDLE IN GAME":"TABBED OUT");
    if(rem<=60 && rem>0 && !warned){ warned=true; Beep.Play(Cfg.Volume); last=Ts()+"AFK warning: 1 minute left in the AFK window."; }
    if(rem>60) warned=false; }
  // Warning banner slot: fixed WH px reserved in the layout so the window never resizes.
  void SetWarn(bool w){ if(w==warnOn) return; warnOn=w; Invalidate(); }
  void SetVol(int x){ int v=(int)Math.Round((x-slider.X)*100.0/slider.Width); Cfg.Volume=Math.Max(0,Math.Min(100,v)); Invalidate(); }
  protected override void OnMouseMove(MouseEventArgs e){ if(Cfg.Mini){ Cursor=lockR.Contains(e.Location)?Cursors.Hand:Cursors.SizeAll; return; } if(dragVol){ SetVol(e.X); return; } if(dragOp){ SetOp(e.X); return; }
    bool h=close.Contains(e.Location); if(h!=hoverClose){hoverClose=h;Invalidate();}
    Rectangle sh=Rectangle.Inflate(slider,4,6);
    Cursor=(tog.Contains(e.Location)||chk.Contains(e.Location)||mchk.Contains(e.Location)||Rectangle.Inflate(opSl,4,4).Contains(e.Location)||test.Contains(e.Location)||sh.Contains(e.Location)||h)?Cursors.Hand:Cursors.Default; }
  protected override void OnMouseUp(MouseEventArgs e){ if(dragVol||dragOp){ dragVol=false; dragOp=false; Cfg.Save(); } }
  protected override void OnMouseDown(MouseEventArgs e){
    if(Cfg.Mini){ if(e.Button==MouseButtons.Right){ tray.ContextMenu.Show(this,e.Location); return; } if(e.Button!=MouseButtons.Left) return;
      if(lockR.Contains(e.Location)){ SetLocked(true); return; }
      N.ReleaseCapture(); N.SendMessage(Handle,0xA1,2,0); Location=ClampMini(Location); Cfg.MiniX=Location.X; Cfg.MiniY=Location.Y; Cfg.Save(); return; }
    if(e.Button!=MouseButtons.Left) return;
    if(close.Contains(e.Location)){ Close(); return; }
    if(Rectangle.Inflate(slider,4,6).Contains(e.Location)){ dragVol=true; SetVol(e.X); return; }
    if(test.Contains(e.Location)){ Beep.Play(Cfg.Volume); last=Ts()+"Warning tone test at "+Cfg.Volume+" %."; Invalidate(); return; }
    if(Rectangle.Inflate(opSl,4,4).Contains(e.Location)){ dragOp=true; SetOp(e.X); return; }
    if(mchk.Contains(e.Location)){ ApplyMini(true,true); return; }
    if(chk.Contains(e.Location)){ TopMost=!TopMost; Cfg.Top=TopMost; Cfg.Save(); Invalidate(); return; }
    if(tog.Contains(e.Location)){ on=!on; if(on){ left=rnd.Next(min,max+1); standby=true; } last=Ts()+(on?"PULSE ARMED. Pulses only while the game is in focus.":"PULSE OFF. Monitor-only, no input sent."); SetWarn(on && fg!="StarCitizen"); Invalidate(); return; }
    N.ReleaseCapture(); N.SendMessage(Handle,0xA1,2,0); }
  static GraphicsPath RR(float x,float y,float w,float h,float r){ GraphicsPath g=new GraphicsPath(); g.AddArc(x,y,r,r,180,90); g.AddArc(x+w-r,y,r,r,270,90); g.AddArc(x+w-r,y+h-r,r,r,0,90); g.AddArc(x,y+h-r,r,r,90,90); g.CloseFigure(); return g; }
  // Header bar text: top y so the real glyph ink (not the font's internal leading) is centered between y=0 and the line at y=34.
  const float HDR_MID=17f;
  static float InkY(string s, Font f){ using(GraphicsPath gp=new GraphicsPath()) using(StringFormat sf=new StringFormat()){
      gp.AddString(s,f.FontFamily,(int)f.Style,f.SizeInPoints*96f/72f,new PointF(0,0),sf); RectangleF r=gp.GetBounds(); return HDR_MID-(r.Top+r.Height/2f); } }
  protected override void OnPaint(PaintEventArgs e){ Graphics g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    if(Cfg.Mini){ PaintMini(g); return; }
    Brush cb=new SolidBrush(CY), db=new SolidBrush(DIM), tb=new SolidBrush(TX);
    using(Pen p=new Pen(CY,1)){ g.DrawRectangle(p,0,0,Width-1,Height-1); g.DrawLine(p,0,34,Width,34); }
    using(Pen p=new Pen(CY,3)){ g.DrawLine(p,0,0,18,0); g.DrawLine(p,0,0,0,18); g.DrawLine(p,Width-1,Height-19,Width-1,Height-1); g.DrawLine(p,Width-19,Height-1,Width-1,Height-1); }
    using(StringFormat sf=new StringFormat()){ sf.Alignment=StringAlignment.Center; g.DrawString("S T A T I O N   K E E P E R",fT,cb,new RectangleF(0,InkY("S T A T I O N   K E E P E R",fT),Width,24),sf); } // centered on the full window width; ink centered in the header bar
    using(Brush vb=new SolidBrush(VDIM)) using(StringFormat vf=new StringFormat()){ vf.Alignment=StringAlignment.Far; g.DrawString(VER,fV,vb,new RectangleF(250,InkY(VER,fV),94,14),vf); } // subtle version, right of the title, left of the X
    g.DrawString("\u2715",fT,hoverClose?cb:db,354,7);
    int ox=tog.X, oy=tog.Y; Color col=on?GREEN:RED;
    if(on) for(int i=6;i>=1;i--) using(GraphicsPath gp=RR(ox+8-i,oy+8-i,110+2*i,44+2*i,44+2*i)) using(Brush b=new SolidBrush(Color.FromArgb(14,GREEN))) g.FillPath(b,gp);
    using(GraphicsPath gp=RR(ox+8,oy+8,110,44,44)){ using(Brush b=new SolidBrush(PANEL)) g.FillPath(b,gp); using(Pen p=new Pen(col,2)) g.DrawPath(p,gp); }
    using(Brush b=new SolidBrush(col)){ g.FillEllipse(b,ox+(on?78:14),oy+14,32,32); g.DrawString(on?"A R M E D":"O F F",fB,b,ox+135,oy+10); }
    g.DrawString(on?"PULSE ARMED  //  CLICK TO TOGGLE":"PULSE OFF  //  MONITOR ONLY",fS,db,ox+137,oy+38);
    if(!warnOn){ Rectangle pr=new Rectangle(20,120,340,WH-8); using(Pen p=new Pen(Color.FromArgb(40,0,216,255))) g.DrawRectangle(p,pr); using(Brush b=new SolidBrush(Color.FromArgb(40,0,216,255))) g.FillRectangle(b,pr.X,pr.Y,5,pr.Height); TextRenderer.DrawText(g,"N O   A L E R T S",fS,new Rectangle(pr.X+5,pr.Y,pr.Width-5,pr.Height),Color.FromArgb(90,0,216,255),CF); }
    if(warnOn){ bool hi=blink%2==0; Color wc=scRunning?AMBER:ALERT; Rectangle wr=new Rectangle(20,120,340,WH-8);
      using(Brush b=new SolidBrush(Color.FromArgb(hi?70:35,wc))) g.FillRectangle(b,wr);
      using(Pen p=new Pen(Color.FromArgb(hi?255:140,wc),hi?2f:1f)) g.DrawRectangle(p,wr);
      using(Brush b=new SolidBrush(wc)){ g.FillRectangle(b,wr.X,wr.Y,5,wr.Height);
        TextRenderer.DrawText(g,scRunning?"/!\\  PULSE INEFFECTIVE - GAME NOT IN FOCUS":"/!\\  PULSE INEFFECTIVE - GAME NOT RUNNING",fH,new Rectangle(wr.X+5,wr.Y+3,wr.Width-5,14),wc,CF); }
      TextRenderer.DrawText(g,scRunning?"Tab back into the game or the next pulse does nothing.":"Start the game. The next pulse will send nothing.",fS,new Rectangle(wr.X+5,wr.Y+17,wr.Width-5,12),((SolidBrush)tb).Color,CF); }
    int sx=20, sy=124+wo; Rectangle sr=new Rectangle(sx,sy,340,186);
    using(Brush b=new SolidBrush(PANEL)) g.FillRectangle(b,sr);
    using(Pen p=new Pen(Color.FromArgb(80,0,216,255))) g.DrawRectangle(p,sx,sy,339,185);
    g.FillRectangle(cb,sx,sy,4,18); g.DrawString("S Y S T E M   S T A T U S",fH,cb,sx+10,sy+2);
    g.DrawString("NEXT PULSE",fS,db,sx+10,sy+30);
    g.DrawString(on?PT():"--:--",fM,on?cb:db,sx+8,sy+44);
    bool sc=fg=="StarCitizen";
    g.DrawString("TARGET WINDOW",fS,db,sx+190,sy+30);
    using(Brush b=new SolidBrush(sc?CY:RED)) g.FillEllipse(b,sx+190,sy+52,10,10);
    g.DrawString(sc?"STAR CITIZEN":"NOT IN FOCUS",fH,tb,sx+205,sy+47);
    g.DrawString("INTERVAL  "+min+"-"+max+" S",fS,db,sx+190,sy+72);
    using(Pen p=new Pen(Color.FromArgb(60,0,216,255))) g.DrawLine(p,sx+10,sy+100,sx+330,sy+100);
    g.DrawString("LAST EVENT",fS,db,sx+10,sy+106);
    g.DrawString(last,fL,tb,new RectangleF(sx+10,sy+124,320,56));
    // --- AFK window panel ---
    int ay=AY; bool counting=afkState=="IDLE IN GAME"||afkState=="TABBED OUT"||afkState=="WINDOW EXPIRED";
    Color ac=!counting?DIM:(afkLeft<=60?ALERT:(afkLeft<=180?AMBER:CY));
    using(Brush b=new SolidBrush(PANEL)) g.FillRectangle(b,sx,ay,340,118);
    using(Pen p=new Pen(Color.FromArgb(80,ac))) g.DrawRectangle(p,sx,ay,339,117);
    using(Brush b=new SolidBrush(counting?ac:CY)) { g.FillRectangle(b,sx,ay,4,18); g.DrawString("A F K   W I N D O W",fH,b,sx+10,ay+2); }
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
    TextRenderer.DrawText(g,"TEST",fS,test,((SolidBrush)cb).Color,CF);
    using(Pen p=new Pen(TopMost?CY:DIM,1.5f)) g.DrawRectangle(p,chk.X,chk.Y+3,14,14);
    if(TopMost){ using(Brush b=new SolidBrush(Color.FromArgb(40,0,216,255))) g.FillRectangle(b,chk.X+1,chk.Y+4,13,13);
      using(Pen p=new Pen(CY,2.2f)) g.DrawLines(p,new PointF[]{ new PointF(chk.X+3,chk.Y+10), new PointF(chk.X+6,chk.Y+14), new PointF(chk.X+12,chk.Y+6) }); }
    g.DrawString("A L W A Y S   O N   T O P",fS,TopMost?cb:db,chk.X+22,chk.Y+4);
    using(Pen p=new Pen(DIM,1.5f)) g.DrawRectangle(p,mchk.X,mchk.Y+3,14,14);
    g.DrawString("M I N I",fS,db,mchk.X+20,mchk.Y+4);
    using(Brush b=new SolidBrush(Color.FromArgb(50,0,216,255))) g.FillRectangle(b,opSl.X,opSl.Y+5,opSl.Width,4);
    float ox2=opSl.X+opSl.Width*(Cfg.MiniOpacity-10)/90f; g.FillRectangle(cb,opSl.X,opSl.Y+5,ox2-opSl.X,4); g.FillRectangle(cb,ox2-3,opSl.Y-1,6,16);
    g.DrawString(Cfg.MiniOpacity+"%",fS,tb,opSl.Right+5,mchk.Y+4); }
  public void MiniForShot(){ ApplyMini(true,false); }
  public void ArmForShot(){ on=true; left=rnd.Next(min,max+1); last=Ts()+"PULSE ARMED. Pulses only while the game is in focus."; Tick(null,null); }
  public static Icon MakeIcon(){ Bitmap b=new Bitmap(32,32); using(Graphics g=Graphics.FromImage(b)){ g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
      using(Brush br=new SolidBrush(BG)) g.FillEllipse(br,1,1,30,30); using(Pen p=new Pen(CY,2)) g.DrawEllipse(p,2,2,28,28); using(Brush br=new SolidBrush(CY)) g.FillEllipse(br,11,11,10,10); }
    return Icon.FromHandle(b.GetHicon()); }
}
static class Program {
  [STAThread] static void Main(string[] a){ int mn=180, mx=300; string shot=null; bool armed=false;
    for(int i=0;i<a.Length-1;i++){ if(a[i]=="--min") int.TryParse(a[i+1],out mn); if(a[i]=="--max") int.TryParse(a[i+1],out mx); if(a[i]=="--screenshot") shot=a[i+1]; }
    bool mini=false; foreach(string x in a){ if(x=="--screenshot-armed") armed=true; if(x=="--screenshot-mini") mini=true; }
    if(shot!=null) Cfg.NoLoad=true;
    if(a.Length>0 && a[0]=="--make-icon"){ using(var fs=File.Create(a[1])) AfkForm.MakeIcon().Save(fs); return; }
    if(mn<1) mn=1; if(mx<mn) mx=mn;
    Application.EnableVisualStyles(); AfkForm f=new AfkForm(mn,mx); if(armed) f.ArmForShot(); if(mini && shot!=null) f.MiniForShot();
    if(shot!=null){ Timer t2=new Timer(); t2.Interval=1200; t2.Tick+=delegate{ t2.Stop(); Bitmap b=new Bitmap(f.Width,f.Height); f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height)); b.Save(shot); f.Close(); }; t2.Start(); }
    Application.Run(f); }
}