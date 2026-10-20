param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'release\packages'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$exe=Join-Path $root 'release\PCL-Helper-v1.1.exe'
if(-not (Test-Path -LiteralPath $exe)){throw '请先运行 scripts/build.ps1'}
$staging=Join-Path $root ('.test-data\package-'+[Guid]::NewGuid().ToString('N'))
$portable=Join-Path $staging 'PCL-Helper-v1.1'
$source=Join-Path $staging 'PCL-Helper-v1.1-source'
New-Item -ItemType Directory -Force $portable,$source | Out-Null
Copy-Item -LiteralPath $exe -Destination $portable
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $portable '使用说明.md')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $portable
Copy-Item -LiteralPath (Join-Path $root 'NOTICE.md') -Destination $portable
Copy-Item -LiteralPath (Join-Path $root 'docs\Mono.Cecil.LICENSE.txt') -Destination $portable
Copy-Item -LiteralPath (Join-Path $root 'docs\UI-VALIDATION.md') -Destination (Join-Path $portable '验证记录.md')
foreach($directory in @('src','scripts','tests')){Copy-Item -LiteralPath (Join-Path $root $directory) -Destination $source -Recurse}
New-Item -ItemType Directory -Force (Join-Path $source 'docs'),(Join-Path $source 'tools\cecil\lib\net40') | Out-Null
foreach($file in @('README.md','LICENSE','NOTICE.md','.gitignore')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $source}
foreach($file in @('README.md','Mono.Cecil.LICENSE.txt','UI-VALIDATION.md')){Copy-Item -LiteralPath (Join-Path $root ('docs\'+$file)) -Destination (Join-Path $source 'docs')}
Copy-Item -LiteralPath (Join-Path $root 'tools\cecil\lib\net40\Mono.Cecil.dll') -Destination (Join-Path $source 'tools\cecil\lib\net40')
Copy-Item -LiteralPath $exe -Destination $OutputDirectory
Compress-Archive -LiteralPath $portable -DestinationPath (Join-Path $OutputDirectory 'PCL-Helper-v1.1-portable.zip') -Force
Compress-Archive -LiteralPath $source -DestinationPath (Join-Path $OutputDirectory 'PCL-Helper-v1.1-source.zip') -Force
$sum=@()
foreach($name in @('PCL-Helper-v1.1.exe','PCL-Helper-v1.1-portable.zip','PCL-Helper-v1.1-source.zip')) {
 $hash=(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
 $sum+="$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'),$sum,(New-Object Text.UTF8Encoding($false)))
Get-ChildItem -LiteralPath $OutputDirectory | Select-Object Name,Length
