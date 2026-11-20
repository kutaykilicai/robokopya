# Sağ tık girdilerini ve %LOCALAPPDATA%\RoboKopya klasörünü kaldırır.
foreach ($a in '*\shell\RoboKopyala','Directory\shell\RoboKopyala','Directory\shell\RoboYapistir','Directory\Background\shell\RoboYapistir','Drive\shell\RoboYapistir') {
  Remove-Item -LiteralPath "HKCU:\Software\Classes\$a" -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item (Join-Path $env:LOCALAPPDATA 'RoboKopya') -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'RoboKopya kaldırıldı.'
