# cesta k launchSettings.json
$launchSettingsPath = ".\Properties\launchSettings.json"

# načtení JSON
$json = Get-Content $launchSettingsPath -Raw | ConvertFrom-Json

# získání URL stringu
$urls = $json.profiles.https.applicationUrl

# rozdělení na jednotlivé URL (oddělené ;)
$urlList = $urls -split ";"

# najdi HTTPS URL
$httpsUrl = $urlList | Where-Object { $_ -like "https://*" } | Select-Object -First 1

if (-not $httpsUrl) {
    Write-Error "HTTPS URL not found"
    exit 1
}

# extrahuj port
if ($httpsUrl -match "https://[^:]+:(\d+)") {
    $port = $Matches[1]
} else {
    Write-Error "Port not found in HTTPS URL"
    exit 1
}

Write-Host "Using HTTPS port: $port"

# spuštění aplikace
cd bin\Debug\net10.0
.\Alza.PricingService.exe --urls "https://localhost:$port"