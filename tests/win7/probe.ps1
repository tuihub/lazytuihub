$ErrorActionPreference = 'Stop'
$media = Split-Path -Parent $MyInvocation.MyCommand.Path
# Propagate loader errors to the caller even when no interactive desktop is logged in.
Add-Type -Namespace Lab -Name ProcessOptions -MemberDefinition '[System.Runtime.InteropServices.DllImport("kernel32.dll")] public static extern uint SetErrorMode(uint mode);'
[void][Lab.ProcessOptions]::SetErrorMode(3)
Add-Type -AssemblyName System.Web.Extensions
$json = New-Object System.Web.Script.Serialization.JavaScriptSerializer
function Report($message) {
    [IO.File]::AppendAllText('C:\lth-checks.txt', $message + [Environment]::NewLine)
}
$os = Get-WmiObject Win32_OperatingSystem
Report ("OS: " + $os.Caption + " " + $os.Version + " SP " + $os.ServicePackMajorVersion + " " + $os.OSArchitecture)

function Invoke-Product($exe, $arguments) {
    $stderr = 'C:\lth-stderr.txt'
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.FileName = 'cmd.exe'
    $info.Arguments = '/d /s /c ""' + $exe + '" ' + ($arguments -join ' ') + ' 2>"' + $stderr + '""'
    $p = [Diagnostics.Process]::Start($info)
    try {
        $out = $p.StandardOutput.ReadToEnd()
        $p.WaitForExit()
        if ($p.ExitCode -ne 0) {
            $message = [IO.File]::ReadAllText($stderr)
            throw ("Exit 0x{0:X8}: {1}" -f $p.ExitCode,$message.Trim())
        }
        return $out
    } finally { $p.Dispose() }
}
function Run-Check($label, $exe, $arguments) {
    $out = Invoke-Product $exe $arguments
    Report ("PASS " + $label)
    return $out
}
function Check-Remote($exe) {
    $env:Access__Session = $null
    $login = $json.DeserializeObject((Run-Check 'login' $exe @('--endpoint',$endpoint,'--command','access','--action','login','--username','lab')))
    $env:Access__Session = $login['token']
    if (!$env:Access__Session) { throw 'Missing session token' }
    $feeds = $json.DeserializeObject((Run-Check 'list feeds' $exe @('--endpoint',$endpoint,'--command','rss','--action','feeds')))
    $seed = $feeds['feeds'] | Where-Object { $_['uri'].EndsWith('/feed') } | Select-Object -First 1
    if (!$seed) { throw 'Missing seeded feed' }
    $uri = $seed['uri'] + '?probe=' + [Guid]::NewGuid().ToString('N')
    $created = $json.DeserializeObject((Run-Check 'add feed' $exe @('--endpoint',$endpoint,'--command','rss','--action','add','--feed-url',$uri)))
    $source = $created['name']
    if (!$source -or $created['uri'] -ne $uri) { throw 'Wrong new feed' }
    $refresh = $json.DeserializeObject((Run-Check 'refresh feed' $exe @('--endpoint',$endpoint,'--command','rss','--action','refresh','--name',$source)))
    if ($refresh['processedArticleCount'] -lt 1) { throw 'Refresh did not process the test article' }
    $feed = $json.DeserializeObject((Run-Check 'get feed' $exe @('--endpoint',$endpoint,'--command','rss','--action','get','--name',$source)))
    if ($feed['name'] -ne $source) { throw 'Wrong feed' }
    $list = $json.DeserializeObject((Run-Check 'list articles by source' $exe @('--endpoint',$endpoint,'--command','articles','--action','list','--source',$source)))
    $name = $list['articles'][0]['name']
    if (!$name) { throw 'Missing seeded article' }
    $article = $json.DeserializeObject((Run-Check 'get' $exe @('--endpoint',$endpoint,'--command','articles','--action','get','--name',$name)))
    if ($article['name'] -ne $name) { throw 'Wrong article' }
    $read = $json.DeserializeObject((Run-Check 'mark-read' $exe @('--endpoint',$endpoint,'--command','articles','--action','read','--name',$name)))
    if (!$read['read']) { throw 'Article was not marked read' }
    [void](Run-Check 'logout' $exe @('--endpoint',$endpoint,'--command','access','--action','logout'))
}
function Check-Local($exe) {
    $db = 'C:\win7-games.db'
    $games = @('--cli','--database',$db,'--Logging:LogLevel:Default','None','--command','games','--action')
    $newGame = $json.DeserializeObject((Run-Check 'create game' $exe ($games + @('add','--title','Win7Test'))))
    $entry = $json.DeserializeObject((Run-Check 'read game after restart' $exe ($games + @('show','--game',$newGame['id']))))
    if ($entry['entry']['title'] -ne 'Win7Test') { throw 'Game was not persisted' }
}

$client = Join-Path $media 'cli\LazyTuiHub.Cli.exe'
$hostExe = Join-Path $media 'host\LazyTuiHub.exe'
$clientOnly = Test-Path "$media\client-only"
if (!(Test-Path $client)) { throw 'Missing CLI publish directory' }
if (!$clientOnly -and !(Test-Path $hostExe)) { throw 'Missing host publish directory' }
& certutil.exe -f -addstore Root "$media\lab-ca.cer" | Out-Null
if ($LASTEXITCODE) { throw 'Cannot install the guest test CA' }
$endpoint = [IO.File]::ReadAllText("$media\endpoint.txt").Trim()
$env:Access__Password = [IO.File]::ReadAllText("$media\password.txt").Trim()
try {
    Report 'CLIENT cli'
    $help = Run-Check 'process and managed entry point' $client @()
    if (!$help.Contains('LazyTuiHub CLI')) { throw 'Missing product help' }
    Check-Remote $client
    if (!$clientOnly) {
        Report 'LOCAL HOST host'
        Check-Local $hostExe
    }
    Report 'LTH-TEST-PASS'
} catch { Report ("FAIL " + $_.Exception.Message); throw }
