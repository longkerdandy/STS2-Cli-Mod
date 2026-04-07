# STS2 CLI Mod Installer for Windows (PowerShell)
# Downloads and installs the CLI tool and game mod from GitHub Releases.
#
# Install (latest):
#   irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
#
# Install specific version:
#   $env:STS2_VERSION="0.102.1"; irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
#
# Uninstall:
#   $env:STS2_UNINSTALL="1"; irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
#
# Or download and run with parameters:
#   .\install.ps1 -Version 0.102.1
#   .\install.ps1 -GameDir "D:\Steam\steamapps\common\Slay the Spire 2"
#   .\install.ps1 -Uninstall

[CmdletBinding()]
param(
    [string]$Version,
    [string]$GameDir,
    [string]$CliDir,
    [switch]$SkipMod,
    [switch]$SkipCli,
    [switch]$NoModifyPath,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# ── Constants ──────────────────────────────────────────────────────────────────

$Repo = "longkerdandy/STS2-Cli-Mod"
$GameName = "Slay the Spire 2"
$ModFiles = @("STS2.Cli.Mod.dll", "STS2.Cli.Mod.json")
$DefaultCliDir = Join-Path $env:LOCALAPPDATA "sts2-cli"

# Support piped invocation: pick up env vars set before piping.
# NOTE: param() in iex context runs in the caller's scope and overwrites any
# global variables with the same name, so we use $env: vars which are immune.
if (-not $Version -and $env:STS2_VERSION) {
    $Version = $env:STS2_VERSION
    $env:STS2_VERSION = $null
}
if (-not $Uninstall -and $env:STS2_UNINSTALL) {
    $Uninstall = [switch]::Present
    $env:STS2_UNINSTALL = $null
}

# ── Helpers ────────────────────────────────────────────────────────────────────

function Write-Step {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Red
}

function Write-Muted {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor DarkGray
}

# ── Steam Game Directory Detection ────────────────────────────────────────────

function Find-SteamLibraryFolders {
    <#
    .SYNOPSIS
        Returns all Steam library paths by parsing libraryfolders.vdf.
    #>
    $steamDir = "C:\Program Files (x86)\Steam"
    $vdfPath = Join-Path $steamDir "config\libraryfolders.vdf"

    $paths = @()

    # Always include the default Steam directory
    $defaultApps = Join-Path $steamDir "steamapps\common"
    if (Test-Path $defaultApps) {
        $paths += $steamDir
    }

    # Parse libraryfolders.vdf for additional library paths
    if (Test-Path $vdfPath) {
        try {
            $content = Get-Content $vdfPath -Raw
            # Match "path" entries: "path"    "C:\\SteamLibrary"
            $matches = [regex]::Matches($content, '"path"\s+"([^"]+)"')
            foreach ($m in $matches) {
                $libPath = $m.Groups[1].Value -replace '\\\\', '\'
                if ($libPath -ne $steamDir -and (Test-Path $libPath)) {
                    $paths += $libPath
                }
            }
        }
        catch {
            # Silently ignore parse errors
        }
    }

    return $paths
}

function Find-GameDirectory {
    <#
    .SYNOPSIS
        Locates the STS2 game directory. Priority:
        1. -GameDir parameter
        2. STS2_GAME_DIR environment variable
        3. Steam default path
        4. libraryfolders.vdf scan
    #>

    # 1. Explicit parameter
    if ($GameDir) {
        if (Test-Path $GameDir) {
            return $GameDir
        }
        Write-Err "Specified game directory not found: $GameDir"
        return $null
    }

    # 2. Environment variable
    $envDir = $env:STS2_GAME_DIR
    if ($envDir -and (Test-Path $envDir)) {
        return $envDir
    }

    # 3 & 4. Search all Steam libraries
    $steamLibs = Find-SteamLibraryFolders
    foreach ($lib in $steamLibs) {
        $candidate = Join-Path $lib "steamapps\common\$GameName"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

# ── Version Helpers ────────────────────────────────────────────────────────────

function Get-LatestReleaseTag {
    <#
    .SYNOPSIS
        Fetches the latest release tag from GitHub (e.g. "v0.102.1").
        Tries the GitHub API first, falls back to parsing the 302 redirect
        from the releases/latest page if the API is rate-limited.
    #>

    # Primary: GitHub REST API
    try {
        $response = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{
            "Accept"     = "application/vnd.github+json"
            "User-Agent" = "STS2-Installer"
        }
        if ($response.tag_name) {
            return $response.tag_name
        }
    }
    catch {
        Write-Muted "  GitHub API unavailable (rate limit?), trying fallback..."
    }

    # Fallback: parse redirect Location from releases/latest page (no API quota)
    try {
        $request = [System.Net.WebRequest]::Create("https://github.com/$Repo/releases/latest")
        $request.AllowAutoRedirect = $false
        $request.UserAgent = "STS2-Installer"
        $response = $request.GetResponse()
        $location = $response.Headers["Location"]
        $response.Close()
        if ($location -match '/tag/(.+)$') {
            return $Matches[1]
        }
    }
    catch {
        # Ignore fallback errors
    }

    Write-Err "Failed to fetch latest release from GitHub."
    return $null
}

function Get-InstalledCliVersion {
    param([string]$ExePath)
    if (Test-Path $ExePath) {
        try {
            $ver = & $ExePath --version 2>$null
            if ($LASTEXITCODE -eq 0 -and $ver) {
                return ($ver | Out-String).Trim()
            }
        }
        catch { }
    }
    return $null
}

# ── PATH Management ────────────────────────────────────────────────────────────

function Add-ToUserPath {
    param([string]$Directory)
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    $entries = $currentPath -split ";" | Where-Object { $_.Trim() -ne "" }
    $normalized = $Directory.TrimEnd('\')
    foreach ($entry in $entries) {
        if ($entry.TrimEnd('\') -eq $normalized) {
            return $false  # already present
        }
    }
    [Environment]::SetEnvironmentVariable("PATH", "$Directory;$currentPath", "User")
    $env:PATH = "$Directory;$env:PATH"
    return $true
}

function Remove-FromUserPath {
    param([string]$Directory)
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    $entries = $currentPath -split ";" | Where-Object { $_.Trim() -ne "" }
    $normalized = $Directory.TrimEnd('\')
    $filtered = $entries | Where-Object { $_.TrimEnd('\') -ne $normalized }
    $newPath = ($filtered -join ";")
    if ($newPath -ne $currentPath) {
        [Environment]::SetEnvironmentVariable("PATH", $newPath, "User")
        return $true
    }
    return $false
}

# ── Download & Install ─────────────────────────────────────────────────────────

function Install-Cli {
    param(
        [string]$Tag,       # e.g. "v0.102.1"
        [string]$InstallDir
    )

    $ver = $Tag.TrimStart('v')
    $fileName = "sts2-cli-v${ver}-win-x64.zip"
    $url = "https://github.com/$Repo/releases/download/$Tag/$fileName"

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "sts2-install-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    try {
        Write-Step "Downloading CLI ($fileName)..."
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-WebRequest -Uri $url -OutFile (Join-Path $tempDir $fileName) -UseBasicParsing
        }
        catch {
            Write-Err "Failed to download CLI: $_"
            Write-Muted "URL: $url"
            return $false
        }
        finally {
            $ProgressPreference = 'Continue'
        }

        Write-Step "Extracting..."
        Expand-Archive -Path (Join-Path $tempDir $fileName) -DestinationPath $tempDir -Force

        # Find sts2.exe in extracted files
        $exePath = Join-Path $tempDir "sts2.exe"
        if (-not (Test-Path $exePath)) {
            $exePath = Get-ChildItem -Path $tempDir -Recurse -Filter "sts2.exe" |
                       Select-Object -First 1 -ExpandProperty FullName
        }
        if (-not $exePath -or -not (Test-Path $exePath)) {
            Write-Err "Could not find sts2.exe in the downloaded archive"
            return $false
        }

        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        Copy-Item -Path $exePath -Destination (Join-Path $InstallDir "sts2.exe") -Force
        Write-Success "CLI installed to $InstallDir\sts2.exe"
        return $true
    }
    finally {
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Install-Mod {
    param(
        [string]$Tag,       # e.g. "v0.102.1"
        [string]$ModDir     # e.g. "C:\...\Slay the Spire 2\mods"
    )

    $ver = $Tag.TrimStart('v')
    $fileName = "sts2-mod-v${ver}.zip"
    $url = "https://github.com/$Repo/releases/download/$Tag/$fileName"

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "sts2-install-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    try {
        Write-Step "Downloading Mod ($fileName)..."
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-WebRequest -Uri $url -OutFile (Join-Path $tempDir $fileName) -UseBasicParsing
        }
        catch {
            Write-Err "Failed to download Mod: $_"
            Write-Muted "URL: $url"
            return $false
        }
        finally {
            $ProgressPreference = 'Continue'
        }

        Write-Step "Extracting..."
        Expand-Archive -Path (Join-Path $tempDir $fileName) -DestinationPath $tempDir -Force

        New-Item -ItemType Directory -Path $ModDir -Force | Out-Null
        foreach ($file in $ModFiles) {
            $src = Join-Path $tempDir $file
            if (-not (Test-Path $src)) {
                $src = Get-ChildItem -Path $tempDir -Recurse -Filter $file |
                       Select-Object -First 1 -ExpandProperty FullName
            }
            if ($src -and (Test-Path $src)) {
                Copy-Item -Path $src -Destination (Join-Path $ModDir $file) -Force
            }
            else {
                Write-Warn "Mod file not found in archive: $file"
            }
        }
        Write-Success "Mod installed to $ModDir"
        return $true
    }
    finally {
        if (Test-Path $tempDir) {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# ── Uninstall ──────────────────────────────────────────────────────────────────

function Invoke-Uninstall {
    Write-Host ""
    Write-Host "  STS2 CLI Mod Uninstaller" -ForegroundColor Cyan
    Write-Host ""

    $installDir = if ($CliDir) { $CliDir } else { $DefaultCliDir }
    $removed = $false

    # Remove CLI
    $exePath = Join-Path $installDir "sts2.exe"
    if (Test-Path $exePath) {
        Remove-Item -Path $exePath -Force
        Write-Success "Removed CLI: $exePath"
        $removed = $true
    }

    # Remove bug reports directory
    $bugDir = Join-Path $installDir "sts2-cli-bugs"
    if (Test-Path $bugDir) {
        Remove-Item -Path $bugDir -Recurse -Force
        Write-Success "Removed bug reports: $bugDir"
    }

    # Remove CLI directory if empty
    if ((Test-Path $installDir) -and @(Get-ChildItem $installDir -Force).Count -eq 0) {
        Remove-Item -Path $installDir -Force
        Write-Success "Removed empty directory: $installDir"
    }

    # Remove from PATH
    $pathRemoved = Remove-FromUserPath -Directory $installDir
    if ($pathRemoved) {
        Write-Success "Removed $installDir from user PATH"
    }

    # Remove Mod files
    $gameDirectory = Find-GameDirectory
    if ($gameDirectory) {
        $modDir = Join-Path $gameDirectory "mods"
        foreach ($file in $ModFiles) {
            $filePath = Join-Path $modDir $file
            if (Test-Path $filePath) {
                Remove-Item -Path $filePath -Force
                Write-Success "Removed mod file: $filePath"
                $removed = $true
            }
        }
    }
    else {
        Write-Warn "Could not locate game directory. Mod files may need manual removal."
        Write-Muted 'Mod files: STS2.Cli.Mod.dll, STS2.Cli.Mod.json in <game>/mods/'
    }

    if ($removed) {
        Write-Host ""
        Write-Success "Uninstall complete."
    }
    else {
        Write-Host ""
        Write-Muted "Nothing to uninstall (STS2 CLI Mod does not appear to be installed)."
    }
}

# ── Main ───────────────────────────────────────────────────────────────────────

function Main {
    Write-Host ""
    Write-Host "  +===================================+" -ForegroundColor DarkCyan
    Write-Host "  |       STS2 CLI Mod Installer      |" -ForegroundColor DarkCyan
    Write-Host "  +===================================+" -ForegroundColor DarkCyan
    Write-Host ""

    # Handle uninstall mode
    if ($Uninstall) {
        Invoke-Uninstall
        return
    }

    # Resolve CLI install directory
    $installDir = if ($CliDir) { $CliDir } else { $DefaultCliDir }

    # ── Resolve version ────────────────────────────────────────────────────

    $releaseTag = $null
    if ($Version) {
        $Version = $Version.TrimStart('v')
        $releaseTag = "v$Version"
        Write-Step "Using specified version: $releaseTag"
    }
    else {
        Write-Step "Fetching latest release..."
        $releaseTag = Get-LatestReleaseTag
        if (-not $releaseTag) {
            Write-Err "Could not determine latest version."
            Write-Muted "Try specifying a version: .\install.ps1 -Version 0.102.1"
            return
        }
        Write-Step "Latest version: $releaseTag"
    }
    $ver = $releaseTag.TrimStart('v')
    Write-Host ""

    # ── Check existing installation ────────────────────────────────────────

    if (-not $SkipCli) {
        $exePath = Join-Path $installDir "sts2.exe"
        $installedVer = Get-InstalledCliVersion -ExePath $exePath
        if ($installedVer -and $installedVer -eq $ver) {
            Write-Muted "CLI version $ver is already installed. Skipping CLI."
            $SkipCli = [switch]::Present
        }
        elseif ($installedVer) {
            Write-Muted "Upgrading CLI from $installedVer to $ver"
        }
    }

    # ── Detect game directory ──────────────────────────────────────────────

    $gameDirectory = $null
    $modDir = $null
    if (-not $SkipMod) {
        $gameDirectory = Find-GameDirectory
        if ($gameDirectory) {
            $modDir = Join-Path $gameDirectory "mods"
            Write-Step "Game directory: $gameDirectory"
        }
        else {
            Write-Warn "Could not locate $GameName installation."
            Write-Muted "Mod installation will be skipped."
            Write-Muted 'To install the mod, re-run with: -GameDir "<path to game>"'
            $SkipMod = [switch]::Present
        }
        Write-Host ""
    }

    # ── Install CLI ────────────────────────────────────────────────────────

    $cliOk = $false
    if (-not $SkipCli) {
        $cliOk = Install-Cli -Tag $releaseTag -InstallDir $installDir
        if ($cliOk -and -not $NoModifyPath) {
            $added = Add-ToUserPath -Directory $installDir
            if ($added) {
                Write-Success "Added $installDir to user PATH"
                Write-Muted "Restart your terminal for PATH changes to take effect."
            }
        }
        Write-Host ""
    }

    # ── Install Mod ────────────────────────────────────────────────────────

    $modOk = $false
    if (-not $SkipMod) {
        $modOk = Install-Mod -Tag $releaseTag -ModDir $modDir
        Write-Host ""
    }

    # ── Verify & Summary ──────────────────────────────────────────────────

    Write-Host "  -- Summary --" -ForegroundColor White
    Write-Host ""

    if ($cliOk -or $SkipCli) {
        $exePath = Join-Path $installDir "sts2.exe"
        if (Test-Path $exePath) {
            $verOutput = & $exePath --version 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Success "CLI:  v$verOutput  ($installDir)"
            }
            else {
                Write-Success "CLI:  $installDir\sts2.exe"
            }
        }
        elseif ($SkipCli) {
            Write-Muted "CLI:  skipped"
        }
    }
    else {
        Write-Err "CLI:  installation failed"
    }

    if ($modOk) {
        Write-Success "Mod:  $modDir"
    }
    elseif ($SkipMod) {
        Write-Muted "Mod:  skipped"
    }
    else {
        Write-Err "Mod:  installation failed"
    }

    Write-Host ""
    Write-Host "  -- Get Started --" -ForegroundColor White
    Write-Host ""
    Write-Muted "1. Launch $GameName with mods enabled"
    Write-Muted "2. Open a terminal and run:"
    Write-Host ""
    Write-Host "     sts2 ping" -ForegroundColor White
    Write-Host ""
    Write-Muted "For more information: https://github.com/$Repo"
    Write-Host ""
}

Main
