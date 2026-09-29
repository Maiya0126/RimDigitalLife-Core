Add-Type -AssemblyName System.Drawing

function New-Cover([string]$outPath, [scriptblock]$paint) {
    $w = 1024; $h = 559
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    & $paint $g $w $h
    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved $outPath"
}

function Draw-Vignette($g, $w, $h) {
    $r = New-Object System.Drawing.Rectangle(0,0,$w,$h)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddRectangle($r)
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $pgb.CenterColor = [System.Drawing.Color]::Transparent
    $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(200,0,0,0))
    $pgb.FocusScales = New-Object System.Drawing.PointF(0.55, 0.55)
    $g.FillPath($pgb, $path)
    $pgb.Dispose(); $path.Dispose()
}

function Draw-Title($g, $w, $h, [string]$bigEn, [string]$zh, [string]$tagEn, [System.Drawing.Color]$accent) {
    # bottom panel
    $ptA = [System.Drawing.Point]::new(0, ($h-170)); $ptB = [System.Drawing.Point]::new(0, $h)
    $pb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $ptA, $ptB,
        [System.Drawing.Color]::FromArgb(0,0,0,0), [System.Drawing.Color]::FromArgb(230,0,0,0))
    $g.FillRectangle($pb, 0, $h-170, $w, 170); $pb.Dispose()

    $fBig = New-Object System.Drawing.Font('Segoe UI', 46, [System.Drawing.FontStyle]::Bold)
    $fZh  = New-Object System.Drawing.Font('Microsoft YaHei', 20, [System.Drawing.FontStyle]::Bold)
    $fTag = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Regular)

    $g.DrawString($bigEn, $fBig, [System.Drawing.Brushes]::White, 34, $h-140)
    $sz = $g.MeasureString($bigEn, $fBig)
    $g.DrawString($zh, $fZh, (New-Object System.Drawing.SolidBrush $accent), 38, $h-140+$sz.Height-4)
    $g.DrawString($tagEn, $fTag, [System.Drawing.Brushes]::Gray, 40, $h-46)

    # accent line
    $pen = New-Object System.Drawing.Pen($accent, 4)
    $g.DrawLine($pen, 36, $h-150, 36+120, $h-150)
    $pen.Dispose()
    $fBig.Dispose(); $fZh.Dispose(); $fTag.Dispose()
}

