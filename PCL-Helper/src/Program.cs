using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
[assembly: AssemblyTitle("PCL-Helper")]
[assembly: AssemblyDescription("PCL 下载功能恢复工具")]
[assembly: AssemblyCompany("Turing Mirror Software")]
[assembly: AssemblyProduct("PCL-Helper")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
namespace PclHelper {
 public static class Program {
  [STAThread] public static int Main(string[] args) {
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {
    if(new AssemblyName(e.Name).Name!="Mono.Cecil")return null;
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Mono.Cecil.dll")) {
     var bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);return Assembly.Load(bytes);
    }
   };
   return Run(args);
  }
  [MethodImpl(MethodImplOptions.NoInlining)] static int Run(string[] args) {
   if(args.Length>=2 && args[0].StartsWith("--") && args[0]!="--ui") {
    var result="";var exit=0;
    try {
     switch(args[0]){case "--inspect":result=Patcher.Inspect(args[1]);break;case "--enable":result=Patcher.Enable(args[1]);break;case "--restore":result=Patcher.Restore(args[1]);break;default:throw new ArgumentException("未知命令");}
    }catch(Exception ex){result=ex.ToString();exit=1;}
    Console.WriteLine(result);
    var idx=Array.IndexOf(args,"--report");if(idx>=0 && idx+1<args.Length)File.WriteAllText(args[idx+1],result);
    return exit;
   }
   var app=new Application();app.Run(new MainWindow(args.Length>0 ? args[args.Length-1] : null));return 0;
  }
 }
 public sealed class MainWindow : Window {
  readonly TextBox path,details;
  readonly TextBlock stateTitle,stateHint,fileName,stateTag;
  readonly Border stateIcon,stateBadge;
  readonly TextBlock stateGlyph;
  readonly ProgressBar progress;
  readonly Button browse,enable,restore,inspect,launch,findPcl,useFound;
  readonly Border searchPanel;
  readonly ListBox searchResults;
  readonly TextBlock searchSummary;
  CancellationTokenSource searchCancel;
  bool busy,verified,patched,hasBackup,searching,closed;
  public MainWindow(string selected) {
   Title="PCL-Helper · 下载修复助手";Width=980;Height=760;MinWidth=820;MinHeight=700;
   var workArea=SystemParameters.WorkArea;
   MinWidth=Math.Min(MinWidth,Math.Max(320,workArea.Width-24));
   MinHeight=Math.Min(MinHeight,Math.Max(300,workArea.Height-24));
   Width=Math.Min(Width,Math.Max(MinWidth,workArea.Width-24));
   Height=Math.Min(Height,Math.Max(MinHeight,workArea.Height-24));
   WindowStartupLocation=WindowStartupLocation.CenterScreen;
   FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
   Background=Brush("#F5F7FB");Foreground=Brush("#202B40");
   UseLayoutRounding=true;SnapsToDevicePixels=true;
   TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
   var assembly=Assembly.GetExecutingAssembly();
   using(var stream=assembly.GetManifestResourceStream("PclHelper.MainWindow.xaml")) {
    if(stream==null)throw new InvalidOperationException("界面资源缺失，请重新下载完整程序。");
    Content=XamlReader.Load(stream);
   }
   var grid=(Grid)Content;
   Icon=(ImageSource)grid.Resources["AppIcon"];
   path=(TextBox)grid.FindName("PathBox");details=(TextBox)grid.FindName("Details");
   stateTitle=(TextBlock)grid.FindName("StateTitle");stateHint=(TextBlock)grid.FindName("StateHint");
   stateTag=(TextBlock)grid.FindName("StateTag");fileName=(TextBlock)grid.FindName("FileName");
   stateIcon=(Border)grid.FindName("StateIcon");stateBadge=(Border)grid.FindName("StateBadge");
   stateGlyph=(TextBlock)grid.FindName("StateGlyph");progress=(ProgressBar)grid.FindName("BusyProgress");
   browse=(Button)grid.FindName("Browse");enable=(Button)grid.FindName("Enable");restore=(Button)grid.FindName("Restore");
   inspect=(Button)grid.FindName("Inspect");launch=(Button)grid.FindName("Launch");
   findPcl=(Button)grid.FindName("FindPcl");useFound=(Button)grid.FindName("UseFound");
   searchPanel=(Border)grid.FindName("SearchPanel");searchResults=(ListBox)grid.FindName("SearchResults");searchSummary=(TextBlock)grid.FindName("SearchSummary");
   findPcl.Click+=async(s,e)=>{if(searching){if(searchCancel!=null)searchCancel.Cancel();}else await Search(null);};
   searchResults.SelectionChanged+=(s,e)=>UpdateActions();
   useFound.Click+=async(s,e)=> {
    var candidate=searchResults.SelectedItem as PclCandidate;
    if(busy || searching || candidate==null || !candidate.Supported)return;
    path.Text=candidate.Path;await Check();
   };
   path.TextChanged+=(s,e)=> {
    verified=false;patched=false;hasBackup=false;
    fileName.Text=string.IsNullOrWhiteSpace(path.Text)?"尚未选择文件":"已选择文件 · 请检查状态";
    SetStatus("等待检查","选择文件后检查状态，再恢复下载功能。","原程序会自动备份；备份与校验记录保存在 PCL 程序旁。","idle");
    UpdateActions();
   };
   browse.Click+=async(s,e)=> {
    if(busy || searching)return;
    var dialog=new OpenFileDialog {Filter="PCL 程序 (*.exe)|*.exe",Title="选择 PCL 2.99.0.0",CheckFileExists=true};
    if(dialog.ShowDialog(this)==true){path.Text=dialog.FileName;await Check();}
   };
   inspect.Click+=async(s,e)=>await Check();
   enable.Click+=async(s,e)=>await Work(Patcher.Enable,"正在恢复下载","恢复完成","可重新启动 PCL，使用下载和整合包安装功能。");
   restore.Click+=async(s,e)=>await Work(Patcher.Restore,"正在还原原版","还原完成","已恢复操作前的程序，原始备份继续保留。");
   launch.Click+=(s,e)=> {
    if(busy || searching || !verified)return;
    try {
     var file=Selected();Process.Start(new ProcessStartInfo(file){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(file)});
     SetStatus("已启动 PCL","现在可以切换到 PCL 窗口。",details.Text,"success");
    }catch(Exception ex){SetStatus("启动失败","请检查文件是否仍然存在。",Friendly(ex),"error");}
   };
   AllowDrop=true;
   PreviewDragOver+=(s,e)=>{e.Effects=!busy && !searching && e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   Drop+=async(s,e)=> {
    if(busy || searching)return;
    var files=e.Data.GetData(DataFormats.FileDrop) as string[];
    if(files!=null && files.Length==1){path.Text=files[0];await Check();}
    else SetStatus("请选择一个文件","一次拖入一个 PCL 程序文件。","支持 PCL 2.99.0.0 的 exe 文件。","error");
    e.Handled=true;
   };
   PreviewKeyDown+=async(s,e)=> {
    if(busy || searching)return;
    if(e.Key==Key.O && Keyboard.Modifiers==ModifierKeys.Control){browse.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}
    else if(e.Key==Key.F5 && inspect.IsEnabled){await Check();e.Handled=true;}
   };
   Closing+=(s,e)=>{if(busy){e.Cancel=true;stateHint.Text="正在处理文件，请等待完成后再关闭窗口。";}else if(searchCancel!=null)searchCancel.Cancel();};
   Closed+=(s,e)=>{closed=true;if(searchCancel!=null)searchCancel.Cancel();};
   UpdateActions();
   if(!string.IsNullOrWhiteSpace(selected) && File.Exists(selected))path.Text=Path.GetFullPath(selected);
   else {
    try {
     var candidates=Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory,"*.exe").Where(p=>!Path.GetFileName(p).StartsWith("PCL-Helper",StringComparison.OrdinalIgnoreCase) && (Path.GetFileName(p).IndexOf("PCL",StringComparison.OrdinalIgnoreCase)>=0 || Path.GetFileName(p).IndexOf("Plain Craft",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
     if(candidates.Length==1)path.Text=candidates[0];
    }catch(IOException){}catch(UnauthorizedAccessException){}
   }
   if(path.Text.Length>0)Loaded+=async(s,e)=>await Check();
  }
  static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
  string Selected() {
   if(string.IsNullOrWhiteSpace(path.Text))throw new FileNotFoundException("请先选择 PCL 程序文件。");
   var file=Path.GetFullPath(path.Text.Trim().Trim('"'));
   if(!File.Exists(file))throw new FileNotFoundException("找不到所选程序，请重新选择 PCL 文件。");return file;
  }
  void UpdateActions() {
   var exists=File.Exists(path.Text.Trim().Trim('"'));
   var available=!busy && !searching;
   browse.IsEnabled=available;path.IsEnabled=available;
   inspect.IsEnabled=available && exists;
   enable.IsEnabled=available && verified && !patched;
   restore.IsEnabled=available && verified && hasBackup;
   launch.IsEnabled=available && verified;
   findPcl.IsEnabled=!busy;findPcl.Content=searching?"取消搜索":"一键查找 PCL";
   var candidate=searchResults.SelectedItem as PclCandidate;
   useFound.IsEnabled=available && candidate!=null && candidate.Supported;
   progress.Visibility=busy || searching?Visibility.Visible:Visibility.Collapsed;
  }
  void SetStatus(string title,string hint,string text,string kind) {
   stateTitle.Text=title;stateHint.Text=hint;details.Text=text;
   var color=kind=="success"?"#16805A":kind=="error"?"#B63C41":"#3069E8";
   var pale=kind=="success"?"#E9F7F0":kind=="error"?"#FFF0F0":"#EDF3FF";
   stateIcon.Background=Brush(pale);stateBadge.Background=Brush(pale);stateGlyph.Foreground=Brush(color);stateTag.Foreground=Brush(color);
   stateGlyph.Text=kind=="success"?"\uE73E":kind=="error"?"\uE783":kind=="busy"?"\uE895":"\uE946";
   stateTag.Text=kind=="success"?"已完成":kind=="error"?"需处理":kind=="busy"?"处理中":"待操作";
  }
  async Task Check() {
   if(busy || searching || closed)return;
   try {
    var file=Selected();busy=true;verified=false;UpdateActions();
    SetStatus("正在检查文件","正在读取程序版本与当前下载状态。","请稍候，检查过程不会修改 PCL 文件。","busy");
    var result=await Task.Run(()=>Patcher.Inspect(file));
    // Apply all state changes on the dispatcher, after background inspection completes.
    if(!result.StartsWith("PCL 2.99.0.0\r\n",StringComparison.Ordinal))throw new InvalidDataException("此版本暂不支持。请选择 PCL 2.99.0.0。");
    patched=result.Contains("已恢复下载功能");
    if(!patched && !result.Contains("下载功能受限"))throw new InvalidDataException("此程序未开启下载限制，无需恢复。");
    hasBackup=File.Exists(file+".pcl-helper.bak") && File.Exists(file+".pcl-helper.json");verified=true;
    fileName.Text=Path.GetFileName(file);
    SetStatus(patched?"下载功能已恢复":"文件已就绪",patched?"可启动 PCL，也可使用备份还原原版。":"请先退出 PCL，然后点击「恢复下载」。",result,patched?"success":"idle");
   }catch(Exception ex){verified=false;SetStatus("检查失败","请重新选择支持的 PCL 程序文件。",Friendly(ex),"error");}
   finally{busy=false;UpdateActions();}
  }
  async Task Work(Func<string,string> action,string message,string done,string hint) {
   if(busy || searching || !verified)return;
   try {
    var file=Selected();busy=true;verified=false;UpdateActions();
    SetStatus(message,"请稍候，完成前请保持窗口打开。","正在处理所选 PCL 文件，完成后会自动更新状态。","busy");
    var result=await Task.Run(()=>action(file));
    var current=await Task.Run(()=>Patcher.Inspect(file));
    patched=current.Contains("已恢复下载功能");hasBackup=File.Exists(file+".pcl-helper.bak") && File.Exists(file+".pcl-helper.json");verified=true;
    SetStatus(done,hint,result+"\r\n\r\n"+current,"success");
   }catch(Exception ex){verified=false;SetStatus("操作失败","检查下方详情，处理后可再次检查状态。",Friendly(ex),"error");}
   finally{busy=false;UpdateActions();}
  }
  async Task Search(PclSearchPlan plan) {
   if(busy || searching || closed)return;
   var cancel=new CancellationTokenSource();searchCancel=cancel;searching=true;verified=false;
   searchResults.ItemsSource=null;searchPanel.Visibility=Visibility.Collapsed;UpdateActions();
   string autoSelect=null;
   SetStatus("正在查找 PCL","先查常用位置和快捷方式，没有兼容版本时继续扫描本地固定磁盘。","搜索只读取文件，可随时点击「取消搜索」。","busy");
   var reporter=new Progress<PclSearchProgress>(p=> {
    if(closed || !searching || searchCancel!=cancel)return;
    stateHint.Text=p.Stage+" · 已检查 "+p.Directories+" 个目录 · 找到 "+p.Found+" 个 PCL";
    details.Text="当前位置："+p.Directory+"\r\n搜索只读取文件，找到后仍需手动点击「恢复下载」。";
   });
   try {
    var result=await PclSearch.FindAsync(plan,cancel.Token,((IProgress<PclSearchProgress>)reporter).Report);
    if(closed)return;
    var cancelled=result.Cancelled || cancel.IsCancellationRequested;
    searchResults.ItemsSource=result.Candidates;
    searchPanel.Visibility=result.Candidates.Count>0?Visibility.Visible:Visibility.Collapsed;
    searchSummary.Text="找到 "+result.Candidates.Count+" 个 PCL · 请选择实际使用的程序";
    var note="已检查 "+result.Directories+" 个目录；跳过 "+result.SkippedDirectories+" 个无法访问或链接目录。\r\n支持修复 PCL 2.99.0.0。网络路径、系统目录和目录链接不在扫描范围内。";
    if(cancelled)SetStatus("搜索已取消","已找到的结果会保留，可以选择或重新搜索。",note,"idle");
    else if(result.Candidates.Count==1 && result.Candidates[0].Supported)autoSelect=result.Candidates[0].Path;
    else if(result.Candidates.Count>0)SetStatus("找到 "+result.Candidates.Count+" 个 PCL","在结果列表中选择，再点击「使用选中的程序」。",note,"idle");
    else SetStatus("没有找到 PCL","可手动选择文件，或将 PCL 拖入窗口。",note,"idle");
   }catch(Exception ex){if(!closed)SetStatus("搜索失败","可重试搜索，或手动选择 PCL 文件。",Friendly(ex),"error");}
   finally {
    searching=false;if(searchCancel==cancel)searchCancel=null;cancel.Dispose();
    if(!closed)UpdateActions();
   }
   if(autoSelect!=null && !closed){searchPanel.Visibility=Visibility.Collapsed;path.Text=autoSelect;await Check();}
  }
  static string Friendly(Exception e) {
   if(e is PathTooLongException)return "所选程序路径过长，无法创建临时文件。\r\n请将 PCL 和已有备份、校验记录一起移到较短的文件夹（例如 D:\\PCL），然后重新选择。";
   if(e is UnauthorizedAccessException)return e.Message+"\r\n请将 PCL 放在有写入权限的文件夹后重试。";
   if(e is FileNotFoundException)return e.Message;
   if(e is IOException)return e.Message+"\r\n请退出 PCL，并确认文件夹可写。";return e.Message;
  }
 }
}

