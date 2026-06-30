#Requires -Version 5.1
# build_release.ps1
# 1. Converts src/Scriptr.Gui/Assets/scriptr_icon.png -> scriptr_icon.ico (16/32/48 px)
# 2. Publishes Scriptr.Stub  (win-x64, self-contained, single-file)
# 3. Publishes Scriptr.Gui   (win-x64, self-contained, single-file)
# 4. Copies both EXEs into dist\
#
# Place scriptr_icon.png in src\Scriptr.Gui\Assets\ before running.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$guiSrc    = "src\Scriptr.Gui"
$stubSrc   = "src\Scriptr.Stub"
$assetsDir = "$guiSrc\Assets"
$distDir   = "dist"
$pngPath   = "$assetsDir\scriptr_icon.png"
$icoPath   = "$assetsDir\scriptr_icon.ico"

# --- Clean dist ---------------------------------------------------------------

if (Test-Path $distDir) { Remove-Item -Recurse -Force $distDir }
New-Item -ItemType Directory -Force $distDir | Out-Null

# --- PNG -> ICO (16, 32, 48 px, 32bpp) ---------------------------------------

if (Test-Path $pngPath) {
    Write-Host "Converting icon: $pngPath -> $icoPath" -ForegroundColor Cyan
    Add-Type -AssemblyName System.Drawing

    $sizes  = @(16, 32, 48)
    $frames = [System.Collections.Generic.List[byte[]]]::new()
    $src    = [System.Drawing.Image]::FromFile((Resolve-Path $pngPath).Path)

    foreach ($sz in $sizes) {
        $bmp  = New-Object System.Drawing.Bitmap($src, $sz, $sz)
        $bits = $bmp.LockBits(
            [System.Drawing.Rectangle]::new(0, 0, $sz, $sz),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $rowBytes = $sz * 4
        $raw      = New-Object byte[] ($rowBytes * $sz)
        [System.Runtime.InteropServices.Marshal]::Copy($bits.Scan0, $raw, 0, $raw.Length)
        $bmp.UnlockBits($bits)
        $bmp.Dispose()

        # AND mask: DWORD-aligned 1bpp rows, all zeros (alpha channel drives transparency)
        $andStride = (([int][math]::Ceiling($sz / 8.0)) + 3) -band (-4)
        $andRow    = New-Object byte[] $andStride

        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter($ms)

        # BITMAPINFOHEADER (height doubled: upper half = AND mask, lower = XOR pixels)
        $bw.Write([int32]40);          $bw.Write([int32]$sz)
        $bw.Write([int32]($sz * 2));   $bw.Write([int16]1)
        $bw.Write([int16]32);          $bw.Write([int32]0)
        $bw.Write([int32]($rowBytes * $sz))
        $bw.Write([int32]0); $bw.Write([int32]0)
        $bw.Write([int32]0); $bw.Write([int32]0)

        # XOR bitmap (DIBs are stored bottom-up)
        for ($r = $sz - 1; $r -ge 0; $r--) { $bw.Write($raw, $r * $rowBytes, $rowBytes) }

        # AND mask (all zeros = fully visible, alpha channel handles transparency)
        for ($r = 0; $r -lt $sz; $r++) { $bw.Write($andRow, 0, $andStride) }

        $bw.Flush()
        $frames.Add($ms.ToArray())
        $ms.Dispose()
    }
    $src.Dispose()

    # ICO layout: ICONDIR + N*ICONDIRENTRY + N*DIB blob
    $count    = $sizes.Count
    $dirBytes = 6 + $count * 16

    $fs = [System.IO.File]::Create($icoPath)
    $fs.Write([byte[]](0x00, 0x00, 0x01, 0x00, [byte]$count, 0x00), 0, 6)

    $offset = $dirBytes
    for ($i = 0; $i -lt $count; $i++) {
        $sz    = [byte]$sizes[$i]
        $len   = $frames[$i].Length
        $entry = [byte[]](
            $sz, $sz, 0x00, 0x00,
            0x01, 0x00,
            0x20, 0x00,
            [byte]($len -band 0xFF),    [byte](($len -shr  8) -band 0xFF),
            [byte](($len -shr 16) -band 0xFF), [byte](($len -shr 24) -band 0xFF),
            [byte]($offset -band 0xFF), [byte](($offset -shr  8) -band 0xFF),
            [byte](($offset -shr 16) -band 0xFF), [byte](($offset -shr 24) -band 0xFF)
        )
        $fs.Write($entry, 0, $entry.Length)
        $offset += $len
    }

    foreach ($frame in $frames) { $fs.Write($frame, 0, $frame.Length) }
    $fs.Close()

    $icoKB = [math]::Round((Get-Item $icoPath).Length / 1KB, 1)
    Write-Host "  scriptr_icon.ico written ($($icoKB) KB, sizes: 16 32 48)" -ForegroundColor Green
} else {
    Write-Warning "scriptr_icon.png not found at '$pngPath' -- skipping ICO generation."
    Write-Warning "The published Scriptr.exe will use the default Windows application icon."
}

# --- Publish Scriptr.Stub -----------------------------------------------------

Write-Host ""
Write-Host "Publishing Scriptr.Stub..." -ForegroundColor Cyan
$stubTmp = "$distDir\_stub"
dotnet publish $stubSrc -c Release -o $stubTmp --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for Scriptr.Stub" }

Copy-Item "$stubTmp\Scriptr.Stub.exe" "$distDir\Scriptr.Stub.exe"
Remove-Item -Recurse -Force $stubTmp
$stubMB = [math]::Round((Get-Item "$distDir\Scriptr.Stub.exe").Length / 1MB, 1)
Write-Host "  Scriptr.Stub.exe  -> dist\  ($($stubMB) MB)" -ForegroundColor Green

# --- Publish Scriptr.Gui ------------------------------------------------------

Write-Host ""
Write-Host "Publishing Scriptr.Gui..." -ForegroundColor Cyan
$guiTmp = "$distDir\_gui"
dotnet publish $guiSrc -c Release -o $guiTmp --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for Scriptr.Gui" }

Copy-Item "$guiTmp\Scriptr.exe" "$distDir\Scriptr.exe"
Remove-Item -Recurse -Force $guiTmp
$guiMB = [math]::Round((Get-Item "$distDir\Scriptr.exe").Length / 1MB, 1)
Write-Host "  Scriptr.exe        -> dist\  ($($guiMB) MB)" -ForegroundColor Green

# --- Summary ------------------------------------------------------------------

Write-Host ""
Write-Host "dist\ contents:" -ForegroundColor Yellow
Get-ChildItem $distDir | Sort-Object Name | ForEach-Object {
    $mb = "{0:N1} MB" -f ($_.Length / 1MB)
    Write-Host ("  {0,-28} {1,8}" -f $_.Name, $mb)
}
Write-Host ""
Write-Host "Release build complete. Both executables are standalone -- no .NET runtime required." -ForegroundColor Green
