using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tesseract;
using Rect=System.Windows.Rect;

namespace QingJie {
    // Local recognition models live only for this job in the short-lived OCR worker.
    public static class EnglishOcr {
        static readonly object gate=new object();
        sealed class Line {public readonly List<WordBox> Words=new List<WordBox>();public float Confidence;public Rect Bounds=Rect.Empty;}
        public static Task<OcrPage> Improve(byte[] png,double ratio,OcrPage original,string extraLanguage=""){return Task.Run(()=>{
            lock(gate)using(var pix=Pix.LoadFromMemory(png)){
                var data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tessdata");
                List<Line> lines;
                var oldLines=original.Words.GroupBy(w=>w.Line).Select(g=>g.ToList()).ToList();
                using(var engine=new TesseractEngine(data,"eng",EngineMode.LstmOnly)){
                    lines=ReadLines(engine,pix,ratio,PageSegMode.SparseText);
                    Merge(oldLines,lines,"");
                    engine.SetVariable("thresholding_method",2);engine.SetVariable("thresholding_kfactor",0.04);
                    var faint=ReadLines(engine,pix,ratio,PageSegMode.Auto);
                    Merge(oldLines,faint.Where(l=>!lines.Any(prior=>prior.Confidence>=70&&Overlaps(prior.Bounds,l.Bounds))).ToList(),"");
                }
                // Default English mode never opens an optional model. Only one
                // explicitly selected language is processed, after releasing English.
                if(!string.IsNullOrEmpty(extraLanguage)){
                if(!OcrLanguagePacks.Installed(extraLanguage))throw new InvalidOperationException("附加语言包尚未安装，请在设置中安装或切回英文优先。");
                using(var engine=new TesseractEngine(OcrLanguagePacks.Folder,extraLanguage,EngineMode.LstmOnly)){
                    engine.SetVariable("thresholding_method",2);
                    engine.SetVariable("thresholding_kfactor",0.04);
                    var paragraphs=ReadLines(engine,pix,ratio,PageSegMode.Auto);
                    Merge(oldLines,paragraphs,extraLanguage);
                    var sparse=JoinFragments(ReadLines(engine,pix,ratio,PageSegMode.SparseText));
                    Merge(oldLines,sparse.Where(l=>!paragraphs.Any(p=>IsSupplemental(p,extraLanguage)&&Overlaps(p.Bounds,l.Bounds))).ToList(),extraLanguage);
                }}
                var result=new OcrPage();int number=0;
                foreach(var line in oldLines.OrderBy(g=>Bounds(g).Top).ThenBy(g=>Bounds(g).Left)){foreach(var w in line){w.Line=number;result.Words.Add(w);}number++;}
                OcrService.IndexText(result);return result;
            }
        });}
        static List<Line> ReadLines(TesseractEngine engine,Pix pix,double ratio,PageSegMode mode){
                var lines=new List<Line>();
                using(var page=engine.Process(pix,mode))using(var it=page.GetIterator()){
                    it.Begin();Line line=null;string lineText="";int textEnd=0;
                    do{
                        if(line==null||it.IsAtBeginningOf(PageIteratorLevel.TextLine)){line=new Line{Confidence=it.GetConfidence(PageIteratorLevel.TextLine)};lines.Add(line);lineText=it.GetText(PageIteratorLevel.TextLine)??"";textEnd=0;}
                        Tesseract.Rect b;string text=it.GetText(PageIteratorLevel.Word);
                        if(string.IsNullOrWhiteSpace(text)||!it.TryGetBoundingBox(PageIteratorLevel.Word,out b))continue;
                        var box=new System.Windows.Rect(b.X1/ratio,b.Y1/ratio,b.Width/ratio,b.Height/ratio);
                        text=text.Trim();int start=lineText.IndexOf(text,textEnd,StringComparison.Ordinal);
                        // Tesseract can expose each Korean syllable as a Word even
                        // though its line transcript correctly has no space there.
                        if(start==textEnd&&line.Words.Count>0&&text.Any(JoinedScript)&&line.Words.Last().Text.Any(JoinedScript)){
                            var previous=line.Words.Last();previous.Text+=text;previous.Box=Rect.Union(previous.Box,box);
                        }else line.Words.Add(new WordBox{Text=text,Box=box});
                        if(start>=0)textEnd=start+text.Length;
                        line.Bounds.Union(box);
                    }while(it.Next(PageIteratorLevel.Word));
                }
                return lines;
        }
        static bool JoinedScript(char c){return Geometry.IsHangul(c)||OcrLanguagePacks.IsScript(c,"tha");}
        static void Merge(List<List<WordBox>> oldLines,List<Line> lines,string language){
                bool supplemental=!string.IsNullOrEmpty(language);
                foreach(var line in lines){
                    string text=Geometry.JoinWords(line.Words);
                    if(line.Confidence<70||text.Count(char.IsLetter)<2)continue;
                    if(supplemental&&!IsSupplemental(line,language))continue;
                    var matches=oldLines.Where(g=>Overlaps(Bounds(g),line.Bounds)).ToList();
                    string prior=string.Join("",matches.SelectMany(g=>g).Select(w=>w.Text));
                    // English-only OCR must never replace genuine Chinese paragraphs.
                    int chinese=prior.Count(c=>c>=0x3400&&c<=0x9fff);
                    if((!supplemental||OcrLanguagePacks.IsLatin(language))&&(chinese>=4||chinese>Math.Max(2,prior.Length*.3)))continue;
                    // A small fragment must not erase a complete Windows OCR line.
                    if(supplemental&&matches.Any(g=>Rect.Intersect(Bounds(g),line.Bounds).Width<Bounds(g).Width*.65))continue;
                    if(matches.Count==0&&line.Confidence<85)continue;
                    foreach(var group in matches)oldLines.Remove(group);
                    oldLines.Add(line.Words);
                }
        }
        static bool IsSupplemental(Line line,string language){string text=Geometry.JoinWords(line.Words);int count=text.Count(c=>OcrLanguagePacks.IsScript(c,language));return count>=2&&line.Confidence>=(count<4?90:85)&&(language=="jpn"||count>=text.Count(char.IsLetter)*.55);}
        static List<Line> JoinFragments(List<Line> lines){
            var result=new List<Line>();
            foreach(var line in lines.OrderBy(l=>l.Bounds.Left)){
                var prior=result.LastOrDefault(p=>!p.Bounds.IsEmpty&&!line.Bounds.IsEmpty&&Math.Abs(p.Bounds.Top-line.Bounds.Top)<Math.Min(p.Bounds.Height,line.Bounds.Height)*.4&&line.Bounds.Left-p.Bounds.Right>=0&&line.Bounds.Left-p.Bounds.Right<Math.Max(p.Bounds.Height,line.Bounds.Height)*3);
                if(prior==null)result.Add(line);else{prior.Words.AddRange(line.Words);prior.Bounds.Union(line.Bounds);prior.Confidence=Math.Min(prior.Confidence,line.Confidence);}
            }return result;
        }
        static Rect Bounds(IEnumerable<WordBox> words){var r=Rect.Empty;foreach(var w in words)r.Union(w.Box);return r;}
        static bool Overlaps(Rect a,Rect b){var r=Rect.Intersect(a,b);return !r.IsEmpty&&r.Width*r.Height>Math.Min(a.Width*a.Height,b.Width*b.Height)*.35;}
    }
}
