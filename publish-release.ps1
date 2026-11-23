param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a numeric major.minor.patch version'}
$tag="v$Version"
$root=$PSScriptRoot
Push-Location $root
try {
    $commit=(& git rev-parse HEAD).Trim()
    if($LASTEXITCODE){throw 'Cannot resolve source commit'}
    & "$root/build-remake.ps1" -Version $Version
    & "$root/tests/run.ps1"
    if($LASTEXITCODE){throw 'Tests failed; release will not be published'}
    $assets=Join-Path $root '.release-assets'
    $folder=Join-Path $assets "SamReMake-$tag-windows-x64"
    New-Item -ItemType Directory -Force $folder | Out-Null
    Copy-Item "$root/dist/SteamAchievementManager-Remake/*" $folder -Recurse -Force
    & git archive --format=zip "--output=$folder/source.zip" $commit
    if($LASTEXITCODE){throw 'Source archive failed'}
    @"
SamReMake $tag
Source commit: $commit
Validation: release build and automated tests passed.
Automated tests do not write to Steam accounts.
"@ | Set-Content (Join-Path $folder 'BUILD-INFO.txt')
    $zip=Join-Path $assets "SamReMake-$tag-windows-x64.zip"
    Compress-Archive -Path $folder -DestinationPath $zip -Force
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256"
    $notes=Join-Path $assets 'release-notes.md'
    @"
Windows portable development preview. Extract the complete ZIP and start SteamAchievementManager-Remake.exe.

Source commit: $commit
Includes the matching source archive, English/Chinese documentation and SHA-256 checksum.
Build and automated tests passed. Live Steam account writes are not exercised by these tests.

Windows 便携预览版：完整解压 ZIP 后运行 SteamAchievementManager-Remake.exe。
包含对应源码、中英文说明和 SHA-256 校验文件。构建及自动测试通过；自动测试不修改 Steam 账号。
"@ | Set-Content $notes
    & gh release view $tag --json url 2>$null | Out-Null
    if($LASTEXITCODE -ne 0){
        & gh release create $tag --target $commit --title "SamReMake $tag" --notes-file $notes --prerelease
        if($LASTEXITCODE){throw 'Release creation failed'}
    }
    & gh release upload $tag $zip "$zip.sha256" --clobber
    if($LASTEXITCODE){throw 'Release upload failed'}
} finally {Pop-Location}
