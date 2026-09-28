using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QingJie {
    public sealed class SettingsWindow : Window {
        readonly CheckBox startup=new CheckBox {Content="开机自动启动",FontSize=15,Margin=new Thickness(0,10,0,7)};
        readonly TextBlock status=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Ui.Green,Margin=new Thickness(0,8,0,0)};
        bool updating;
        readonly ComboBox languages=new ComboBox {Name="OcrLanguages",MinWidth=225,FontSize=13,Margin=new Thickness(0,8,0,8)};
        readonly TextBlock languageStatus=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Ui.Brush("#718078")};
        Button languageApply,languageImport,languageRemove;
        CancellationTokenSource languageDownload;
        readonly OcrLanguageSelection languageSelection;
        bool refreshingLanguage,closed;
        readonly TextBlock shortcutStatus=new TextBlock {FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap};
        ShortcutEditor captureKey,pinKey,translationKey;
        public SettingsWindow():this(new OcrLanguageSelection(AppState.Settings,()=>AppState.Settings.SaveChecked(),OcrLanguagePacks.Installed,id=>File.Delete(OcrLanguagePacks.ModelPath(id)))){}
        internal SettingsWindow(OcrLanguageSelection selection){
            languageSelection=selection;
            Title="Luma · 设置";Icon=Ui.AppIcon;Width=520;Height=600;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");
            var panel=new StackPanel {Margin=new Thickness(26,20,26,20)};Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            panel.Children.Add(new TextBlock {Text="设置",FontSize=23,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});panel.Children.Add(startup);
            panel.Children.Add(new TextBlock {Text="登录 Windows 后在托盘运行，并恢复未收起的贴图。",FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap});
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,18,0,16)});
            panel.Children.Add(new TextBlock {Text="快捷键",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});
            panel.Children.Add(new TextBlock {Text="点击右侧按键，再按新组合键。保存后立即生效。",FontSize=12,Foreground=Ui.Brush("#718078"),Margin=new Thickness(0,6,0,6)});
            captureKey=ShortcutRow(panel,"截图",AppState.Settings.Shortcuts.Capture);pinKey=ShortcutRow(panel,"贴图 / 找回",AppState.Settings.Shortcuts.Pin);translationKey=ShortcutRow(panel,"输入翻译",AppState.Settings.Shortcuts.Translate);
            var shortcutActions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            shortcutActions.Children.Add(Ui.TextButton("恢复默认",()=>{var defaults=new ShortcutSet();captureKey.Reset(defaults.Capture);pinKey.Reset(defaults.Pin);translationKey.Reset(defaults.Translate);shortcutStatus.Text="已填入默认按键，点击保存后生效。";}));
            shortcutActions.Children.Add(Ui.TextButton("保存快捷键",()=>{var error=AppState.SaveShortcuts(new ShortcutSet {Capture=captureKey.Value,Pin=pinKey.Value,Translate=translationKey.Value});shortcutStatus.Foreground=error==null?Ui.Green:Ui.Brush("#B94B56");shortcutStatus.Text=error??"已保存，无需重启。";},true));panel.Children.Add(shortcutActions);panel.Children.Add(shortcutStatus);
            Closed+=(s,e)=>AppState.ShortcutRecorder=null;
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,18,0,16)});
            panel.Children.Add(new TextBlock {Text="翻译服务",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});
            var provider=new ComboBox {FontSize=13,Margin=new Thickness(0,10,0,8),Name="TranslationProvider"};provider.Items.Add("腾讯翻译 · 国内直连");provider.Items.Add("Google 翻译 · 使用系统网络 / 代理");provider.Items.Add("离线翻译 · 中文 ↔ 英文");provider.SelectedIndex=Translation.Provider=="offline"?2:Translation.Provider=="google"?1:0;panel.Children.Add(provider);
            var providerStatus=new TextBlock {Text="只发送文字，不上传图片。Google 需要能访问谷歌；沿用现有系统代理，不自动更换线路。",FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap};panel.Children.Add(providerStatus);
            panel.Children.Add(Ui.TextButton("应用翻译服务",()=>{string selected=provider.SelectedIndex==2?"offline":provider.SelectedIndex==1?"google":"tencent",previous=AppState.Settings.TranslationProvider;try{AppState.Settings.TranslationProvider=selected;AppState.Settings.SaveChecked();if(previous!=selected)AppState.ProviderChanged();providerStatus.Text="当前："+Translation.ProviderName+"。输入、截图及所选文字统一使用此服务。"+(selected=="offline"?"仅本机处理，请先安装下方离线包；失败时也不会联网。":"网页接口可能限流，失败时不会自动改用另一家。");}catch(Exception ex){AppState.Settings.TranslationProvider=previous;providerStatus.Text="保存失败："+ex.Message;}}));
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,18,0,16)});
            var offline=new OfflinePackPanel();panel.Children.Add(offline);Closed+=(s,e)=>offline.Dispose();
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,18,0,16)});
            panel.Children.Add(new TextBlock {Text="识别语言",FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});
            panel.Children.Add(new TextBlock {Text="中文和英文始终保留；可再加一种语言。已安装的选中即生效，未安装的需点击下载。只影响图片文字识别，不改变翻译目标语言。",FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
            languages.Items.Add(new ComboBoxItem {Tag=""});foreach(var pack in OcrLanguagePacks.Available)languages.Items.Add(new ComboBoxItem {Tag=pack.Id});panel.Children.Add(languages);
            var languageActions=new WrapPanel();languageApply=Ui.TextButton("下载并启用",ApplyLanguage,true);languageApply.Name="ApplyOcrLanguage";languageImport=Ui.TextButton("导入语言包",ImportLanguage);languageRemove=Ui.TextButton("卸载",RemoveLanguage);languageActions.Children.Add(languageApply);languageActions.Children.Add(languageImport);languageActions.Children.Add(languageRemove);panel.Children.Add(languageActions);panel.Children.Add(languageStatus);
            languages.SelectionChanged+=(s,e)=>ChooseLanguage();Closed+=(s,e)=>{closed=true;if(languageDownload!=null)languageDownload.Cancel();};RefreshLanguage();
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,20,0,16)});
            panel.Children.Add(new TextBlock {Text="贴图保存位置",FontSize=14,Foreground=Ui.Ink});
            panel.Children.Add(new TextBox {Text=AppState.Pins.Folder,IsReadOnly=true,BorderThickness=new Thickness(0),Background=Ui.Brush("#F3F6F4"),Padding=new Thickness(8),Margin=new Thickness(0,7,0,7),FontSize=12});
            var actions=new StackPanel {Orientation=Orientation.Horizontal};actions.Children.Add(Ui.TextButton("打开文件夹",()=>{try{Process.Start(new ProcessStartInfo(AppState.Pins.Folder){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}));panel.Children.Add(actions);panel.Children.Add(status);
            try{updating=true;startup.IsChecked=StartupRegistration.Current.Enabled;}catch(Exception ex){startup.IsEnabled=false;status.Text=ex.Message;}finally{updating=false;}
            startup.Checked+=(s,e)=>SaveStartup();startup.Unchecked+=(s,e)=>SaveStartup();
        }
        static ShortcutEditor ShortcutRow(Panel panel,string name,Shortcut value){var row=new DockPanel();var editor=new ShortcutEditor(value);DockPanel.SetDock(editor,Dock.Right);row.Children.Add(editor);row.Children.Add(new TextBlock {Text=name,FontSize=13,Foreground=Ui.Ink,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(row);return editor;}
        void RefreshLanguage(){
            var pack=OcrLanguagePacks.Find(languageSelection.SelectedId);bool busy=languageDownload!=null;
            refreshingLanguage=true;try{foreach(ComboBoxItem item in languages.Items){string id=(string)item.Tag;var option=OcrLanguagePacks.Find(id);item.Content=(option==null?"中文 + 英文":option.Name)+(id==languageSelection.CurrentId&&(id==""||OcrLanguagePacks.Installed(id))?" · 使用中":option==null?" · 默认":OcrLanguagePacks.Installed(id)?" · 已安装":" · 未安装（"+(option.Bytes/1048576.0).ToString("0.0")+" MB）");if(id==languageSelection.SelectedId)languages.SelectedItem=item;}}finally{refreshingLanguage=false;}
            languages.IsEnabled=!busy;languageImport.IsEnabled=!busy&&pack!=null;languageRemove.IsEnabled=!busy&&pack!=null&&OcrLanguagePacks.Installed(pack.Id);
            bool active=languageSelection.Ready&&languageSelection.SelectedId==languageSelection.CurrentId;
            languageApply.Content=busy?"取消下载":active?"已启用":languageSelection.Ready?"启用":"下载并启用";languageApply.IsEnabled=busy||!active;
            if(!busy){var current=OcrLanguagePacks.Find(languageSelection.CurrentId);bool missing=current!=null&&!OcrLanguagePacks.Installed(current.Id);languageStatus.Text=(missing?"当前配置：":"当前生效：")+"中文 + 英文"+(current==null?"":" + "+current.Name)+(missing?"（语言包缺失，请下载或切回中文 + 英文）":"。下次识别立即使用，无需重启。")+(pack!=null&&!OcrLanguagePacks.Installed(pack.Id)?"\n所选 "+pack.Name+" 尚未安装，未切换；点击“下载并启用”。":"");}
        }
        void ChooseLanguage(){if(refreshingLanguage||languageDownload!=null)return;var item=languages.SelectedItem as ComboBoxItem;if(item==null)return;try{languageSelection.Choose((string)item.Tag);RefreshLanguage();}catch(Exception ex){RefreshLanguage();languageStatus.Text+="\n保存失败，保留原语言："+ex.Message;}}
        async void ApplyLanguage(){
            if(languageDownload!=null){languageDownload.Cancel();return;}
            var pack=OcrLanguagePacks.Find(languageSelection.SelectedId);
            if(languageSelection.Ready){try{languageSelection.Apply();RefreshLanguage();}catch(Exception ex){RefreshLanguage();languageStatus.Text+="\n未切换："+ex.Message;}return;}
            var cancel=new CancellationTokenSource();languageDownload=cancel;RefreshLanguage();languageStatus.Text="正在下载 "+pack.Name+"，不上传截图…";
            string error=null;
            try{await OcrLanguagePacks.Install(pack.Id,new Progress<int>(value=>{if(!closed&&languageDownload==cancel)languageStatus.Text="正在下载 "+pack.Name+" · "+value+"%";}),cancel.Token);cancel.Token.ThrowIfCancellationRequested();if(!closed)languageSelection.Apply();}
            catch(OperationCanceledException){error="已取消或下载超时，原语言设置未改变。";}
            catch(Exception ex){error="未能启用，原语言设置未改变。"+ex.Message+" 可重试或导入官方语言包。";}
            finally{languageDownload=null;cancel.Dispose();if(!closed){RefreshLanguage();if(error!=null)languageStatus.Text+="\n"+error;}}
        }
        void ImportLanguage(){
            var pack=OcrLanguagePacks.Find(languageSelection.SelectedId);if(pack==null)return;
            var dialog=new Microsoft.Win32.OpenFileDialog {Title="导入 "+pack.Name+" 官方语言包",Filter="Tesseract 语言包|*.traineddata",FileName=pack.Id+".traineddata"};
            if(dialog.ShowDialog(this)!=true)return;try{OcrLanguagePacks.Import(dialog.FileName,pack.Id);languageSelection.Apply();RefreshLanguage();}catch(Exception ex){RefreshLanguage();languageStatus.Text+="\n未能启用，原语言保留："+ex.Message;}
        }
        void RemoveLanguage(){
            var pack=OcrLanguagePacks.Find(languageSelection.SelectedId);if(pack==null||!OcrLanguagePacks.Installed(pack.Id))return;
            if(MessageBox.Show(this,"卸载 "+pack.Name+" 语言包？之后可重新下载。"+(languageSelection.CurrentId==pack.Id?"\n正在使用该语言，卸载后自动恢复中文 + 英文。":"")+"\n截图和贴图不会删除。","Luma",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            try{languageSelection.Remove();RefreshLanguage();}catch(Exception ex){RefreshLanguage();languageStatus.Text+="\n卸载未完成："+ex.Message;}
        }
        void SaveStartup(){if(updating)return;try{StartupRegistration.Current.SetEnabled(startup.IsChecked==true);status.Foreground=Ui.Green;status.Text=startup.IsChecked==true?"已开启，下次登录 Windows 自动启动。":"已关闭，不影响当前运行和贴图记录。";}catch(Exception ex){updating=true;startup.IsChecked=!(startup.IsChecked==true);updating=false;status.Foreground=Ui.Brush("#B94B56");status.Text=ex.Message;}}
    }
}
