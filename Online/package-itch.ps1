param([string]$ServerUrl = '')
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webSource = Join-Path $projectRoot 'Online/Server/wwwroot'
$itchTarget = Join-Path $projectRoot 'Builds/WebGLItch'
$archivePath = Join-Path $projectRoot 'Builds/Origins-itch-webgl.zip'
if (!(Test-Path -LiteralPath (Join-Path $webSource 'index.html'))) { throw 'Сначала соберите WebGL в Unity: Истоки → Онлайн → Собрать браузерную игру.' }
if ($ServerUrl) {
    $serverUri = [uri]$ServerUrl
    if (!$serverUri.IsAbsoluteUri -or ($serverUri.Scheme -ne 'https' -and !($serverUri.Scheme -eq 'http' -and $serverUri.IsLoopback))) { throw 'Адрес онлайн-сервера должен быть HTTPS; для локальной проверки допустим localhost.' }
}
if (Test-Path -LiteralPath $itchTarget) {
    $resolvedTarget = (Resolve-Path -LiteralPath $itchTarget).Path
    $resolvedBuilds = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'Builds')) + [System.IO.Path]::DirectorySeparatorChar
    if (!$resolvedTarget.StartsWith($resolvedBuilds,[System.StringComparison]::OrdinalIgnoreCase)) { throw 'Неверный путь выходной сборки.' }
    Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $itchTarget | Out-Null
Get-ChildItem -LiteralPath $webSource | Copy-Item -Destination $itchTarget -Recurse
$serverJson = ConvertTo-Json -InputObject $ServerUrl -Compress
[System.IO.File]::WriteAllText((Join-Path $itchTarget 'server-config.js'),"window.ORIGINS_SERVER_URL = $serverJson;`n")
Compress-Archive -Path (Join-Path $itchTarget '*') -DestinationPath $archivePath -Force -CompressionLevel Optimal
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entries = @($archive.Entries | Where-Object { $_.Name })
    if (!($entries.FullName -contains 'index.html')) { throw 'Нет index.html в корне архива.' }
    if ($entries.Count -gt 1000 -or ($entries | Measure-Object Length -Sum).Sum -gt 500MB -or @($entries | Where-Object { $_.Length -gt 200MB -or $_.FullName.Length -gt 240 }).Count -gt 0) { throw 'Архив не соответствует ограничениям itch.io.' }
    Write-Output ('ITCH PACKAGE READY: ' + $archivePath)
    Write-Output ('Files: ' + $entries.Count + '; bytes: ' + (Get-Item -LiteralPath $archivePath).Length + '; online configured: ' + [bool]$ServerUrl)
} finally { $archive.Dispose() }
