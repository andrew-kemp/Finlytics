Add-Type -AssemblyName System.Drawing

$sourcePath = "C:\Users\AndrewKemp\Kemponline\GitHub - General\FinanceHub\New-Logo.png"
$outDir     = "C:\Users\AndrewKemp\Kemponline\GitHub - General\FinanceHub\StaticWebApp\public\images"

$srcBytes = [System.IO.File]::ReadAllBytes($sourcePath)
$ms  = New-Object System.IO.MemoryStream(,$srcBytes)
$src = [System.Drawing.Image]::FromStream($ms)
Write-Host "Source loaded: $($src.Width) x $($src.Height) px"

$png = [System.Drawing.Imaging.ImageFormat]::Png

function New-Resized($img, $w, $h) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.SetResolution(96, 96)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    # Solid brand-colour background so the OS never shows a white halo/circle
    $g.Clear([System.Drawing.Color]::FromArgb(255, 21, 101, 192))   # #1565C0
    $g.DrawImage($img, 0, 0, $w, $h)
    $g.Dispose()
    return $bmp
}

function New-Maskable($img, $size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.SetResolution(96, 96)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    # Blue fill (#1565C0) matching the main icon background
    $g.Clear([System.Drawing.Color]::FromArgb(255, 21, 101, 192))
    $iconSize = [int]($size * 0.80)
    $offset   = [int](($size - $iconSize) / 2)
    $g.DrawImage($img, $offset, $offset, $iconSize, $iconSize)
    $g.Dispose()
    return $bmp
}

# any variants — plain resize, white bg is fine (iOS clips to rounded rect automatically)
@(
    @{ size=32;  name="favicon-32.png" },
    @{ size=180; name="apple-touch-icon.png" },
    @{ size=192; name="icon-192.png" },
    @{ size=512; name="icon-512.png" }
) | ForEach-Object {
    $b = New-Resized $src $_.size $_.size
    $outPath = [System.IO.Path]::Combine($outDir, $_.name)
    $b.Save($outPath, $png)
    $b.Dispose()
    Write-Host "  OK  $($_.name) ($($_.size)x$($_.size))"
}

# maskable variants — coloured background + 80% scale for Android safe-zone
@(
    @{ size=192; name="icon-192-maskable.png" },
    @{ size=512; name="icon-512-maskable.png" }
) | ForEach-Object {
    $b = New-Maskable $src $_.size
    $outPath = [System.IO.Path]::Combine($outDir, $_.name)
    $b.Save($outPath, $png)
    $b.Dispose()
    Write-Host "  OK  $($_.name) ($($_.size)x$($_.size) maskable)"
}

$src.Dispose()
$ms.Dispose()

Write-Host ""
Write-Host "Done. Files in $outDir :"
Get-ChildItem $outDir -Filter "*.png" | Sort-Object Name | Format-Table Name, Length -AutoSize
