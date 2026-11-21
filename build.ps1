$ErrorActionPreference = "Stop"

$valManaged = "E:\SteamLibrary\steamapps\common\Valheim\valheim_Data\Managed"
$bepRoot = "C:\Users\ROMAN\AppData\Roaming\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default\BepInEx"

# Generate 256x256 Thunderstore icon.png for "Valheim - Path of the Fallen" if missing
if (-not (Test-Path "icon.png")) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $rect = New-Object System.Drawing.Rectangle(0, 0, 256, 256)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 22, 16, 30),
        [System.Drawing.Color]::FromArgb(255, 8, 10, 14),
        45.0)
    $g.FillRectangle($bgBrush, $rect)

    # Outer forged gold & inner crimson-emerald runic borders
    $goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 218, 172, 78), 7)
    $g.DrawRectangle($goldPen, 8, 8, 240, 240)
    $innerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 74, 226, 142), 2.5)
    $g.DrawRectangle($innerPen, 18, 18, 220, 220)

    # Glowing Othala / Fallen Altar rune emblem in center-top
    $glowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 220, 64, 88))
    $g.FillEllipse($glowBrush, 64, 30, 128, 128)

    $runePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 246, 214, 136), 8)
    $runePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $runePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    # Othala rune (heritage / path of the fallen): diamond top + crossed legs
    $g.DrawLine($runePen, 128, 38, 170, 82)
    $g.DrawLine($runePen, 170, 82, 100, 152)
    $g.DrawLine($runePen, 128, 38, 86, 82)
    $g.DrawLine($runePen, 86, 82, 156, 152)

    $gemBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 72, 242, 152))
    $g.FillEllipse($gemBrush, 118, 72, 20, 20)

    # Title typography
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    $fontTop = New-Object System.Drawing.Font("Georgia", 15, [System.Drawing.FontStyle]::Bold)
    $fontBot = New-Object System.Drawing.Font("Georgia", 20, [System.Drawing.FontStyle]::Bold)
    $goldText = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 242, 216, 156))
    $crimsonText = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 238, 92, 104))

    $g.DrawString("PATH OF THE", $fontTop, $goldText, (New-Object System.Drawing.RectangleF(12, 166, 232, 28)), $sf)
    $g.DrawString("FALLEN", $fontBot, $crimsonText, (New-Object System.Drawing.RectangleF(12, 192, 232, 36)), $sf)

    $bmp.Save("icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
}

$manifest = Get-Content -Raw "manifest.json" | ConvertFrom-Json
$modVer = $manifest.version_number

$refs = @(
    "$valManaged\mscorlib.dll",
    "$valManaged\netstandard.dll",
    "$valManaged\UnityEngine.dll",
    "$valManaged\UnityEngine.CoreModule.dll",
    "$valManaged\UnityEngine.AnimationModule.dll",
    "$valManaged\UnityEngine.ClothModule.dll",
    "$valManaged\UnityEngine.PhysicsModule.dll",
    "$valManaged\UnityEngine.ParticleSystemModule.dll",
    "$valManaged\UnityEngine.ImageConversionModule.dll",
    "$valManaged\UnityEngine.AssetBundleModule.dll",
    "$valManaged\UnityEngine.InputLegacyModule.dll",
    "$valManaged\UnityEngine.TextRenderingModule.dll",
    "$valManaged\UnityEngine.UI.dll",
    "$valManaged\UnityEngine.UIModule.dll",
    "$valManaged\Unity.TextMeshPro.dll",
    "$valManaged\assembly_valheim.dll",
    "$valManaged\assembly_utils.dll",
    "$valManaged\assembly_guiutils.dll",
    "$valManaged\Splatform.dll",
    "$valManaged\SoftReferenceableAssets.dll",
    "$bepRoot\core\BepInEx.dll",
    "$bepRoot\core\0Harmony.dll",
    "$bepRoot\plugins\ValheimModding-Jotunn\Jotunn.dll"
)

$rArgs = $refs | ForEach-Object { "/r:$_" }
$resFiles = Get-ChildItem -Path "src\Resources" -File
$resArgs = $resFiles | ForEach-Object { "/res:src\Resources\$($_.Name),$($_.Name)" }
$srcFiles = Get-ChildItem -Path "src" -Recurse -Filter "*.cs" -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | ForEach-Object { $_.FullName }

& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nostdlib /target:library /optimize+ /codepage:65001 /utf8output /out:PathOfTheFallen.dll `
    $rArgs $resArgs $srcFiles

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: PathOfTheFallen.dll v$modVer ($((Get-Item PathOfTheFallen.dll).Length) bytes)"

    Get-ChildItem -Filter "PathOfTheFallen-*.zip" -File | Where-Object { $_.Name -ne "PathOfTheFallen-$modVer.zip" } | Remove-Item -Force

    $zipFiles = @("PathOfTheFallen.dll", "manifest.json", "icon.png", "README.md")
    if (Test-Path "CHANGELOG.md") { $zipFiles += "CHANGELOG.md" }
    Compress-Archive -Path $zipFiles -DestinationPath "PathOfTheFallen-$modVer.zip" -CompressionLevel Optimal -Force
    Copy-Item -Force "PathOfTheFallen-$modVer.zip" "PathOfTheFallen.zip"
    Write-Host "Created Thunderstore archives: PathOfTheFallen-$modVer.zip & PathOfTheFallen.zip ($((Get-Item PathOfTheFallen.zip).Length) bytes)"
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
