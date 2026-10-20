param([string]$PclPath,[string]$HelperPath,[string]$TestDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $HelperPath){$HelperPath=Join-Path $root 'release/PCL-Helper-v1.1.exe'}
if(-not $PclPath){$PclPath=Join-Path $root '.test-data/original/Plain Craft Launcher 2.exe'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
New-Item -ItemType Directory -Force (Join-Path $root '.test-data') | Out-Null
$refs=@('System.dll','System.Core.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
& "$framework/csc.exe" /nologo /codepage:65001 /target:exe "/out:$root\.test-data\MainWindowTest.exe" @refs "$root\tests\MainWindowTest.cs"
if($LASTEXITCODE -ne 0){throw 'UI test compilation failed'}
$testDir=if($TestDirectory){[IO.Path]::GetFullPath($TestDirectory)}else{Join-Path $root ('.test-data/ui-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
& "$root/.test-data/MainWindowTest.exe" $HelperPath $PclPath $testDir
if($LASTEXITCODE -ne 0){throw 'UI integration failed'}
Write-Output "Render output: $testDir"
