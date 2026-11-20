<#
.SYNOPSIS
    Windows ajanının tek dosyalık kurulum paketini (aiguard-agent.exe) oluşturur (Y4).

.DESCRIPTION
    Kurumun sunucu adresi ve sınav sitesinin adresi paketin içine gömülür; aday hiçbir ayar yapmaz.
    Çıktı: <Output>\aiguard-agent.exe (self-contained, .NET kurulumu gerektirmez).

.EXAMPLE
    ./publish-windows.ps1 -ApiBaseUrl https://api.sinav.ornek.edu.tr -AllowedOrigins https://sinav.ornek.edu.tr

.EXAMPLE
    # Yerel deneme (Docker'daki API ve Vite önyüzü)
    ./publish-windows.ps1 -ApiBaseUrl http://localhost:8080 -AllowedOrigins http://localhost:5173
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ApiBaseUrl,

    [Parameter(Mandatory = $true)]
    [string[]] $AllowedOrigins,

    [string] $Output = (Join-Path $PSScriptRoot "..\dist\windows")
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\AIGuard.Agent.Windows\AIGuard.Agent.Windows.csproj"
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("aiguard-agent-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force $staging | Out-Null

try {
    # Ajan açılışta bu ayarları doğruluyor (https zorunluluğu, origin biçimi); hatalıysa ajan açılmaz.
    $deployment = @{ Agent = @{ ApiBaseUrl = $ApiBaseUrl; AllowedOrigins = @($AllowedOrigins) } } | ConvertTo-Json -Depth 4
    $settings = Join-Path $staging "deployment.json"
    [System.IO.File]::WriteAllText($settings, $deployment, (New-Object System.Text.UTF8Encoding $false))

    dotnet publish $project -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -p:DeploymentSettingsFile="$settings" `
        -o $Output
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish başarısız ($LASTEXITCODE)." }
}
finally {
    Remove-Item -Recurse -Force $staging
}

$exe = Join-Path $Output "aiguard-agent.exe"
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Kurulum paketi hazır: $exe ($size MB)"
Write-Host "Kurulum notu: docs/ajan-kurulum-windows.md"
