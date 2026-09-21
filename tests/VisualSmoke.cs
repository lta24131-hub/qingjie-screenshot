using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using QingJie;
// Synthetic-only UI host. No global hotkeys, user pins or private screenshots.
class VisualSmoke {
    [STAThread] static void Main(){
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        AppState.Pins=new PinStore(Path.Combine(Path.GetTempPath(),"QingJie-Visual-"+Guid.NewGuid().ToString("N")));
        app.Startup+=(s,e)=>{
            var image=Snapshot.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"english-dark-test.png"));
            AppState.Pins.NewAt(image,new Int32Rect(450,240,600,263));
            foreach(Window window in app.Windows)window.ShowInTaskbar=true;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
            timer.Tick+=(a,b)=>{if(!app.Windows.Cast<Window>().Any()){timer.Stop();app.Shutdown();}};timer.Start();
        };app.Run();
    }
}
