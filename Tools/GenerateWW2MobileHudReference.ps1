param(
    [string]$OutputPath = "Assets/ArtReferences/ww2_mobile_hud_reference.png"
)

Add-Type -AssemblyName System.Drawing

$width = 1080
$height = 1920
$script:fontName = "Microsoft YaHei UI"
$script:rng = [System.Random]::new(1944)

function Get-Color {
    param(
        [string]$Hex,
        [int]$Alpha = 255
    )

    $base = [System.Drawing.ColorTranslator]::FromHtml($Hex)
    return [System.Drawing.Color]::FromArgb($Alpha, $base.R, $base.G, $base.B)
}

function New-Brush {
    param(
        [string]$Hex,
        [int]$Alpha = 255
    )

    return [System.Drawing.SolidBrush]::new((Get-Color $Hex $Alpha))
}

function New-Pen {
    param(
        [string]$Hex,
        [float]$Width = 1,
        [int]$Alpha = 255
    )

    return [System.Drawing.Pen]::new((Get-Color $Hex $Alpha), $Width)
}

function New-Font {
    param(
        [float]$Size,
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular
    )

    return [System.Drawing.Font]::new($script:fontName, [single]$Size, $Style, [System.Drawing.GraphicsUnit]::Pixel)
}

function New-RoundedPath {
    param(
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$R
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $R * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-Round {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$R,
        [string]$Hex,
        [int]$Alpha = 255
    )

    $path = New-RoundedPath $X $Y $W $H $R
    $brush = New-Brush $Hex $Alpha
    $G.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

function Stroke-Round {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$R,
        [string]$Hex,
        [float]$LineWidth = 1,
        [int]$Alpha = 255
    )

    $path = New-RoundedPath $X $Y $W $H $R
    $pen = New-Pen $Hex $LineWidth $Alpha
    $G.DrawPath($pen, $path)
    $pen.Dispose()
    $path.Dispose()
}

function Draw-Text {
    param(
        [System.Drawing.Graphics]$G,
        [string]$Text,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$Size,
        [string]$Hex,
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular,
        [System.Drawing.StringAlignment]$Align = [System.Drawing.StringAlignment]::Near,
        [System.Drawing.StringAlignment]$LineAlign = [System.Drawing.StringAlignment]::Center,
        [int]$Alpha = 255
    )

    $font = New-Font $Size $Style
    $brush = New-Brush $Hex $Alpha
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = $Align
    $format.LineAlignment = $LineAlign
    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
    $rect = [System.Drawing.RectangleF]::new($X, $Y, $W, $H)
    $G.DrawString($Text, $font, $brush, $rect, $format)
    $format.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Fill-Polygon {
    param(
        [System.Drawing.Graphics]$G,
        [System.Drawing.PointF[]]$Points,
        [string]$Hex,
        [int]$Alpha = 255
    )

    $brush = New-Brush $Hex $Alpha
    $G.FillPolygon($brush, $Points)
    $brush.Dispose()
}

function Draw-ProgressBar {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$Value,
        [string]$FillHex
    )

    Fill-Round $G $X $Y $W $H 8 "#141816" 205
    Stroke-Round $G $X $Y $W $H 8 "#6f6b56" 1.5 190
    $inner = [Math]::Max(0, [Math]::Min($W - 8, ($W - 8) * $Value))
    Fill-Round $G ($X + 4) ($Y + 4) $inner ($H - 8) 6 $FillHex 235
}

function Draw-Rivet {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y
    )

    $brush = New-Brush "#c9b77a" 210
    $pen = New-Pen "#2a2c25" 1.5 220
    $G.FillEllipse($brush, $X - 4, $Y - 4, 8, 8)
    $G.DrawEllipse($pen, $X - 4, $Y - 4, 8, 8)
    $brush.Dispose()
    $pen.Dispose()
}

function Draw-Panel {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [float]$R = 18
    )

    Fill-Round $G $X $Y $W $H $R "#1d221c" 215
    Stroke-Round $G $X $Y $W $H $R "#b9aa73" 2 150
    Stroke-Round $G ($X + 5) ($Y + 5) ($W - 10) ($H - 10) ([Math]::Max(4, $R - 5)) "#34372d" 1.2 210
    Draw-Rivet $G ($X + 17) ($Y + 17)
    Draw-Rivet $G ($X + $W - 17) ($Y + 17)
}

function Draw-IconCircle {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$Size,
        [string]$Label,
        [string]$FillHex
    )

    $brush = New-Brush $FillHex 240
    $pen = New-Pen "#f1d38a" 2 190
    $G.FillEllipse($brush, $X, $Y, $Size, $Size)
    $G.DrawEllipse($pen, $X, $Y, $Size, $Size)
    $brush.Dispose()
    $pen.Dispose()
    Draw-Text $G $Label $X $Y $Size $Size 22 "#fff0bc" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
}

