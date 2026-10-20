using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Integration tests against the actual packaged WPF window and real PCL copies.
// Catches stale file validation, enabled destructive actions during work, and lost restoration.
class MainWindowTest {
 static Assembly helper;
 static int checks;
 [STAThread] static int Main(string[] args) {
  try {
   helper=Assembly.LoadFrom(Path.GetFullPath(args[0]));
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {
    if(new AssemblyName(e.Name).Name!="Mono.Cecil")return null;
    using(var input=helper.GetManifestResourceStream("Mono.Cecil.dll")) {
     var bytes=new byte[input.Length];input.Read(bytes,0,bytes.Length);return Assembly.Load(bytes);
    }
   };
   var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
   SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
   app.DispatcherUnhandledException+=(s,e)=>{Dump(e.Exception);e.Handled=true;Environment.Exit(1);};
   AppDomain.CurrentDomain.UnhandledException+=(s,e)=>Dump(e.ExceptionObject as Exception);
   var root=Path.GetFullPath(args[2]);Directory.CreateDirectory(root);
   var target=Path.Combine(root,"测试 PCL.exe");File.Copy(args[1],target,true);
   var original=Hash(target);
   var window=(Window)Activator.CreateInstance(helper.GetType("PclHelper.MainWindow"),new object[]{null});
   var grid=(FrameworkElement)window.Content;
   var path=(TextBox)grid.FindName("PathBox");
   var enable=(Button)grid.FindName("Enable");var restore=(Button)grid.FindName("Restore");
   var inspect=(Button)grid.FindName("Inspect");var launch=(Button)grid.FindName("Launch");
   var browse=(Button)grid.FindName("Browse");
   Assert(grid.FindName("FindPcl") is Button,"file selection exposes one-click PCL search");
   Assert(!enable.IsEnabled && !restore.IsEnabled && !launch.IsEnabled && !inspect.IsEnabled,"empty selection disables file actions");
   Assert(browse.IsEnabled,"file chooser remains available");
   path.Text=target;
   Assert(inspect.IsEnabled && !enable.IsEnabled && !launch.IsEnabled,"new path must be inspected before use");
   Click(inspect);Wait(()=>inspect.IsEnabled);
   Assert(enable.IsEnabled && !restore.IsEnabled && launch.IsEnabled,"verified original can be enabled but has no restore yet");
   path.Text=Path.Combine(root,"missing.exe");
   Assert(!enable.IsEnabled && !restore.IsEnabled && !launch.IsEnabled && !inspect.IsEnabled,"editing path revokes previous validation");
   path.Text=Path.GetFullPath(args[0]);Click(inspect);Wait(()=>inspect.IsEnabled);
   Assert(!enable.IsEnabled && !restore.IsEnabled && !launch.IsEnabled,"non-PCL executable is rejected without enabling actions");
   Assert(((TextBlock)grid.FindName("StateTitle")).Text.Contains("失败"),"inspection error is visibly distinguished");
   Assert(Hash(target)==original,"inspection and invalid selection never modify original");
   path.Text=target;Click(inspect);Wait(()=>inspect.IsEnabled);
   Click(enable);
   Assert(!browse.IsEnabled && !path.IsEnabled && !restore.IsEnabled && !inspect.IsEnabled && !launch.IsEnabled,"all competing inputs disabled during write");
   Assert(((ProgressBar)grid.FindName("BusyProgress")).Visibility==Visibility.Visible,"write shows progress");
   var closing=new System.ComponentModel.CancelEventArgs();
   typeof(Window).GetMethod("OnClosing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{closing});
   Assert(closing.Cancel,"closing is blocked until file operation completes");
   Wait(()=>inspect.IsEnabled);
   Assert(Hash(target)!=original && File.Exists(target+".pcl-helper.bak"),"GUI enable creates actual patch and backup");
   Assert(!enable.IsEnabled && restore.IsEnabled && launch.IsEnabled,"patched state offers restore instead of repeated enable");
   Assert(((ProgressBar)grid.FindName("BusyProgress")).Visibility==Visibility.Collapsed,"progress hidden on completion");
   Render(window,960,720,Path.Combine(root,"preview-success.png"));
   var pathHost=path.Template.FindName("PART_ContentHost",path) as ScrollViewer;
   Assert(pathHost.ViewportHeight>=pathHost.ExtentHeight,"single-line path text is vertically visible without clipping");
   var pageScroll=grid.FindName("PageScroll") as ScrollViewer;
   Assert(pageScroll!=null && pageScroll.ScrollableWidth==0,"long selected paths never expand normal-width layout horizontally");
   // A corrupt backup must never overwrite the selected executable.
   var backup=target+".pcl-helper.bak";
   var backupBytes=File.ReadAllBytes(backup);File.WriteAllText(backup,"corrupted test backup");
   var patchedHash=Hash(target);Click(restore);Wait(()=>inspect.IsEnabled);
   Assert(Hash(target)==patchedHash,"bad backup is rejected without overwriting patched file");
   Assert(!enable.IsEnabled && !restore.IsEnabled && browse.IsEnabled && path.IsEnabled,"operation failure revokes validation and unlocks selection");
   Assert(((TextBlock)grid.FindName("StateTitle")).Text.Contains("失败"),"failed operation shows error state");
   Render(window,960,720,Path.Combine(root,"preview-error.png"));
   File.WriteAllBytes(backup,backupBytes);Click(inspect);Wait(()=>inspect.IsEnabled);
   Click(restore);Wait(()=>inspect.IsEnabled);
   Assert(Hash(target)==original,"GUI restore recovers original SHA256");
   Assert(enable.IsEnabled && restore.IsEnabled && browse.IsEnabled && path.IsEnabled,"restore refreshes verified state and unlocks controls");
   var longTarget=Path.Combine(root,new string('L',214-root.Length-5)+".exe");File.Copy(args[1],longTarget,true);
   path.Text=longTarget;Click(inspect);Wait(()=>inspect.IsEnabled);Click(enable);Wait(()=>inspect.IsEnabled);
   Assert(Hash(longTarget)==original,"legacy path limit leaves original unchanged");
   Assert(((TextBox)grid.FindName("Details")).Text.Contains("较短"),"long-path failure tells user to use a shorter folder");
   // Rendering the real WPF tree catches XAML/template errors without clicking the desktop.
   // Fresh window is the actual first-launch view.
   window.Close();window=(Window)Activator.CreateInstance(helper.GetType("PclHelper.MainWindow"),new object[]{null});
   grid=(FrameworkElement)window.Content;
   Render(window,960,720,Path.Combine(root,"preview-default.png"));
   Render(window,960,720,Path.Combine(root,"preview-150pct.png"),144);
   Render(window,960,720,Path.Combine(root,"preview-200pct.png"),192);
   Render(window,window.MinWidth-16,window.MinHeight-40,Path.Combine(root,"preview-minimum.png"));
   foreach(var name in new[]{"Browse","Enable","Restore","Inspect","Launch"}) {
    var control=(FrameworkElement)grid.FindName(name);
    var rect=control.TransformToAncestor(grid).TransformBounds(new Rect(control.RenderSize));
    Assert(rect.Left>=0 && rect.Right<=grid.ActualWidth+1 && rect.Top>=0 && rect.Bottom<=grid.ActualHeight+1,"minimum size keeps "+name+" in bounds");
   }
   Render(window,960,530,Path.Combine(root,"preview-small-workarea.png"));
   var scroll=(ScrollViewer)grid.FindName("PageScroll");
   Assert(scroll!=null && scroll.ScrollableHeight>0,"small work area offers vertical scrolling instead of clipping");
   scroll.ScrollToBottom();grid.UpdateLayout();
   var detail=(FrameworkElement)grid.FindName("Details");
   var detailBounds=detail.TransformToAncestor(grid).TransformBounds(new Rect(detail.RenderSize));
   Assert(detailBounds.Bottom<=grid.ActualHeight+1,"status details become visible after scrolling");
   window.Close();window=(Window)Activator.CreateInstance(helper.GetType("PclHelper.MainWindow"),new object[]{null});
   grid=(FrameworkElement)window.Content;path=(TextBox)grid.FindName("PathBox");
   var find=(Button)grid.FindName("FindPcl");var use=(Button)grid.FindName("UseFound");var list=(ListBox)grid.FindName("SearchResults");
   var searchRoot=Path.Combine(root,"find");Directory.CreateDirectory(searchRoot);
   var a=Path.Combine(searchRoot,"PCL-A.exe");var b=Path.Combine(searchRoot,"PCL-B.exe");File.Copy(args[1],a,true);File.Copy(args[1],b,true);
   var planType=helper.GetType("PclHelper.PclSearchPlan");var plan=Activator.CreateInstance(planType);planType.GetField("QuickRoots").SetValue(plan,new[]{searchRoot});
   var searchMethod=window.GetType().GetMethod("Search",BindingFlags.Instance|BindingFlags.NonPublic);
   var searchTask=(Task)searchMethod.Invoke(window,new[]{plan});
   Assert(find.IsEnabled && !((Button)grid.FindName("Browse")).IsEnabled && !path.IsEnabled,"search keeps cancellation available while locking target selection");
   Wait(()=>searchTask.IsCompleted);searchTask.GetAwaiter().GetResult();
   Assert(list.Items.Count==2 && path.Text=="" && !use.IsEnabled,"multiple results require explicit selection");
   Render(window,960,800,Path.Combine(root,"preview-search-results.png"));
   list.SelectedIndex=0;Assert(use.IsEnabled,"supported selected search result becomes usable");
   Click(use);Wait(()=>((Button)grid.FindName("Inspect")).IsEnabled);
   Assert(path.Text==a && ((Button)grid.FindName("Enable")).IsEnabled,"using a result fills and verifies its exact path");
   Assert(Hash(a)==original && Hash(b)==original,"search and selecting results never patch either copy");
   var unsupported=Activator.CreateInstance(helper.GetType("PclHelper.PclCandidate"));
   unsupported.GetType().GetProperty("Path").SetValue(unsupported,a,null);
   unsupported.GetType().GetProperty("Version").SetValue(unsupported,"2.98.0.0",null);
   list.ItemsSource=new[]{unsupported};list.SelectedIndex=0;
   Assert(!use.IsEnabled,"unsupported result cannot be used from the search list");
   plan=Activator.CreateInstance(planType);planType.GetField("DirectFiles").SetValue(plan,new[]{b});
   searchTask=(Task)searchMethod.Invoke(window,new[]{plan});Wait(()=>searchTask.IsCompleted);searchTask.GetAwaiter().GetResult();
   Assert(path.Text==b && ((Button)grid.FindName("Enable")).IsEnabled,"single supported result is automatically filled and inspected");
   plan=Activator.CreateInstance(planType);planType.GetField("QuickRoots").SetValue(plan,new[]{searchRoot});
   searchTask=(Task)searchMethod.Invoke(window,new[]{plan});Click(find);Wait(()=>searchTask.IsCompleted);searchTask.GetAwaiter().GetResult();
   Assert(((TextBlock)grid.FindName("StateTitle")).Text.Contains("取消") && find.IsEnabled && path.IsEnabled,"cancel search finishes cleanly and unlocks controls");
   searchTask=(Task)searchMethod.Invoke(window,new[]{plan});
   closing=new System.ComponentModel.CancelEventArgs();typeof(Window).GetMethod("OnClosing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{closing});
   Wait(()=>searchTask.IsCompleted);searchTask.GetAwaiter().GetResult();
   Assert(!closing.Cancel && path.IsEnabled,"closing during read-only search cancels without blocking the window");
   plan=Activator.CreateInstance(planType);planType.GetField("DirectFiles").SetValue(plan,new[]{Path.GetFullPath(args[0])});
   searchTask=(Task)searchMethod.Invoke(window,new[]{plan});Wait(()=>searchTask.IsCompleted);searchTask.GetAwaiter().GetResult();
   Assert(((TextBlock)grid.FindName("StateTitle")).Text.Contains("没有找到") && path.IsEnabled && !((Button)grid.FindName("Enable")).IsEnabled,"empty results restore manual selection and revoke old validation");
   window.Close();app.Shutdown();
   Console.WriteLine("PASS: "+checks+" WPF integration assertions, real patch/restore, two layout sizes and 96/144/192 DPI renders.");return 0;
  } catch(Exception e){Dump(e);return 1;}
 }
 static void Dump(Exception e){for(var n=0;e!=null && n<8;n++,e=e.InnerException){Console.Error.WriteLine(e.GetType().FullName);Console.Error.WriteLine(e.Message);Console.Error.WriteLine(e.StackTrace);}}
 static void Assert(bool condition,string message){if(!condition)throw new Exception("FAIL: "+message);checks++;Console.WriteLine("OK: "+message);}
 static void Click(Button b){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
 static void Wait(Func<bool> condition) {
  var clock=Stopwatch.StartNew();
  do {
   var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
   if(condition())return;Thread.Sleep(10);
  }while(clock.Elapsed.TotalSeconds<30);
  throw new Exception("Timed out waiting for UI operation");
 }
 static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream));}
 static void Render(Window w,double width,double height,string file,double dpi=96) {
  var content=(FrameworkElement)w.Content;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)(width*dpi/96),(int)(height*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(content);
  var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(file))png.Save(output);
 }
}
