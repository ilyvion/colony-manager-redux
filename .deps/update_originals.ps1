$RimWorldSteamWorkshopFolderPath = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100"

foreach ($original in Get-ChildItem -Path $PSScriptRoot\originals -Directory ) {
    $ModName = $original.Name
    
    # Remove old backup, if any
    Remove-Item -Force -Recurse $PSScriptRoot\backups\$ModName -ErrorAction SilentlyContinue
    
    # Make new backup
    Move-Item $PSScriptRoot\originals\$ModName $PSScriptRoot\backups\$ModName
    
    # Copy new version over
    Copy-Item -Recurse $RimWorldSteamWorkshopFolderPath\$ModName $PSScriptRoot\originals\$ModName
}
