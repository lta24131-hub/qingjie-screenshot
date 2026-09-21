using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tesseract;
using Rect=System.Windows.Rect;

namespace QingJie {
    // Small local English model: no server, browser runtime or system language install.
    public static class EnglishOcr {
        static readonly object gate=new object();
        static TesseractEngine engine;
        sealed class Line {public readonly List<WordBox> Words=new List<WordBox>();public float Confidence;public Rect Bounds=Rect.Empty;}
        public static Task<OcrPage> Improve(byte[] png,double ratio,OcrPage original){return Task.Run(()=>{
            lock(gate){
                if(engine==null)engine=new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tessdata"),"eng",EngineMode.LstmOnly);
                var lines=new List<Line>();
                using(var pix=Pix.LoadFromMemory(png))using(var page=engine.Process(pix,PageSegMode.SparseText))using(var it=page.GetIterator()){
                    it.Begin();Line line=null;
                    do{
                        if(line==null||it.IsAtBeginningOf(PageIteratorLevel.TextLine)){line=new Line{Confidence=it.GetConfidence(PageIteratorLevel.TextLine)};lines.Add(line);}
                        Tesseract.Rect b;string text=it.GetText(PageIteratorLevel.Word);
                        if(string.IsNullOrWhiteSpace(text)||!it.TryGetBoundingBox(PageIteratorLevel.Word,out b))continue;
                        var box=new System.Windows.Rect(b.X1/ratio,b.Y1/ratio,b.Width/ratio,b.Height/ratio);
                        line.Words.Add(new WordBox{Text=text.Trim(),Box=box});line.Bounds.Union(box);
                    }while(it.Next(PageIteratorLevel.Word));
                }
                var oldLines=original.Words.GroupBy(w=>w.Line).Select(g=>g.ToList()).ToList();
                foreach(var line in lines){
                    string text=Geometry.JoinWords(line.Words);
                    if(line.Confidence<70||text.Count(char.IsLetter)<2)continue;
                    var matches=oldLines.Where(g=>Overlaps(Bounds(g),line.Bounds)).ToList();
                    string prior=string.Join("",matches.SelectMany(g=>g).Select(w=>w.Text));
                    // English-only OCR must never replace genuine Chinese paragraphs.
                    if(prior.Count(c=>c>=0x3400&&c<=0x9fff)>Math.Max(2,prior.Length*.3))continue;
                    if(matches.Count==0&&line.Confidence<85)continue;
                    foreach(var group in matches)oldLines.Remove(group);
                    oldLines.Add(line.Words);
                }
                var result=new OcrPage();int number=0;
                foreach(var line in oldLines.OrderBy(g=>Bounds(g).Top).ThenBy(g=>Bounds(g).Left)){foreach(var w in line){w.Line=number;result.Words.Add(w);}number++;}
                OcrService.IndexText(result);return result;
            }
        });}
        static Rect Bounds(IEnumerable<WordBox> words){var r=Rect.Empty;foreach(var w in words)r.Union(w.Box);return r;}
        static bool Overlaps(Rect a,Rect b){var r=Rect.Intersect(a,b);return !r.IsEmpty&&r.Width*r.Height>Math.Min(a.Width*a.Height,b.Width*b.Height)*.35;}
    }
}
