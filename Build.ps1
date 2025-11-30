$currentPath = Get-Location
$AssemblyInfoPath = Join-Path -Path $currentPath -ChildPath "wsl-usb-manager/AssemblyInfo.cs"
$NewProductCode = [guid]::NewGuid().ToString().ToUpper()
$vswherePath = "$env:TEMP\vswhere.exe"
$InstallerScriptPath = Join-Path -Path $currentPath -ChildPath "Installer.iss"
$global:AppVersion = ""
$InstallerOutputDir = Join-Path -Path $currentPath -ChildPath "BuildOutput/Installer"
function Test-Winget {

    try {
        winget --version | Out-Null -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-VisualStudio {
    # Check if vswhere.exe is available
    if (-Not (Test-Path -Path $vswherePath)) {
        Write-Host "Downloading vswhere.exe..." -ForegroundColor Cyan
        try{
            # Use the x64 version specifically for better compatibility
            Invoke-WebRequest -Uri "https://github.com/microsoft/vswhere/releases/latest/download/vswhere.exe" -OutFile $vswherePath -ErrorAction Stop
        }
        catch
        {
            Write-Host "Failed to download vswhere.exe" -ForegroundColor Red
            return $false
        }
    }

    # Verify the downloaded file is valid
    if (-Not (Test-Path $vswherePath)) {
        Write-Host "vswhere.exe not found at $vswherePath" -ForegroundColor Red
        return $false
    }

    # Check if file is valid by getting its info
    try {
        $fileInfo = Get-Item $vswherePath
        if ($fileInfo.Length -eq 0) {
            Write-Host "vswhere.exe file is empty, re-downloading..." -ForegroundColor Yellow
            Remove-Item $vswherePath -Force -ErrorAction SilentlyContinue
            Invoke-WebRequest -Uri "https://github.com/microsoft/vswhere/releases/latest/download/vswhere.exe" -OutFile $vswherePath -ErrorAction Stop
        }
    }
    catch {
        Write-Host "Error checking vswhere.exe file: $_" -ForegroundColor Red
        return $false
    }

    # Find all installed Visual Studio instances
    try {
        $vsInstances = & $vswherePath -all -format json | ConvertFrom-Json

        if ($vsInstances.Count -eq 0) {
            Write-Host "Cannot find any Visual Studio instances." -ForegroundColor Red
            return $false
        }

        foreach ($instance in $vsInstances) {
            $installationPath = $instance.installationPath
            $installationVersion = $instance.catalog.productDisplayVersion
            $edition = $instance.catalog.productLine

            Write-Host "Found installed Visual Studio instance:"
            Write-Host "Version: $installationVersion"
            Write-Host "Path: $installationPath"
            Write-Host "Edition: $edition"
            Write-Host "-------------------------"
        }
    }
    catch {
        Write-Host "Error running vswhere.exe: $_" -ForegroundColor Red
        Write-Host "Trying to re-download vswhere.exe..." -ForegroundColor Yellow

        # Try to re-download vswhere.exe
        try {
            Remove-Item $vswherePath -Force -ErrorAction SilentlyContinue
            Invoke-WebRequest -Uri "https://github.com/microsoft/vswhere/releases/latest/download/vswhere.exe" -OutFile $vswherePath -ErrorAction Stop
            $vsInstances = & $vswherePath -all -format json | ConvertFrom-Json

            if ($vsInstances.Count -eq 0) {
                Write-Host "Cannot find any Visual Studio instances after re-download." -ForegroundColor Red
                return $false
            }
        }
        catch {
            Write-Host "Failed to run vswhere.exe after re-download: $_" -ForegroundColor Red
            return $false
        }
    }

    # Use the first found Visual Studio instance to build the project
    if ($vsInstances.Count -gt 0) {
        $firstInstance = $vsInstances[0]
        $devenvPath = Join-Path -Path $firstInstance.installationPath -ChildPath "Common7\IDE\devenv.com"
        Write-Host "devenv path: $devenvPath" -ForegroundColor Cyan
        return $true
    }

    Write-Host "Cannot find any Visual Studio instances." -ForegroundColor Red
    return $false
}


function Get-GitVersion-Update-AssemblyInfo {
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $gitVersion = (git --version) -replace 'git version '
        Write-Host "Git is installed: $gitVersion" -ForegroundColor Green
    }
    else {
        Write-Host "Git is not installed or not in the PATH environment variable. Please install git and add it to the PATH environment variable." -ForegroundColor Red
        return $false
    }
    # Check dotnet-gitversion and install if not installed
    $gitversionInstalled = dotnet tool list -g | Select-String -Pattern "dotnet-gitversion"

    if (-not $gitversionInstalled) {
        Write-Host "dotnet-gitversion is not installed, and is being installed..." -ForegroundColor Cyan
        dotnet tool install --global GitVersion.Tool
    } else {
        Write-Host "dotnet-gitversion is installed." -ForegroundColor Green
    }

    Write-Host "Updating assembly info..." -ForegroundColor Cyan
    try{
        & git tag -l | ForEach-Object{ git tag -d $_ }
        & git fetch origin --tags
        if ($LastExitCode -ne 0) { return $false }
        & git checkout $AssemblyInfoPath
        if ($LastExitCode -ne 0) { return $false }
        & dotnet-gitversion /updateassemblyinfo $AssemblyInfoPath /ensureassemblyinfo
        if ($LastExitCode -ne 0) { return $false }
    }
    catch {
        Write-Host "Failed to check git version" -ForegroundColor Red
        return $false
    }

    Write-Host "Checking git version..." -ForegroundColor Cyan
    # Get the version number from AssemblyInfo.cs
    $AssembleInfoContent = Get-Content -Path $AssemblyInfoPath
    $versionPattern = '\[assembly: AssemblyVersion\("(\d+\.\d+\.\d+\.\d+)"\)\]'
    $versionMatch = [regex]::Match($AssembleInfoContent, $versionPattern)

    if ($versionMatch.Success) {
        $fullVersion = $versionMatch.Groups[1].Value
        # remove the last part of the version number
        $global:AppVersion = ($fullVersion -split '\.')[0..2] -join '.'
        Write-Host "Version number found: $fullVersion" -ForegroundColor Green
        Write-Host "App version number: $global:AppVersion" -ForegroundColor Green
        return $true
    }

    Write-Host "Failed to get the version number from AssemblyInfo.cs" -ForegroundColor Red
    return $false
}

function Get-InnoSetupCompilerPath {
    <#
    .SYNOPSIS
    Checks if Inno Setup is installed and returns its installation path.
    .DESCRIPTION
    This function searches for the Inno Setup installation location using both registry queries and common path searches,
    supporting Inno Setup versions 5 and 6, returning a string path or $null (if not found).
    .OUTPUTS
    System.String or $null
    #>
    $innoUninstallKeys = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    foreach ($keyPath in $innoUninstallKeys) {
        $key = Get-ItemProperty -Path $keyPath -ErrorAction SilentlyContinue |
               Where-Object { $_.DisplayName -like "*Inno Setup*" }

        if ($key -and $key.InstallLocation) {
            $isccPath = Join-Path $key.InstallLocation "ISCC.exe"
            if (Test-Path $isccPath) {
                return $isccPath
            }
        }
    }

    $possiblePaths = @(
        "${env:ProgramFiles}\Inno Setup 6",
        "${env:ProgramFiles(x86)}\Inno Setup 6",
        "${env:ProgramFiles}\Inno Setup 5",
        "${env:ProgramFiles(x86)}\Inno Setup 5",
        "$env:USERPROFILE\AppData\Local\Programs\Inno Setup 6",
        "$env:USERPROFILE\AppData\Local\Programs\Inno Setup 5",
        "$env:USERPROFILE\AppData\Local\Programs\Inno Setup",
        "$env:USERPROFILE\AppData\Local\Microsoft\WindowsApps",
        "$env:USERPROFILE\AppData\Local\Inno Setup",
        "$env:USERPROFILE\AppData\Local\Inno Setup 5",
        "$env:USERPROFILE\AppData\Local\Inno Setup 6"
    )

    $foundPath = $possiblePaths | Where-Object {
        Test-Path -Path $_ -PathType Container -ErrorAction SilentlyContinue
    } | Where-Object {
        $null -ne (Get-ChildItem -Path $_ -Filter "ISCC.exe" -ErrorAction SilentlyContinue)
    } | Select-Object -First 1

    if ($foundPath) {
        $isccPath = Join-Path -Path $foundPath "ISCC.exe"
        return $isccPath
    }

    return $null
}


function Install-InnoSetup {
    Write-Host "Inno Setup not detected, installing via winget..." -ForegroundColor Cyan
    # Check if winget is available
    if (-not (Test-Winget)) {
        Write-Host "Error: winget tool not found, cannot install Inno Setup" -ForegroundColor Red
        return $false
    }

    Write-Host "Installing Inno Setup using winget..." -ForegroundColor Yellow

    try {
        # Install Inno Setup using winget
        $process = Start-Process -FilePath "winget" -ArgumentList "install --id JRSoftware.InnoSetup -e -s winget --accept-source-agreements --accept-package-agreements" -Wait -PassThru -NoNewWindow

        if ($process.ExitCode -ne 0) {
            Write-Host "Failed to install Inno Setup via winget, exit code: $($process.ExitCode)" -ForegroundColor Red
            return $false
        }

        Write-Host "Inno Setup installation completed" -ForegroundColor Cyan
        # Wait a bit after installation to ensure it's complete
        Start-Sleep -Seconds 3
    }
    catch {
        Write-Host "Failed to install Inno Setup: $_" -ForegroundColor Red
        return $false
    }
    return $true
}

function Update-InnoSetupScript {
    try {
        # Ensure the file exists
        if (-Not (Test-Path -Path $InstallerScriptPath)) {
            Write-Host "File not found: $InstallerScriptPath" -ForegroundColor Red
            return $false
        }

        # Read file content
        $content = Get-Content -Path $InstallerScriptPath -Raw

        # Replace version number
        # Replace version number
        Write-Host "Updating AppVersion number to $global:AppVersion..." -ForegroundColor Cyan
        $content = $content -replace '(#define\s+MyAppVersion\s+")([^"]+)(")', "`${1}$global:AppVersion`${3}"
        $content = $content -replace '(AppId\s*=\s*["{{]*)([^}"\r\n]*)(["{}]*)', "`${1}$NewProductCode`${3}"
        $content = $content -replace '(OutputBaseFilename\s*=\s*["{{]*)([^}"\r\n]*)(["{}]*)', "`${1}WSL USB Manager v$global:AppVersion`${3}"

        try{
            # Write the modified content
            Set-Content -Path $InstallerScriptPath -Value $content -NoNewline -ErrorAction Stop
        }
        catch {
            Write-Host "Failed to update AppVersion and AppId: $_" -ForegroundColor Red
            return $false
        }

        Write-Host "Successfully updated $InstallerScriptPath" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "Error updating version number: $_" -ForegroundColor Red
        return $false
    }
    return $true
}

if (-Not (Test-VisualStudio)) {
    Write-Host "Visual Studio is not installed or not found. Please install Visual Studio to build the project." -ForegroundColor Red
    exit 1
}

if (-not (Get-InnoSetupCompilerPath) -and -Not (Install-InnoSetup)) {
    Write-Host "Failed to install Inno Setup." -ForegroundColor Red
    Write-Host "Please install Inno Setup manually from https://jrsoftware.org/isdl" -ForegroundColor Yellow
    exit 1
}

# Get compiler path
$isccPath = Get-InnoSetupCompilerPath
if (-not $isccPath) {
    Write-Host "Error: Could not find Inno Setup compiler (ISCC.exe)" -ForegroundColor Red
    Write-Host "Although the installation process is complete, you may need to restart PowerShell or your computer to recognize the newly installed component" -ForegroundColor Yellow
    exit 1
}

if (-not (Get-GitVersion-Update-AssemblyInfo)) {
    Write-Host "Failed to get the version number from AssemblyInfo.cs. Exiting." -ForegroundColor Red
    exit 1
}

if (-Not (Update-InnoSetupScript)) {
    Write-Host "Failed to update Inno Setup script with the new version number." -ForegroundColor Red
    exit 1
}

Write-Host "Building and publish dotnet..." -ForegroundColor Cyan
& dotnet publish -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

if (Test-Path $InstallerOutputDir) {
    Get-ChildItem -Path $InstallerOutputDir -File | Remove-Item -Force
}

Write-Host "Compiling $InstallerScriptPath using $isccPath ..." -ForegroundColor Cyan
try {
    $process = Start-Process -FilePath $isccPath -ArgumentList "$InstallerScriptPath" -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) {
        Write-Host "Compilation failed, exit code: $($process.ExitCode)" -ForegroundColor Red
        exit $process.ExitCode
    }
    Write-Host "Compilation completed!" -ForegroundColor Green
}
catch {
    Write-Host "An error occurred during compilation: $_" -ForegroundColor Red
    exit 1
}