function Draw-ResourceChip {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [string]$Title,
        [string]$Value,
        [string]$Icon,
        [string]$Color
    )

    Fill-Round $G $X $Y $W 72 16 "#23291f" 225
    Stroke-Round $G $X $Y $W 72 16 "#95895f" 1.5 160
    Draw-IconCircle $G ($X + 12) ($Y + 12) 48 $Icon $Color
    Draw-Text $G $Title ($X + 72) ($Y + 9) ($W - 84) 22 20 "#c8c0a2"
    Draw-Text $G $Value ($X + 72) ($Y + 31) ($W - 84) 33 28 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
}

function Draw-Tank {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$Scale = 1.0,
        [bool]$Enemy = $false
    )

    $body = if ($Enemy) { "#6b3d35" } else { "#596844" }
    $trim = if ($Enemy) { "#d28c73" } else { "#c5c58d" }
    Fill-Round $G $X $Y (92 * $Scale) (48 * $Scale) (10 * $Scale) "#22241c" 130
    Fill-Round $G ($X + 4 * $Scale) ($Y + 6 * $Scale) (84 * $Scale) (36 * $Scale) (9 * $Scale) $body 245
    Stroke-Round $G ($X + 4 * $Scale) ($Y + 6 * $Scale) (84 * $Scale) (36 * $Scale) (9 * $Scale) $trim (2 * $Scale) 185
    Fill-Round $G ($X + 31 * $Scale) ($Y + 13 * $Scale) (32 * $Scale) (22 * $Scale) (7 * $Scale) "#34382a" 250
    $pen = New-Pen $trim (6 * $Scale) 220
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $G.DrawLine($pen, $X + 58 * $Scale, $Y + 22 * $Scale, $X + 118 * $Scale, $Y + 10 * $Scale)
    $pen.Dispose()
}

function Draw-Building {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [string]$Kind,
        [string]$BaseHex
    )

    Fill-Round $G ($X + 10) ($Y + $H - 18) ($W - 20) 28 12 "#171814" 95
    Fill-Round $G $X $Y $W $H 14 $BaseHex 238
    Stroke-Round $G $X $Y $W $H 14 "#c9bc86" 2 145
    $roof = @(
        [System.Drawing.PointF]::new($X + 12, $Y + 22),
        [System.Drawing.PointF]::new($X + $W * 0.5, $Y - 10),
        [System.Drawing.PointF]::new($X + $W - 12, $Y + 22)
    )
    Fill-Polygon $G $roof "#3a3d31" 245
    Draw-Text $G $Kind ($X + 8) ($Y + $H * 0.42) ($W - 16) 34 23 "#f6e4a6" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
}

function Draw-CommandButton {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H,
        [string]$Title,
        [string]$Sub,
        [string]$Icon,
        [string]$Accent,
        [bool]$Locked = $false
    )

    $fill = if ($Locked) { "#20221d" } else { "#2b3127" }
    $alpha = if ($Locked) { 185 } else { 235 }
    Fill-Round $G $X $Y $W $H 14 $fill $alpha
    Stroke-Round $G $X $Y $W $H 14 $Accent 2 160
    Draw-IconCircle $G ($X + 14) ($Y + 14) 48 $Icon $Accent
    Draw-Text $G $Title ($X + 74) ($Y + 12) ($W - 86) 30 24 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
    Draw-Text $G $Sub ($X + 74) ($Y + 44) ($W - 86) 24 18 "#c8c0a2"
    if ($Locked) {
        Fill-Round $G ($X + $W - 50) ($Y + 18) 32 32 8 "#151613" 230
        Draw-Text $G "锁" ($X + $W - 50) ($Y + 17) 32 32 20 "#a8a28b" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
    }
}

