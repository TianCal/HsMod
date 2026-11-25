# Writes the saved login token into client.config and starts Hearthstone.exe
# directly (BepInEx/HsMod load through winhttp.dll), without the Battle.net app.
#   launch_hsmod.ps1 [-Account <name>] [-WriteConfigOnly]
#   -Account          use .hsmod_token.<name> (e.g. ct) instead of .hsmod_token
#   -WriteConfigOnly  just rebuild client.config and exit
#   -WindowSize WxH   start the game with a WxH physical-pixel client area (e.g. 3324x1957)
#   -WindowPos X,Y    with -WindowSize, also place the window's top-left corner at X,Y
param(
    [string]$Account = "",
    [switch]$WriteConfigOnly,
    [string]$HsAutoAccount,
    [switch]$UseHsAutoToken,
    [string]$WindowSize,
    [string]$WindowPos
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

function Show-Message([string]$text) {
    Add-Type -AssemblyName PresentationFramework
    [void][System.Windows.MessageBox]::Show($text, 'HsMod')
}

# Saves the requested client size (and position) where Hearthstone and Unity
# read them at startup, so the game opens that way and the launcher does not
# have to wait for its window. Leaves fullscreen setups alone.
function Set-HsWindowPrefs {
    if ($WriteConfigOnly -or -not $WindowSize) { return }
    if ($WindowSize -notmatch '^(\d+)x(\d+)$') { Show-Message "Bad -WindowSize '$WindowSize'. Use e.g. 3324x1957."; return }
    $cw = [int]$Matches[1]; $ch = [int]$Matches[2]
    $pos = $null
    if ($WindowPos) {
        if ($WindowPos -notmatch '^(-?\d+),(-?\d+)$') { Show-Message "Bad -WindowPos '$WindowPos'. Use e.g. 319,63."; return }
        $pos = @([int]$Matches[1], [int]$Matches[2])
    }
    # Unity PlayerPrefs value names end in _h<djb2-xor hash of the name>.
    # The mask is written in decimal: 0xFFFFFFFF is int32 -1 in Windows PowerShell.
    function PrefName([string]$n) {
        [uint64]$h = 5381
        foreach ($c in [Text.Encoding]::UTF8.GetBytes($n)) { $h = (($h * 33) -bxor $c) -band 4294967295 }
        "${n}_h$h"
    }
    $prefs = 'HKCU:\Software\Blizzard Entertainment\Hearthstone'
    if (-not (Test-Path -LiteralPath $prefs)) { New-Item -Path $prefs -Force | Out-Null }
    $mode = (Get-ItemProperty -LiteralPath $prefs -ErrorAction SilentlyContinue).(PrefName 'Screenmanager Fullscreen mode')
    if ($null -ne $mode -and $mode -ne 3) { return }   # 3 = windowed
    $values = [ordered]@{
        'Screenmanager Resolution Width' = $cw; 'Screenmanager Resolution Height' = $ch
        'Screenmanager Resolution Window Width' = $cw; 'Screenmanager Resolution Window Height' = $ch
    }
    if ($pos) { $values['Screenmanager Window Position X'] = $pos[0]; $values['Screenmanager Window Position Y'] = $pos[1] }
    foreach ($n in $values.Keys) {
        New-ItemProperty -LiteralPath $prefs -Name (PrefName $n) -PropertyType DWord -Value ([BitConverter]::ToInt32([BitConverter]::GetBytes([int]$values[$n]), 0)) -Force | Out-Null
    }
    # Hearthstone re-applies graphicswidth/graphicsheight from options.txt once it loads.
    $opts = Join-Path $env:LOCALAPPDATA 'Blizzard\Hearthstone\options.txt'
    if (Test-Path -LiteralPath (Split-Path -Parent $opts)) {
        $want = @{ graphicswidth = $cw; graphicsheight = $ch }
        $lines = New-Object System.Collections.Generic.List[string]
        if (Test-Path -LiteralPath $opts) {
            foreach ($l in [IO.File]::ReadAllLines($opts)) {
                if ($l -match '^(graphicswidth|graphicsheight)=') { $k = $Matches[1]; if ($want.ContainsKey($k)) { $lines.Add("$k=$($want[$k])"); $want.Remove($k) } }
                else { $lines.Add($l) }
            }
        }
        foreach ($k in @($want.Keys)) { $lines.Add("$k=$($want[$k])") }
        [IO.File]::WriteAllText($opts, ($lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding($false)))
    }
}
if (-not $WriteConfigOnly -and (Get-Process Hearthstone -ErrorAction SilentlyContinue)) {
    Show-Message 'Hearthstone is already running. Quit it first to switch accounts.'
    exit 1
}

if ($HsAutoAccount) {
    if ($Account -or $UseHsAutoToken) { throw 'Conflicting HsMod account options.' }
    $mode = if ($WriteConfigOnly) { 'config' } else { 'launch' }
    $sessionId = (Get-Process -Id $PID).SessionId
    $app = Get-Process -Name 'HsAuto.Maintenance' -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -eq $sessionId -and $_.Path } |
        ForEach-Object { Split-Path -Parent $_.Path } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ 'HsAuto.Core.dll') -PathType Leaf } |
        Select-Object -First 1
    if (-not $app) {
        $downloads = Join-Path $env:USERPROFILE 'Downloads'
        $app = Get-ChildItem -LiteralPath $downloads -Directory -Filter 'HsAuto-Maintenance-win-x64*' -ErrorAction SilentlyContinue |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'HsAuto.Core.dll') -PathType Leaf } |
            ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'HsAuto.Maintenance.exe') -ErrorAction SilentlyContinue } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty DirectoryName
    }
    if (-not $app) {
        Show-Message 'Unable to find the HsAuto Maintenance installation. Start HsAuto and try again.'
        exit 1
    }
    $helper = Join-Path $PSScriptRoot 'HsModAutoLogin.dll'
    $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if ($mode -eq 'config') {
        & $dotnet $helper $app $PSCommandPath $HsAutoAccount $mode
        exit $LASTEXITCODE
    }
    Set-HsWindowPrefs
    # The helper stays until Hearthstone quits (it then clears the launch
    # token), so run it in the background and leave once the game is up.
    $q = { param($s) '"' + $s + '"' }
    $argLine = (@($helper, $app, $PSCommandPath, $HsAutoAccount, $mode) | ForEach-Object { & $q $_ }) -join ' '
    $proc = Start-Process -FilePath $dotnet -ArgumentList $argLine -WindowStyle Hidden -PassThru
    $null = $proc.Handle   # keeps ExitCode readable after the process ends
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        if (Get-Process Hearthstone -ErrorAction SilentlyContinue) { exit 0 }
        if ($proc.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $proc.HasExited -or $proc.ExitCode -eq 0) { exit 0 }
    Show-Message 'Unable to start Hearthstone with the HsAuto account. Check that the account is still configured in HsAuto.'
    exit 1
}

