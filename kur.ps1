# RoboKopya kurulumu: dosyaları %LOCALAPPDATA%\RoboKopya altına kopyalar ve
# sağ tık menüsüne "Robocopy ile kopyala" / "Robocopy ile yapıştır" ekler.
# Yalnızca HKCU altına yazar, yönetici izni gerekmez.
param([string]$Exe = (Join-Path $PSScriptRoot 'out\RoboKopya.exe'))
$ErrorActionPreference = 'Stop'
$hedef = Join-Path $env:LOCALAPPDATA 'RoboKopya'
New-Item -ItemType Directory -Force $hedef | Out-Null
if (-not (Test-Path $Exe)) { throw "RoboKopya.exe bulunamadı: $Exe. Önce derle: dotnet publish src\RoboKopya -c Release -o out" }
Copy-Item $Exe $hedef -Force
Copy-Item (Join-Path $PSScriptRoot 'kabuk\*') $hedef -Force

$kopyala = "wscript.exe `"$hedef\kopyala.vbs`" `"%1`""
$yapistir = "`"$hedef\RoboKopya.exe`" `"%V`""
function Ekle($anahtar, $yazi, $simge, $komut) {
  $k = "HKCU:\Software\Classes\$anahtar"
  New-Item -Path "$k\command" -Force | Out-Null
  Set-Item -LiteralPath $k -Value $yazi
  New-ItemProperty -LiteralPath $k -Name Icon -Value $simge -Force | Out-Null
  Set-Item -LiteralPath "$k\command" -Value $komut
}
Ekle '*\shell\RoboKopyala'                        'Robocopy ile kopyala'  'imageres.dll,-5302' $kopyala
Ekle 'Directory\shell\RoboKopyala'                'Robocopy ile kopyala'  'imageres.dll,-5302' $kopyala
Ekle 'Directory\shell\RoboYapistir'               'Robocopy ile yapıştır' 'imageres.dll,-5301' $yapistir
Ekle 'Directory\Background\shell\RoboYapistir'    'Robocopy ile yapıştır' 'imageres.dll,-5301' $yapistir
Ekle 'Drive\shell\RoboYapistir'                   'Robocopy ile yapıştır' 'imageres.dll,-5301' $yapistir
Write-Host "RoboKopya kuruldu: $hedef"
