param([string]$PclPath,[string]$HelperPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $PclPath){$PclPath=Join-Path $root '.test-data\original\Plain Craft Launcher 2.exe'}
if(-not $HelperPath){$HelperPath=Join-Path $root 'release\PCL-Helper-v1.1.exe'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force (Join-Path $root '.test-data') | Out-Null
& "$framework\csc.exe" /nologo /codepage:65001 "/reference:$root\tools\cecil\lib\net40\Mono.Cecil.dll" "/out:$root\.test-data\SearchTest.exe" "$root\tests\SearchTest.cs"
if($LASTEXITCODE -ne 0){throw 'Search test compilation failed'}
& "$root\.test-data\SearchTest.exe" $HelperPath $PclPath (Join-Path $root ('.test-data\find-'+[Guid]::NewGuid().ToString('N').Substring(0,8)))
if($LASTEXITCODE -ne 0){throw 'Search tests failed'}
