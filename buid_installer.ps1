$currentPath = Get-Location
$SolutionPath = Join-Path -Path $currentPath -ChildPath "wsl-usb-manager.sln"
$AssemblyInfoPath = Join-Path -Path $currentPath -ChildPath "wsl-usb-manager/AssemblyInfo.cs"
$InstallerPrjPath = Join-Path -Path $currentPath -ChildPath "Installer/Installer.vdproj"
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
git fetch --prune
dotnet-gitversion /updateassemblyinfo $AssemblyInfoPath /ensureassemblyinfo

# Get the version number from AssemblyInfo.cs
$AssembleInfoContent = Get-Content -Path $AssemblyInfoPath
$versionPattern = '\[assembly: AssemblyVersion\("(\d+\.\d+\.\d+\.\d+)"\)\]'
$versionMatch = [regex]::Match($AssembleInfoContent, $versionPattern)

if ($versionMatch.Success) {
    $fullVersion = $versionMatch.Groups[1].Value
    # remove the last part of the version number
    $shortVersion = ($fullVersion -split '\.')[0..2] -join '.'

    # Replace the version number in the Installer project
    $InstallerFileContent = Get-Content -Path $InstallerPrjPath
    Write-Output "Update version '$shortVersion' to Installer project"
    $updatedContent = $InstallerFileContent -replace '("ProductVersion" = "8:)(\d+\.\d+\.\d+)(")', "`"ProductVersion`" = `"8:$shortVersion`""
    #Write-Output "Update ProductCode '$NewProductCode' to Installer project"
    #$updatedContent = $updatedContent -replace '("ProductCode" = "8:)([^"]+)(")', "`"ProductCode`" = `"8:{$NewProductCode}`""
    # Write the updated content back to the file
    Set-Content -Path $InstallerPrjPath -Value $updatedContent -ErrorAction Stop
} else {
    Write-Output "Failed to get the version number from AssemblyInfo.cs"
    exit 1
}

& $devenvPath $SolutionPath /Rebuild Release /Project $InstallerPrjPath
