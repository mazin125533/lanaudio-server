# src/IconArt.cs の描画から src/app.ico（exe のアイコン）を生成する
# 使い方: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
$src = Join-Path $PSScriptRoot "..\src"
$type = Add-Type -Path (Join-Path $src "IconArt.cs") -ReferencedAssemblies System.Drawing -PassThru |
    Where-Object Name -eq "IconArt"

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) {
    $bmp = $type::Render($s, $type::Accent, $false)
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO 形式: ヘッダ(6) + エントリ(16×枚数) + PNG データ
$out = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $src "app.ico"), $out.ToArray())
Write-Output "Generated: src\app.ico"
