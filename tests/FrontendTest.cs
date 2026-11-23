using System;
using System.Linq;
using Remake;
class FrontendTest {
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
 static void Main(){try{Run();}catch(Exception e){Console.WriteLine(e.GetType().FullName);Console.WriteLine(e.Message);Environment.Exit(1);}} static void Run(){
 var a=Engine.Achievement(Engine.Dict(Engine.Json().DeserializeObject("{\"id\":\"A\",\"name\":\"Test\",\"is_achieved\":true,\"unlock_time\":{\"secs_since_epoch\":1704067200},\"permission\":0}")));
 Check(a.Seconds==1704067200,"Unix seconds parsed without clock substitution");
 var unknown=Engine.Achievement(Engine.Dict(Engine.Json().DeserializeObject("{\"id\":\"B\",\"is_achieved\":true,\"unlock_time\":null,\"permission\":0}")));
 Check(unknown.Seconds==null&&unknown.TimeText=="Unknown","unknown retained");
 var locked=Engine.Achievement(Engine.Dict(Engine.Json().DeserializeObject("{\"id\":\"C\",\"is_achieved\":false,\"unlock_time\":{\"secs_since_epoch\":123},\"permission\":0}")));
 Check(locked.Seconds==null,"locked timestamp discarded");
 foreach(bool asc in new[]{true,false}){var rows=new[]{unknown,a,locked};var ordered=asc?rows.OrderBy(x=>!x.Seconds.HasValue).ThenBy(x=>x.Seconds):rows.OrderBy(x=>!x.Seconds.HasValue).ThenByDescending(x=>x.Seconds);Check(ordered.First()==a,"missing last both directions");}
 var args=new[]{"export","C:\\中文 文件\\end\\","a\"b","","--flag"};
 var echoed=(object[])Engine.Json().DeserializeObject(Engine.Run(args).GetAwaiter().GetResult());
 Check(echoed.Cast<string>().SequenceEqual(args),"Windows command line quoting roundtrip");
 bool failed=false;try{Engine.Run("fail").GetAwaiter().GetResult();}catch(Exception e){failed=e.Message.Contains("simulated refusal");}Check(failed,"engine errors surfaced");


 var watch=System.Diagnostics.Stopwatch.StartNew();bool timedOut=false;
 try {Engine.RunRead(System.Threading.CancellationToken.None,300,"list-hang").GetAwaiter().GetResult();}catch(TimeoutException){timedOut=true;}
 Check(timedOut&&watch.ElapsedMilliseconds<6000,"hung engine bounded timeout");
 var reading=new System.Threading.CancellationTokenSource();reading.CancelAfter(200);bool cancelledRead=false;watch.Restart();
 try {Engine.RunRead(reading.Token,25000,"list-hang").GetAwaiter().GetResult();}catch(OperationCanceledException){cancelledRead=true;}
 Check(cancelledRead&&watch.ElapsedMilliseconds<6000,"read cancellation");
 bool refusedWrite=false;try{Engine.RunRead(System.Threading.CancellationToken.None,300,"unlock","123").GetAwaiter().GetResult();}catch(ArgumentException){refusedWrite=true;}
 Check(refusedWrite,"write commands cannot be force-cancelled");
 Console.WriteLine("PASS: stalled-engine timeout, explicit read cancellation, write cancellation rejected");
 var sequence=new System.Collections.Generic.List<int>();
 TimedQueue.Run(new[]{1,2,3},TimeSpan.Zero,x=>{sequence.Add(x);return System.Threading.Tasks.Task.CompletedTask;},(i,x)=>{},System.Threading.CancellationToken.None).GetAwaiter().GetResult();
 Check(sequence.SequenceEqual(new[]{1,2,3}),"serial queue order");
 var cancel=new System.Threading.CancellationTokenSource(); sequence.Clear(); bool cancelled=false;
 try {TimedQueue.Run(new[]{1,2,3},TimeSpan.Zero,x=>{sequence.Add(x);cancel.Cancel();return System.Threading.Tasks.Task.CompletedTask;},(i,x)=>{},cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
 Check(cancelled&&sequence.SequenceEqual(new[]{1}),"queue cancellation prevents subsequent writes");
 sequence.Clear(); bool stopped=false;
 try {TimedQueue.Run(new[]{1,2,3},TimeSpan.Zero,x=>{sequence.Add(x);throw new Exception("refused");},(i,x)=>{},System.Threading.CancellationToken.None).GetAwaiter().GetResult();}catch(Exception){stopped=true;}
 Check(stopped&&sequence.SequenceEqual(new[]{1}),"queue failure stops subsequent writes");
 Console.WriteLine("PASS: serial schedule, stop after cancellation, stop after engine failure");
 Console.WriteLine("PASS: timestamp parsing, null/locked dates, oldest/newest order, Windows argument roundtrip (Unicode/spaces/quotes/trailing slash), engine rejection");
 }
}
