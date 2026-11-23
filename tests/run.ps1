# Run after building the frontend. Fake engine is confined to a temporary test directory.
$ErrorActionPreference='Stop'
$testRoot=Join-Path $PSScriptRoot '../work-tests'
New-Item -ItemType Directory -Force "$testRoot/engine" | Out-Null
$compiler="$env:WINDIR/Microsoft.NET/Framework/v4.0.30319/csc.exe"
Copy-Item "$PSScriptRoot/../desktop/Remake/bin/Release/net48/SteamAchievementManager-Remake.exe*" $testRoot -Force
& $compiler /nologo /codepage:65001 /r:System.Web.Extensions.dll "/out:$testRoot/engine/samrewritten-cli.exe" "$PSScriptRoot/FakeEngine.cs"
if($LASTEXITCODE){throw 'Fake build failed'}
& $compiler /nologo /codepage:65001 "/r:$testRoot/SteamAchievementManager-Remake.exe" /r:System.Web.Extensions.dll "/out:$testRoot/FrontendTest.exe" "$PSScriptRoot/FrontendTest.cs"
if($LASTEXITCODE){throw 'Test build failed'}
Copy-Item "$testRoot/SteamAchievementManager-Remake.exe.config" "$testRoot/FrontendTest.exe.config" -Force
& "$testRoot/FrontendTest.exe"
if($LASTEXITCODE){throw 'Tests failed'}

& $compiler /nologo /codepage:65001 "/r:$testRoot/SteamAchievementManager-Remake.exe" /r:System.Web.Extensions.dll "/out:$testRoot/AdvancedTest.exe" "$PSScriptRoot/AdvancedTest.cs"
if($LASTEXITCODE){throw 'Advanced test build failed'}
Copy-Item "$testRoot/FrontendTest.exe.config" "$testRoot/AdvancedTest.exe.config" -Force
& "$testRoot/AdvancedTest.exe"
if($LASTEXITCODE){throw 'Advanced tests failed'}
$referenceRoot="${env:ProgramFiles(x86)}/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8"
& $compiler /nologo /codepage:65001 "/r:$testRoot/SteamAchievementManager-Remake.exe" "/r:$referenceRoot/PresentationFramework.dll" "/r:$referenceRoot/PresentationCore.dll" "/r:$referenceRoot/WindowsBase.dll" /r:System.Xaml.dll "/out:$testRoot/UiReadTest.exe" "$PSScriptRoot/UiReadTest.cs"
if($LASTEXITCODE){throw 'UI test build failed'}
Copy-Item "$testRoot/FrontendTest.exe.config" "$testRoot/UiReadTest.exe.config" -Force
& "$testRoot/UiReadTest.exe"
if($LASTEXITCODE){throw 'UI tests failed'}
