# Run in Windows PowerShell -STA. Draws the same vector resource used by the window.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$stream=[IO.File]::OpenRead((Join-Path $root 'src\MainWindow.xaml'))
try{$grid=[Windows.Markup.XamlReader]::Load($stream)}finally{$stream.Dispose()}
$source=$grid.Resources['AppIcon']
$frames=@()
foreach($size in @(16,32,48,256)) {
 $visual=New-Object Windows.Media.DrawingVisual
 $context=$visual.RenderOpen()
 $context.DrawImage($source,(New-Object Windows.Rect(0,0,$size,$size)))
 $context.Close()
 $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,([Windows.Media.PixelFormats]::Pbgra32))
 $bitmap.Render($visual)
 $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $buffer=New-Object IO.MemoryStream
 $encoder.Save($buffer)
 $frames+=,@{Size=$size;Bytes=$buffer.ToArray()};$buffer.Dispose()
}
$output=[IO.File]::Create((Join-Path $root 'src\App.ico'))
$writer=New-Object IO.BinaryWriter($output)
try {
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
 $offset=6+16*$frames.Count
 foreach($frame in $frames) {
  $dimension=if($frame.Size -eq 256){0}else{$frame.Size}
  $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
  $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset)
  $offset+=$frame.Bytes.Length
 }
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
}finally{$writer.Dispose();$output.Dispose()}
