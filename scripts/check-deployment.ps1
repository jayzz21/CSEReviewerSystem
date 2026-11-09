param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    foreach ($file in @('Dockerfile', '.dockerignore', 'render.yaml', 'deploy/appsettings.json')) {
        if (!(Test-Path -LiteralPath $file)) { throw "Missing deployment file: $file" }
    }
    $deploymentConfig = Get-Content deploy/appsettings.json -Raw | ConvertFrom-Json
    if ($deploymentConfig.ConnectionStrings.Study) { throw 'Deployment settings must use the environment variable, not an embedded connection string.' }
    foreach ($file in @('auth.js')) {
        & node --check "CseReviewer/wwwroot/$file"
        if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax check failed: $file" }
    }
    & dotnet publish CseReviewer/CseReviewer.csproj -c Release -r linux-x64 --self-contained false -p:UseAppHost=false -p:OutputPath=bin/DeployCheck/ -o artifacts/deploy-check
    if ($LASTEXITCODE -ne 0) { throw 'Linux publish failed.' }
    Copy-Item -LiteralPath deploy/appsettings.json -Destination artifacts/deploy-check/appsettings.json -Force
    $developmentConfig = Join-Path $projectRoot 'artifacts/deploy-check/appsettings.Development.json'
    if (Test-Path -LiteralPath $developmentConfig) { Remove-Item -LiteralPath $developmentConfig }
    foreach ($file in @('CseReviewer.dll','wwwroot/site.css','wwwroot/auth.js')) {
        if (!(Test-Path -LiteralPath "artifacts/deploy-check/$file")) { throw "Publish output is missing: $file" }
    }
    Write-Host 'PASS: deployment files, JavaScript, and Linux publish.'
    Write-Host 'Render will build the Docker image. This check does not deploy or connect to the database.'
} finally { Pop-Location }
