$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'updates'
foreach ($package in @('loader','ucrt')) {
    $dest = "C:\lth-$package"
    New-Item -ItemType Directory -Force $dest | Out-Null
    & expand.exe '-F:Windows6.1*.cab' "$source\$package.msu" $dest
    if ($LASTEXITCODE) { throw "Cannot unpack $package" }
    $cab = Get-ChildItem "$dest\Windows6.1*.cab"
    & dism.exe /online /add-package "/packagepath:$($cab.FullName)" /norestart
    if (@(0,3010) -notcontains $LASTEXITCODE) { throw "$package failed with $LASTEXITCODE" }
}
$p = [Diagnostics.Process]::Start("$source\vc_redist.x64.exe", '/install /quiet /norestart')
$p.WaitForExit()
Write-Output ("VC++ redistributable returned " + $p.ExitCode)
if (@(0,3010,1638) -notcontains $p.ExitCode) { throw "VC++ redistributable failed" }
