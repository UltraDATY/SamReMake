// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace Remake {
 public class AdvancedWorkspace : UserControl {
  uint currentApp; readonly Window owner; readonly Func<bool> mainBusy;
  public bool IsBusy => cancellation!=null;
  public bool IsWriting => writing;
  public bool IsIdling => idle!=null&&!idle.HasExited;
  readonly TabControl pages;
  public void SelectPage(int index){pages.SelectedIndex=index;}
  public void SetCurrentApp(uint app){if(currentApp==app)return;currentApp=app;ids.Text=app>0?app.ToString():"";plan=null;timeline.ItemsSource=null;}
  public void CancelPending(){cancellation?.Cancel();}
  public Task ShutdownIdle()=>StopIdle(); readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap};
  readonly TextBox ids=new TextBox(), friendId=new TextBox(), minutes=new TextBox{Text="60"};
  readonly ListBox friends=new ListBox{DisplayMemberPath="Label"}, timeline=new ListBox{DisplayMemberPath="Name"}, history=new ListBox{DisplayMemberPath="Label"};
  readonly Button cancel; CancellationTokenSource cancellation; bool writing; Process idle; string stopFile; List<ReplayStep> plan; uint planApp;
  static readonly Dictionary<string,Tuple<string,string>> labels=new Dictionary<string,Tuple<string,string>>();
  static string A(string en,string zh){var pair=Tuple.Create(en,zh);labels[en]=pair;labels[zh]=pair;return Localization.Language=="zh-CN"?zh:en;}
  public void RefreshLanguage(){TranslateTree(this);}
  void TranslateTree(DependencyObject node){
   if(node is TextBlock text && labels.TryGetValue(text.Text,out var pair))text.Text=Localization.Language=="zh-CN"?pair.Item2:pair.Item1;
   if(node is ContentControl control && control.Content is string content && labels.TryGetValue(content,out var p))control.Content=Localization.Language=="zh-CN"?p.Item2:p.Item1;
   foreach(var child in LogicalTreeHelper.GetChildren(node).Cast<object>().ToArray())if(child is DependencyObject element)TranslateTree(element);
  }
  static TextBlock Text(string en,string zh)=>new TextBlock{Text=A(en,zh),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};
  public AdvancedWorkspace(Window owner,Func<bool> mainBusy,uint app) {
   this.owner=owner;this.mainBusy=mainBusy;currentApp=app;
   Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#101722");Foreground=System.Windows.Media.Brushes.White;FontFamily=new System.Windows.Media.FontFamily("Microsoft YaHei UI");FontSize=13;
   var root=new DockPanel{Margin=new Thickness(12)};Content=root;
   var footer=new DockPanel{Margin=new Thickness(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   cancel=new Button{Content=A("Cancel read / schedule","取消读取／计划")};cancel.Click+=(s,e)=>cancellation?.Cancel();DockPanel.SetDock(cancel,Dock.Right);footer.Children.Add(cancel);footer.Children.Add(status);
   var tabs=new TabControl{Background=Background,BorderThickness=new Thickness(0)};pages=tabs;
   var template=new ControlTemplate(typeof(TabControl));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentSourceProperty,"SelectedContent");template.VisualTree=presenter;tabs.Template=template;root.Children.Add(tabs);
   var batch=Page(tabs,"Batch operations","跨游戏批处理");batch.Children.Add(Text("AppIDs, separated by spaces or commas. All writes are previewed and backed up first.","用空格或逗号分隔 AppID。所有写入操作先预览并备份。"));ids.Text=app>0?app.ToString():"";batch.Children.Add(ids);
   var batchButtons=new WrapPanel{Margin=new Thickness(0,15,0,0)};batch.Children.Add(batchButtons);
   batchButtons.Children.Add(Button(A("Export selected apps","导出这些游戏"),ExportBatch));batchButtons.Children.Add(Button(A("Import backup file","导入备份文件"),ImportBatch));batchButtons.Children.Add(Button(A("Unlock all achievements","解锁全部成就"),()=>BatchChange(true)));batchButtons.Children.Add(Button(A("Reset achievements and stats","重置成就及统计"),()=>BatchChange(false)));
   var replay=Page(tabs,"Friend timeline","好友时间回放");replay.Children.Add(Text("Uses the game selected in the sidebar. Preview a friend timeline and replay relative intervals; historical times are never overwritten.","使用侧边栏当前选中的游戏。预览好友时间线并按相对间隔回放，不覆盖历史解锁时间。"));
   var row=new WrapPanel();row.Children.Add(Button(A("Load friends","读取好友"),LoadFriends));friends.Height=95;friends.SelectionChanged+=(s,e)=>{if(friends.SelectedItem is GameRow64 f)friendId.Text=f.Id;};replay.Children.Add(row);replay.Children.Add(friends);replay.Children.Add(Text("SteamID64","SteamID64"));replay.Children.Add(friendId);replay.Children.Add(Text("Replay duration (minutes, 1–10080)","回放总时长（分钟，1–10080）"));replay.Children.Add(minutes);
   var replayButtons=new WrapPanel{Margin=new Thickness(0,12,0,8)};replayButtons.Children.Add(Button(A("Read and preview timeline","读取并预览时间线"),PreviewReplay));replayButtons.Children.Add(Button(A("Start previewed plan","启动预览计划"),RunReplay));replay.Children.Add(replayButtons);timeline.Height=190;replay.Children.Add(timeline);
   var idlePage=Page(tabs,"Idle","挂机");idlePage.Children.Add(Text("Idle the current game without launching its executable. Steam may show you as in-game. Stop idling before closing the application.","不启动游戏程序，保持当前游戏的游戏中状态。关闭程序前请停止挂机。"));idlePage.Children.Add(Text("Uses the current sidebar selection; an active idle session keeps its original AppID.","使用侧边栏当前选中的游戏；已启动的挂机继续使用启动时的 AppID。"));idlePage.Children.Add(Button(A("Start idling","开始挂机"),StartIdle));idlePage.Children.Add(Button(A("Stop idling","停止挂机"),StopIdle));
   var logPage=Page(tabs,"History and restore","历史与恢复");logPage.Children.Add(Text("Snapshots preserve achievement states and statistics before changes. Restoring cannot restore original unlock timestamps; Steam decides new timestamps. A backup failure prevents the write.","快照保留变更前的成就状态和统计。恢复不能还原原解锁时间，时间由 Steam 决定；备份失败将阻止写入。"));history.Height=330;logPage.Children.Add(history);var hb=new WrapPanel{Margin=new Thickness(0,12,0,0)};hb.Children.Add(Button(A("Refresh history","刷新历史"),()=>{history.ItemsSource=Backups.List();return Task.CompletedTask;}));hb.Children.Add(Button(A("Restore selected snapshot","恢复选中快照"),Restore));logPage.Children.Add(hb);history.ItemsSource=Backups.List();

  }
  StackPanel Page(TabControl tabs,string en,string zh) {var panel=new StackPanel{Margin=new Thickness(18)};tabs.Items.Add(new TabItem{Header=A(en,zh),Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});return panel;}
  Button Button(string title,Func<Task> action) {var b=new Button{Content=title,Margin=new Thickness(0,0,8,8),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=async(s,e)=>{if(cancellation!=null||mainBusy()){status.Text=A("An operation is running. Cancel it or wait.","当前有任务正在执行，请取消或等待。");return;}cancellation=new CancellationTokenSource();try{status.Text=A("Working…","处理中…");await action();status.Text=A("Done","完成");}catch(OperationCanceledException){status.Text=A("Cancelled. An in-flight write may still complete.","已取消，正在提交的写入可能仍会完成。");}catch(Exception ex){status.Text=ex.Message;}finally{writing=false;cancellation.Dispose();cancellation=null;history.ItemsSource=Backups.List();}};return b;}
  bool Confirm(string message)=>MessageBox.Show(owner,message,A("Preview changes","变更预览"),MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
  uint[] AppIds() {var parts=ids.Text.Split(new[]{' ', ',', ';','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries);if(parts.Length==0||parts.Any(p=>!uint.TryParse(p,out uint value)||value==0))throw new Exception(A("Enter valid AppIDs.","请输入有效 AppID。"));return parts.Select(uint.Parse).Distinct().ToArray();}
  async Task ExportBatch(){var apps=AppIds();var dialog=new SaveFileDialog{Filter="JSON|*.json",FileName="steam-backup.json"};if(dialog.ShowDialog(owner)!=true)return;var data=await Engine.RunRead(cancellation.Token,120000,new[]{"export"}.Concat(apps.Select(x=>x.ToString())).ToArray());Engine.Json().DeserializeObject(data);File.WriteAllText(dialog.FileName,data);}
  async Task BatchChange(bool unlock){var apps=AppIds();if(!Confirm(string.Join(", ",apps)+"\n"+(unlock?A("Unlock ALL achievements?","解锁全部成就？"):A("Reset ALL achievements AND statistics?","重置全部成就及统计？"))))return;foreach(var app in apps){cancellation.Token.ThrowIfCancellationRequested();status.Text=A("Backing up ","正在备份 ")+app;await Backups.Save(app,unlock?"Before batch unlock":"Before reset");cancellation.Token.ThrowIfCancellationRequested();writing=true;try{await Engine.Run(unlock?"unlock-all":"lock-all",app.ToString());}finally{writing=false;}}}
  async Task ImportBatch(){var dialog=new OpenFileDialog{Filter="JSON|*.json"};if(dialog.ShowDialog(owner)!=true)return;var doc=Engine.Dict(Engine.Json().DeserializeObject(File.ReadAllText(dialog.FileName)));var apps=((System.Collections.IEnumerable)doc["apps"]).Cast<object>().Select(x=>Convert.ToUInt32(Engine.Dict(x)["app_id"])).Distinct().ToArray();if(apps.Length==0||!Confirm(A("Import states and statistics for ","导入成就状态及统计：")+string.Join(", ",apps)))return;foreach(var id in apps){cancellation.Token.ThrowIfCancellationRequested();await Backups.Save(id,"Before batch import");}cancellation.Token.ThrowIfCancellationRequested();writing=true;try{var result=await Engine.Run("import",dialog.FileName);CheckImport(result);}finally{writing=false;}}
  static void CheckImport(string json){foreach(var row in Engine.Array(json))if(Engine.Get(row,"error")!=null)throw new Exception(Engine.Text(row,"error"));}
  async Task Restore(){if(!(history.SelectedItem is SnapshotRecord record))return;if(!Confirm(A("Restore states and statistics for AppID ","恢复成就状态及统计，AppID ")+record.AppId+"?\n"+A("Original unlock timestamps cannot be restored.","无法还原原解锁时间。")))return;await Backups.Save(record.AppId,"Before snapshot restore");cancellation.Token.ThrowIfCancellationRequested();writing=true;try{CheckImport(await Engine.Run("import",Backups.PathFor(record),"--app-id",record.AppId.ToString()));}finally{writing=false;}}
  public class GameRow64{public string Id{get;set;}public string Label{get;set;}}
  async Task LoadFriends(){friends.ItemsSource=Engine.Array(await Engine.RunRead(cancellation.Token,25000,"list-friends")).Select(d=>new GameRow64{Id=Engine.Text(d,"steam_id64"),Label=Engine.Text(d,"name")+" · "+Engine.Text(d,"steam_id64")}).ToList();}
  async Task PreviewReplay(){plan=null;timeline.ItemsSource=null;if(currentApp==0||!ulong.TryParse(friendId.Text,out ulong friend)||friend<76561197960265728UL)throw new Exception(A("Select a game and enter a valid SteamID64.","请先选择游戏并输入有效 SteamID64。"));if(!double.TryParse(minutes.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double duration)||duration<1||duration>10080)throw new Exception(A("Duration must be 1–10080 minutes.","时长应为 1–10080 分钟。"));var source=Engine.Array(await Engine.RunRead(cancellation.Token,45000,"list-friend-achievements",currentApp.ToString(),friend.ToString())).ToList();var own=Engine.Array(await Engine.RunRead(cancellation.Token,25000,"list-achievements",currentApp.ToString())).Select(Engine.Achievement).Where(x=>!x.Achieved&&(x.Permission&3)==0).Select(x=>x.Id);plan=ReplayPlan.Build(source,new HashSet<string>(own),TimeSpan.FromMinutes(duration));planApp=currentApp;timeline.ItemsSource=plan.Select(x=>new{Name=x.Name+" · +"+x.Delay.ToString()}).ToList();if(plan.Count==0)throw new Exception(A("No eligible achievements with known friend timestamps.","没有具有已知好友时间且可解锁的成就。"));}
  async Task RunReplay(){if(plan==null||plan.Count==0)throw new Exception(A("Read a timeline first.","请先读取时间线。"));var items=plan.ToArray();if(!Confirm(A("Replay ","回放 ")+items.Length+A(" achievements for AppID "," 项成就，AppID ")+planApp+"?"))return;await Backups.Save(planApp,"Before friend replay");foreach(var item in items){status.Text=A("Waiting: ","正在等待：")+item.Name;await Task.Delay(item.Delay,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();writing=true;try{var result=await Engine.Run("unlock",planApp.ToString(),item.Id);if(Engine.Array(result).Any(d=>!Convert.ToBoolean(d["success"])))throw new Exception("Steam rejected "+item.Id);}finally{writing=false;}}}
  async Task StartIdle(){if(currentApp==0)throw new Exception(A("Select a game first.","请先选择游戏。"));if(idle!=null&&!idle.HasExited)throw new Exception(A("Already idling.","挂机已启动。"));stopFile=Path.Combine(Path.GetTempPath(),"remake-idle-"+Guid.NewGuid().ToString("N")+".stop");idle=new Process{StartInfo=new ProcessStartInfo(Path.Combine(Engine.Root,"engine","samrewritten-cli.exe"),"idle "+currentApp+" --stop-file "+Engine.Quote(stopFile)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};idle.Start();idle.OutputDataReceived+=(s,e)=>{};idle.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Dispatcher.BeginInvoke(new Action(()=>status.Text=e.Data));};idle.BeginOutputReadLine();idle.BeginErrorReadLine();await Task.Delay(500);if(idle.HasExited)throw new Exception(A("Idle startup failed. Check Steam.","挂机启动失败，请检查 Steam。"));}
  async Task StopIdle(){if(idle==null)return;if(!idle.HasExited){File.WriteAllText(stopFile,"");if(!await Task.Run(()=>idle.WaitForExit(15000)))throw new Exception(A("Waiting for Steam idle cleanup. Retry stop shortly.","正在等待 Steam 清理挂机状态，请稍后再次停止。"));}idle.Dispose();idle=null;if(File.Exists(stopFile))File.Delete(stopFile);}

 }
}
