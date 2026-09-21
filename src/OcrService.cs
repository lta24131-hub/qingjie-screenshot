using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace QingJie {
    public sealed class WordBox { public string Text; public Rect Box; public int Line; public int TextStart; }
    public sealed class OcrPage { public string Text; public List<WordBox> Words = new List<WordBox>(); }
    public static class OcrService {
        public static void IndexText(OcrPage page){var text=new System.Text.StringBuilder();int line=-1;foreach(var word in page.Words){if(text.Length>0){if(line!=word.Line)text.AppendLine();else if(text[text.Length-1]<0x2e80&&word.Text.Length>0&&word.Text[0]<0x2e80)text.Append(' ');}word.TextStart=text.Length;text.Append(word.Text);line=word.Line;}page.Text=text.ToString();}
        private static Task<T> Wait<T>(Windows.Foundation.IAsyncOperation<T> operation) {
            var completion = new TaskCompletionSource<T>();
            operation.Completed = (op, status) => {
                try { if (status == Windows.Foundation.AsyncStatus.Canceled) completion.TrySetCanceled(); else completion.TrySetResult(op.GetResults()); }
                catch (Exception error) { completion.TrySetException(error); }
            };
            return completion.Task;
        }
        public static string Languages() {
            var result = new List<string>();
            foreach (var lang in OcrEngine.AvailableRecognizerLanguages) result.Add(lang.LanguageTag);
            return string.Join(", ", result);
        }
        public static async Task<OcrPage> Read(BitmapSource source) {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            // Prefer a Chinese recognizer when installed; it also recognizes Latin text.
            foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                if (lang.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) { engine = OcrEngine.TryCreateFromLanguage(lang); break; }
            if (engine == null) throw new InvalidOperationException("Windows 尚未安装文字识别语言。请在系统的语言选项中添加中文或英文 OCR 组件。");
            // Small, anti-aliased website text needs more pixels than the original
            // screenshot gives Windows OCR. Keep the inverse coordinate mapping.
            double ratio = Math.Min(3.0, (double)OcrEngine.MaxImageDimension / Math.Max(source.PixelWidth, source.PixelHeight));
            BitmapSource input = Prepare(source, ratio);
            byte[] png;
            using (var memory = new System.IO.MemoryStream()) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(input)); encoder.Save(memory); png = memory.ToArray(); }
            using (var stream = new InMemoryRandomAccessStream()) {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(png); await Wait<uint>(writer.StoreAsync()); await Wait<bool>(writer.FlushAsync()); }
                stream.Seek(0);
                var decoder = await Wait(Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream));
                using (var bitmap = await Wait(decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))) {
                    var result = await Wait(engine.RecognizeAsync(bitmap));
                    var page = new OcrPage { Text = result.Text };
                    int line = 0;
                    foreach (var l in result.Lines) {
                        foreach (var word in l.Words) { var b = word.BoundingRect; page.Words.Add(new WordBox { Text=word.Text, Line=line, Box=new Rect(b.X/ratio,b.Y/ratio,b.Width/ratio,b.Height/ratio) }); }
                        line++;
                    }
                    IndexText(page);return await EnglishOcr.Improve(png,ratio,page);
                }
            }
        }
        public static BitmapSource Prepare(BitmapSource source,double ratio){
            var bgra=new FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Bgra32,null,0);
            int width=source.PixelWidth,height=source.PixelHeight;var pixels=new byte[checked(width*height*4)];bgra.CopyPixels(pixels,width*4,0);
            int dark=0,samples=0;for(int i=0;i<pixels.Length;i+=64){if((pixels[i]*.0722+pixels[i+1]*.7152+pixels[i+2]*.2126)<100)dark++;samples++;}
            bool invert=dark>samples*.6;
            for(int i=0;i<pixels.Length;i+=4){int gray=(int)(pixels[i]*.0722+pixels[i+1]*.7152+pixels[i+2]*.2126);gray=(gray*pixels[i+3]+255*(255-pixels[i+3]))/255;if(invert)gray=255-gray;pixels[i]=pixels[i+1]=pixels[i+2]=(byte)gray;pixels[i+3]=255;}
            var normalized=BitmapSource.Create(width,height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,width*4);normalized.Freeze();
            var scaled=new TransformedBitmap(normalized,new System.Windows.Media.ScaleTransform(ratio,ratio));scaled.Freeze();return scaled;
        }
    }
}
