using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Luma: two rising beams, one monochrome silhouette on a transparent canvas.
class BuildIcon {
    static SolidColorBrush Color(string value){return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));}
    [STAThread] static void Main(string[] args){
        var frames=new List<byte[]>();int[] sizes={256,64,48,32,24,16};
        foreach(int size in sizes){
            var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
                d.PushTransform(new ScaleTransform(size/256.0,size/256.0));
                var line=new Pen(Color("#7C7F84"),size<=24?35:30){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                d.DrawLine(line,new Point(67,175),new Point(127,45));
                d.DrawLine(line,new Point(128,211),new Point(188,81));
                d.Pop();
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
