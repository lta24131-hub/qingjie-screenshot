using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Luma: the selected charcoal tile with four white, rounded capture corners.
// Generated at build time; no image runtime or user reference file is bundled.
class BuildIcon {
    static SolidColorBrush Color(string value){return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));}
    [STAThread] static void Main(string[] args){
        var frames=new List<byte[]>();int[] sizes={256,64,48,32,24,16};
        foreach(int size in sizes){
            var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
                d.PushTransform(new ScaleTransform(size/256.0,size/256.0));
                d.DrawRoundedRectangle(Color("#292929"),null,new Rect(8,8,240,240),56,56);
                var line=new Pen(Brushes.White,18){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                d.DrawGeometry(null,line,Geometry.Parse("M 62,101 L 62,64 L 99,64"));
                d.Pop();
            }
            var image=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);image.Render(visual);
            // Mirror one quadrant to keep the four corners pixel-symmetric at every size.
            byte[] pixels=new byte[size*size*4];image.CopyPixels(pixels,size*4,0);
            for(int y=0;y<size/2;y++)for(int x=0;x<size/2;x++)for(int channel=0;channel<4;channel++){
                byte value=pixels[(y*size+x)*4+channel];
                pixels[(y*size+size-1-x)*4+channel]=value;
                pixels[((size-1-y)*size+x)*4+channel]=value;
                pixels[((size-1-y)*size+size-1-x)*4+channel]=value;
            }
            var mirrored=BitmapSource.Create(size,size,96,96,PixelFormats.Pbgra32,null,pixels,size*4);
            using(var stream=new MemoryStream()){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(mirrored));png.Save(stream);frames.Add(stream.ToArray());}
        }
        Directory.CreateDirectory(args[0]);File.WriteAllBytes(Path.Combine(args[0],"qingjie-icon.png"),frames[0]);
        using(var w=new BinaryWriter(File.Create(Path.Combine(args[0],"qingjie.ico")))){
            w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(frames[i].Length);w.Write(offset);offset+=frames[i].Length;}
            foreach(var frame in frames)w.Write(frame);
        }
    }
}
