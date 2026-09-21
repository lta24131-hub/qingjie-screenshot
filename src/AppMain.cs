using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace QingJie {
    public static class AppState {
        public static Preferences Settings=Preferences.Load();
        public static PinStore Pins;
        public static bool Quitting;
        public static readonly List<CaptureWindow> Captures=new List<CaptureWindow>();
        public static Forms.NotifyIcon Tray;
        public static WelcomeWindow Welcome;
        static SettingsWindow settingsWindow;
        public static void ShowSettings(){if(settingsWindow==null){settingsWindow=new SettingsWindow();settingsWindow.Closed+=(s,e)=>settingsWindow=null;}settingsWindow.Show();settingsWindow.Activate();}
        public static void Notify(string message){if(Tray!=null){Tray.BalloonTipTitle="轻截";Tray.BalloonTipText=message;Tray.ShowBalloonTip(3000);}}
        public static void ChooseCapture(CaptureWindow chosen){foreach(var w in Captures.ToList())if(w!=chosen)w.Close();}
        public static void CancelCapture(){foreach(var w in Captures.ToList())w.Close();}
        public static void BeginCapture(string testImage=null){
            if(Captures.Count>0){Captures[0].Activate();return;}
            if(Welcome!=null)Welcome.Hide();
            var delay=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(130)};
            delay.Tick+=(s,e)=>{delay.Stop();try{
                var shots=new List<Snapshot>();
                if(testImage!=null){var img=Snapshot.Load(testImage);var screen=Forms.Screen.PrimaryScreen;shots.Add(new Snapshot{Image=img,Bounds=new System.Drawing.Rectangle(screen.Bounds.X,screen.Bounds.Y,img.PixelWidth,img.PixelHeight)});}
                else foreach(var screen in Forms.Screen.AllScreens)shots.Add(Snapshot.Screen(screen));
                foreach(var shot in shots){var w=new CaptureWindow(shot);Captures.Add(w);w.Show();}
            }catch(Exception ex){MessageBox.Show("无法截屏："+ex.Message,"轻截");}};delay.Start();
        }
        public static void Paste(bool recover=true){try{if(recover&&Pins.Recover())return;if(Clipboard.ContainsImage())Pins.New(Clipboard.GetImage());else Notify("没有可找回的贴图。先截图并复制，再按 F3 贴到屏幕。");}catch(Exception ex){Notify("贴图失败："+ex.Message);}}
        public static void ShowWelcome(){if(Welcome==null){Welcome=new WelcomeWindow();Welcome.Closed+=(s,e)=>Welcome=null;}Welcome.Show();Welcome.Activate();}
    }
    public sealed class WelcomeWindow : Window {
        public WelcomeWindow(){Title="轻截";Icon=Ui.AppIcon;Width=430;Height=460;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");
            var panel=new StackPanel {Margin=new Thickness(28)};panel.Children.Add(new TextBlock {Text="轻截",FontSize=30,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});panel.Children.Add(new TextBlock {Text="截图 · 标注 · 贴图 · 文字翻译",Foreground=Ui.Brush("#718078"),Margin=new Thickness(0,6,0,23)});
            panel.Children.Add(new TextBlock {Text="F1     截图，选区后展开工具栏\nF3     找回收起的贴图 / 粘贴剪贴板图片\n滚轮   标注时调粗细，贴图时调大小",FontSize=14,LineHeight=28});
            panel.Children.Add(Ui.TextButton("开始截图  F1",()=>AppState.BeginCapture(),true));
            panel.Children.Add(Ui.TextButton("打开图片 · 识别 / 翻译",()=>{var dialog=new Microsoft.Win32.OpenFileDialog {Filter="图片|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(this)==true){try{new TextWindow(Snapshot.Load(dialog.FileName)).Show();Hide();}catch(Exception ex){MessageBox.Show(this,ex.Message,"轻截");}}}));
            panel.Children.Add(Ui.TextButton("设置",AppState.ShowSettings));panel.Children.Add(new TextBlock {Text="文字选择：截图后点工具栏的文字识别按钮。\n只在点击翻译时联网；贴图保存在本机。\n关闭此窗口后继续在托盘运行。",FontSize=12,Foreground=Ui.Brush("#7F8983"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});Content=panel;
        }
    }
    public static class Program {
        static HwndSource hotkeys;static Mutex mutex;static EventWaitHandle wake,quitEvent;static RegisteredWaitHandle waiter,quitWaiter;
        static bool diagnostics;
        static void Trace(string message){if(diagnostics)File.AppendAllText(Path.Combine(Path.GetTempPath(),"QingJie-startup.log"),DateTime.Now.ToString("O")+" "+message+Environment.NewLine);}
        [STAThread] public static void Main(string[] args){
            string worker=args.FirstOrDefault(a=>a.StartsWith("--ocr-worker=",StringComparison.Ordinal));
            if(worker!=null){Environment.ExitCode=OcrWorker.Execute(worker.Substring("--ocr-worker=".Length));return;}
            diagnostics=args.Contains("--diagnostics");Trace("Main");
            bool created;mutex=new Mutex(true,"Local\\QingJie.NativeScreenshot",out created);
            if(!created){try{EventWaitHandle.OpenExisting(args.Contains("--quit")?"Local\\QingJie.QuitRequested":"Local\\QingJie.CaptureRequested").Set();}catch{}return;}
            if(args.Contains("--quit")){mutex.ReleaseMutex();mutex.Dispose();return;}
            wake=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\QingJie.CaptureRequested");
            quitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\QingJie.QuitRequested");
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show("操作未完成："+e.Exception.Message,"轻截");e.Handled=true;};
            app.SessionEnding+=(s,e)=>{AppState.Quitting=true;try{AppState.Pins.PersistAll();}catch{}};
            app.Startup+=(s,e)=>{
                Trace("Startup");
                AppState.Pins=new PinStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"QingJie","pins"));
                Trace("Pins loaded");
                var param=new HwndSourceParameters("QingJie.Hotkeys"){WindowStyle=0,Width=0,Height=0,ParentWindow=new IntPtr(-3)};hotkeys=new HwndSource(param);hotkeys.AddHook(Hotkey);
                Trace("Hotkey source");
                AppState.Tray=new Forms.NotifyIcon {Text="轻截 · F1 截图 / F3 贴图找回",Icon=System.Drawing.Icon.ExtractAssociatedIcon(typeof(Program).Assembly.Location),Visible=true};
                Trace("Tray");
                var menu=new Forms.ContextMenuStrip();menu.Items.Add("截图  F1",null,(o,a)=>AppState.BeginCapture());menu.Items.Add("找回贴图 / 粘贴  F3",null,(o,a)=>AppState.Paste());menu.Items.Add("粘贴剪贴板图片",null,(o,a)=>AppState.Paste(false));menu.Items.Add("使用说明",null,(o,a)=>AppState.ShowWelcome());menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出（下次恢复贴图）",null,(o,a)=>Quit());AppState.Tray.ContextMenuStrip=menu;AppState.Tray.DoubleClick+=(o,a)=>AppState.BeginCapture();
                menu.Items.Insert(3,new Forms.ToolStripMenuItem("设置",null,(o,a)=>AppState.ShowSettings()));
                bool f1=Native.RegisterHotKey(hotkeys.Handle,1,0x4000,0x70),f3=Native.RegisterHotKey(hotkeys.Handle,3,0x4000,0x72);
                Trace("Hotkeys "+f1+" "+f3);
                if(!f1||!f3)AppState.Notify("快捷键被其他软件占用，请退出 Snipaste/eSearch 后重新打开轻截。");
                waiter=ThreadPool.RegisterWaitForSingleObject(wake,(o,t)=>app.Dispatcher.BeginInvoke(new Action(()=>AppState.BeginCapture())),null,-1,false);
                quitWaiter=ThreadPool.RegisterWaitForSingleObject(quitEvent,(o,t)=>app.Dispatcher.BeginInvoke(new Action(Quit)),null,-1,false);
                AppState.Pins.Restore();
                Trace("Pins restored");
                string test=args.FirstOrDefault(a=>a.StartsWith("--test-image="));
                if(test!=null)AppState.BeginCapture(test.Substring("--test-image=".Length));else if(args.Contains("--capture"))AppState.BeginCapture();else if(!args.Contains("--background"))AppState.ShowWelcome();
                Trace("Startup complete");
            };
            app.Exit+=(s,e)=>{if(hotkeys!=null){Native.UnregisterHotKey(hotkeys.Handle,1);Native.UnregisterHotKey(hotkeys.Handle,3);hotkeys.Dispose();}if(AppState.Tray!=null)AppState.Tray.Dispose();if(waiter!=null)waiter.Unregister(null);if(quitWaiter!=null)quitWaiter.Unregister(null);wake.Dispose();quitEvent.Dispose();mutex.ReleaseMutex();mutex.Dispose();};
            app.Run();
        }
        static IntPtr Hotkey(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x312){handled=true;if(w.ToInt32()==1)AppState.BeginCapture();else if(w.ToInt32()==3)AppState.Paste();}return IntPtr.Zero;}
        static void Quit(){try{AppState.Pins.PersistAll();AppState.Quitting=true;Application.Current.Shutdown();}catch(Exception ex){MessageBox.Show("贴图状态保存失败，未退出："+ex.Message,"轻截");}}
    }
}
