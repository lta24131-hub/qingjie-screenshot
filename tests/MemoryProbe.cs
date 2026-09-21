using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QingJie;
class MemoryProbe {
    static T Await<T>(Task<T> task){var frame=new System.Windows.Threading.DispatcherFrame();task.ContinueWith(t=>frame.Continue=false);System.Windows.Threading.Dispatcher.PushFrame(frame);return task.GetAwaiter().GetResult();}
    [STAThread] static int Main(){try{
        var drawing=new DrawingVisual();using(var d=drawing.RenderOpen()){
            d.DrawRectangle(Brushes.Black,null,new Rect(0,0,960,420));
            string[] lines={"Ready. Create. Fold.","Gimbal first, then body up. Body down, then gimbal folds.","Manually adjustable mast","Set camera height by hand, independent of wheel lifts."};
            for(int i=0;i<lines.Length;i++)d.DrawText(new FormattedText(lines[i],System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),i%2==0?18:16,Brushes.White,1),new Point(80,60+i*60));
        }
        var image=new RenderTargetBitmap(960,420,96,96,PixelFormats.Pbgra32);image.Render(drawing);image.Freeze();
        long peak=0;bool sampling=true;var own=Process.GetCurrentProcess();string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"QingJie.exe");
        var sampler=Task.Run(()=>{while(Volatile.Read(ref sampling)){try{own.Refresh();long total=own.PrivateMemorySize64;foreach(var p in Process.GetProcessesByName("QingJie")){using(p){try{if(string.Equals(p.MainModule.FileName,executable,StringComparison.OrdinalIgnoreCase))total+=p.PrivateMemorySize64;}catch{}}}if(total>peak)peak=total;}catch{}Thread.Sleep(25);}});
        for(int i=0;i<3;i++){var watch=Stopwatch.StartNew();var page=Await(OcrService.Read(image));Console.WriteLine("OCR "+i+" ms="+watch.ElapsedMilliseconds+" text="+page.Text.Replace(Environment.NewLine," | "));}
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Thread.Sleep(500);own.Refresh();Volatile.Write(ref sampling,false);sampler.Wait();
        Console.WriteLine("idle_private_bytes="+own.PrivateMemorySize64+" idle_working_bytes="+own.WorkingSet64+" peak_combined_private_bytes="+peak);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
