$ErrorActionPreference = 'Stop'

$Configuration = 'Debug'

$VersionTargetPrefix = "D:\RimWorld"
$VersionTargetSuffix = "Mods\ColonyManagerRedux"
$Target = "$VersionTargetPrefix\1.5\$VersionTargetSuffix"

# build dlls
dotnet build --configuration $Configuration Source/ColonyManagerRedux/ColonyManagerRedux.csproj
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}

# remove mod folder
Remove-Item -Path $Target -Recurse -ErrorAction SilentlyContinue

# copy mod files
Copy-Item -Path 1.5 $Target\1.5 -Recurse

# copy interop mod files
# <NONE>

Copy-Item -Path Defs $Target\Defs -Recurse
Copy-Item -Path Languages $Target\Languages -Recurse
Copy-Item -Path Patches $Target\Patches -Recurse
Copy-Item -Path Textures $Target\Textures -Recurse

New-Item -Path $Target -ItemType Directory -Name About
Copy-Item -Path About\About.xml $Target\About
Copy-Item -Path About\Preview.png $Target\About
Copy-Item -Path About\ModIcon.png $Target\About
Copy-Item -Path About\PublishedFileId.txt $Target\About

Copy-Item -Path CHANGELOG.md $Target
Copy-Item -Path LICENSE $Target
#Copy-Item -Path LICENSE.Apache-2.0 $Target
#Copy-Item -Path LICENSE.MIT $Target
Copy-Item -Path README.md $Target
#Copy-Item -Path LoadFolders.xml $Target

# Trigger auto-hotswap
New-Item -Path $Target\1.5\Assemblies\ColonyManagerRedux.dll.hotswap -Type file