# ============ RimPay ============
New-Cover "D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_RimPay\About\Preview.png" {
    param($g, $w, $h)
    # bg: dark navy-gold vertical gradient
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point(0,$h)),
        [System.Drawing.Color]::FromArgb(255,20,18,10), [System.Drawing.Color]::FromArgb(255,66,50,16))
    $g.FillRectangle($bg, 0,0,$w,$h); $bg.Dispose()
    # gold radial glow center
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($w/2-380, $h/2-300, 760, 600)
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $pgb.CenterColor = [System.Drawing.Color]::FromArgb(120,255,200,60)
    $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0,255,200,60))
    $g.FillPath($pgb, $path); $pgb.Dispose(); $path.Dispose()

    # faint grid (fintech feel)
    $penG = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(28,255,210,90), 1)
    for($x=0;$x -lt $w;$x+=64){$g.DrawLine($penG,$x,0,$x,$h)}
    for($y=0;$y -lt $h;$y+=64){$g.DrawLine($penG,0,$y,$w,$y)}
    $penG.Dispose()

    # ---- candlestick chart across mid-lower area ----
    $rand = New-Object System.Random(42)
    $cx = 60; $base = $h*0.62
    for($i=0;$i -lt 16;$i++){
        $o = $base; $c = $base - (30 + $rand.Next(70)) + $i*8
        $hi = [Math]::Min($o,$c) - $rand.Next(25); $lo = [Math]::Max($o,$c) + $rand.Next(25)
        $up = $c -lt $o
        $col = if($up){[System.Drawing.Color]::FromArgb(230,90,220,110)}else{[System.Drawing.Color]::FromArgb(230,235,90,80)}
        $br = New-Object System.Drawing.SolidBrush($col)
        $pen = New-Object System.Drawing.Pen($col, 2)
        $g.DrawLine($pen, $cx+12, $hi, $cx+12, $lo)
        $top=[Math]::Min($o,$c); $hh=[Math]::Abs($c-$o); if($hh -lt 6){$hh=6}
        $g.FillRectangle($br, $cx, $top, 24, $hh)
        $br.Dispose(); $pen.Dispose()
        $cx += 46
    }

    # ---- big smartphone ----
    $phx = $w/2; $phy = $h*0.42
    $phW = 190; $phH = 340
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rad = 28
    $body.AddArc($phx-$phW/2, $phy-$phH/2, $rad*2, $rad*2, 180, 90)
    $body.AddArc($phx+$phW/2-$rad*2, $phy-$phH/2, $rad*2, $rad*2, 270, 90)
    $body.AddArc($phx+$phW/2-$rad*2, $phy+$phH/2-$rad*2, $rad*2, $rad*2, 0, 90)
    $body.AddArc($phx-$phW/2, $phy+$phH/2-$rad*2, $rad*2, $rad*2, 90, 90)
    $body.CloseFigure()
    $darkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,28,26,22))
    $g.FillPath($darkBrush, $body)
    $goldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,255,205,80), 5)
    $g.DrawPath($goldPen, $body)
    $goldPen.Dispose(); $darkBrush.Dispose(); $body.Dispose()
    # screen
    $scr = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,44,38,20))
    $g.FillRectangle($scr, $phx-$phW/2+14, $phy-$phH/2+16, $phW-28, $phH-32)
    $scr.Dispose()
    # "R" logo & pay glyph on screen
    $fPay = New-Object System.Drawing.Font('Segoe UI', 54, [System.Drawing.FontStyle]::Bold)
    $goldBr = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,255,210,90))
    $szp = $g.MeasureString("R$", $fPay)
    $g.DrawString("R$", $fPay, $goldBr, $phx-$szp.Width/2, $phy-70)
    $fPay.Dispose()
    # wifi-ish contactless arcs
    $penA = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220,255,225,130), 4)
    for($i=1;$i -le 3;$i++){
        $g.DrawArc($penA, $phx+10, $phy+40-10*$i, 20*$i, 20*$i, -70, 140)
    }
    $penA.Dispose(); $goldBr.Dispose()

    # ---- cloud treasury (top-left) with coin entering ----
    $cloudCol = [System.Drawing.Color]::FromArgb(235,235,230,215)
    $br = New-Object System.Drawing.SolidBrush($cloudCol)
    $g.FillEllipse($br, 100, 70, 130, 70)
    $g.FillEllipse($br, 140, 40, 110, 80)
    $g.FillEllipse($br, 70, 95, 100, 55)
    $br.Dispose()
    $fY = New-Object System.Drawing.Font('Segoe UI', 20, [System.Drawing.FontStyle]::Bold)
    $darkB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,70,55,15))
    $g.DrawString("R$", $fY, $darkB, 140, 82); $fY.Dispose(); $darkB.Dispose()
    # coin falling into cloud
    $coin = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,255,200,60))
    $g.FillEllipse($coin, 195, 145, 36, 36)
    $coinPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,140,100,20), 3)
    $g.DrawEllipse($coinPen, 195, 145, 36, 36)
    $g.DrawEllipse($coinPen, 202, 152, 22, 22)
    $coinPen.Dispose()

    # ---- silver/coin stack (left bottom) ----
    $bx = 120; $by = $h-105
    for($row=0;$row -lt 4;$row++){
        $g.FillEllipse($coin, $bx, $by-$row*20, 90, 26)
        $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,150,110,25), 2)
        $g.DrawEllipse($edge, $bx, $by-$row*20, 90, 26)
        $edge.Dispose()
    }

    # ---- gold coins scattered right ----
    foreach($pt in @(@(870,120,30),@(930,180,22),@(830,200,18))){
        $g.FillEllipse($coin, $pt[0], $pt[1], $pt[2], $pt[2])
    }

    Draw-Vignette $g $w $h
    Draw-Title $g $w $h "RimPay Expansion" "边缘数码生活：数字经济拓展" "RIM DIGITAL LIFE  |  EXPANSION  |  v0.7" ([System.Drawing.Color]::FromArgb(255,255,205,80))
}

