# Copyright (c) Files Community
# Licensed under the MIT License.

param(
    [string]$Branch = "", # This has to correspond with one of the AppEnvironment enum values
    [string]$SolutionPath = "Files.sln"
    [string]$StartupProjectPath = ""
    [string]$Platform = "x64"
    [string]$Configuration = "Debug"
    [string]$AppxBundlePlatforms = "x64|arm64"
    [string]$AppxPackageDir = ""
    [string]$AppInstallerUrl = "" # Sideload only
    [string]$AppxPackageCertKeyFile = "" # Debug only
)

# Restore the solution
msbuild $SolutionPath /t:Restore /p:Platform=$Platform /p:Configuration=$Configuration /p:PublishReadyToRun=true

if ($Branch -eq "Debug")
{
    if ($Platform -eq "x64")
    {
        msbuild $StartupProjectPath `
            /t:Build `
            /clp:ErrorsOnly `
            /p:Platform=$Platform `
            /p:Configuration=$Configuration `
            /p:AppxBundlePlatforms=$Platform `
            /p:AppxBundle=Always `
            /p:UapAppxPackageBuildMode=SideloadOnly `
            /p:AppxPackageDir=$AppxPackageDir `
            /p:AppxPackageSigningEnabled=true `
            /p:PackageCertificateKeyFile=$AppxPackageCertKeyFile `
            /p:PackageCertificatePassword="" `
            /p:PackageCertificateThumbprint=""
    }
    else
    {
        msbuild $StartupProjectPath `
            /t:Build `
            /clp:ErrorsOnly `
            /p:Platform=$Platform `
            /p:Configuration=$Configuration `
            /p:AppxBundle=Never
    }
}
elseif ($Branch -contains "Sideload")
{
    msbuild $StartupProjectPath `
        /t:Build `
        /p:Platform=$Platform `
        /p:Configuration=$Configuration `
        /p:AppxBundlePlatforms=$AppxBundlePlatforms `
        /p:AppxPackageDir=$AppxPackageDir `
        /p:AppxBundle=Always `
        /p:UapAppxPackageBuildMode=Sideload `
        /p:GenerateAppInstallerFile=True `
        /p:AppInstallerUri=$AppInstallerUrl

    $newSchema = 'http://schemas.microsoft.com/appx/appinstaller/2018'
    $localFilePath = '$AppxPackageDir/Files.Package.appinstaller'
    $fileContent = Get-Content $localFilePath
    $fileContent = $fileContent.Replace('http://schemas.microsoft.com/appx/appinstaller/2017/2', $newSchema)
    $fileContent | Set-Content $localFilePath
}
elseif ($Branch -contains "Store")
{
    msbuild $StartupProjectPath `
        /t:Build `
        /p:Platform=$Platform `
        /p:Configuration=$Configuration `
        /p:AppxBundlePlatforms=$AppxBundlePlatforms `
        /p:AppxPackageDir=$AppxPackageDir `
        /p:AppxBundle=Always `
        /p:UapAppxPackageBuildMode=StoreUpload
}