function Draw-Minimap {
    param(
        [System.Drawing.Graphics]$G,
        [float]$X,
        [float]$Y,
        [float]$W,
        [float]$H
    )

    Draw-Panel $G $X $Y $W $H 18
    Draw-Text $G "地图" ($X + 20) ($Y + 10) 88 32 25 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
    $mx = $X + 16
    $my = $Y + 52
    $mw = $W - 32
    $mh = $H - 70
    Fill-Round $G $mx $my $mw $mh 12 "#31412f" 255
    Stroke-Round $G $mx $my $mw $mh 12 "#d6c88d" 2 150

    $waterBrush = New-Brush "#304f58" 215
    $G.FillEllipse($waterBrush, $mx + 14, $my + $mh - 68, 106, 42)
    $waterBrush.Dispose()

    $roadPen = New-Pen "#b19b68" 7 180
    $roadPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $roadPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $G.DrawBezier($roadPen, $mx + 18, $my + $mh - 26, $mx + 96, $my + 62, $mx + 170, $my + 98, $mx + $mw - 18, $my + 34)
    $roadPen.Dispose()

    $gridPen = New-Pen "#d8ce98" 1 55
    for ($i = 1; $i -lt 4; $i++) {
        $gx = $mx + $mw * $i / 4
        $gy = $my + $mh * $i / 4
        $G.DrawLine($gridPen, $gx, $my, $gx, $my + $mh)
        $G.DrawLine($gridPen, $mx, $gy, $mx + $mw, $gy)
    }
    $gridPen.Dispose()

    $blue = New-Brush "#70d782" 245
    $red = New-Brush "#d65d4e" 245
    $gold = New-Brush "#f4cf62" 245
    $G.FillEllipse($blue, $mx + 42, $my + 44, 14, 14)
    $G.FillEllipse($blue, $mx + 68, $my + 64, 12, 12)
    $G.FillEllipse($blue, $mx + 96, $my + 88, 12, 12)
    $G.FillEllipse($gold, $mx + 132, $my + 42, 14, 14)
    $G.FillEllipse($red, $mx + $mw - 64, $my + 40, 14, 14)
    $G.FillEllipse($red, $mx + $mw - 88, $my + 82, 12, 12)
    $blue.Dispose()
    $red.Dispose()
    $gold.Dispose()

    $viewPen = New-Pen "#fff0bc" 3 230
    $G.DrawRectangle($viewPen, $mx + 34, $my + 34, 108, 84)
    $viewPen.Dispose()
}

$bmp = [System.Drawing.Bitmap]::new($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

$bgRect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
$bg = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bgRect, (Get-Color "#303928"), (Get-Color "#786943"), 55)
$g.FillRectangle($bg, $bgRect)
$bg.Dispose()

for ($i = 0; $i -lt 460; $i++) {
    $x = $script:rng.Next(-80, $width)
    $y = $script:rng.Next(110, 1510)
    $s = $script:rng.Next(12, 92)
    $alpha = $script:rng.Next(18, 55)
    $hex = if (($i % 3) -eq 0) { "#1f281d" } elseif (($i % 3) -eq 1) { "#8b7a4c" } else { "#4b5638" }
    $brush = New-Brush $hex $alpha
    $g.FillEllipse($brush, $x, $y, $s, $s * 0.55)
    $brush.Dispose()
}

$road = New-Pen "#b49d70" 56 105
$road.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$road.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawBezier($road, 70, 1170, 280, 820, 520, 860, 1040, 650)
$road.Dispose()

$roadEdge = New-Pen "#453c2c" 5 95
$roadEdge.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$g.DrawBezier($roadEdge, 70, 1170, 280, 820, 520, 860, 1040, 650)
$roadEdge.Dispose()

$trenchPen = New-Pen "#2a271f" 18 125
$trenchPen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::DashDot
$g.DrawBezier($trenchPen, 35, 510, 300, 470, 420, 560, 655, 470)
$g.DrawBezier($trenchPen, 880, 1030, 740, 1130, 650, 1240, 480, 1250)
$trenchPen.Dispose()

Draw-Building $g 148 760 165 118 "指挥" "#5d6047"
Draw-Building $g 376 840 168 112 "兵营" "#58624a"
Draw-Building $g 608 742 184 126 "工厂" "#5b5846"
Draw-Building $g 768 942 146 108 "电站" "#4e5d4e"
Draw-Building $g 238 1016 150 104 "金矿" "#6c5a38"

Draw-Tank $g 282 690 0.95 $false
Draw-Tank $g 536 1015 0.82 $false
Draw-Tank $g 746 602 0.72 $true
Draw-Tank $g 853 680 0.67 $true

$infantryBrush = New-Brush "#d9d7a6" 235
$enemyBrush = New-Brush "#d28776" 235
foreach ($pt in @(@(192, 930), @(215, 955), @(248, 944), @(692, 906), @(720, 930), @(744, 920))) {
    $g.FillEllipse($infantryBrush, $pt[0], $pt[1], 15, 15)
}
foreach ($pt in @(@(872, 760), @(902, 786), @(832, 812), @(802, 790))) {
    $g.FillEllipse($enemyBrush, $pt[0], $pt[1], 14, 14)
}
$infantryBrush.Dispose()
$enemyBrush.Dispose()

