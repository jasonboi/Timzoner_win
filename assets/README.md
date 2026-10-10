# Application icon

The final artwork is the filled image supplied by the user as `背景.png`. `Timzoner-source.png` preserves that input. The user authorized circular trimming without regenerating the artwork.

`Timzoner.png` is a 1128 × 1128 circular crop with transparent corners. The clock hands, globe arc and filled background are retained from the supplied image. `Timzoner.ico` contains 16, 20, 24, 32, 48, 64, 128 and 256 pixel frames for the application, system tray and installer.

Rebuild using ImageMagick 7.

```powershell
./scripts/Build-Icon.ps1
```

The concept was originally generated with OpenAI ImageGen. The final filled source and precise crop supersede the generated candidates that had transparent interior defects.

`licenses/` contains the unmodified licenses from the .NETCore and Windows Desktop runtime packages at version 10.0.12, plus the .NETCore third-party notices. These files are copied into the application output and installer.
