param([switch]$Console)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
$cecil=Join-Path $root 'tools\cecil\lib\net40\Mono.Cecil.dll'
if(-not (Test-Path (Join-Path $root 'src\App.ico'))) {
 & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'make-icon.ps1')
 if($LASTEXITCODE -ne 0){throw '图标生成失败'}
}
if(-not (Test-Path $cecil)) {
 New-Item -ItemType Directory -Force (Join-Path $root 'tools') | Out-Null
 $package=Join-Path $root 'tools\mono.cecil.nupkg'
 Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/mono.cecil/0.11.6/mono.cecil.0.11.6.nupkg' -OutFile $package
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Compression.ZipFile]::ExtractToDirectory($package,(Join-Path $root 'tools\cecil'))
}
New-Item -ItemType Directory -Force (Join-Path $root 'release') | Out-Null
$kind=if($Console){'exe'}else{'winexe'}
$out=if($Console){'PCL-Helper-CLI.exe'}else{'PCL-Helper-v1.1.exe'}
$outputDir=if($Console){Join-Path $root '.test-data'}else{Join-Path $root 'release'}
New-Item -ItemType Directory -Force $outputDir | Out-Null
$refs=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:'+(Join-Path $framework $_) }
& $compiler /nologo /codepage:65001 /optimize+ /platform:anycpu "/target:$kind" "/out:$outputDir\$out" "/win32icon:$root\src\App.ico" "/win32manifest:$root\src\app.manifest" "/reference:$cecil" "/resource:$cecil,Mono.Cecil.dll" "/resource:$root\docs\Mono.Cecil.LICENSE.txt,Mono.Cecil.LICENSE.txt" "/resource:$root\src\MainWindow.xaml,PclHelper.MainWindow.xaml" @refs "$root\src\Patcher.cs" "$root\src\PclSearch.cs" "$root\src\Program.cs"
if($LASTEXITCODE -ne 0){throw '编译失败'}
Get-Item (Join-Path $outputDir $out) | Select-Object FullName,Length



