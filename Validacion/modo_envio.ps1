# Cambia el modo de envío de la copia de pruebas (fase 2E) y verifica que sea seguro.
#
#   -Modo pruebas     Enviar*=NO en la API y EnviarOSE=SI en WFRSEE (estado de la fase 0). No envía nada.
#   -Modo beta-sunat  Envío REAL al entorno BETA de SUNAT por el canal directo: Enviar*=SI en la API
#                     (salvo EnviarResumenDiario) y EnviarOSE=NO en WFRSEE.
#   -Modo ver         Solo muestra el estado actual.
#
# Antes de activar beta-sunat comprueba que todos los endpoints de envío apunten a e-beta.sunat.gob.pe,
# que no haya endpoint OSE y que GrabarOracle=NO. No muestra credenciales.
param([Parameter(Mandatory = $true)][ValidateSet('pruebas', 'beta-sunat', 'ver')][string]$Modo)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$apiCfg = Join-Path $raiz 'APISEE\eFACT-COMPONENTE\EDITORAPERU.FACTELECT\FACTELECT.SERVICE\Web.config'
$wfrCfg = Join-Path $raiz 'WFRSEE\FacturacionElectronica\FacturacionElectronica\Web.config'
$clavesEnvio = 'EnviarFactura', 'EnviarBoleta', 'EnviarCredito', 'EnviarDebito', 'EnviarComunicacionBaja', 'EnviarRetencion', 'EnviarReversion'
# Endpoints que envían; BillConsultServicePort solo consulta CDR y no lo usa Procesar
$endpointsEnvio = 'CurrencyServiceSoap', 'BillServicePort'

function Estado {
    $api = [xml](Get-Content -Raw -Encoding UTF8 $apiCfg)
    $wfr = [xml](Get-Content -Raw -Encoding UTF8 $wfrCfg)
    $ap = @{}; $api.configuration.appSettings.add | ForEach-Object { $ap[$_.key] = $_.value }
    $wp = @{}; $wfr.configuration.appSettings.add | ForEach-Object { $wp[$_.key] = $_.value }
    $eps = @($api.configuration.'system.serviceModel'.client.endpoint)
    [pscustomobject]@{
        Envio        = ($clavesEnvio + 'EnviarResumenDiario' | ForEach-Object { "$_=$($ap[$_])" }) -join ' '
        EnviarOSE    = $wp['EnviarOSE']
        GrabarOracle = $wp['GrabarOracle']
        Endpoints    = ($eps | ForEach-Object { "$($_.name)=$(([uri]$_.address).Host)" }) -join ' '
        EnvioBeta    = @($eps | Where-Object { $endpointsEnvio -contains $_.name -and ([uri]$_.address).Host -ne 'e-beta.sunat.gob.pe' }).Count -eq 0 -and
                       @($eps | Where-Object { $endpointsEnvio -contains $_.name }).Count -eq $endpointsEnvio.Count
        SinOse       = @($eps | Where-Object { $_.name -like '*OSE*' }).Count -eq 0
        ApiValores   = $ap
    }
}

function Fijar($ruta, $clave, $valor) {
    $b = [IO.File]::ReadAllBytes($ruta); $bom = ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB)
    $t = [IO.File]::ReadAllText($ruta, [Text.Encoding]::UTF8)
    $patron = '(<add key="' + $clave + '"\s+value\s*=\s*")(SI|NO)(")'
    if ([regex]::Matches($t, $patron).Count -ne 1) { throw "No se encontró una sola clave activa $clave en $ruta" }
    $t = [regex]::Replace($t, $patron, '${1}' + $valor + '${3}')
    [IO.File]::WriteAllText($ruta, $t, (New-Object Text.UTF8Encoding $bom))
}

$e = Estado
if ($Modo -ne 'ver') {
    if ($e.GrabarOracle -ne 'NO') { throw 'GrabarOracle no es NO: no se cambia nada.' }
    if ($Modo -eq 'beta-sunat') {
        if (-not $e.EnvioBeta) { throw 'Algún endpoint de envío no apunta a e-beta.sunat.gob.pe: no se activa el envío.' }
        if (-not $e.SinOse) { throw 'Hay un endpoint OSE definido: este modo es solo para el canal SUNAT beta.' }
        foreach ($c in $clavesEnvio) { Fijar $apiCfg $c 'SI' }
        Fijar $apiCfg 'EnviarResumenDiario' 'NO'
        Fijar $wfrCfg 'EnviarOSE' 'NO'
    } else {
        foreach ($c in $clavesEnvio + 'EnviarResumenDiario') { Fijar $apiCfg $c 'NO' }
        Fijar $wfrCfg 'EnviarOSE' 'SI'
    }
    $e = Estado
}
"Modo: $Modo"
"  API:    $($e.Envio)"
"  WFRSEE: EnviarOSE=$($e.EnviarOSE) GrabarOracle=$($e.GrabarOracle)"
"  Endpoints: $($e.Endpoints)"
"  Envío solo a beta: $($e.EnvioBeta)   Sin endpoint OSE: $($e.SinOse)"
