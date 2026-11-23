using System;
using System.Web.Script.Serialization;
class Fake {static int Main(string[] args) {Console.OutputEncoding=new System.Text.UTF8Encoding(false);if(Array.IndexOf(args,"list-hang")>=0)System.Threading.Thread.Sleep(60000);if(Array.IndexOf(args,"fail")>=0){Console.Error.Write("simulated refusal");return 2;}Console.Write(new JavaScriptSerializer().Serialize(args));return 0;}}
