$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$shape = 'M128 16 C111 49 48 106 48 159 C48 204 84 240 128 240 C172 240 208 204 208 159 C208 106 145 49 128 16 Z M128 92 C110 92 99 105 99 121 C99 132 105 141 114 146 L106 181 L150 181 L142 146 C151 141 157 132 157 121 C157 105 146 92 128 92 Z'
foreach ($tone in @('Black', 'White')) {
    $color = if ($tone -eq 'Black') { '#000000' } else { '#FFFFFF' }
    $svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><path fill="' + $color + '" fill-rule="evenodd" d="' + $shape + '"/></svg>'
    Set-Content -LiteralPath (Join-Path $PSScriptRoot "Assets/Mizu-$tone.svg") -Value $svg -Encoding UTF8
    $visual = New-Object Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawGeometry([Windows.Media.BrushConverter]::new().ConvertFromString($color), $null, [Windows.Media.Geometry]::Parse('F0 ' + $shape))
    $context.Close()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap(256,256,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = [IO.File]::Create((Join-Path $PSScriptRoot "Assets/Mizu-$tone.png"))
    try { $encoder.Save($file) } finally { $file.Dispose() }
    & (Join-Path $PSScriptRoot 'Build-Icon.ps1') -SourceName "Mizu-$tone.png" -OutputName "Mizu-$tone.ico"
}
