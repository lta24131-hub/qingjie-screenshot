using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QingJie {
    public sealed class SettingsWindow : Window {
        readonly CheckBox startup=new CheckBox {Content="开机自动启动",FontSize=15,Margin=new Thickness(0,10,0,7)};
        readonly TextBlock status=new TextBlock {FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=Ui.Green,Margin=new Thickness(0,8,0,0)};
        bool updating;
        public SettingsWindow(){
            Title="轻截 · 设置";Icon=Ui.AppIcon;Width=490;Height=355;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");
            var panel=new StackPanel {Margin=new Thickness(26,20,26,20)};Content=panel;
            panel.Children.Add(new TextBlock {Text="设置",FontSize=23,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink});panel.Children.Add(startup);
            panel.Children.Add(new TextBlock {Text="登录 Windows 后在托盘运行，并恢复未收起的贴图。",FontSize=12,Foreground=Ui.Brush("#718078"),TextWrapping=TextWrapping.Wrap});
            panel.Children.Add(new Border {Height=1,Background=Ui.Brush("#EBEFED"),Margin=new Thickness(0,20,0,16)});
            panel.Children.Add(new TextBlock {Text="贴图保存位置",FontSize=14,Foreground=Ui.Ink});
            panel.Children.Add(new TextBox {Text=AppState.Pins.Folder,IsReadOnly=true,BorderThickness=new Thickness(0),Background=Ui.Brush("#F3F6F4"),Padding=new Thickness(8),Margin=new Thickness(0,7,0,7),FontSize=12});
            var actions=new StackPanel {Orientation=Orientation.Horizontal};actions.Children.Add(Ui.TextButton("打开文件夹",()=>{try{Process.Start(new ProcessStartInfo(AppState.Pins.Folder){UseShellExecute=true});}catch(Exception ex){status.Text=ex.Message;}}));panel.Children.Add(actions);panel.Children.Add(status);
            try{updating=true;startup.IsChecked=StartupRegistration.Current.Enabled;}catch(Exception ex){startup.IsEnabled=false;status.Text=ex.Message;}finally{updating=false;}
            startup.Checked+=(s,e)=>SaveStartup();startup.Unchecked+=(s,e)=>SaveStartup();
        }
        void SaveStartup(){if(updating)return;try{StartupRegistration.Current.SetEnabled(startup.IsChecked==true);status.Foreground=Ui.Green;status.Text=startup.IsChecked==true?"已开启，下次登录 Windows 自动启动。":"已关闭，不影响当前运行和贴图记录。";}catch(Exception ex){updating=true;startup.IsChecked=!(startup.IsChecked==true);updating=false;status.Foreground=Ui.Brush("#B94B56");status.Text=ex.Message;}}
    }
}
