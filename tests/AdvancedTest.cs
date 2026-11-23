using System;
using System.Linq;
using System.Collections.Generic;
using Remake;
class AdvancedTest {
 static Dictionary<string,object> Row(string id,long? time,bool achieved=true) {return new Dictionary<string,object>{{"api_name",id},{"display_name",id},{"achieved",achieved},{"unlock_time",time}};}
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main(){
 var input=new[]{Row("A",100),Row("B",200),Row("C",400),Row("Missing",null),Row("Locked",500,false),Row("B",200)};
 var plan=ReplayPlan.Build(input,new HashSet<string>(new[]{"A","B","C","Missing","Locked"}),TimeSpan.FromMinutes(3));
 Check(plan.Select(x=>x.Id).SequenceEqual(new[]{"A","B","C"}),"filter unknown/locked/duplicates");
 Check(plan[0].Delay==TimeSpan.Zero&&plan[1].Delay==TimeSpan.FromMinutes(1)&&plan[2].Delay==TimeSpan.FromMinutes(2),"scaled relative intervals");
 var equal=ReplayPlan.Build(new[]{Row("A",100),Row("B",100)},new HashSet<string>(new[]{"A","B"}),TimeSpan.FromMinutes(1));Check(equal.All(x=>x.Delay==TimeSpan.Zero),"equal source times");
 Check(ReplayPlan.Build(input,new HashSet<string>(),TimeSpan.FromMinutes(1)).Count==0,"exclude current unlocked");
 bool blocked=false;try{Backups.PathFor(new SnapshotRecord{Id="../escape",File="../escape.json"});}catch{blocked=true;}Check(blocked,"snapshot path confined");
 Check(Localization.Language=="en"&&Localization.L("已解锁")=="Unlocked","English default");
 Console.WriteLine("PASS: English default; friend replay scaling/filtering/equal dates; snapshot path validation");
 }
}
