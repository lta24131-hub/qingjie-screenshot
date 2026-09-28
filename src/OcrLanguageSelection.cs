using System;

namespace QingJie {
    // Separate browsing an uninstalled pack from the persisted OCR configuration.
    internal sealed class OcrLanguageSelection {
        readonly Preferences settings;
        readonly Action save;
        readonly Func<string,bool> installed;
        readonly Action<string> remove;
        public string SelectedId {get;private set;}
        public string CurrentId {get{return Normalize(settings.OcrLanguage);}}
        public bool Ready {get{return SelectedId==""||installed(SelectedId);}}
        public OcrLanguageSelection(Preferences settings,Action save,Func<string,bool> installed,Action<string> remove){this.settings=settings;this.save=save;this.installed=installed;this.remove=remove;SelectedId=CurrentId;}
        static string Normalize(string id){return OcrLanguagePacks.Find(id)==null?"":id;}
        public void Choose(string id){
            if(id!=""&&OcrLanguagePacks.Find(id)==null)throw new ArgumentException("Unknown OCR language");
            SelectedId=id;
            if(Ready){try{Apply();}catch{SelectedId=CurrentId;throw;}}
        }
        public void Apply(){
            if(!Ready)throw new InvalidOperationException("请先下载所选识别语言包。");
            Commit(SelectedId);
        }
        void Commit(string id){
            string before=settings.OcrLanguage;if(before==id)return;
            settings.OcrLanguage=id;try{save();}catch{settings.OcrLanguage=before;throw;}
        }
        public void Remove(){
            string id=SelectedId;if(id==""||!installed(id))return;
            bool active=CurrentId==id;if(active)Commit("");
            try{remove(id);}catch{if(active)Commit(id);throw;}
            if(active)SelectedId="";
        }
    }
}
