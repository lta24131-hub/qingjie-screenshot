using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Luma: a four-way light mark, symmetric about both axes, with no frame or tile.
class BuildIcon {
    static SolidColorBrush Color(string value){return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));}
    [STAThread] static void Main(string[] args){
        var frames=new List<byte[]>();int[] sizes={256,64,48,32,24,16};
        foreach(int size in sizes){
            var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
                d.PushTransform(new ScaleTransform(size/256.0,size/256.0));
                var mark=Geometry.Parse("M128,28 C128,90 166,128 228,128 C166,128 128,166 128,228 C128,166 90,128 28,128 C90,128 128,90 128,28 Z");
                d.DrawGeometry(Color("#7C7F84"),null,mark);
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