$smokeBrush = New-Brush "#e4dcc7" 42
for ($i = 0; $i -lt 14; $i++) {
    $g.FillEllipse($smokeBrush, 650 + $script:rng.Next(-40, 64), 675 + $script:rng.Next(-68, 22), $script:rng.Next(38, 94), $script:rng.Next(24, 70))
}
$smokeBrush.Dispose()

$vignette = [System.Drawing.Drawing2D.GraphicsPath]::new()
$vignette.AddRectangle([System.Drawing.RectangleF]::new(0, 0, $width, $height))
$vBrush = [System.Drawing.Drawing2D.PathGradientBrush]::new($vignette)
$vBrush.CenterColor = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
$vBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(142, 8, 10, 8))
$g.FillRectangle($vBrush, 0, 0, $width, $height)
$vBrush.Dispose()
$vignette.Dispose()

Fill-Round $g 24 22 1032 154 22 "#161a16" 226
Stroke-Round $g 24 22 1032 154 22 "#c2b174" 2 160
Draw-Text $g "前线指挥部" 52 36 230 34 28 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
Draw-Text $g "移动端二战 RTS HUD 参考" 52 72 280 28 18 "#bbb49a"
Draw-ResourceChip $g 326 44 168 "金币" "1,260" "金" "#a77b28"
Draw-ResourceChip $g 506 44 172 "时间" "14:35" "时" "#44546a"
Draw-ResourceChip $g 690 44 174 "电力" "+42/60" "电" "#39715c"
Draw-ResourceChip $g 876 44 150 "科技" "T3" "科" "#6b5d88"

Draw-Panel $g 36 206 294 246 20
Draw-Text $g "科技" 62 222 92 34 30 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
Draw-Text $g "装甲强化" 62 266 200 30 24 "#e9d596" ([System.Drawing.FontStyle]::Bold)
Draw-ProgressBar $g 62 306 220 22 0.68 "#d5ad48"
Draw-Text $g "68%" 240 282 56 28 20 "#fff0bc" ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
Draw-CommandButton $g 62 346 230 76 "雷达侦察" "剩余 0:42" "R" "#607b74"

Draw-Panel $g 760 214 282 184 20
Draw-Text $g "战况" 786 230 90 34 28 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
Draw-Text $g "敌军推进中" 786 272 210 30 24 "#f1c095" ([System.Drawing.FontStyle]::Bold)
Draw-ProgressBar $g 786 318 210 22 0.41 "#c55f48"
Draw-Text $g "防线完整度 41%" 786 348 210 28 18 "#c8c0a2"

Draw-Panel $g 30 1442 650 406 20
Draw-Text $g "建筑" 58 1460 90 38 31 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
Draw-Text $g "建造队列 2/5" 176 1464 180 30 21 "#c8c0a2"
Draw-CommandButton $g 58 1512 284 88 "兵营" "步兵 150 金币" "兵" "#657448"
Draw-CommandButton $g 366 1512 284 88 "坦克工厂" "装甲单位" "坦" "#6d694b"
Draw-CommandButton $g 58 1618 284 88 "发电站" "电力 +18" "电" "#4f8064"
Draw-CommandButton $g 366 1618 284 88 "雷达站" "解锁小地图" "雷" "#607b74"
Draw-CommandButton $g 58 1724 284 88 "科技中心" "升级 T4" "科" "#77658b" $true
Draw-CommandButton $g 366 1724 284 88 "金矿精炼" "收入 +20%" "金" "#9d7b39"

Draw-Minimap $g 704 1442 346 406

Fill-Round $g 36 1294 1008 116 18 "#161a16" 206
Stroke-Round $g 36 1294 1008 116 18 "#c2b174" 1.5 120
Draw-Text $g "指挥操作" 64 1312 132 32 24 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
$orders = @(
    @("集结", "A", "#516f55"),
    @("进攻", "X", "#9a5445"),
    @("防守", "D", "#5d6f82"),
    @("维修", "W", "#6a7453"),
    @("空袭", "B", "#7a6b54")
)
for ($i = 0; $i -lt $orders.Count; $i++) {
    $order = $orders[$i]
    $x = 220 + $i * 160
    Fill-Round $g $x 1324 124 60 13 "#2b3127" 230
    Stroke-Round $g $x 1324 124 60 13 $order[2] 2 170
    Draw-IconCircle $g ($x + 10) 1334 40 $order[1] $order[2]
    Draw-Text $g $order[0] ($x + 58) 1334 54 40 20 "#fff0bc" ([System.Drawing.FontStyle]::Bold)
}

Fill-Round $g 392 1868 296 18 9 "#efe5bd" 145

$dir = Split-Path -Parent $OutputPath
if ($dir -and -not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$fullPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
$bmp.Save($fullPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

Write-Output $fullPath
