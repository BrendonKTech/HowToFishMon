# builds the mod and makes the zips to upload, they end up in dist/
#   powershell -ExecutionPolicy Bypass -File package.ps1
# version comes from Version in src/Plugin.cs, so bump it there first

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = [regex]::Match((Get-Content src/Plugin.cs -Raw), 'Version = "([^"]+)"').Groups[1].Value
if (-not $version) { throw "couldn't find Version in src/Plugin.cs" }
Write-Host "Packaging FishMon $version"

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$dll = "bin/Release/HowToFishMon.dll"

New-Item -ItemType Directory -Force dist | Out-Null
New-Item -ItemType Directory -Force obj/package | Out-Null
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$utf8 = New-Object System.Text.UTF8Encoding $false   # thunderstore rejects files with a BOM

# zip with forward slashes (Compress-Archive on PS 5.1 uses backslashes, which breaks mod managers)
function New-Zip($path, $files) {
    if (Test-Path $path) { Remove-Item $path }
    $zip = [System.IO.Compression.ZipFile]::Open((Join-Path $PWD $path), 'Create')
    try {
        foreach ($entry in $files.Keys) {
            $src = (Resolve-Path $files[$entry]).Path
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $src, $entry) | Out-Null
        }
    }
    finally { $zip.Dispose() }
    Write-Host "  made $path"
}

# --- thunderstore / r2modman ---
$manifest = [ordered]@{
    name           = "FishMon"
    version_number = $version
    website_url    = "https://github.com/BrendonKTech/HowToFishMon"
    description    = "Catch fish, level them up, evolve them and have them fight bosses with you. Party, Box, Fishdex, moves, types and multiplayer."
    dependencies   = @("BepInEx-BepInExPack-5.4.2305")
}
[System.IO.File]::WriteAllText((Join-Path $PWD "obj/package/manifest.json"), ($manifest | ConvertTo-Json), $utf8)

New-Zip "dist/FishMon-$version-thunderstore.zip" ([ordered]@{
    "manifest.json"              = "obj/package/manifest.json"
    "icon.png"                   = "thunderstore/icon.png"
    "README.md"                  = "README.md"
    "CHANGELOG.md"               = "CHANGELOG.md"
    "plugins/HowToFishMon.dll"   = $dll
})

# --- manual install (github release / nexus): unzip into the game folder ---
$install = @"
FishMon $version for How to Fish

1. Install BepInEx 5 if you don't have it yet:
   https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/
   (unzip it into your How to Fish folder and run the game once)
2. Unzip this file into your How to Fish folder too, so you end up with
   How to Fish/BepInEx/plugins/HowToFishMon/HowToFishMon.dll
3. Start the game. Catch a fish and press F6!

In multiplayer the host needs the mod for players to see each other's FishMon.
"@
[System.IO.File]::WriteAllText((Join-Path $PWD "obj/package/INSTALL.txt"), $install.Replace("`n", "`r`n"), $utf8)

New-Zip "dist/FishMon-$version.zip" ([ordered]@{
    "BepInEx/plugins/HowToFishMon/HowToFishMon.dll" = $dll
    "INSTALL.txt"                                   = "obj/package/INSTALL.txt"
})

Write-Host "Done! Upload the zips in dist/"
