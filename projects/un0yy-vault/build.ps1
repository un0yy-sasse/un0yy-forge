# Builds un0yy-vault.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src  = Join-Path $root 'src\Program.cs'
$out  = Join-Path $root 'dist'
$ico  = Join-Path $root 'build\icon.ico'
New-Item -ItemType Directory -Force $out, (Join-Path $root 'build') | Out-Null

$refs = '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'

# 1) bootstrap build (no icon) -> generates the icon
$boot = Join-Path $root 'build\bootstrap.exe'
& $csc /nologo /target:winexe /optimize+ "/out:$boot" @refs $src
if ($LASTEXITCODE -ne 0) { throw 'bootstrap build failed' }
& $boot --make-icon $ico | Out-Null
if (-not (Test-Path $ico)) { throw 'icon generation failed' }

# 2) final build with icon
$exe = Join-Path $out 'un0yy-vault.exe'
& $csc /nologo /target:winexe /optimize+ "/out:$exe" "/win32icon:$ico" @refs $src
if ($LASTEXITCODE -ne 0) { throw 'final build failed' }

# 3) self test of the encryption layer
$report = Join-Path $root 'build\selftest.txt'
$p = Start-Process -FilePath $exe -ArgumentList '--selftest', "`"$report`"" -Wait -PassThru
Get-Content $report
if ($p.ExitCode -ne 0) { throw 'selftest failed' }
Write-Host "Built: $exe"
