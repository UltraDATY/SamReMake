using System.Linq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
class UiReadTest {
 [STAThread] static void Main() {try {Run();}catch(Exception e){Console.WriteLine(e.GetType().FullName+": "+e.Message); if(e.InnerException!=null)Console.WriteLine(e.InnerException.Message);Environment.Exit(1);}} static void Run() {
 var app=new Remake.App();app.InitializeComponent();var window=new Remake.MainWindow();Remake.Localization.SetLanguage("en",false);
 var type=window.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
 Func<Task> waiting=()=>Task.Delay(60000,((CancellationTokenSource)type.GetField("readCancellation",flags).GetValue(window)).Token);
 var task=(Task)type.GetMethod("Guard",flags).Invoke(window,new object[]{waiting,true});
 var progress=(ProgressBar)window.FindName("ReadProgress");
 if(!progress.IsIndeterminate || ((StackPanel)window.FindName("ReadProgressPanel")).Visibility!=Visibility.Visible)throw new Exception("Read progress not active");
 if(!window.IsEnabled)throw new Exception("Window was disabled during read");
 var cancel=(Button)window.FindName("CancelReadButton");if(!cancel.IsEnabled)throw new Exception("Cancel unavailable");
 type.GetMethod("CancelRead",flags).Invoke(window,new object[]{null,new RoutedEventArgs()});
 var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
 timer.Tick+=(s,e)=>{if(task.IsCompleted)frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();task.GetAwaiter().GetResult();
 if(progress.IsIndeterminate || ((StackPanel)window.FindName("ReadProgressPanel")).Visibility!=Visibility.Collapsed)throw new Exception("Read progress not stopped");
 if(cancel.IsEnabled)throw new Exception("Cancel remained enabled after completion");
 Console.WriteLine(((TextBlock)window.FindName("Status")).Text); if(!((TextBlock)window.FindName("Status")).Text.Contains("cancelled"))throw new Exception("Missing cancellation message");
 Remake.Localization.SetLanguage("zh-CN",false);
 if(!app.Resources.Values.Cast<object>().Any(x=>Equals(x,"设置")))throw new Exception("Chinese settings resource missing");
 Remake.Localization.SetLanguage("en",false);
 if(!app.Resources.Values.Cast<object>().Any(x=>Equals(x,"Settings")))throw new Exception("English settings resource missing");
 Console.WriteLine("PASS: live language resource switching");
 Console.WriteLine("PASS: progress starts during read and stops on cancellation; WPF stays enabled during read; cancel button available; cancellation completes and restores state");window.Close();app.Shutdown();
 }
}
