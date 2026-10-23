param(
    [string]$Abis = "arm64-v8a",
    [bool]$CompressNativeLibs = $false
)

$env:ANDROID_HOME = 'F:\AndroidSDK'
$env:ANDROID_SDK_ROOT = 'F:\AndroidSDK'
$env:JAVA_HOME = 'C:\Program Files\Java\jdk-17'

$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$Gradlew = Join-Path $ProjectRoot "android\build\gradlew.bat"
$BuildDir = Join-Path $ProjectRoot "android\build"

Write-Host "Cleaning unused/stale assets from android build assets..."
$cleanScript = Join-Path $ProjectRoot "scratch\clean_android_assets.py"
if (Test-Path $cleanScript) {
    & E:\python13\python.exe $cleanScript
}

if ($Abis -match "arm64-v8a") {
    Write-Host "Publishing C# assembly for android-arm64..."
    & dotnet publish "$ProjectRoot\GodotRTS.csproj" -c ExportDebug -r android-arm64 --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish arm64 failed" }

    $srcDirArm64 = Join-Path $ProjectRoot ".godot\mono\temp\bin\ExportDebug\android-arm64\publish"
    $destDirArm64 = Join-Path $ProjectRoot "android\build\src\main\assets\.godot\mono\publish\arm64"
    if (-not (Test-Path $destDirArm64)) {
        New-Item -ItemType Directory -Force -Path $destDirArm64 | Out-Null
    }
    Copy-Item "$srcDirArm64\*" -Destination $destDirArm64 -Force -Recurse
}

if ($Abis -match "x86_64") {
    Write-Host "Publishing C# assembly for android-x64..."
    & dotnet publish "$ProjectRoot\GodotRTS.csproj" -c ExportDebug -r android-x64 --no-self-contained
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish x64 failed" }

    $srcDllX64 = Join-Path $ProjectRoot ".godot\mono\temp\bin\ExportDebug\android-x64\publish\GodotRTS.dll"
    $destDllX64 = Join-Path $ProjectRoot "android\build\src\main\assets\.godot\mono\publish\x86_64\GodotRTS.dll"
    Copy-Item $srcDllX64 -Destination $destDllX64 -Force
}

Write-Host "Running Gradle assembleMonoDebug (ABIs: $Abis, CompressNativeLibs: $CompressNativeLibs)..."
& $Gradlew assembleMonoDebug -p $BuildDir `
    "-Paddons_directory=$ProjectRoot/addons" `
    "-Pexport_package_name=com.game.godotrts" `
    "-Pexport_version_code=1" `
    "-Pexport_version_name=1.0.0" `
    "-Pexport_version_min_sdk=24" `
    "-Pexport_version_target_sdk=36" `
    "-Pexport_enabled_abis=$Abis" `
    "-Pperform_zipalign=true" `
    "-Pperform_signing=true" `
    "-Pcompress_native_libraries=$CompressNativeLibs"

if ($LASTEXITCODE -ne 0) { throw "gradle assembleMonoDebug failed" }

$apkSource = Join-Path $ProjectRoot "android\build\build\outputs\apk\mono\debug\android_monoDebug.apk"
$apkDest = Join-Path $ProjectRoot "Build\Android\GodotRTS-debug.apk"
Copy-Item $apkSource -Destination $apkDest -Force

$apkSizeMB = [Math]::Round(((Get-Item $apkDest).Length / 1MB), 2)
Write-Host "Quick APK build successful! Output: $apkDest ($apkSizeMB MB)"
