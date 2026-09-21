using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QingJie {
    public static class Translation {
        static readonly HttpClient client=CreateClient();
        static readonly string clientKey="browser-chrome-120.0.0-Windows-"+Guid.NewGuid().ToString();
        static HttpClient CreateClient(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var c=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(12),MaxResponseContentBufferSize=1024*1024};c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");c.DefaultRequestHeaders.Referrer=new Uri("https://transmart.qq.com/");return c;}
        public static async Task<string> Translate(string text,CancellationToken cancel) {return (await TranslateMany(new[]{text},cancel))[0];}
        public static string[] ParseImageContext(string text,int count){
            var matches=Regex.Matches(text,@"\[{1,3}(\d{4})\]{1,3}");
            if(matches.Count!=count)throw new InvalidOperationException("译文区域对应失败，已保留原图，请重试。");
            var result=new string[count];
            for(int i=0;i<count;i++){
                if(matches[i].Groups[1].Value!=i.ToString("D4"))throw new InvalidOperationException("译文区域顺序异常，已保留原图。");
                int start=matches[i].Index+matches[i].Length,end=i+1<count?matches[i+1].Index:text.Length;
                result[i]=text.Substring(start,end-start).Trim();if(string.IsNullOrWhiteSpace(result[i]))throw new InvalidOperationException("译文不完整，已保留原图。");
            }return result;
        }
        public static async Task<string[]> TranslateImage(IList<string> texts,CancellationToken cancel){
            if(texts==null||texts.Count==0||texts.Count>500||texts.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException("没有可翻译的文字区域。");
            var result=new List<string>();
            for(int offset=0;offset<texts.Count;){var block=new StringBuilder();int count=0;
                while(offset<texts.Count&&(count==0||block.Length+texts[offset].Length+20<9500)){block.Append("[[[").Append(count.ToString("D4")).Append("]]]\n").Append(texts[offset++]).Append("\n\n");count++;}
                // Keep neighbouring headings and descriptions together. Isolated words
                // like Fold otherwise lose context, e.g. become the poker term.
                var translated=await Translate(block.ToString(),cancel);result.AddRange(ParseImageContext(translated,count));
            }return DeviceTerms(texts,result);
        }
        public static string[] DeviceTerms(IList<string> texts,IList<string> translated){
            string context=string.Join("\n",texts);
            bool equipment=Regex.IsMatch(context,@"\b(gimbal|tripod|camera|hinge|mast)\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(context,@"\b(poker|cards|bet|origami)\b",RegexOptions.IgnoreCase);
            return translated.Select((text,i)=>{if(!equipment)return text;string source=texts[i];
                if(Regex.IsMatch(source,@"\bgimbals?\b",RegexOptions.IgnoreCase))text=Regex.Replace(text,@"万向节|平衡环|万向架|台子|\bgimbals?\b","云台",RegexOptions.IgnoreCase);
                if(Regex.IsMatch(source,@"\b(fold|folds|folded|folding)\b",RegexOptions.IgnoreCase))text=Regex.Replace(text,@"弃牌|\b(fold|folds|folded|folding)\b","折叠",RegexOptions.IgnoreCase);
                if(Regex.IsMatch(source,@"\bmasts?\b",RegexOptions.IgnoreCase))text=Regex.Replace(text,@"桅杆|\bmasts?\b","立柱",RegexOptions.IgnoreCase);
                if(Regex.IsMatch(source,@"\bbody\b",RegexOptions.IgnoreCase))text=text.Replace("身体","机身");return text;
            }).ToArray();
        }
        public static string[] ParseResponse(string response,int count){
                var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(response);
                object raw,head;var ok=data!=null&&data.TryGetValue("header",out head)?head as Dictionary<string,object>:null;
                if(ok==null||!ok.ContainsKey("ret_code")||Convert.ToString(ok["ret_code"])!="succ"||!data.TryGetValue("auto_translation",out raw))throw new InvalidOperationException("翻译接口返回异常或被限流。截图功能不受影响。");
                var list=raw as IList;
                if(list==null||list.Count!=count||list.Cast<object>().Any(v=>!(v is string)||string.IsNullOrWhiteSpace((string)v)))throw new InvalidOperationException("翻译结果不完整，请稍后重试。");
                return list.Cast<string>().ToArray();
        }
        public static async Task<string[]> TranslateMany(IList<string> texts,CancellationToken cancel,string sourceLanguage="auto"){
            if(texts==null||texts.Count==0||texts.Any(string.IsNullOrWhiteSpace))throw new InvalidOperationException("请先选择要翻译的文字。");
            if(texts.Count>500||texts.Any(t=>t.Length>10000))throw new InvalidOperationException("文字区域太大，请分区域翻译。");
            var result=new List<string>();var json=new JavaScriptSerializer();
            for(int offset=0;offset<texts.Count;){
                cancel.ThrowIfCancellationRequested();var batch=new List<string>();int length=0;
                while(offset<texts.Count&&batch.Count<40&&(batch.Count==0||length+texts[offset].Length<=10000)){length+=texts[offset].Length;batch.Add(texts[offset++]);}
                var payload=new {header=new {fn="auto_translation",client_key=clientKey},type="plain",model_category="normal",source=new {lang=sourceLanguage,text_list=batch},target=new {lang="zh"}};
                try{using(var body=new StringContent(json.Serialize(payload),Encoding.UTF8,"application/json"))
                using(var response=await client.PostAsync("https://transmart.qq.com/api/imt",body,cancel)) {
                    if(!response.IsSuccessStatusCode)throw new InvalidOperationException("腾讯翻译暂时不可用（"+(int)response.StatusCode+"），请稍后重试。");
                    result.AddRange(ParseResponse(await response.Content.ReadAsStringAsync(),batch.Count));
                }}catch(HttpRequestException){throw new InvalidOperationException("无法连接腾讯翻译，请检查网络后重试。原图仍保留。");}
            }
            return result.ToArray();
        }
    }
}