if ($UseHsAutoToken) {
    $token = $env:HSMOD_HSAUTO_WEB_TOKEN
    $region = $env:HSMOD_HSAUTO_REGION
    Remove-Item Env:HSMOD_HSAUTO_WEB_TOKEN, Env:HSMOD_HSAUTO_REGION -ErrorAction SilentlyContinue
    if ($Account -or $token -notmatch '^[A-Za-z0-9._~+/=:-]+$' -or $region -notmatch '^(us|eu|kr|tw|cn)$') {
        throw 'Invalid HsAuto login data.'
    }
    $who = 'HsAuto account'
} else {
    if ($Account -and $Account -notmatch '^[A-Za-z0-9_-]+$') {
        Show-Message 'Account name may only use letters, digits, _ and -.'
        exit 1
    }
    $tokenFile = if ($Account) { ".hsmod_token.$Account" } else { '.hsmod_token' }
    $who = if ($Account) { $Account } else { 'main account' }
    
    if (-not (Test-Path -LiteralPath $tokenFile) -or -not (Get-Content -LiteralPath $tokenFile -Raw).Trim()) {
        Show-Message "No login token for $who. Log in at https://us.battle.net/login/en/?app=wtcg, copy the localhost:0 address, then run set_token.ps1 $Account"
        exit 1
    }
    # Only one game at a time: both accounts share client.config and HsMod's settings.
    $token = (Get-Content -LiteralPath $tokenFile -Raw).Trim()
    
    # Env is the token's first two characters (US-..., EU-...), except China.
    $region = $token.Substring(0, 2).ToLower()
}
$envHost = if ($region -eq 'cn') { 'cn.actual.battlenet.com.cn' } else { "$region.actual.battle.net" }

# Keep whatever [Log] settings are already there; replace everything else.
$log = "[Log]`r`nFileSizeLimit.Int=-1"
if (Test-Path -LiteralPath 'client.config') {
    $m = [regex]::Match((Get-Content -LiteralPath 'client.config' -Raw), '(?ms)^\[Log\].*?(?=^\[|\z)')
    if ($m.Success) { $log = $m.Value.TrimEnd() }
}
$config = @(
    '[Config]', 'Version = 3', '[Aurora]',
    "VerifyWebCredentials = `"$token`"",
    'ClientCheck = 0', 'Env.Override = 1', "Env = $envHost",
    $log
) -join "`r`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'client.config'), $config + "`r`n", (New-Object Text.UTF8Encoding($false)))
Write-Output "client.config written for $who (Env = $envHost)."

if ($WriteConfigOnly) { exit 0 }
$preloader = Join-Path $PSScriptRoot 'BepInEx.HsMod\core\BepInEx.Preloader.dll'
$doorstopArgs = '--doorstop-target-assembly "' + $preloader + '"'
Set-HsWindowPrefs
Start-Process -FilePath (Join-Path $PSScriptRoot 'Hearthstone.exe') -ArgumentList $doorstopArgs -WorkingDirectory $PSScriptRoot
