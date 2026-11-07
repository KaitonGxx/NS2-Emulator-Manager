# Builds src\app.ico (16-256 px) and src\app.png (sidebar logo) from Eglantine main's flower (dist\Eglantine.png)
# with a wrench badge in the bottom-right corner. Run: powershell -NoProfile -File make_icon.ps1 [path\to\Eglantine.png]
param([Parameter(Mandatory=$true)][string]$Flower)
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = [System.Drawing.Image]::FromFile($Flower)

function Render([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $s, $s)
    # badge: bigger share of the icon at small sizes so the wrench stays readable
    $f = if ($s -le 16) { 0.62 } elseif ($s -le 32) { 0.56 } else { 0.50 }
    $d = [single]($s * $f); $x = [single]($s - $d - $s * 0.01); $y = $x
    $ring = [Math]::Max(1.0, $s / 40.0)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), $x - $ring, $y - $ring, $d + 2 * $ring, $d + 2 * $ring)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x2b, 0x2d, 0x33))), $x, $y, $d, $d)
    # wrench: Segoe Fluent Icons "Repair" (U+E90F), MDL2 fallback has the same code point
    $fam = "Segoe Fluent Icons"; try { $t = New-Object System.Drawing.Font($fam, 10); if ($t.Name -ne $fam) { $fam = "Segoe MDL2 Assets" } } catch { $fam = "Segoe MDL2 Assets" }
    $font = New-Object System.Drawing.Font($fam, [single]($d * 0.58), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString([string][char]0xE90F, $font, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)),
                  (New-Object System.Drawing.RectangleF($x, ($y + $d * 0.02), $d, $d)), $sf)
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$pngs = @()
foreach ($s in $sizes) {
    $b = Render $s
    if ($s -ge 256) {
        $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $pngs += , $ms.ToArray()
        $b.Save((Join-Path $here "app.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    else {
        # classic DIB entry: BITMAPINFOHEADER (height doubled), BGRA rows bottom-up, empty AND mask (alpha does the work)
        $ms = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter($ms)
        $bw.Write([UInt32]40); $bw.Write([Int32]$s); $bw.Write([Int32]($s * 2)); $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]0); $bw.Write([UInt32]0); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
        for ($yy = $s - 1; $yy -ge 0; $yy--) { for ($xx = 0; $xx -lt $s; $xx++) { $c = $b.GetPixel($xx, $yy); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4); $bw.Write((New-Object byte[] ($maskRow * $s)))
        $bw.Flush(); $pngs += , $ms.ToArray()
    }
    $b.Dispose()
}
# ICO: DIB entries up to 128 px, PNG for 256 (what Windows and .NET both read)
$out = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$off = 6 + 16 * $sizes.Count
for ($k = 0; $k -lt $sizes.Count; $k++) {
    $s = $sizes[$k]; $v = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$v); $w.Write([byte]$v); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$pngs[$k].Length); $w.Write([UInt32]$off)
    $off += $pngs[$k].Length
}
foreach ($p in $pngs) { $w.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $here "app.ico"), $out.ToArray())
"app.ico: $($sizes.Count) sizes, $($out.Length) bytes"
