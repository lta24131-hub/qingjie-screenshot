using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QingJie {
    // Exactly one small transcript, never the screenshot or a live OCR engine.
    internal sealed class OcrResultCache {
        string key;
        OcrPage page;
        long expires;
        static long Now{get{return System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;}}
        public static string Fingerprint(BitmapSource source,CancellationToken cancel){
            var pixels=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
            int stride=checked(source.PixelWidth*4);var rows=new byte[checked(stride*Math.Min(16,source.PixelHeight))];
            using(var hash=SHA256.Create()){
                for(int y=0;y<source.PixelHeight;y+=16){cancel.ThrowIfCancellationRequested();int count=Math.Min(16,source.PixelHeight-y);pixels.CopyPixels(new Int32Rect(0,y,source.PixelWidth,count),rows,stride,0);hash.TransformBlock(rows,0,stride*count,null,0);}
                hash.TransformFinalBlock(new byte[0],0,0);return source.PixelWidth+"x"+source.PixelHeight+":"+Convert.ToBase64String(hash.Hash);
            }
        }
        static OcrPage Copy(OcrPage source){return new OcrPage{Text=source.Text,Words=source.Words.Select(w=>new WordBox{Text=w.Text,Line=w.Line,Box=w.Box,TextStart=w.TextStart}).ToList()};}
        // Calls are serialized by OcrWorker's existing queue.
        public OcrPage Get(string fingerprint){if(Now>=expires){key=null;page=null;}return page!=null&&key==fingerprint?Copy(page):null;}
        public void Put(string fingerprint,OcrPage result){
            key=null;page=null;
            if(result.Words.Count==0||result.Words.Count>500||result.Text.Length>16000)return;
            key=fingerprint;page=Copy(result);expires=Now+120;
        }
    }
}
