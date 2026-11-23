using static Remake.Localization;
// SPDX-License-Identifier: GPL-3.0-only
// SteamAchievementManager-Remake: new Windows frontend by UltraDATY.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
namespace Remake {
 public class GameRow { public uint Id {get;set;} public string Name {get;set;} public string Label => Name+" · "+Id; }
 public class AchievementRow { public string Id {get;set;} public string Name {get;set;} public string Description {get;set;} public bool Achieved {get;set;} public long? Seconds {get;set;} public double? Percent {get;set;} public int Permission {get;set;} public string State => Achieved?L("已解锁"):L("未解锁"); public string TimeText => !Achieved?"—":Seconds.HasValue?DateTimeOffset.FromUnixTimeSeconds(Seconds.Value).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"):L("未知"); }
 public class StatRow { public string Id {get;set;} public string Name {get;set;} public string Value {get;set;} public int Permission {get;set;} public bool Integer {get;set;} public bool IncrementOnly {get;set;} public double Min {get;set;} public double Max {get;set;} public string Access => (Permission&2)!=0?L("受保护"):IncrementOnly?L("仅可增加"):L("可编辑"); }
 public static class Engine {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  public static JavaScriptSerializer Json() => new JavaScriptSerializer { MaxJsonLength=int.MaxValue, RecursionLimit=200 };
  public static string Quote(string value) { var b=new StringBuilder("\""); int n=0; foreach(char c in value) { if(c=='\\') { n++; continue; } if(c=='\"') b.Append('\\',n*2+1); else b.Append('\\',n); b.Append(c); n=0; } b.Append('\\',n*2); return b.Append('"').ToString(); }
  public static Task<string> Run(params string[] args) {
   bool readOnly = args.Length > 0 && (args[0].StartsWith("list-",StringComparison.Ordinal) || args[0]=="export");
   return RunCore(args,CancellationToken.None,readOnly ? 25000 : Timeout.Infinite);
  }
  public static Task<string> RunRead(CancellationToken token,int timeoutMs,params string[] args) {
   if(args.Length==0 || !(args[0].StartsWith("list-",StringComparison.Ordinal)||args[0]=="export"))
    throw new ArgumentException("Only read commands can be cancelled.");
   return RunCore(args,token,timeoutMs);
  }
  static async Task<string> RunCore(string[] args,CancellationToken token,int timeoutMs) {
   token.ThrowIfCancellationRequested();
   var path=Path.Combine(Root,"engine","samrewritten-cli.exe");
   if(!File.Exists(path)) throw new Exception(L("缺少 Steam 引擎，请使用完整便携包。"));
   using(var p=new Process())
   using(var timer=new CancellationTokenSource()) {
    p.StartInfo=new ProcessStartInfo(path,string.Join(" ",args.Select(Quote))) {WorkingDirectory=Path.GetDirectoryName(path),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
    p.Start(); var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync();
    var exit=Task.Run(()=>p.WaitForExit());
    using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,timer.Token)) {
     var deadline=Task.Delay(timeoutMs,linked.Token);
     if(await Task.WhenAny(exit,deadline)!=exit) {
      // Terminate only this read request and its engine children, never a Steam process.
      await Task.Run(()=> {
       try { using(var killer=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"taskkill.exe"),"/PID "+p.Id+" /T /F") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})) { killer.WaitForExit(5000); } } catch { }
       try { if(!p.HasExited) p.Kill(); } catch { }
      });
      if(token.IsCancellationRequested) throw new OperationCanceledException(token);
      throw new TimeoutException(L("Steam 读取超过 ")+(timeoutMs/1000)+L(" 秒，已停止本次请求。请检查 Steam 连接后重试，或选择其他游戏。"));
     }
     timer.Cancel();
    }
    await exit; var output=await stdout; var error=await stderr;
    if(p.ExitCode!=0) throw new Exception(L("Steam 请求失败：\n")+error+"\n"+output);
    return output.Trim();
   }
  }
  public static Dictionary<string,object> Dict(object o) => (Dictionary<string,object>)o;
  public static object Get(Dictionary<string,object>d,string k) => d.TryGetValue(k,out var v)?v:null;
  public static string Text(Dictionary<string,object>d,string k) => Convert.ToString(Get(d,k),CultureInfo.InvariantCulture);
  public static IEnumerable<Dictionary<string,object>> Array(string json) => ((IEnumerable)Json().DeserializeObject(json)).Cast<object>().Select(Dict);
  public static AchievementRow Achievement(Dictionary<string,object>d) {
   bool unlocked=Convert.ToBoolean(Get(d,"is_achieved")); long? seconds=null;
   if(unlocked && Get(d,"unlock_time") is Dictionary<string,object> t) { var v=Get(t,"secs_since_epoch"); if(v!=null && Convert.ToInt64(v)>0) seconds=Convert.ToInt64(v); }
   return new AchievementRow {Id=Text(d,"id"),Name=Text(d,"name"),Description=Text(d,"description"),Achieved=unlocked,Seconds=seconds,Percent=Get(d,"global_achieved_percent")==null?(double?)null:Convert.ToDouble(d["global_achieved_percent"]),Permission=Convert.ToInt32(Get(d,"permission"))};
  }
 }
 public partial class MainWindow : Window {
  List<GameRow> allGames=new List<GameRow>(); List<AchievementRow> allAchievements=new List<AchievementRow>(); uint appId; bool busy; CancellationTokenSource queue; bool queueRunning; CancellationTokenSource readCancellation; Task activeOperation = Task.CompletedTask;
  readonly Stopwatch readWatch = new Stopwatch();
  System.Windows.Threading.DispatcherTimer readProgressTimer;
  string readStage;
  void BeginReadProgress(string stage) {
   readStage=stage; readWatch.Restart();
   ReadProgressPanel.Visibility=Visibility.Visible;
   ReadProgress.IsIndeterminate=true;
   UpdateReadElapsed();
   if(readProgressTimer==null) {
    readProgressTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
    readProgressTimer.Tick+=(s,e)=>UpdateReadElapsed();
   }
   readProgressTimer.Start();
  }
  void UpdateReadElapsed() {ReadElapsed.Text=readStage+L(" · 已等待 ")+(int)readWatch.Elapsed.TotalSeconds+L(" 秒 · 接口未提供百分比");}
  void EndReadProgress() {
   readProgressTimer?.Stop();readWatch.Stop();
   ReadProgress.IsIndeterminate=false;
   ReadProgressPanel.Visibility=Visibility.Collapsed;
  }
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
  static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  void ApplyTitleColors() {
   try {
    var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
    int dark=1,caption=0x221710,text=0xF7EDE6;
    DwmSetWindowAttribute(handle,20,ref dark,4);
    DwmSetWindowAttribute(handle,35,ref caption,4);
    DwmSetWindowAttribute(handle,36,ref text,4);
   } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
  }
  public MainWindow() { Localization.Initialize(); InitializeComponent(); SetupLanguage(); SetupWorkspace(); SourceInitialized+=(s,e)=>ApplyTitleColors(); PreviewKeyDown+=(s,e)=>{if(e.Key==Key.F5)Refresh(s,e);if(e.Key==Key.Escape)SettingsPopup.IsOpen=false;}; Closing+=(s,e)=>{if(queueRunning && MessageBox.Show(L("关闭程序会停止后续定时任务。是否关闭？"),L("停止队列"),MessageBoxButton.YesNo)!=MessageBoxResult.Yes)e.Cancel=true; else {if(!PrepareClose())e.Cancel=true;}}; }
  async Task Guard(Func<Task> action,bool readOnly=false) {
   if(advanced!=null&&advanced.IsBusy){Status.Text="An operation is running on another page. Stop it or wait.";return;}
   if(busy) {
    if(readOnly && readCancellation!=null) {
     readCancellation.Cancel();
     await activeOperation;
     if(busy) return;
    } else {Status.Text=L("正在提交上一项变更，请稍候。");return;}
   }
   var finished=new TaskCompletionSource<bool>(); activeOperation=finished.Task;
   busy=true;
   if(readOnly) readCancellation=new CancellationTokenSource();
   CancelReadButton.IsEnabled=readOnly;
   if(readOnly) BeginReadProgress(L("读取 Steam"));
   try {Status.Text=readOnly?L("正在读取 Steam（每项最多 25 秒，可取消）…"):L("正在提交变更，请勿重复操作…");await action();Status.Text=L("完成 · ")+DateTime.Now.ToString("HH:mm:ss");}
   catch(OperationCanceledException) {Status.Text=L("已取消读取，可以切换游戏或重新尝试读取。");}
   catch(Exception e) {Status.Text=e.Message;}
   finally {EndReadProgress();busy=false;CancelReadButton.IsEnabled=false;readCancellation?.Dispose();readCancellation=null;finished.TrySetResult(true);}
  }
  void CancelRead(object s,RoutedEventArgs e) {Status.Text=L("正在取消读取…");readCancellation?.Cancel();}
  async Task<string> ReadCommand(params string[] args) {
   string stage=args.Length>0 && args[0]=="list-achievements"?L("读取成就"):args.Length>0 && args[0]=="list-statistics"?L("读取统计"):L("读取游戏库");
   BeginReadProgress(stage);
   try {return await Engine.RunRead(readCancellation?.Token??CancellationToken.None,25000,args);}
   finally {EndReadProgress();}
  }
  async void LoadGames(object s,RoutedEventArgs e) {await Guard(async()=>{allGames=Engine.Array(await ReadCommand("list-apps")).Select(d=>new GameRow{Id=Convert.ToUInt32(d["app_id"]),Name=Engine.Text(d,"app_name")}).OrderBy(g=>g.Name).ToList();SearchGames(null,null);},true);}
  void SearchGames(object s,TextChangedEventArgs e) {if(Games==null)return; string q=GameSearch.Text.Trim();Games.ItemsSource=allGames.Where(g=>g.Label.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();}
  async void PickGame(object s,SelectionChangedEventArgs e) {if(Games.SelectedItem is GameRow g) await Open(g.Id,g.Name);}
  async void OpenId(object s,RoutedEventArgs e) {if(uint.TryParse(AppIdInput.Text,out uint id)&&id>0)await Open(id,"AppID "+id);else Status.Text=L("请输入有效的正整数 AppID。");}
  async Task Open(uint id,string name) {if(queueRunning){Status.Text=L("请先停止当前定时队列再切换游戏。");return;} await Guard(async()=>{appId=id;advanced?.SetCurrentApp(id);GameTitle.Text=name+" / "+id;allAchievements.Clear();Achievements.ItemsSource=null;Stats.ItemsSource=null;await Read();},true);}
  async Task Read() {
   if(appId==0)return;
   Status.Text=L("正在读取成就（最多 25 秒，可取消）…");
   allAchievements=Engine.Array(await ReadCommand("list-achievements",appId.ToString())).Select(Engine.Achievement).ToList();
   ApplyFilter(); Summary.Text=(Localization.Language=="zh-CN" ? $"{allAchievements.Count(a=>a.Achieved)} / {allAchievements.Count} 已解锁 · {allAchievements.Count(a=>a.Achieved&&!a.Seconds.HasValue)} 项时间未知" : $"{allAchievements.Count(a=>a.Achieved)} / {allAchievements.Count} unlocked · {allAchievements.Count(a=>a.Achieved&&!a.Seconds.HasValue)} unknown timestamps");
   Status.Text=L("成就已读取，正在读取统计（最多 25 秒，可取消）…");
   var stats=new List<StatRow>();
   foreach(var wrapper in Engine.Array(await ReadCommand("list-statistics",appId.ToString()))) {bool integer=wrapper.ContainsKey("Integer");var d=Engine.Dict(wrapper.Values.First());stats.Add(new StatRow{Id=Engine.Text(d,"id"),Name=Engine.Text(d,"display_name"),Value=Engine.Text(d,integer?"int_value":"float_value"),Integer=integer,Permission=Convert.ToInt32(Engine.Get(d,"permission")),IncrementOnly=Convert.ToBoolean(Engine.Get(d,"is_increment_only")),Min=Convert.ToDouble(Engine.Get(d,"min_value")),Max=Convert.ToDouble(Engine.Get(d,"max_value"))});} Stats.ItemsSource=stats;
  }
  void FilterAchievements(object s,RoutedEventArgs e) {if(Achievements!=null&&StateFilter!=null&&Sort!=null)ApplyFilter();}
  void ApplyFilter() {string q=AchievementSearch.Text.Trim();var rows=allAchievements.Where(a=>(a.Name+" "+a.Description+" "+a.Id).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0).Where(a=>StateFilter.SelectedIndex==0||a.Achieved==(StateFilter.SelectedIndex==1)); switch(Sort.SelectedIndex){case 0:rows=rows.OrderBy(a=>!a.Seconds.HasValue).ThenByDescending(a=>a.Seconds).ThenBy(a=>a.Id);break;case 1:rows=rows.OrderBy(a=>!a.Seconds.HasValue).ThenBy(a=>a.Seconds).ThenBy(a=>a.Id);break;case 2:rows=rows.OrderBy(a=>a.Name);break;case 3:rows=rows.OrderByDescending(a=>a.Percent);break;} Achievements.ItemsSource=rows.ToList();}
  async void Refresh(object s,RoutedEventArgs e) {if(queueRunning){Status.Text=L("请先停止队列再刷新。");return;} await Guard(Read,true);}
  AchievementRow[] Selected(bool? achieved=null) => Achievements.SelectedItems.Cast<AchievementRow>().Where(a=>(a.Permission&3)==0&&(!achieved.HasValue||a.Achieved==achieved)).ToArray();
  bool Confirm(string title,string text) => MessageBox.Show(text,title,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
  void Journal(string operation,object details) {var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamAchievementManager-Remake");Directory.CreateDirectory(dir);File.AppendAllText(Path.Combine(dir,"history.jsonl"),Engine.Json().Serialize(new{at=DateTimeOffset.Now.ToString("o"),app_id=appId,operation,details})+Environment.NewLine,Encoding.UTF8);}
  async Task Change(bool unlock) {if(busy){Status.Text=L("请先取消读取或等待当前请求完成。");return;}if(queueRunning){Status.Text=L("请先停止定时队列。");return;}var rows=Selected(!unlock);if(rows.Length==0){Status.Text=L("请选中可修改且状态需要变化的成就（Ctrl / Shift 多选）。");return;}if(!Confirm(L("变更预览"),(Localization.Language=="zh-CN" ? $"游戏 {appId}\n将{(unlock?"解锁":"重新锁定")} {rows.Length} 项：\n" : $"Game {appId}\n{(unlock?"Unlock":"Relock")} {rows.Length} achievements:\n")+string.Join("\n",rows.Take(12).Select(a=>a.Name))+L("\n\n是否提交给 Steam？")))return;await Guard(async()=>{await Backups.Save(appId,unlock?"Before manual unlock":"Before manual relock");var result=await Engine.Run(new[]{unlock?"unlock":"lock",appId.ToString()}.Concat(rows.Select(a=>a.Id)).ToArray());var parsed=Engine.Array(result).ToList();Journal(unlock?"unlock":"lock",parsed);if(parsed.Any(d=>!Convert.ToBoolean(d["success"])))throw new Exception(L("部分条目被 Steam 拒绝，请刷新检查。"));await Read();});}
  async void Unlock(object s,RoutedEventArgs e)=>await Change(true);
  async void Relock(object s,RoutedEventArgs e)=>await Change(false);
  async void SaveStat(object s,RoutedEventArgs e) {if(busy)return;if(queueRunning)return;if(!(Stats.SelectedItem is StatRow stat)||appId==0)return;string value=StatValue.Text.Trim();if((stat.Permission&2)!=0){Status.Text=L("该统计受保护。");return;}if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out double number)||double.IsNaN(number)||double.IsInfinity(number)||(stat.Integer&&(number!=Math.Truncate(number)||number<int.MinValue||number>int.MaxValue))){Status.Text=L("请输入有效数字，小数请使用英文句点。");return;}if((stat.Min<stat.Max&&(number<stat.Min||number>stat.Max))||(stat.IncrementOnly&&number<double.Parse(stat.Value,CultureInfo.InvariantCulture))){Status.Text=L("新值不符合该统计的范围或仅可增加限制。");return;}if(!Confirm(L("统计变更预览"),(Localization.Language=="zh-CN" ? $"{stat.Name}\n{stat.Value} → {value}\n是否写入？" : $"{stat.Name}\n{stat.Value} → {value}\nSave this value?")))return;await Guard(async()=>{await Backups.Save(appId,"Before statistic edit");await Engine.Run("set-stat",appId.ToString(),stat.Id,value);Journal("set-stat",new{stat.Id,before=stat.Value,after=value});await Read();});}
  async void Export(object s,RoutedEventArgs e) {if(busy)return;if(appId==0)return;var dialog=new SaveFileDialog{Filter="JSON|*.json",FileName=$"steam-{appId}.json"};if(dialog.ShowDialog()!=true)return;await Guard(async()=>{var json=await Engine.Run("export",appId.ToString());Engine.Json().DeserializeObject(json);File.WriteAllText(dialog.FileName,json,new UTF8Encoding(false));});}
  async void Import(object s,RoutedEventArgs e) {if(busy)return;if(appId==0||queueRunning)return;var dialog=new OpenFileDialog{Filter="JSON|*.json"};if(dialog.ShowDialog()!=true)return;if(!Confirm(L("导入预览"),(Localization.Language=="zh-CN" ? $"将从 {Path.GetFileName(dialog.FileName)} 导入当前游戏 {appId} 的成就和统计。\n已有历史解锁时间无法恢复。是否继续？" : $"Import achievements and statistics from {Path.GetFileName(dialog.FileName)} for game {appId}?\nHistorical unlock timestamps cannot be restored. Continue?")))return;await Guard(async()=>{await Backups.Save(appId,"Before progress import");var result=await Engine.Run("import",dialog.FileName,"--app-id",appId.ToString());Journal("import",result);await Read();});}
  async void Schedule(object s,RoutedEventArgs e) {if(queueRunning||busy||(advanced!=null&&advanced.IsBusy))return;var rows=Selected(false);if(rows.Length==0){Status.Text=L("先在成就表中选中未解锁条目，再打开定时队列。");return;}if(!int.TryParse(Interval.Text,out int seconds)||seconds<1||seconds>86400){Status.Text=L("间隔应为 1–86400 秒。");return;}if(!Confirm(L("定时队列预览"),(Localization.Language=="zh-CN" ? $"游戏 {appId}\n{rows.Length} 项，每隔 {seconds} 秒执行一项，首项也会等待。\n" : $"Game {appId}\n{rows.Length} achievements, one every {seconds} seconds. The first item also waits.\n")+string.Join("\n",rows.Take(10).Select(a=>a.Name))+L("\n保持本程序运行，关闭后不自动补执行。是否启动？")))return;
   queue=new CancellationTokenSource();queueRunning=true;var token=queue.Token;Tabs.SelectedIndex=2;int done=0;try {await Backups.Save(appId,"Before timed unlock");await TimedQueue.Run(rows,TimeSpan.FromSeconds(seconds),async row=>{var result=await Engine.Run("unlock",appId.ToString(),row.Id);if(Engine.Array(result).Any(d=>!Convert.ToBoolean(d["success"])))throw new Exception(L("Steam 拒绝该条目"));done++;Journal("scheduled-unlock",row.Id);QueueLog.AppendText((Localization.Language=="zh-CN" ? $"{DateTime.Now:HH:mm:ss} 已提交：{row.Name}\n" : $"{DateTime.Now:HH:mm:ss} Submitted: {row.Name}\n"));}, (index,row)=>QueueStatus.Text=(Localization.Language=="zh-CN" ? $"{index}/{rows.Length} · 等待 {row.Name}" : $"{index}/{rows.Length} · Waiting for {row.Name}"),token);QueueStatus.Text=(Localization.Language=="zh-CN" ? $"完成 {done}/{rows.Length} · 按 F5 重新读取真实时间" : $"Completed {done}/{rows.Length} · Press F5 to read the timestamps from Steam");}catch(OperationCanceledException){QueueStatus.Text=(Localization.Language=="zh-CN" ? $"已停止 · 完成 {done}/{rows.Length} 项；正在提交的请求可能仍然完成" : $"Stopped · Completed {done}/{rows.Length}; an in-flight request may still finish");}catch(Exception ex){QueueStatus.Text=L("队列停止：")+ex.Message;}finally{queueRunning=false;queue.Dispose();queue=null;}
  }
  void StopSchedule(object s,RoutedEventArgs e)=>queue?.Cancel();
  void OpenSettings(object sender,RoutedEventArgs e) {SettingsPopup.IsOpen=!SettingsPopup.IsOpen;}
  bool settingLanguage;
  void SetupLanguage() {settingLanguage=true;LanguageSelector.SelectedIndex=Localization.Language=="zh-CN"?1:0;settingLanguage=false;}
  void ChangeLanguage(object sender,SelectionChangedEventArgs e) {
   if(settingLanguage || LanguageSelector==null || LanguageSelector.SelectedIndex<0)return;
   try {Localization.SetLanguage(LanguageSelector.SelectedIndex==1?"zh-CN":"en");Achievements.Items.Refresh();Stats.Items.Refresh();SetupNavigation();advanced?.RefreshLanguage();}
   catch(Exception ex) {Status.Text=ex.Message;}
  }
  AdvancedWorkspace advanced;
  TabItem advancedHost;
  bool closingAfterIdle;
  readonly List<Button> navigationButtons=new List<Button>();
  string B(string en,string zh)=>Localization.Language=="zh-CN"?zh:en;
  void SetupWorkspace(){advanced=new AdvancedWorkspace(this,()=>busy||queueRunning,appId);SetupNavigation();Tabs.SelectedIndex=0;}
  void SetupNavigation(){
   NavigationPanel.Children.Clear();navigationButtons.Clear();
   AddNavigationGroup(B("GAME PROGRESS","游戏进度"),new[]{0,1},new[]{B("Achievements","成就"),B("Statistics","统计编辑")});
   AddNavigationGroup(B("AUTOMATION","自动操作"),new[]{2,4,5},new[]{B("Schedule","定时队列"),B("Friend replay","好友回放"),B("Idle","挂机")});
   AddNavigationGroup(B("DATA MANAGEMENT","数据管理"),new[]{3,6},new[]{B("Batch operations","跨游戏批处理"),B("History / restore","历史与恢复")});
   HighlightNavigation();
  }
  void AddNavigationGroup(string title,int[] indexes,string[] labels){
   var group=new StackPanel{Margin=new Thickness(0,0,18,0)};
   group.Children.Add(new TextBlock{Text=title,FontSize=10,Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(159,176,200)),Margin=new Thickness(0,0,0,4)});
   var row=new StackPanel{Orientation=Orientation.Horizontal};group.Children.Add(row);
   for(int i=0;i<indexes.Length;i++){
    var button=new Button{Content=labels[i],Tag=indexes[i],Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,5,0)};
    button.Click+=(sender,e)=>{Tabs.SelectedIndex=(int)((Button)sender).Tag;};row.Children.Add(button);navigationButtons.Add(button);
   }
   NavigationPanel.Children.Add(group);
  }
  void HighlightNavigation(){foreach(var button in navigationButtons){bool active=(int)button.Tag==Tabs.SelectedIndex;button.Background=new System.Windows.Media.SolidColorBrush(active?System.Windows.Media.Color.FromRgb(34,84,92):System.Windows.Media.Color.FromRgb(38,56,77));}}
  void NavigateContent(object sender,SelectionChangedEventArgs e){
   if(e.Source!=Tabs||advanced==null)return;
   if(advancedHost!=null){advancedHost.Content=null;advancedHost=null;}
   if(Tabs.SelectedIndex>=3){advancedHost=(TabItem)Tabs.SelectedItem;advanced.SelectPage(Tabs.SelectedIndex-3);advancedHost.Content=advanced;}
   HighlightNavigation();
  }
  bool PrepareClose(){
   if(busy&&readCancellation==null){Status.Text=B("Wait for the active Steam write to finish.","请等待当前 Steam 写入完成。");return false;}
   if(advanced==null){queue?.Cancel();readCancellation?.Cancel();return true;}
   if(advanced.IsWriting){Status.Text=B("Wait for the active Steam write to finish.","请等待当前 Steam 写入完成。");return false;}
   queue?.Cancel();readCancellation?.Cancel();advanced.CancelPending();
   if(advanced.IsIdling){if(!closingAfterIdle)CloseAfterIdle();return false;}return true;
  }
  async void CloseAfterIdle(){closingAfterIdle=true;try{await advanced.ShutdownIdle();Close();}catch(Exception ex){Status.Text=ex.Message;closingAfterIdle=false;}}

 }
}
