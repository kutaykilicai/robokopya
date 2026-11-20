param([string]$Hedef)
try { $Host.UI.RawUI.WindowTitle = 'Robocopy ile yapıştır' } catch {}
$Hedef = $Hedef.Trim().Trim('"')
if ($Hedef -match '^[A-Za-z]:$') { $Hedef += '\' }
function RY([string]$y) { if ($y.EndsWith('\')) { $y + '.' } else { $y } }
$liste = Join-Path $PSScriptRoot 'secim.txt'
if (-not (Test-Path -LiteralPath $liste)) { Write-Host 'Kopyalanacak bir şey seçilmemiş. Önce sağ tık > Robocopy ile kopyala.' -ForegroundColor Yellow; Read-Host 'Kapatmak için Enter'; exit }
$kaynaklar = @(Get-Content -LiteralPath $liste -Encoding UTF8 | Where-Object { $_ -and (Test-Path -LiteralPath $_) })
$hata = 0
foreach ($k in $kaynaklar) {
  $item = Get-Item -LiteralPath $k -Force
  if ($item.PSIsContainer) {
    $dst = [IO.Path]::Combine($Hedef, $item.Name)
    $src = $item.FullName.TrimEnd('\')
    if ($dst.TrimEnd('\') -ieq $src -or $Hedef.TrimEnd('\').StartsWith($src + '\', 'OrdinalIgnoreCase') -or $Hedef.TrimEnd('\') -ieq $src) { Write-Host "Atlandı (klasör kendi içine kopyalanamaz): $k" -ForegroundColor Yellow; continue }
    Write-Host "`n>>> Klasör: $k  ->  $dst" -ForegroundColor Cyan
    robocopy "$(RY $item.FullName)" "$(RY $dst)" /E /COPY:DAT /DCOPY:DAT /MT:16 /R:1 /W:1 /NP /NDL /XJ
  } else {
    if ($item.DirectoryName.TrimEnd('\') -ieq $Hedef.TrimEnd('\')) { Write-Host "Atlandı (aynı klasör): $k" -ForegroundColor Yellow; continue }
    Write-Host "`n>>> Dosya: $k  ->  $Hedef" -ForegroundColor Cyan
    robocopy "$(RY $item.DirectoryName)" "$(RY $Hedef)" "$($item.Name)" /COPY:DAT /J /R:1 /W:1 /NDL
  }
  if ($LASTEXITCODE -ge 8) { $hata++ }
}
if ($hata -eq 0) { Write-Host "`nKopyalama tamamlandı." -ForegroundColor Green; Start-Sleep 3 } else { Write-Host "`n$hata öğede hata oluştu, yukarıdaki çıktıya bak." -ForegroundColor Red; Read-Host 'Kapatmak için Enter' }
