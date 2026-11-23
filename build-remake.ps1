param([string]$Version="")
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$dist=Join-Path $root 'dist/SteamAchievementManager-Remake'
New-Item -ItemType Directory -Force "$dist/engine" | Out-Null
$properties=@('/restore','/p:Configuration=Release')
if($Version){
 if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
 $properties+=@("/p:Version=$Version","/p:AssemblyVersion=$Version.0","/p:FileVersion=$Version.0")
}
& msbuild "$root/desktop/Remake/Remake.csproj" @properties
if($LASTEXITCODE){throw 'Frontend build failed'}
Copy-Item "$root/desktop/Remake/bin/Release/net48/*.exe","$root/desktop/Remake/bin/Release/net48/*.config" $dist -Force
Push-Location $root
try {
 & cargo build --locked --release --no-default-features --features cli
 if($LASTEXITCODE){throw 'CLI engine build failed'}
 Copy-Item target/release/samrewritten.exe "$dist/engine/samrewritten-cli.exe" -Force
} finally {Pop-Location}
Copy-Item "$root/LICENSE","$root/README.md","$root/README.zh-CN.md" $dist -Force
Write-Output $dist
