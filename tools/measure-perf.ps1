#requires -Version 5.1
<#
.SYNOPSIS
    Measures CPU, memory, threads and handles of a running NotchWin process.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/measure-perf.ps1 -Label "collapsed-idle" -Seconds 30
#>
param(
    [string]$ProcessName = "NotchWin",
    [int]$Seconds = 30,
    [int]$SampleMs = 1000,
    [string]$Label = "run",
    [string]$OutCsv = ""
)

$ErrorActionPreference = "Stop"

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Error "Process '$ProcessName' is not running."
    return
}

$cores = [Environment]::ProcessorCount
$startCpu = $proc.TotalProcessorTime.TotalSeconds
$startClock = [DateTime]::UtcNow

$samples = New-Object System.Collections.Generic.List[object]
$peakWs = 0.0
$peakPrivate = 0.0
$peakHandles = 0
$peakThreads = 0

while (([DateTime]::UtcNow - $startClock).TotalSeconds -lt $Seconds) {
    Start-Sleep -Milliseconds $SampleMs
    $proc.Refresh()
    if ($proc.HasExited) { break }

    $ws = $proc.WorkingSet64 / 1MB
    $private = $proc.PrivateMemorySize64 / 1MB
    $handles = $proc.HandleCount
    $threads = $proc.Threads.Count

    if ($ws -gt $peakWs) { $peakWs = $ws }
    if ($private -gt $peakPrivate) { $peakPrivate = $private }
    if ($handles -gt $peakHandles) { $peakHandles = $handles }
    if ($threads -gt $peakThreads) { $peakThreads = $threads }

    $samples.Add([pscustomobject]@{
        ElapsedSec = [math]::Round(([DateTime]::UtcNow - $startClock).TotalSeconds, 1)
        CpuSeconds = [math]::Round($proc.TotalProcessorTime.TotalSeconds - $startCpu, 3)
        WorkingSetMB = [math]::Round($ws, 1)
        PrivateMB = [math]::Round($private, 1)
        Threads = $threads
        Handles = $handles
    })
}

$endCpu = $proc.TotalProcessorTime.TotalSeconds
$wall = ([DateTime]::UtcNow - $startClock).TotalSeconds
$cpuUsed = $endCpu - $startCpu
$cpuSystemPct = if ($wall -gt 0) { ($cpuUsed / ($wall * $cores)) * 100 } else { 0 }
$cpuOneCorePct = if ($wall -gt 0) { ($cpuUsed / $wall) * 100 } else { 0 }

$proc.Refresh()
Write-Host ""
Write-Host ("=== NotchWin perf [{0}] ===" -f $Label)
Write-Host ("Duration        : {0:N1}s @ {1}ms samples ({2} logical cores)" -f $wall, $SampleMs, $cores)
Write-Host ("CPU seconds     : {0:N3}s" -f $cpuUsed)
Write-Host ("CPU % system    : {0:N3}%" -f $cpuSystemPct)
Write-Host ("CPU % one core  : {0:N2}%" -f $cpuOneCorePct)
Write-Host ("WorkingSet      : {0:N1} MB (peak {1:N1})" -f ($proc.WorkingSet64 / 1MB), $peakWs)
Write-Host ("Private commit  : {0:N1} MB (peak {1:N1})" -f ($proc.PrivateMemorySize64 / 1MB), $peakPrivate)
Write-Host ("Threads         : {0} (peak {1})" -f $proc.Threads.Count, $peakThreads)
Write-Host ("Handles         : {0} (peak {1})" -f $proc.HandleCount, $peakHandles)

if ($OutCsv -ne "") {
    $samples | Export-Csv -Path $OutCsv -NoTypeInformation -Encoding UTF8
    Write-Host ("Samples written : {0}" -f $OutCsv)
}
