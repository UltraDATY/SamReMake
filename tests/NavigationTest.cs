using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
class NavigationTest {
 [STAThread] static void Main(){var app=new Remake.App();app.InitializeComponent();var window=new Remake.MainWindow();var tabs=(TabControl)window.FindName("Tabs");
 if(tabs.Items.Count!=7||tabs.SelectedIndex!=0||tabs.SelectedContent==null)throw new Exception("Initial page missing");
 var panel=(System.Windows.Controls.WrapPanel)window.FindName("NavigationPanel");
 if(panel.Children.Count!=3)throw new Exception("Navigation groups missing");
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;
 tabs.SelectedIndex=4;window.UpdateLayout();var workspace=tabs.SelectedContent as Remake.AdvancedWorkspace;
 if(workspace==null)throw new Exception("Friend page not embedded");
 var pages=(TabControl)workspace.GetType().GetField("pages",flags).GetValue(workspace);
 if(pages.SelectedIndex!=1||pages.SelectedContent==null)throw new Exception("Embedded selected page missing");
 var friend=(TextBox)workspace.GetType().GetField("friendId",flags).GetValue(workspace);friend.Text="preserve-test";
 tabs.SelectedIndex=0;tabs.SelectedIndex=6;tabs.SelectedIndex=4;
 if(tabs.SelectedContent!=workspace||friend.Text!="preserve-test")throw new Exception("Page state lost");
 if(Application.Current.Windows.Count!=1)throw new Exception("Extra workspace window opened");
 Console.WriteLine("PASS: seven direct pages, three navigation groups, initial content, embedded friend/history pages, persistent input, single window");
 window.Close();app.Shutdown();}
}
