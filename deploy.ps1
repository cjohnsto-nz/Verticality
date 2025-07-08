# Verticality Mod Deployment Script
# This script stops the game, builds the project, copies the output, and restarts the game.

# --- Configuration ---
$ErrorActionPreference = 'Stop' # Exit script on any error
$ProjectName = "Verticality"
$ProjectRoot = $PSScriptRoot # This special variable gets the directory where the script is located
$ModsDir = "C:\Users\chris\AppData\Roaming\VintagestoryData\Mods"
$VSProcessName = "Vintagestory"
$VSExePath = "C:\Users\chris\AppData\Roaming\Vintagestory\Vintagestory.exe"

# --- Pre-Build: Stop Game ---
Write-Host "Checking for running Vintage Story process..." -ForegroundColor Cyan
$vsProcess = Get-Process -Name $VSProcessName -ErrorAction SilentlyContinue
if ($vsProcess) {
    Write-Host "Vintage Story is running. Stopping process..."
    Stop-Process -Name $VSProcessName -Force
    # Give it a moment to release file locks
    Start-Sleep -Seconds 2
}

# --- Build Step ---
Write-Host "Building $ProjectName project..." -ForegroundColor Cyan

# Instead of using dotnet build directly, use the project's existing build script
# which is already set up with the correct packaging process
./build.ps1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED. Deployment aborted." -ForegroundColor Red
    exit 1
}

Write-Host "Build SUCCEEDED." -ForegroundColor Green

# --- Deploy Step ---
# Get the mod output directory from build results
$SourceDir = Join-Path $ProjectRoot "Verticality\bin\Release\Mods\mod"

if (-not (Test-Path $SourceDir)) {
    Write-Host "Error: Packaged mod directory not found at '$SourceDir'." -ForegroundColor Red
    exit 1
}

# Get version from modinfo.json to create versioned zip file
$ModInfoPath = Join-Path $SourceDir "modinfo.json"
$Version = "0.0.0" # Default version if not found
if (Test-Path $ModInfoPath) {
    $ModInfo = Get-Content $ModInfoPath | ConvertFrom-Json
    $Version = $ModInfo.version
}

# Create zip filename
$ZipFileName = "verticality_$Version.zip"
$ZipFilePath = Join-Path $ModsDir $ZipFileName

Write-Host "Deploying mod as '$ZipFileName' to '$ModsDir'..." -ForegroundColor Cyan

# Create temp directory for packaging
$TempDir = Join-Path $env:TEMP "VerticalityTempDeploy"

# Clean temp directory if it exists
if (Test-Path $TempDir) {
    Remove-Item -Recurse -Force $TempDir
}

# Create temp directory
New-Item -ItemType Directory -Path $TempDir | Out-Null

# Copy DLL to root of temp directory
Write-Host "Ensuring DLL is in root folder as required by Vintage Story" -ForegroundColor Cyan
Copy-Item -Path "$SourceDir\Verticality.dll" -Destination $TempDir

# Copy all other files
Copy-Item -Path "$SourceDir\modinfo.json" -Destination $TempDir
Copy-Item -Path "$SourceDir\modIcon.png" -Destination $TempDir -ErrorAction SilentlyContinue

# Copy assets folder if it exists
if (Test-Path "$SourceDir\assets") {
    Copy-Item -Path "$SourceDir\assets" -Destination $TempDir -Recurse
}

# Copy any other important folders that might be needed
foreach ($folder in @("native", "resources", "config")) {
    if (Test-Path "$SourceDir\$folder") {
        Copy-Item -Path "$SourceDir\$folder" -Destination $TempDir -Recurse
    }
}

# Ensure modicon.png is included
$ModIconSource = Join-Path $SourceDir "modIcon.png"
if (Test-Path $ModIconSource) {
    Write-Host "Including modIcon.png in package"
    # modIcon.png is already in the output directory, no need to copy
} else {
    Write-Host "Warning: modIcon.png not found at '$ModIconSource'. Icon will not be included." -ForegroundColor Yellow
}

# Remove any existing zip file
if (Test-Path $ZipFilePath) {
    Write-Host "Removing existing zip at '$ZipFilePath'"
    Remove-Item -Force $ZipFilePath
}

# Create zip file
Write-Host "Creating zip file '$ZipFilePath'"
Compress-Archive -Path "$TempDir\*" -DestinationPath $ZipFilePath

# Delete old config if it exists
if (Test-Path "C:\Users\chris\AppData\Roaming\VintagestoryData\ModConfig\verticality.json") {
    Write-Host "Removing old config at 'C:\Users\chris\AppData\Roaming\VintagestoryData\ModConfig\verticality.json'"
    Remove-Item -Path "C:\Users\chris\AppData\Roaming\VintagestoryData\ModConfig\verticality.json" -Force
}

# --- Post-Deploy: Start Game ---
Write-Host "`nDeployment COMPLETE. Launching Vintage Story..." -ForegroundColor Green
Start-Process -FilePath $VSExePath

# --- Post-Launch: Commit Logs ---
# Give the game a moment to start and write initial logs before committing.
Start-Sleep -Seconds 5 
$LogRepoDir = "C:\Users\chris\AppData\Roaming\VintagestoryData\Logs"
if (Test-Path (Join-Path $LogRepoDir ".git")) {
    Write-Host "Committing logs to reset diff for next session..." -ForegroundColor Cyan
    Push-Location $LogRepoDir
    git add .
    # Use a generic commit message. The timestamp will differentiate commits.
    git commit -m "Autocommit logs post-launch" | Out-Null
    Pop-Location
    Write-Host "Log commit complete." -ForegroundColor Green
} else {
    Write-Host "Git repository not found in Logs directory. Skipping commit." -ForegroundColor Yellow
}
