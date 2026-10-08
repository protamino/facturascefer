# Instala (o reinstala) el servicio de Windows «FacturasCefer Importador».
# Ejecutar en PowerShell COMO ADMINISTRADOR desde la carpeta donde está FacturasCefer.Importador.exe:
#   .\instalar-servicio.ps1 -Cuenta 'DOMINIO\usuario'
# La cuenta debe poder escribir en la carpeta de red de los PDF (Repositorio.RutaUnc).
param(
    [Parameter(Mandatory = $true)][string]$Cuenta
)
$ErrorActionPreference = 'Stop'
$nombre = 'FacturasCefer.Importador'
$exe = Join-Path $PSScriptRoot 'FacturasCefer.Importador.exe'
if (-not (Test-Path $exe)) { throw "No se encuentra $exe" }
if (-not (Test-Path (Join-Path $PSScriptRoot 'appsettings.json'))) { throw 'Falta appsettings.json (copia appsettings.example.json y rellénalo).' }

$existente = Get-Service -Name $nombre -ErrorAction SilentlyContinue
if ($existente) {
    Write-Host 'Parando y eliminando el servicio anterior...'
    if ($existente.Status -ne 'Stopped') { Stop-Service $nombre -Force }
    sc.exe delete $nombre | Out-Null
    Start-Sleep -Seconds 2
}

$cred = Get-Credential -UserName $Cuenta -Message "Contraseña de $Cuenta (cuenta con la que se ejecuta el servicio)"
New-Service -Name $nombre -DisplayName 'FacturasCefer Importador' -BinaryPathName "`"$exe`"" `
    -Description 'Importa a FACTURASCEFER las facturas de proveedores que llegan por correo (Google Drive CEFER/Facturas).' `
    -StartupType Automatic -Credential $cred | Out-Null

# Reiniciar automáticamente si se cae (1 min, 1 min, 5 min).
sc.exe failure $nombre reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null

Start-Service $nombre
Get-Service $nombre | Format-Table Name, Status, StartType
Write-Host "Log: $(Join-Path $PSScriptRoot 'logs')"
