using System;
using System.Windows;
using System.Windows.Controls;
class NavigationTest {
 [STAThread] static void Main(){var app=new Remake.App();app.InitializeComponent();var window=new Remake.MainWindow();var tabs=(TabControl)window.FindName("Tabs");
 if(tabs.Items.Count!=2||tabs.SelectedIndex!=0||tabs.SelectedContent==null)throw new Exception("Initial achievement page missing");
 var panel=(WrapPanel)window.FindName("NavigationPanel");if(panel.Children.Count!=1)throw new Exception("Unused navigation groups remain");
 var group=(StackPanel)panel.Children[0];var buttons=(StackPanel)group.Children[1];if(buttons.Children.Count!=2)throw new Exception("Removed tools remain in navigation");
 tabs.SelectedIndex=1;if(tabs.SelectedContent==null)throw new Exception("Statistics page missing");tabs.SelectedIndex=0;
 if(window.FindName("SettingsButton")==null||window.FindName("LanguageSelector")==null)throw new Exception("Settings missing");
 if(Application.Current.Windows.Count!=1)throw new Exception("Extra window opened");
 Console.WriteLine("PASS: achievements and statistics remain accessible; removed tools absent; top-left language settings retained");window.Close();app.Shutdown();}
}
