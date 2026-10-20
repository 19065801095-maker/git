using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Mono.Cecil;

class SearchTest {
 static Assembly helper;
 static Type engine,planType;
 static int count;
 static CancellationTokenSource cancelOnProgress;
 [STAThread] static int Main(string[] args) {
  try {
   helper=Assembly.LoadFrom(Path.GetFullPath(args[0]));
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {
    if(new AssemblyName(e.Name).Name!="Mono.Cecil")return null;
    using(var input=helper.GetManifestResourceStream("Mono.Cecil.dll")) {
     var bytes=new byte[input.Length];input.Read(bytes,0,bytes.Length);return Assembly.Load(bytes);
    }
   };
   return Run(args);
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args) {
  engine=helper.GetType("PclHelper.PclSearch");planType=helper.GetType("PclHelper.PclSearchPlan");
  Assert(engine!=null && planType!=null,"one-click search engine is available");
  var root=Path.GetFullPath(args[2]);Directory.CreateDirectory(root);
  var quick=Path.Combine(root,"quick");var full=Path.Combine(root,"disk");Directory.CreateDirectory(quick);Directory.CreateDirectory(full);
  var first=Path.Combine(quick,"Plain Craft Launcher 2.exe");File.Copy(args[1],first,true);
  File.Copy(args[0],Path.Combine(quick,"PCL-impostor.exe"),true);
  File.WriteAllText(Path.Combine(quick,"PCL-corrupt.exe"),"not a PE image");
  var result=Find(new[]{quick},new[]{full},new[]{first,first},CancellationToken.None,null);
  Assert(Items(result).Count==1,"identity rejects fake names and duplicate paths collapse");
  Assert(!(bool)Get(result,"UsedFullScan"),"valid quick result avoids unnecessary disk scan");
  var item=Items(result)[0];Assert((string)Get(item,"Path")==first && (bool)Get(item,"Supported"),"candidate exposes exact path and supported version");
  var renamed=Path.Combine(full,"我的游戏.exe");File.Copy(args[1],renamed,true);
  result=Find(new[]{Path.Combine(root,"missing")},new[]{full},new string[0],CancellationToken.None,null);
  Assert((bool)Get(result,"UsedFullScan") && Items(result).Count==1,"disk fallback finds PCL renamed by the user using version metadata");
  var second=Path.Combine(full,"PCL2.exe");File.Copy(args[1],second,true);
  result=Find(new string[0],new[]{full},new string[0],CancellationToken.None,null);
  Assert(Items(result).Count==2,"multiple distinct PCL paths are preserved for user selection");
  var older=Path.Combine(quick,"PCL-old.exe");
  using(var assembly=AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("PCL test fixture",new Version(2,98,0,0)),"fixture",ModuleKind.Dll)) {
   var module=assembly.MainModule;
   var secret=new TypeDefinition("PCL","ModSecret",Mono.Cecil.TypeAttributes.Public,module.TypeSystem.Object);
   secret.Fields.Add(new FieldDefinition("RestrictMcDownload",Mono.Cecil.FieldAttributes.Static|Mono.Cecil.FieldAttributes.Literal|Mono.Cecil.FieldAttributes.HasDefault,module.TypeSystem.Boolean){Constant=true});
   module.Types.Add(secret);
   module.Types.Add(new TypeDefinition("PCL","PageDownloadInstall",Mono.Cecil.TypeAttributes.Public,module.TypeSystem.Object));
   module.Types.Add(new TypeDefinition("PCL","ModDownload",Mono.Cecil.TypeAttributes.Public,module.TypeSystem.Object));assembly.Write(older);
  }
  result=Find(new string[0],new string[0],new[]{older},CancellationToken.None,null);
  Assert(Items(result).Count==1 && !(bool)Get(Items(result)[0],"Supported"),"other PCL version is displayed as unsupported instead of misclassified");
  // An unsupported quick result must not hide a supported copy elsewhere.
  result=Find(new[]{quick},new[]{full},new[]{older},CancellationToken.None,null);
  Assert(Items(result).Count==2,"quick scan preserves all real candidates");
  var onlyOld=Path.Combine(root,"old-only");Directory.CreateDirectory(onlyOld);File.Copy(older,Path.Combine(onlyOld,"PCL.exe"),true);
  result=Find(new[]{onlyOld},new[]{full},new string[0],CancellationToken.None,null);
  Assert((bool)Get(result,"UsedFullScan") && Items(result).Count==3,"unsupported quick result still searches disks for a compatible version");
  var cts=new CancellationTokenSource();cts.Cancel();
  result=Find(new[]{quick},new[]{full},new[]{first},cts.Token,null);
  Assert((bool)Get(result,"Cancelled") && Items(result).Count==0,"pre-cancelled search returns promptly without scanning");
  cancelOnProgress=new CancellationTokenSource();
  var callbackType=engine.GetMethod("Find").GetParameters()[2].ParameterType;
  var callback=Delegate.CreateDelegate(callbackType,typeof(SearchTest).GetMethod("Progress",BindingFlags.Static|BindingFlags.NonPublic));
  result=Find(new[]{quick},new[]{full},new[]{first},cancelOnProgress.Token,callback);
  Assert((bool)Get(result,"Cancelled") && Items(result).Count==1,"cancelling after a discovery preserves partial results");
  var shortcuts=Path.Combine(root,"shortcuts");Directory.CreateDirectory(shortcuts);
  object shell=null,shortcut=null;
  try {
   shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
   shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path.Combine(shortcuts,"游戏启动.lnk")});
   shortcut.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{renamed});
   shortcut.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
  }finally{if(shortcut!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
  var plan=Plan(new string[0],new string[0],new string[0]);planType.GetField("ShortcutRoots").SetValue(plan,new[]{shortcuts});
  result=Invoke(plan,CancellationToken.None,null);
  Assert(Items(result).Count==1 && (string)Get(Items(result)[0],"Path")==renamed,"shortcut lookup resolves a differently named link without launching it");
  var junction=Path.Combine(root,"linked-folder");
  var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+junction+"\" \""+full+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});
  var commandOutput=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit();
  if(process.ExitCode!=0)throw new Exception("Cannot create junction fixture: "+commandOutput);process.Dispose();
  result=Find(new string[0],new string[0],new[]{Path.Combine(junction,"PCL2.exe")},CancellationToken.None,null);
  Assert(Items(result).Count==0,"direct targets behind ancestor directory links are skipped before opening the executable");
  Assert(PclHash(first)==PclHash(args[1]) && PclHash(renamed)==PclHash(args[1]),"search never modifies launcher files");
  Console.WriteLine("PASS: "+count+" search assertions.");return 0;
 }
 static void Progress(object value){if((int)Get(value,"Found")>0)cancelOnProgress.Cancel();}
 static object Get(object target,string name){var prop=target.GetType().GetProperty(name);return prop!=null?prop.GetValue(target,null):target.GetType().GetField(name).GetValue(target);}
 static IList Items(object result){return (IList)Get(result,"Candidates");}
 static object Plan(string[] quick,string[] full,string[] direct){var p=Activator.CreateInstance(planType);planType.GetField("QuickRoots").SetValue(p,quick);planType.GetField("FullRoots").SetValue(p,full);planType.GetField("DirectFiles").SetValue(p,direct);return p;}
 static object Find(string[] quick,string[] full,string[] direct,CancellationToken token,Delegate progress){return Invoke(Plan(quick,full,direct),token,progress);}
 static object Invoke(object plan,CancellationToken token,Delegate progress){return engine.GetMethod("Find").Invoke(null,new object[]{plan,token,progress});}
 static string PclHash(string file){using(var sha=System.Security.Cryptography.SHA256.Create())using(var input=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(input));}
 static void Assert(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);count++;Console.WriteLine("OK: "+message);}
}
