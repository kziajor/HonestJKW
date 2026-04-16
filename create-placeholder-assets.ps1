Add-Type -AssemblyName System.Drawing

function New-SolidColorIco {
    param([string]$path, [System.Drawing.Color]$color)

    # Draw 32x32 colored bitmap
    $bmp = New-Object System.Drawing.Bitmap(32, 32)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($color)

    # Draw a simple shape to make each icon visually distinct
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 0, 0))
    $g.FillEllipse($brush, 8, 8, 16, 16)
    $brush.Dispose()
    $g.Dispose()

    # Convert bitmap to proper Windows HICON, then save as .ico
    $hIcon = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs = [System.IO.File]::OpenWrite($path)
    $icon.Save($fs)
    $fs.Close()
    $icon.Dispose()
    [System.Runtime.InteropServices.Marshal]::DestroyIcon($hIcon) | Out-Null
    $bmp.Dispose()
    Write-Host "Created: $path"
}

function New-PlaceholderGif {
    param([string]$path, [System.Drawing.Color]$color)

    $bmp = New-Object System.Drawing.Bitmap(200, 200)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($color)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Gif)
    $bmp.Dispose()
    Write-Host "Created: $path"
}

function New-PlaceholderWav {
    param([string]$path)
    $sampleRate = 44100
    $numSamples = [int]($sampleRate * 0.1)
    $dataSize = $numSamples * 2
    $fs = [System.IO.File]::OpenWrite($path)
    $w = New-Object System.IO.BinaryWriter($fs)
    $w.Write([byte[]][System.Text.Encoding]::ASCII.GetBytes("RIFF"))
    $w.Write([uint32](36 + $dataSize))
    $w.Write([byte[]][System.Text.Encoding]::ASCII.GetBytes("WAVE"))
    $w.Write([byte[]][System.Text.Encoding]::ASCII.GetBytes("fmt "))
    $w.Write([uint32]16)
    $w.Write([uint16]1)
    $w.Write([uint16]1)
    $w.Write([uint32]$sampleRate)
    $w.Write([uint32]($sampleRate * 2))
    $w.Write([uint16]2)
    $w.Write([uint16]16)
    $w.Write([byte[]][System.Text.Encoding]::ASCII.GetBytes("data"))
    $w.Write([uint32]$dataSize)
    $silence = New-Object byte[]($dataSize)
    $w.Write($silence)
    $w.Close()
    Write-Host "Created: $path"
}

# Icons (proper Windows ICO via System.Drawing.Icon)
New-SolidColorIco "Assets\Icons\idle.ico"    ([System.Drawing.Color]::FromArgb(100, 100, 100))
New-SolidColorIco "Assets\Icons\working.ico" ([System.Drawing.Color]::FromArgb(251, 203, 24))
New-SolidColorIco "Assets\Icons\error.ico"   ([System.Drawing.Color]::FromArgb(220, 50, 50))
New-SolidColorIco "Assets\Icons\waiting.ico" ([System.Drawing.Color]::FromArgb(50, 150, 220))

# Animations (placeholder single-frame GIFs — real ones already in Assets\Animations)
# Sounds
New-PlaceholderWav "Assets\Sounds\working.wav"
New-PlaceholderWav "Assets\Sounds\error.wav"
New-PlaceholderWav "Assets\Sounds\success.wav"
New-PlaceholderWav "Assets\Sounds\notify.wav"

Write-Host "Done."
