# Deploy RVT Load LAZ to Revit 2024

$sourceDir = "D:\VSCode_Working\Python\Revit_COPO_LAZ\RvtLoadLaz\bin\Debug"
$targetDir = "$env:APPDATA\Autodesk\Revit\Addins\2024"

Write-Host "Deploying RVT Load LAZ to Revit 2024..." -ForegroundColor Cyan

# Create target directory if it doesn't exist
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# Check if Revit is running
$revitProcess = Get-Process "Revit" -ErrorAction SilentlyContinue
if ($revitProcess) {
    Write-Host "⚠ Revit is running. Please close Revit before deploying." -ForegroundColor Yellow
    Read-Host "Press Enter when Revit is closed"
}

# Copy files
Write-Host "  Copying DLL..." -ForegroundColor Gray
Copy-Item "$sourceDir\RvtLoadLaz.dll" $targetDir -Force
Copy-Item "$sourceDir\RvtLoadLaz.pdb" $targetDir -Force -ErrorAction SilentlyContinue

# Copy Tools directory (important for laszip.exe)
if (Test-Path "D:\VSCode_Working\Python\Revit_COPO_LAZ\RvtLoadLaz\Tools") {
    Write-Host "  Copying Tools folder..." -ForegroundColor Gray
    $toolsTarget = "$targetDir\Tools"
    if (!(Test-Path $toolsTarget)) {
        New-Item -ItemType Directory -Path $toolsTarget -Force | Out-Null
    }
    Copy-Item "D:\VSCode_Working\Python\Revit_COPO_LAZ\RvtLoadLaz\Tools\*" $toolsTarget -Recurse -Force
}

# Copy .addin file
Write-Host "  Copying .addin manifest..." -ForegroundColor Gray
Copy-Item "D:\VSCode_Working\Python\Revit_COPO_LAZ\RvtLoadLaz.addin" $targetDir -Force

Write-Host ""  
Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host "   Location: $targetDir" -ForegroundColor Gray
Write-Host ""
Write-Host "You can now start Revit 2024" -ForegroundColor Cyan
Write-Host ""
Write-Host "📌 Make sure laszip.exe is in the Tools folder before running!" -ForegroundColor Yellow
