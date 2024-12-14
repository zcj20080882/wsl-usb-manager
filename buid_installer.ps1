$currentPath = Get-Location
$SolutionPath = Join-Path -Path $currentPath -ChildPath "wsl-usb-manager.sln"
$AssemblyInfoPath = Join-Path -Path $currentPath -ChildPath "wsl-usb-manager/AssemblyInfo.cs"
$InstallerPrjPath = Join-Path -Path $currentPath -ChildPath "Installer/Installer.vdproj"
$InstallerOutputPath = Join-Path -Path $currentPath -ChildPath "Installer/Release/WSL-USB-Manager.msi"
$NewProductCode = [guid]::NewGuid().ToString().ToUpper()
$devenvPath = ""
$vswherePath = "$env:TEMP\vswhere.exe"
if (-Not (Test-Path -Path $vswherePath)) {
    Write-Output "Downloading vswhere.exe..."
    Invoke-WebRequest -Uri "https://github.com/microsoft/vswhere/releases/latest/download/vswhere.exe" -OutFile $vswherePath
}

# Find all installed Visual Studio instances
$vsInstances = & $vswherePath -all -format json | ConvertFrom-Json

if ($vsInstances.Count -eq 0) {
    Write-Output "Cannot find any Visual Studio instances."
} else {
    foreach ($instance in $vsInstances) {
        $installationPath = $instance.installationPath
        $installationVersion = $instance.catalog.productDisplayVersion
        $edition = $instance.catalog.productLine

        Write-Output "Found installed Visual Studio instance:"
        Write-Output "Version: $installationVersion"
        Write-Output "Path: $installationPath"
        Write-Output "Edition: $edition"
        Write-Output "-------------------------"
    }
}

# Use the first found Visual Studio instance to build the project
if ($vsInstances.Count -gt 0) {
    $firstInstance = $vsInstances[0]
    $devenvPath = Join-Path -Path $firstInstance.installationPath -ChildPath "Common7\IDE\devenv.com"
    Write-Output "devenv path: $devenvPath"
}
else{
    Write-Output "Cannot find any Visual Studio instances."
    exit 1
}

# Check dotnet-gitversion and install if not installed
$gitversionInstalled = dotnet tool list -g | Select-String -Pattern "dotnet-gitversion"

if (-not $gitversionInstalled) {
    Write-Output "dotnet-gitversion is not installed, and is being installed..."
    dotnet tool install --global GitVersion.Tool
} else {
    Write-Output "dotnet-gitversion is installed."
}

git tag -l | ForEach-Object{ git tag -d $_ }
git fetch origin --tags
git checkout $AssemblyInfoPath
dotnet-gitversion /updateassemblyinfo $AssemblyInfoPath /ensureassemblyinfo

# Get the version number from AssemblyInfo.cs
$AssembleInfoContent = Get-Content -Path $AssemblyInfoPath
$versionPattern = '\[assembly: AssemblyVersion\("(\d+\.\d+\.\d+\.\d+)"\)\]'
$versionMatch = [regex]::Match($AssembleInfoContent, $versionPattern)
$ShortVersion =
if ($versionMatch.Success) {
    $fullVersion = $versionMatch.Groups[1].Value
    # remove the last part of the version number
    $ShortVersion = ($fullVersion -split '\.')[0..2] -join '.'
    $NewInstallerPrjPath = Join-Path -Path $currentPath -ChildPath "tmp.vdproj"
    if ((Test-Path -Path $NewInstallerPrjPath)) {
        #Clear-Content -Path $NewInstallerPrjPath -ErrorAction Stop
        Remove-Item -Path $NewInstallerPrjPath
    }

    $state=""
    $cnt=0
    foreach($line in [System.IO.File]::ReadLines($InstallerPrjPath))
    {
        if ($line -match "OutputFilename") {
            if ($line -match "Debug"){
                $line = $line -replace '(?<="OutputFilename" = ")[^"]*', "8:Debug\\WSL-USB-Manager-v$ShortVersion.msi"
            }
            elseif ($line -match "Release") {
                $line = $line -replace '(?<="OutputFilename" = ")[^"]*', "8:Release\\WSL-USB-Manager-v$ShortVersion.msi"
            }
        }
        switch ($state)
        {
            "Deployable" {
                if ($line -match "Product") {
                    $state="Product"
                }
                break
            }
            "Product" {
                if ($line -match "ProductCode") {
                    $cnt += 1
                    Write-Output "Update ProductCode to $NewProductCode"
                    #$NewProductCode = "8:{$NewProductCode}"
                    $line = $line -replace '(?<="ProductCode" = ")[^"]*', "8:{$NewProductCode}"
                    break
                }
                if ($line -match "ProductVersion") {
                    $cnt += 1
                    Write-Output "Update ProductVersion to $ShortVersion"
                    $line = $line -replace '(?<="ProductVersion" = ")[^"]*', "8:$ShortVersion"
                    break
                }
                break
            }
            default {
                if ($line -match "Deployable") {
                    $state = "Deployable"
                }
                break
            }
        }

        Add-Content -Path $NewInstallerPrjPath -Value $line -ErrorAction Stop
    }

    Get-Content -Path $NewInstallerPrjPath | Set-Content -Path $InstallerPrjPath -ErrorAction Stop
    Remove-Item -Path $NewInstallerPrjPath
} else {
    Write-Output "Failed to get the version number from AssemblyInfo.cs"
    exit 1
}

& $devenvPath $SolutionPath /Rebuild Release /Project $InstallerPrjPath

if ($LASTEXITCODE -ne 0) {
    Write-Output "Build failed."
    exit 1
}
