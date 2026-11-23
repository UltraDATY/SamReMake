// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Remake {
 public class SnapshotRecord {
  public string Id {get;set;} public uint AppId {get;set;} public string At {get;set;} public string Reason {get;set;} public string File {get;set;}
  public string Label => At+" · "+AppId+" · "+Reason;
 }
 public static class Backups {
  public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamAchievementManager-Remake","snapshots");
  public static async Task<SnapshotRecord> Save(uint app,string reason) {
   var json=await Engine.RunRead(System.Threading.CancellationToken.None,25000,"export",app.ToString());
   var doc=Engine.Dict(Engine.Json().DeserializeObject(json));
   if(!doc.ContainsKey("apps"))throw new Exception("Invalid backup response; no changes were submitted.");
   Directory.CreateDirectory(Root);var id=Guid.NewGuid().ToString("N");
   var record=new SnapshotRecord{Id=id,AppId=app,At=DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),Reason=reason,File=id+".json"};
   System.IO.File.WriteAllText(Path.Combine(Root,record.File),json,new UTF8Encoding(false));
   System.IO.File.WriteAllText(Path.Combine(Root,id+".meta"),Engine.Json().Serialize(record),new UTF8Encoding(false));return record;
  }
  public static List<SnapshotRecord> List() {
   if(!Directory.Exists(Root))return new List<SnapshotRecord>();
   var rows=new List<SnapshotRecord>();foreach(var file in Directory.GetFiles(Root,"*.meta"))try {var r=Engine.Json().Deserialize<SnapshotRecord>(System.IO.File.ReadAllText(file));if(r!=null&&r.File==r.Id+".json"&&r.Id.All(Uri.IsHexDigit))rows.Add(r);}catch{}
   return rows.OrderByDescending(r=>r.At).ToList();
  }
  public static string PathFor(SnapshotRecord r) {
   if(r.Id.Length!=32||!r.Id.All(Uri.IsHexDigit)||r.File!=r.Id+".json")throw new Exception("Invalid snapshot path");
   return Path.Combine(Root,r.File);
  }
 }
 public class ReplayStep {public string Id {get;set;}public string Name {get;set;}public TimeSpan Delay {get;set;}}
 public static class ReplayPlan {
  public static List<ReplayStep> Build(IEnumerable<Dictionary<string,object>> source,ISet<string> eligible,TimeSpan duration) {
   if(duration<=TimeSpan.Zero||duration>TimeSpan.FromDays(7))throw new ArgumentOutOfRangeException(nameof(duration));
   var rows=source.Where(d=>Convert.ToBoolean(Engine.Get(d,"achieved"))&&Engine.Get(d,"unlock_time")!=null&&Convert.ToInt64(d["unlock_time"])>0&&eligible.Contains(Engine.Text(d,"api_name"))).OrderBy(d=>Convert.ToInt64(d["unlock_time"])).ThenBy(d=>Engine.Text(d,"api_name"),StringComparer.Ordinal).GroupBy(d=>Engine.Text(d,"api_name")).Select(g=>g.First()).ToList();
   if(rows.Count==0)return new List<ReplayStep>();
   long first=Convert.ToInt64(rows[0]["unlock_time"]),last=Convert.ToInt64(rows[rows.Count-1]["unlock_time"]),previous=first;
   return rows.Select(d=> {long time=Convert.ToInt64(d["unlock_time"]);double fraction=last==first?0:(double)(time-previous)/(last-first);previous=time;return new ReplayStep{Id=Engine.Text(d,"api_name"),Name=Engine.Text(d,"display_name"),Delay=TimeSpan.FromMilliseconds(duration.TotalMilliseconds*fraction)};}).ToList();
  }
 }
}
