#requires -Version 5.1
<#
.SYNOPSIS
    Convert PNG to multi-size ICO，自動裁切透明邊框。

.DESCRIPTION
    1. 偵測非透明像素的 bounding box
    2. 加 5% 呼吸邊距後裁切
    3. 為每個 ICO 尺寸（16/32/48/64/128/256）重新渲染
    4. 寫成多尺寸 ICO

.EXAMPLE
    .\build\convert-icon.ps1
#>
param(
    [string]$Source = (Join-Path (Split-Path -Parent $PSScriptRoot) 'pm\icon.png'),
    [string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Tender.Desktop\AppIcon.ico'),
    # 裁切外圈呼吸距離（content 之外保留多少 % 透明邊框）
    [double]$BreathingPercent = 0.0,
    # 在 ICO 畫布內額外放大（>1 會讓內容溢出邊緣，邊緣會被裁掉）
    [double]$ZoomFactor = 1.4
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) {
    throw "Source PNG not found: $Source"
}

# Step 1: 偵測 bounding box（含 alpha > 10 的像素）
$bmp = New-Object System.Drawing.Bitmap $Source
$w = $bmp.Width; $h = $bmp.Height

# 用 LockBits 加速讀取
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$buffer = New-Object byte[] ($stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
$bmp.UnlockBits($data)

$minX = $w; $minY = $h; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        # BGRA 排序：alpha 在 index+3
        $alpha = $buffer[$y * $stride + $x * 4 + 3]
        if ($alpha -gt 10) {
            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

$contentW = $maxX - $minX + 1
$contentH = $maxY - $minY + 1
Write-Host ("Content bounds: ({0},{1}) -> ({2},{3}) size {4}x{5}" -f $minX, $minY, $maxX, $maxY, $contentW, $contentH)

# Step 2: 計算正方形裁切範圍（取 content 的較長邊 + breathing margin）
$contentSize = [Math]::Max($contentW, $contentH)
$margin = [int]($contentSize * $BreathingPercent / 100)
$cropSize = $contentSize + 2 * $margin

# 中心點
$centerX = ($minX + $maxX) / 2
$centerY = ($minY + $maxY) / 2
$cropX = [int]($centerX - $cropSize / 2)
$cropY = [int]($centerY - $cropSize / 2)

# clamp
if ($cropX -lt 0) { $cropX = 0 }
if ($cropY -lt 0) { $cropY = 0 }
if ($cropX + $cropSize -gt $w) { $cropSize = $w - $cropX }
if ($cropY + $cropSize -gt $h) { $cropSize = $h - $cropY }

Write-Host ("Crop: ({0},{1}) {2}x{2} (breathing {3}%)" -f $cropX, $cropY, $cropSize, $BreathingPercent)

# Step 3: 從原圖 crop 出正方形
$cropped = New-Object System.Drawing.Bitmap $cropSize, $cropSize
$gCrop = [System.Drawing.Graphics]::FromImage($cropped)
$gCrop.Clear([System.Drawing.Color]::Transparent)
$srcRect = New-Object System.Drawing.Rectangle $cropX, $cropY, $cropSize, $cropSize
$dstRect = New-Object System.Drawing.Rectangle 0, 0, $cropSize, $cropSize
$gCrop.DrawImage($bmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$gCrop.Dispose()
$bmp.Dispose()

# Step 4: 為每個 ICO 尺寸生成 PNG bytes
# 套用 ZoomFactor — 內容繪製到 size*zoom 大小，置中後超出 ICO 邊緣會被自動裁切
$sizes = @(256, 128, 64, 48, 32, 16)
$pngData = @{}
foreach ($size in $sizes) {
    $resized = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    $drawSize = [int]($size * $ZoomFactor)
    $drawOffset = [int](($size - $drawSize) / 2)  # 負值 → 內容溢出邊緣
    $g.DrawImage($cropped, $drawOffset, $drawOffset, $drawSize, $drawSize)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$size] = $ms.ToArray()
    $ms.Dispose()
    $resized.Dispose()
}
$cropped.Dispose()

# Step 5: 組 ICO 檔
$ico = New-Object System.IO.MemoryStream
$wr = New-Object System.IO.BinaryWriter $ico

# ICONDIR
$wr.Write([uint16]0)
$wr.Write([uint16]1)
$wr.Write([uint16]$sizes.Count)

$dataOffset = 6 + ($sizes.Count * 16)
$offset = $dataOffset
foreach ($size in $sizes) {
    $bytes = $pngData[$size]
    $wr.Write([byte]($size % 256))
    $wr.Write([byte]($size % 256))
    $wr.Write([byte]0)
    $wr.Write([byte]0)
    $wr.Write([uint16]1)
    $wr.Write([uint16]32)
    $wr.Write([uint32]$bytes.Length)
    $wr.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($size in $sizes) { $wr.Write($pngData[$size]) }
$wr.Flush()

$dirOut = Split-Path -Parent $Destination
if (-not (Test-Path $dirOut)) { New-Item -ItemType Directory -Path $dirOut | Out-Null }
[System.IO.File]::WriteAllBytes($Destination, $ico.ToArray())

$wr.Dispose()
$ico.Dispose()

$info = Get-Item $Destination
Write-Host ("ICO created: {0} ({1:N0} bytes, {2} sizes)" -f $info.FullName, $info.Length, $sizes.Count) -ForegroundColor Green