# ============ QuantumNet ============
New-Cover "D:\Visual Studio Code ALL\RimDigitalLife-Core\RimDigitalLife_QuantumNet\About\Preview.png" {
    param($g, $w, $h)
    # bg: deep space blue-purple
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point(0,$h)),
        [System.Drawing.Color]::FromArgb(255,6,10,28), [System.Drawing.Color]::FromArgb(255,16,42,70))
    $g.FillRectangle($bg, 0,0,$w,$h); $bg.Dispose()

    # stars
    $rand = New-Object System.Random(7)
    for($i=0;$i -lt 160;$i++){
        $x=$rand.Next($w); $y=$rand.Next($h); $a=$rand.Next(80,220); $s=1+$rand.Next(2)
        $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($a,220,235,255))
        $g.FillEllipse($sb,$x,$y,$s,$s); $sb.Dispose()
    }

    # cyan glow
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($w/2-400, $h/2-280, 800, 560)
    $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $pgb.CenterColor = [System.Drawing.Color]::FromArgb(90,60,220,255)
    $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0,60,220,255))
    $g.FillPath($pgb, $path); $pgb.Dispose(); $path.Dispose()

    # planet arc at bottom
    $pl = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pl.AddEllipse($w/2-900, $h-160, 1800, 700)
    $ptC = [System.Drawing.Point]::new(0, ($h-160)); $ptD = [System.Drawing.Point]::new(0, ($h+200))
    $pbr = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $ptC, $ptD,
        [System.Drawing.Color]::FromArgb(255,20,90,110), [System.Drawing.Color]::FromArgb(255,5,20,40))
    $g.FillPath($pbr, $pl)
    $glowP = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160,90,230,255), 5)
    $g.DrawPath($glowP, $pl)
    $glowP.Dispose(); $pbr.Dispose(); $pl.Dispose()

    # ---- network nodes + links (upper area) ----
    $nodes = @(@(150,110),@(320,70),@(500,130),@(690,60),@(880,110),@(240,220),@(430,190),@(640,230),@(830,190),@(120,300),@(330,320),@(560,300),@(770,320),@(950,260))
    $penL = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70,90,220,255), 1.5)
    foreach($a in $nodes){ foreach($b in $nodes){
        $d = [Math]::Sqrt([Math]::Pow($a[0]-$b[0],2)+[Math]::Pow($a[1]-$b[1],2))
        if($d -gt 0 -and $d -lt 260){ $g.DrawLine($penL, $a[0], $a[1], $b[0], $b[1]) }
    }}
    $penL.Dispose()
    foreach($n in $nodes){
        $nb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230,140,240,255))
        $g.FillEllipse($nb, $n[0]-5, $n[1]-5, 10, 10)
        $halo = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80,140,240,255), 2)
        $g.DrawEllipse($halo, $n[0]-9, $n[1]-9, 18, 18)
        $halo.Dispose(); $nb.Dispose()
    }

    # ---- satellite ----
    $sx = $w/2; $sy = $h*0.36
    # body
    $satBr = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,190,200,215))
    $g.FillRectangle($satBr, $sx-38, $sy-26, 76, 52)
    $satBr.Dispose()
    # solar panels
    $panBr = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,30,90,180))
    $penP = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,120,200,255), 2)
    foreach($dir in @(-1, 1)){
        $px = if($dir -lt 0){$sx-38-150}else{$sx+38}
        $g.FillRectangle($panBr, $px, $sy-22, 150, 44)
        $g.DrawRectangle($penP, $px, $sy-22, 150, 44)
        $g.DrawLine($penP, $px+50, $sy-22, $px+50, $sy+22)
        $g.DrawLine($penP, $px+100, $sy-22, $px+100, $sy+22)
        # arm
        $arm = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,190,200,215), 4)
        if($dir -lt 0){$g.DrawLine($arm,$sx-38,$sy,$sx-38-150,$sy)}else{$g.DrawLine($arm,$sx+38,$sy,$sx+38+150,$sy)}
        $arm.Dispose()
    }
    $panBr.Dispose(); $penP.Dispose()
    # dish
    $dishP = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,220,230,245), 4)
    $g.DrawArc($dishP, $sx-22, $sy+26, 44, 34, 10, 160)
    $dishP.Dispose()

    # signal waves from satellite downward
    $sigP = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140,90,230,255), 3)
    for($i=1;$i -le 4;$i++){
        $g.DrawArc($sigP, $sx-40*$i, $sy+30, 80*$i, 80*$i, 25, 130)
    }
    $sigP.Dispose()

    # ---- communication console silhouette on planet (RimWorld comms console motif) ----
    $cb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,40,46,58))
    $cp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200,140,240,255), 3)
    $g.FillRectangle($cb, $w/2-60, $h-130, 120, 80)
    $g.DrawRectangle($cp, $w/2-60, $h-130, 120, 80)
    $g.DrawLine($cp, $w/2-60, $h-110, $w/2+60, $h-110)
    # screen dot
    $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,120,240,255))
    $g.FillEllipse($dot, $w/2-12, $h-172, 24, 24)
    $dot.Dispose(); $cb.Dispose(); $cp.Dispose()

    Draw-Vignette $g $w $h
    Draw-Title $g $w $h "Quantum Net Expansion" "边缘数码生活：量子网络拓展" "RIM DIGITAL LIFE  |  EXPANSION  |  v0.3" ([System.Drawing.Color]::FromArgb(255,90,230,255))
}
