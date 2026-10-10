$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $taskDimensions = & magick identify -format '%wx%h' assets/Timzoner-source.png
    if ($taskDimensions -ne '1194x1151') { throw 'The crop coordinates are for the approved 1194 x 1151 source.' }
    & magick assets/Timzoner-source.png -crop 1128x1128+61+2 +repage '(' -size 1128x1128 xc:none -fill white -draw 'circle 563.5,563.5 563.5,0.5' ')' -alpha on -compose DstIn -composite assets/Timzoner.png
    if ($LASTEXITCODE -ne 0) { throw 'Circular crop failed.' }
    & magick assets/Timzoner.png -define icon:auto-resize=256,128,64,48,32,24,20,16 assets/Timzoner.ico
    if ($LASTEXITCODE -ne 0) { throw 'ICO conversion failed.' }
} finally { Pop-Location }
