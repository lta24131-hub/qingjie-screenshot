using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Original code-native icon matching QingJie's green screenshot controls.
class BuildIcon {
    static SolidColorBrush Color(string value){return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));}
    [STAThread] static void Main(string[] args){
        var frames=new List<byte[]>();int[] sizes={256,64,48,32,24,16};
        foreach(int size in sizes){
            var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
                d.PushTransform(new ScaleTransform(size/256.0,size/256.0));
                d.DrawRoundedRectangle(Color("#079B73"),null,new Rect(8,8,240,240),52,52);
                var line=new Pen(Brushes.White,16){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                d.DrawGeometry(null,line,Geometry.Parse("M 60,100 L 60,64 L 96,64 M 160,64 L 196,64 L 196,100 M 60,156 L 60,192 L 96,192 M 160,192 L 196,192 L 196,156"));
                d.DrawRoundedRectangle(Color("#BCF4DC"),null,new Rect(92,101,72,54),7,7);
                d.DrawLine(new Pen(Color("#079B73"),7),new Point(105,120),new Point(151,120));
                d.DrawLine(new Pen(Color("#079B73"),7),new Point(105,136),new Point(138,136));d.Pop();
            }
            var image=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);image.Render(visual);
            using(var stream=new MemoryStream()){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));png.Save(stream);frames.Add(stream.ToArray());}
        }
        Directory.CreateDirectory(args[0]);File.WriteAllBytes(Path.Combine(args[0],"qingjie-icon.png"),frames[0]);
        using(var w=new BinaryWriter(File.Create(Path.Combine(args[0],"qingjie.ico")))){
            w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(frames[i].Length);w.Write(offset);offset+=frames[i].Length;}
            foreach(var frame in frames)w.Write(frame);
        }
    }
}
