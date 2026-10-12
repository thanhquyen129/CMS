$assetsDir = Join-Path $PSScriptRoot "..\assets"
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
}

Add-Type -AssemblyName System.Drawing

$sf = [System.Drawing.StringFormat]::new()
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center

# 1. icon.png (1024x1024)
$bmpIcon = [System.Drawing.Bitmap]::new(1024, 1024)
$g = [System.Drawing.Graphics]::FromImage($bmpIcon)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$p1 = [System.Drawing.Point]::new(0, 0)
$p2 = [System.Drawing.Point]::new(1024, 1024)
$c1 = [System.Drawing.ColorTranslator]::FromHtml('#0F172A')
$c2 = [System.Drawing.ColorTranslator]::FromHtml('#1E3A8A')
$bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($p1, $p2, $c1, $c2)
$g.FillRectangle($bgBrush, 0, 0, 1024, 1024)

$rect = [System.Drawing.Rectangle]::new(140, 140, 744, 744)
$cardBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#1E293B'))
$g.FillRectangle($cardBrush, $rect)

$accentPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#3B82F6'), 12.0)
$g.DrawRectangle($accentPen, $rect)

$fontTitle = [System.Drawing.Font]::new('Arial', 130.0, [System.Drawing.FontStyle]::Bold)
$fontSub = [System.Drawing.Font]::new('Arial', 56.0, [System.Drawing.FontStyle]::Bold)
$whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$blueBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#60A5FA'))

$g.DrawString('LSS', $fontTitle, $blueBrush, 512.0, 430.0, $sf)
$g.DrawString('LCMS', $fontSub, $whiteBrush, 512.0, 600.0, $sf)

$iconPath = Join-Path $assetsDir "icon.png"
$bmpIcon.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmpIcon.Dispose()

# 2. adaptive-icon.png (1024x1024 transparent with centered icon)
$bmpAdaptive = [System.Drawing.Bitmap]::new(1024, 1024)
$gA = [System.Drawing.Graphics]::FromImage($bmpAdaptive)
$gA.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gA.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$rectA = [System.Drawing.Rectangle]::new(260, 260, 504, 504)
$bgA = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#1E3A8A'))
$gA.FillEllipse($bgA, $rectA)
$borderPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#60A5FA'), 16.0)
$gA.DrawEllipse($borderPen, $rectA)

$fontAd1 = [System.Drawing.Font]::new('Arial', 100.0, [System.Drawing.FontStyle]::Bold)
$fontAd2 = [System.Drawing.Font]::new('Arial', 42.0, [System.Drawing.FontStyle]::Bold)
$gA.DrawString('LSS', $fontAd1, $whiteBrush, 512.0, 460.0, $sf)
$gA.DrawString('CMS', $fontAd2, $blueBrush, 512.0, 580.0, $sf)

$adaptivePath = Join-Path $assetsDir "adaptive-icon.png"
$bmpAdaptive.Save($adaptivePath, [System.Drawing.Imaging.ImageFormat]::Png)
$gA.Dispose()
$bmpAdaptive.Dispose()

# 3. splash.png (1080x1920)
$bmpSplash = [System.Drawing.Bitmap]::new(1080, 1920)
$gS = [System.Drawing.Graphics]::FromImage($bmpSplash)
$gS.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gS.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$splashBg = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#0F172A'))
$gS.FillRectangle($splashBg, 0, 0, 1080, 1920)

$fontSp1 = [System.Drawing.Font]::new('Arial', 80.0, [System.Drawing.FontStyle]::Bold)
$fontSp2 = [System.Drawing.Font]::new('Arial', 40.0, [System.Drawing.FontStyle]::Regular)
$fontSp3 = [System.Drawing.Font]::new('Arial', 28.0, [System.Drawing.FontStyle]::Regular)
$grayBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#94A3B8'))

$gS.DrawString('LSS LOGISTICS', $fontSp1, $blueBrush, 540.0, 900.0, $sf)
$gS.DrawString('Cost Management System', $fontSp2, $whiteBrush, 540.0, 1000.0, $sf)
$gS.DrawString('cms.lsslogistics.vn', $fontSp3, $grayBrush, 540.0, 1800.0, $sf)

$splashPath = Join-Path $assetsDir "splash.png"
$bmpSplash.Save($splashPath, [System.Drawing.Imaging.ImageFormat]::Png)
$gS.Dispose()
$bmpSplash.Dispose()

Write-Output "Assets created in: $assetsDir"
