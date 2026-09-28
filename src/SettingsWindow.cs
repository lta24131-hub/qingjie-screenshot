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
        readonly ComboBox languages=new ComboBox {MinWidth=225,FontSize=13,Margin=new Thickness(0,8,0,8)};
        readonly TextBlock languageStatus=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Ui.Brush("#718078")};
        Button languageApply,languageImport,languageRemove;
        CancellationTokenSource languageDownload;
        readonly TextBlock shortcutStatus=new TextBlock {FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap};
        ShortcutEditor captureKey,pinKey,translationKey;
        public SettingsWindow(){
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
            panel.Children.Add(new TextBlock {Text="默认英文优先，保留系统中文识别。其他语言按需安装，最多启用一种附加语言，用完可切回英文优先。",FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
            languages.Items.Add("英文优先 · 无附加语言包");foreach(var pack in OcrLanguagePacks.Available)languages.Items.Add(pack);
            languages.SelectedItem=(object)OcrLanguagePacks.Find(OcrLanguagePacks.SelectedId)??languages.Items[0];panel.Children.Add(languages);
            var languageActions=new WrapPanel();languageApply=Ui.TextButton("应用",ApplyLanguage,true);languageImport=Ui.TextButton("导入语言包",ImportLanguage);languageRemove=Ui.TextButton("卸载",RemoveLanguage);languageActions.Children.Add(languageApply);languageActions.Children.Add(languageImport);languageActions.Children.Add(languageRemove);panel.Children.Add(languageActions);panel.Children.Add(languageStatus);
            languages.SelectionChanged+=(s,e)=>RefreshLanguage();Closed+=(s,e)=>{if(languageDownload!=null)languageDownload.Cancel();};RefreshLanguage();
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,20,0,16)});
            panel.Children.Add(new TextBlock {Text="贴图保存位置",FontSize=14,Foreground=Ui.Ink});
            panel.Children.Add(new TextBox {Text=AppState.Pins.Folder,IsReadOnly=true,BorderThickness=new Thickness(0),Background=Ui.Brush("#F3F6F4"),Padding=new Thickness(8),Margin=new Thickness(0,7,0,7),FontSize=12});
            var actions=new StackPanel {Orientation=Orientation.Horizontal};actions.Children.Add(Ui.TextButton("打开文件夹",()=>{try{Process.Start(new ProcessStartInfo(AppState.Pins.Folder){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}));panel.Children.Add(actions);panel.Children.Add(status);
            try{updating=true;startup.IsChecked=StartupRegistration.Current.Enabled;}catch(Exception ex){startup.IsEnabled=false;status.Text=ex.Message;}finally{updating=false;}
            startup.Checked+=(s,e)=>SaveStartup();startup.Unchecked+=(s,e)=>SaveStartup();
        }
        static ShortcutEditor ShortcutRow(Panel panel,string name,Shortcut value){var row=new DockPanel();var editor=new ShortcutEditor(value);DockPanel.SetDock(editor,Dock.Right);row.Children.Add(editor);row.Children.Add(new TextBlock {Text=name,FontSize=13,Foreground=Ui.Ink,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(row);return editor;}
        void RefreshLanguage(){
            var pack=languages.SelectedItem as OcrLanguagePack;bool busy=languageDownload!=null;
            languages.IsEnabled=!busy;languageImport.IsEnabled=!busy&&pack!=null;languageRemove.IsEnabled=!busy&&pack!=null&&OcrLanguagePacks.Installed(pack.Id);
            languageApply.Content=busy?"取消下载":pack==null?"切回英文优先":OcrLanguagePacks.Installed(pack.Id)?"启用":"下载并启用";
            if(!busy){var current=OcrLanguagePacks.Find(OcrLanguagePacks.SelectedId);languageStatus.Text="当前："+(current==null?"英文优先":"英文 + "+current.Name)+(pack==null?"。":OcrLanguagePacks.Installed(pack.Id)?"。所选语言已安装。":"。所选语言尚未安装。")+"\n这是文字识别包，不是翻译包；非中英翻译仍需在线服务。";}
        }
        void SelectLanguage(string id){AppState.Settings.OcrLanguage=id;AppState.Settings.Save();RefreshLanguage();languageStatus.Text+="\n下次识别生效，无需重启。";}
        async void ApplyLanguage(){
            if(languageDownload!=null){languageDownload.Cancel();return;}
            var pack=languages.SelectedItem as OcrLanguagePack;
            if(pack==null){SelectLanguage("");return;}if(OcrLanguagePacks.Installed(pack.Id)){SelectLanguage(pack.Id);return;}
            var cancel=new CancellationTokenSource();languageDownload=cancel;RefreshLanguage();languageStatus.Text="正在下载 "+pack.Name+"，不上传截图…";
            string error=null;
            try{await OcrLanguagePacks.Install(pack.Id,new Progress<int>(value=>{if(languageDownload==cancel)languageStatus.Text="正在下载 "+pack.Name+" · "+value+"%";}),cancel.Token);cancel.Token.ThrowIfCancellationRequested();AppState.Settings.OcrLanguage=pack.Id;AppState.Settings.Save();}
            catch(OperationCanceledException){error="已取消或下载超时，原语言设置未改变。";}
            catch(Exception){error="下载未完成，原语言设置未改变。官方 GitHub 下载源可能无法直连，可稍后重试或导入已下载的语言包。";}
            finally{languageDownload=null;cancel.Dispose();RefreshLanguage();if(error!=null)languageStatus.Text=error;}
        }
        void ImportLanguage(){
            var pack=languages.SelectedItem as OcrLanguagePack;if(pack==null)return;
            var dialog=new Microsoft.Win32.OpenFileDialog {Title="导入 "+pack.Name+" 官方语言包",Filter="Tesseract 语言包|*.traineddata",FileName=pack.Id+".traineddata"};
            if(dialog.ShowDialog(this)!=true)return;try{OcrLanguagePacks.Import(dialog.FileName,pack.Id);SelectLanguage(pack.Id);}catch(Exception ex){languageStatus.Text=ex.Message;}
        }
        void RemoveLanguage(){
            var pack=languages.SelectedItem as OcrLanguagePack;if(pack==null||!OcrLanguagePacks.Installed(pack.Id))return;
            if(MessageBox.Show(this,"卸载 "+pack.Name+" 语言包？之后可重新下载。截图和贴图不会删除。","Luma",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            try{File.Delete(OcrLanguagePacks.ModelPath(pack.Id));if(AppState.Settings.OcrLanguage==pack.Id)SelectLanguage("");RefreshLanguage();}catch(Exception ex){languageStatus.Text=ex.Message;}
        }
        void SaveStartup(){if(updating)return;try{StartupRegistration.Current.SetEnabled(startup.IsChecked==true);status.Foreground=Ui.Green;status.Text=startup.IsChecked==true?"已开启，下次登录 Windows 自动启动。":"已关闭，不影响当前运行和贴图记录。";}catch(Exception ex){updating=true;startup.IsChecked=!(startup.IsChecked==true);updating=false;status.Foreground=Ui.Brush("#B94B56");status.Text=ex.Message;}}
    }
}
