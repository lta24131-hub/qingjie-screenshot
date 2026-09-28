using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using QingJie;
// Synthetic-only UI host. No global hotkeys, user pins or private screenshots.
class VisualSmoke {
    [STAThread] static void Main(string[] args){
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        AppState.Pins=new PinStore(Path.Combine(Path.GetTempPath(),"QingJie-Visual-"+Guid.NewGuid().ToString("N")));
        AppState.Settings=new Preferences();
        OcrLanguagePacks.TestSelection="";
        OcrLanguagePacks.TestFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-languages");
        app.Startup+=(s,e)=>{
            if(args.Contains("--languages")){
                var selection=new OcrLanguageSelection(AppState.Settings,()=>{},id=>OcrLanguagePacks.Installed(id),id=>{});
                var panel=new OcrLanguagePanel(selection,(id,progress,token)=>System.Threading.Tasks.Task.FromResult(true));
                var window=new Window {Title="Luma · 语言多选测试",Width=500,Height=600,Content=new System.Windows.Controls.ScrollViewer {Content=panel},WindowStartupLocation=WindowStartupLocation.CenterScreen};window.Closed+=(a,b)=>panel.Dispose();window.Show();
            }else if(args.Contains("--capture")){
                var source=Snapshot.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"english-dark-test.png"));
                var capture=new CaptureWindow(new Snapshot {Image=source,Bounds=new System.Drawing.Rectangle(100,100,source.PixelWidth,source.PixelHeight)});AppState.Captures.Add(capture);capture.Show();
            }else if(args.Contains("--pin")){AppState.Pins.New(Snapshot.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mixed-test.png")));
            }else if(args.Contains("--glass")){
                var background=new System.Windows.Controls.Grid();
                foreach(string color in new[]{"#68AAB3","#DDB677","#A697BC","#83B48B"}){int column=background.ColumnDefinitions.Count;background.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());var stripe=new System.Windows.Controls.Border {Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)};System.Windows.Controls.Grid.SetColumn(stripe,column);background.Children.Add(stripe);}
                var label=new System.Windows.Controls.TextBlock {Text="BACKDROP  TEST",FontSize=42,Foreground=System.Windows.Media.Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};System.Windows.Controls.Grid.SetColumnSpan(label,4);background.Children.Add(label);
                var sample=new Window {Title="Luma · 合成背景测试",Width=650,Height=450,WindowStartupLocation=WindowStartupLocation.CenterScreen,Content=background};bool closing=false;sample.Closing+=(a,b)=>closing=true;sample.Show();var writer=new WriteTranslationWindow {Owner=sample,WindowStartupLocation=WindowStartupLocation.CenterOwner};writer.Closed+=(a,b)=>{if(!closing)sample.Close();};writer.Show();
            }else if(args.Contains("--settings"))new SettingsWindow().Show();else if(args.Contains("--image"))new TextWindow(Snapshot.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"mixed-test.png")),"en").Show();else new WriteTranslationWindow().Show();
            foreach(Window window in app.Windows)window.ShowInTaskbar=true;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
            timer.Tick+=(a,b)=>{if(!app.Windows.Cast<Window>().Any()){timer.Stop();app.Shutdown();}};timer.Start();
        };app.Run();
    }
}
