Add-Type -AssemblyName System.Drawing

function Process-ImageResponse {
    param(
        [string]$jsonPath,
        [string]$targetPath
    )

    if (-not (Test-Path $jsonPath)) {
        throw "JSON response file not found: $jsonPath"
    }

    $raw = Get-Content $jsonPath -Raw
    $json = $raw | ConvertFrom-Json
    $b64 = $json.data[0].b64_json

    if (-not $b64 -and $json.data[0].url) {
        Write-Host "Downloading from URL: $($json.data[0].url)"
        $wc = New-Object System.Net.WebClient
        $bytes = $wc.DownloadData($json.data[0].url)
    } elseif ($b64) {
        $bytes = [Convert]::FromBase64String($b64)
    } else {
        throw "Neither b64_json nor url found in response: $raw"
    }

    $ms = New-Object System.IO.MemoryStream($bytes, 0, $bytes.Length)
    $srcBmp = [System.Drawing.Bitmap]::FromStream($ms)
    Write-Host "Original generated dimensions: $($srcBmp.Width) x $($srcBmp.Height)"

    $targetW = 1024
    $targetH = 559
    $outBmp = New-Object System.Drawing.Bitmap($targetW, $targetH)
    $g = [System.Drawing.Graphics]::FromImage($outBmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

    $g.DrawImage($srcBmp, 0, 0, $targetW, $targetH)
    $outBmp.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $g.Dispose()
    $outBmp.Dispose()
    $srcBmp.Dispose()
    $ms.Dispose()

    Write-Host "Successfully generated and resized to 1024x559: $targetPath"
}

$apiKey = 'sk-pLRIp6pdqtDuWlLMc6lfPw'
$srcImage = 'D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife\About\Preview.png'

# 1. RimPay Expansion
Write-Host "================ Generating RimPay Cover ================"
$promptRimPay = @"
Modify this RimWorld mod cover image for the expansion mod 'RimDigital Life: RimPay' (RimPay Expansion - 边缘数码生活：数字经济拓展).
Keep the exact same RimWorld cartoon art style, cute 2D RimWorld pawns (round heads, oval bodies, floating hands), and the stone-walled colony room interior with warm ambient lighting.
Change the theme of the scene to digital finance, banking, and economy:
- Title at the top center: Large bold text 'RimPay Expansion' with cyan and golden-orange colors, white outline, and subtitle '边缘数码生活：数字经济拓展' directly below in clean white font.
- Room features and colonist actions:
  - On the left desk: A colonist holding a sleek RimPay smartphone with a green payment success checkmark and 'R$' currency logo. Piles and stacks of shiny RimWorld silver coins and gold bullion on the counter.
  - A green pixel speech bubble above the left pawn: '已到账！' (Payment received!).
  - In the center: A colonist relaxing on the couch checking an electronic bank statement or digital wallet tablet with a rising green stock market candlestick chart.
  - On the right: A large curved monitor displaying the 'RimPay Stock Exchange' with green and red trading candles, market tickers, and a high-tech server tower labeled 'Cloud Treasury' with glowing golden lights.
  - A digital ATM or cashier terminal on a metal table.
  - Subtle gold coin holographic particles floating in the air.
  - Keep the author logo 'Maiya' in the bottom-right corner.
Maintain the exact high-quality RimWorld mod workshop preview composition, warm color tone with gold and green accents, and subtle dark vignette edges.
"@

$rimPayJson = 'D:\Visual Studio Code ALL\RimDigitalLife-Core\response_rimpay.json'
$rimPayTarget = 'D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_RimPay\About\Preview.png'

curl.exe -s -X POST 'https://sakiko.dev/v1/images/edits' `
    -H "Authorization: Bearer $apiKey" `
    -F "model=image/chat2api-gpt-image-2.5" `
    -F "image=@$srcImage" `
    -F "prompt=$promptRimPay" `
    -o $rimPayJson

Process-ImageResponse -jsonPath $rimPayJson -targetPath $rimPayTarget

# 2. QuantumNet Expansion
Write-Host "================ Generating QuantumNet Cover ================"
$promptQuantumNet = @"
Modify this RimWorld mod cover image for the expansion mod 'RimDigital Life: Quantum Net' (QuantumNet Expansion - 边缘数码生活：量子网络拓展).
Keep the exact same RimWorld cartoon art style, cute 2D RimWorld pawns (round heads, oval bodies, floating hands), and the stone-walled colony room interior with high-tech sci-fi atmosphere.
Change the theme of the scene to quantum satellite networks, starlink, and cloud computing:
- Title at the top center: Large bold text 'Quantum Net Expansion' with glowing cyan and neon blue colors, white outline, and subtitle '边缘数码生活：量子网络拓展' directly below in clean white font.
- Room features and colonist actions:
  - In the center: A high-tech RimWorld Comms Console / Quantum Terminal with a glowing blue core and satellite dish antenna on top, emitting concentric cyan signal wave rings.
  - On the large curved monitor: Displays a global planetary map with orbital Starlink satellites, glowing network nodes, orbital trade beacon trajectories, and a deep-ground radar scan.
  - On the left: A colonist with a futuristic headset and tablet engaged in a holographic RimTalk video call showing another colonist's face floating in a blue holographic frame.
  - Green pixel speech bubble above colonist: '全图覆盖！' (Full Map Coverage!).
  - On the right desk: High-tech quantum server rack labeled 'RimSeek AI' with pulsing cyan fiber-optic cables, antennas, and network status LEDs.
  - Cyan glowing data streams and floating quantum network node particles in the air.
  - Keep the author logo 'Maiya' in the bottom-right corner.
Maintain the exact high-quality RimWorld mod workshop preview composition, cool sci-fi blue/cyan lighting balanced with warm stone room background, and subtle dark vignette edges.
"@

$quantumNetJson = 'D:\Visual Studio Code ALL\RimDigitalLife-Core\response_quantumnet.json'
$quantumNetTarget = 'D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_QuantumNet\About\Preview.png'

curl.exe -s -X POST 'https://sakiko.dev/v1/images/edits' `
    -H "Authorization: Bearer $apiKey" `
    -F "model=image/chat2api-gpt-image-2.5" `
    -F "image=@$srcImage" `
    -F "prompt=$promptQuantumNet" `
    -o $quantumNetJson

Process-ImageResponse -jsonPath $quantumNetJson -targetPath $quantumNetTarget

# Clean up temp json files
Remove-Item $rimPayJson, $quantumNetJson, 'D:\Visual Studio Code ALL\RimDigitalLife-Core\test_edit.json' -ErrorAction SilentlyContinue
