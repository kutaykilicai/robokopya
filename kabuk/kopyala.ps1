param([string]$Yol)
$liste = Join-Path $PSScriptRoot 'secim.txt'
$kilit = Join-Path $PSScriptRoot 'secim.lock'
for ($i=0; $i -lt 50; $i++) { try { $fs=[IO.File]::Open($kilit,'OpenOrCreate','ReadWrite','None'); break } catch { Start-Sleep -Milliseconds 40 } }
try {
  if ((Test-Path $liste) -and ((Get-Date) - (Get-Item $liste).LastWriteTime).TotalSeconds -gt 3) { Remove-Item $liste -Force }
  $mevcut = @(); if (Test-Path $liste) { $mevcut = Get-Content $liste -Encoding UTF8 }
  if ($mevcut -notcontains $Yol) { Add-Content -Path $liste -Value $Yol -Encoding UTF8 }
} finally { if ($fs) { $fs.Close() } }
