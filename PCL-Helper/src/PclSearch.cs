using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Mono.Cecil;

namespace PclHelper {
 public sealed class PclSearchPlan {
  public string[] QuickRoots=new string[0];
  public string[] FullRoots=new string[0];
  public string[] DirectFiles=new string[0];
  public string[] ShortcutRoots=new string[0];
 }
 public sealed class PclCandidate {
  public string Path {get;set;}
  public string Version {get;set;}
  public bool Supported {get;set;}
  public string State {get;set;}
  public string Summary {get{return "PCL "+Version+" · "+(Supported?State:"此版本暂不支持");}}
 }
 public sealed class PclSearchProgress {
  public string Stage;
  public string Directory;
  public int Directories;
  public int Found;
 }
 public sealed class PclSearchResult {
  public readonly List<PclCandidate> Candidates=new List<PclCandidate>();
  public bool UsedFullScan;
  public bool Cancelled;
  public int SkippedDirectories;
  public int Directories;
 }
 public static class PclSearch {
  public static PclSearchPlan CreateDefaultPlan() {
   var user=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   var desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
   var roots=new List<string>{AppDomain.CurrentDomain.BaseDirectory,desktop,
    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(user,"Downloads")};
   try {
    var downloads=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders","{374DE290-123F-4565-9164-39C4925E467B}",null) as string;
    if(!string.IsNullOrWhiteSpace(downloads))roots.Insert(0,Environment.ExpandEnvironmentVariables(downloads));
   }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
   var disks=new List<string>();
   try{foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType==DriveType.Fixed && drive.IsReady)disks.Add(drive.RootDirectory.FullName);}catch(IOException){}catch(UnauthorizedAccessException){}}catch(IOException){}
   return new PclSearchPlan {
    QuickRoots=roots.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
    FullRoots=disks.ToArray(),ShortcutRoots=new[]{desktop,Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
     Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)}
   };
  }
  // Shell shortcut COM objects are created and released on this dedicated STA worker.
  public static Task<PclSearchResult> FindAsync(PclSearchPlan plan,CancellationToken token,Action<PclSearchProgress> progress) {
   var completion=new TaskCompletionSource<PclSearchResult>();
   var thread=new Thread(()=>{try{completion.SetResult(Find(plan ?? CreateDefaultPlan(),token,progress));}catch(Exception ex){completion.SetException(ex);}});
   thread.IsBackground=true;thread.Name="PCL file search";thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
  }
  public static PclSearchResult Find(PclSearchPlan plan,CancellationToken token,Action<PclSearchProgress> progress) {
   if(plan==null)throw new ArgumentNullException("plan");
   return new Scan(plan,token,progress).Run();
  }
  sealed class Scan {
   readonly PclSearchPlan plan;
   readonly CancellationToken token;
   readonly Action<PclSearchProgress> progress;
   readonly PclSearchResult result=new PclSearchResult();
   readonly HashSet<string> seenFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   readonly Stopwatch clock=Stopwatch.StartNew();
   long lastReport=-200;
   string stage="常用位置",current="";
   static readonly HashSet<string> excluded=new HashSet<string>(new[]{"Windows","$Recycle.Bin","System Volume Information",".git",".test-data","node_modules",".cache"},StringComparer.OrdinalIgnoreCase);
   internal Scan(PclSearchPlan p,CancellationToken t,Action<PclSearchProgress> report){plan=p;token=t;progress=report;}
   internal PclSearchResult Run() {
    Report(true);
    foreach(var file in plan.DirectFiles ?? new string[0]){if(token.IsCancellationRequested)break;Probe(file,true);}
    if(!token.IsCancellationRequested)Shortcuts();
    if(!token.IsCancellationRequested)Walk(plan.QuickRoots,3,false);
    if(!token.IsCancellationRequested && !result.Candidates.Any(c=>c.Supported)) {
     result.UsedFullScan=true;stage="本地磁盘";Report(true);Walk(plan.FullRoots,int.MaxValue,false);
    }
    result.Cancelled=token.IsCancellationRequested;
    result.Candidates.Sort((a,b)=>{var supported=b.Supported.CompareTo(a.Supported);return supported!=0?supported:StringComparer.OrdinalIgnoreCase.Compare(a.Path,b.Path);});
    Report(true);return result;
   }
   void Report(bool force) {
    if(progress==null || (!force && clock.ElapsedMilliseconds-lastReport<150))return;
    lastReport=clock.ElapsedMilliseconds;
    progress(new PclSearchProgress{Stage=stage,Directory=current,Directories=result.Directories,Found=result.Candidates.Count});
   }
   static bool Local(string path) {
    if(string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith(@"\\",StringComparison.Ordinal))return false;
    try{var drive=new DriveInfo(Path.GetPathRoot(path));return drive.DriveType==DriveType.Fixed || drive.DriveType==DriveType.Removable;}
    catch(ArgumentException){return false;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
   }
   bool NoLinks(string path) {
    // Check from the root down, before touching a child behind a junction/UNC redirect.
    var root=Path.GetPathRoot(path);var current=root;
    if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;
    foreach(var part in path.Substring(root.Length).Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries)) {
     if(token.IsCancellationRequested)return false;
     current=Path.Combine(current,part);
     if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;
    }
    return true;
   }
   void Probe(string file,bool direct) {
    if(token.IsCancellationRequested || !Local(file) || !string.Equals(Path.GetExtension(file),".exe",StringComparison.OrdinalIgnoreCase))return;
    try {
     file=Path.GetFullPath(file);
     if(!NoLinks(file) || !File.Exists(file) || !seenFiles.Add(file))return;
     if(!direct && !NameMatches(Path.GetFileName(file))) {
      var version=FileVersionInfo.GetVersionInfo(file);
      if(!NameMatches(version.ProductName) && !NameMatches(version.OriginalFilename) && !NameMatches(version.FileDescription))return;
     }
     // Read metadata only: candidate programs are never loaded or executed.
     using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
     using(var assembly=AssemblyDefinition.ReadAssembly(input,new ReaderParameters{ReadSymbols=false,InMemory=true})) {
      var types=assembly.MainModule.Types;
      var secret=types.FirstOrDefault(t=>t.Name=="ModSecret" && t.Namespace=="PCL");
      if(secret==null || !types.Any(t=>t.Name=="PageDownloadInstall" && t.Namespace=="PCL") || !types.Any(t=>t.Name=="ModDownload" && t.Namespace=="PCL"))return;
      var flag=secret.Fields.FirstOrDefault(f=>f.Name=="RestrictMcDownload" && f.IsLiteral && f.Constant is bool);
      if(flag==null)return;
      var version=assembly.Name.Version.ToString();
      result.Candidates.Add(new PclCandidate{Path=file,Version=version,Supported=version=="2.99.0.0",
       State=secret.Fields.Any(f=>f.Name==Patcher.Marker)?"已恢复下载":(bool)flag.Constant?"可恢复下载":"下载未受限"});
     }
     Report(true);
    }catch(IOException){}catch(UnauthorizedAccessException){}catch(BadImageFormatException){}catch(ArgumentException){}catch(NotSupportedException){}catch(InvalidOperationException){}catch(System.Security.SecurityException){}catch(System.ComponentModel.Win32Exception){}catch(IndexOutOfRangeException){}
   }
   static bool NameMatches(string name){return !string.IsNullOrEmpty(name) && (name.IndexOf("PCL",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Plain Craft",StringComparison.OrdinalIgnoreCase)>=0);}
   sealed class Folder {internal string Path;internal int Remaining;}
   void Walk(string[] roots,int depth,bool shortcuts) {
    var pending=new Stack<Folder>();var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var root in (roots ?? new string[0]).Reverse())if(Local(root))pending.Push(new Folder{Path=root,Remaining=depth});
    while(pending.Count>0 && !token.IsCancellationRequested) {
     var item=pending.Pop();
     try {
      var directory=Path.GetFullPath(item.Path);
      if(!visited.Add(directory) || excluded.Contains(new DirectoryInfo(directory).Name))continue;
      if(!NoLinks(directory)){result.SkippedDirectories++;continue;}
      current=directory;result.Directories++;Report(false);
      foreach(var file in Directory.EnumerateFiles(directory,shortcuts?"*.lnk":"*.exe",SearchOption.TopDirectoryOnly)) {
       if(token.IsCancellationRequested)break;
       if(shortcuts)ReadShortcut(file);else Probe(file,false);
      }
      if(item.Remaining>0 && !token.IsCancellationRequested)foreach(var child in Directory.EnumerateDirectories(directory)) {
       if(token.IsCancellationRequested)break;pending.Push(new Folder{Path=child,Remaining=item.Remaining-1});
      }
     }catch(IOException){result.SkippedDirectories++;}catch(UnauthorizedAccessException){result.SkippedDirectories++;}catch(System.Security.SecurityException){result.SkippedDirectories++;}catch(ArgumentException){result.SkippedDirectories++;}
    }
   }
   object shell;
   void Shortcuts() {
    try {
     var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return;shell=Activator.CreateInstance(type);
     Walk(plan.ShortcutRoots,4,true);
    }catch(COMException){}catch(UnauthorizedAccessException){}
    finally{if(shell!=null)Marshal.FinalReleaseComObject(shell);shell=null;}
   }
   void ReadShortcut(string file) {
    object shortcut=null;
    try {
     if(!NoLinks(file))return;
     shortcut=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{file});
     var target=shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null) as string;
     Probe(target,true);
    }catch(COMException){}catch(TargetInvocationException){}catch(ArgumentException){}catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);}
   }
  }
 }
}
