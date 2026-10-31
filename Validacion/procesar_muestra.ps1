# Procesa la muestra fija de la línea base (planificación.md, fase 2A) a través de WFRSEE,
# simulando los clics (postbacks HTTP) que haría el operador: Consultar, marcar el comprobante y Procesar.
#
# REQUISITOS: configuración de pruebas (Enviar*=NO, sin endpoint OSE, GrabarOracle=NO), la API en el
# puerto 52084 y WFRSEE en el que indique -Url. El script lo comprueba antes de empezar.
# No muestra datos de clientes: solo el comprobante y los mensajes de la página.
#
# Uso: .\Validacion\procesar_muestra.ps1 [-Url http://localhost:56799] [-Casos 1,10] [-Canal sin-envio|beta-sunat]
#   -Canal sin-envio (por defecto): exige Enviar*=NO. No sale nada del equipo.
#   -Canal beta-sunat: envío REAL al entorno beta de SUNAT (fase 2E). Exige la configuración de
#                      Validacion\modo_envio.ps1 -Modo beta-sunat.
param([string]$Url = 'http://localhost:56799', [int[]]$Casos = @(), [ValidateSet('sin-envio', 'beta-sunat')][string]$Canal = 'sin-envio')

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Web

# ---------- Seguridad: solo con la configuración de pruebas ----------
$api = [xml](Get-Content -Raw -Encoding UTF8 (Join-Path $raiz 'APISEE\eFACT-COMPONENTE\EDITORAPERU.FACTELECT\FACTELECT.SERVICE\Web.config'))
$wfr = [xml](Get-Content -Raw -Encoding UTF8 (Join-Path $raiz 'WFRSEE\FacturacionElectronica\FacturacionElectronica\Web.config'))
$envioActivo = @($api.configuration.appSettings.add | Where-Object { $_.key -like 'Enviar*' -and $_.value -ne 'NO' })
$endpointOse = @($api.configuration.'system.serviceModel'.client.endpoint | Where-Object { $_.name -like '*OSE*' })
$grabar = ($wfr.configuration.appSettings.add | Where-Object { $_.key -eq 'GrabarOracle' }).value
$enviarOse = ($wfr.configuration.appSettings.add | Where-Object { $_.key -eq 'EnviarOSE' }).value
if ($Canal -eq 'sin-envio') {
    if ($envioActivo.Count -gt 0 -or $endpointOse.Count -gt 0 -or $grabar -ne 'NO') {
        throw 'La configuración no es la de pruebas (Enviar*, endpoint OSE o GrabarOracle). No se procesa nada.'
    }
} else {
    # beta-sunat: los endpoints de envío deben ser los de SUNAT beta, sin OSE ni resumen diario, y sin grabar en Oracle
    $eps = @($api.configuration.'system.serviceModel'.client.endpoint | Where-Object { 'CurrencyServiceSoap', 'BillServicePort' -contains $_.name })
    $noBeta = @($eps | Where-Object { ([uri]$_.address).Host -ne 'e-beta.sunat.gob.pe' })
    $resumen = ($api.configuration.appSettings.add | Where-Object { $_.key -eq 'EnviarResumenDiario' }).value
    if ($eps.Count -ne 2 -or $noBeta.Count -gt 0 -or $endpointOse.Count -gt 0 -or $grabar -ne 'NO' -or $enviarOse -ne 'NO' -or $resumen -ne 'NO') {
        throw 'La configuración no es la de beta SUNAT (modo_envio.ps1 -Modo beta-sunat). No se procesa nada.'
    }
}

# ---------- Muestra fija (planificación.md, fase 2A) ----------
# Tipo = filtro de la consulta. Debe ser el mismo de la línea base: la consulta del detalle no tiene ORDER BY
# y el orden de las líneas en el XML cambia con el filtro (R-34). Los casos 2 y 6 se consultaron con "Todos".
$comprobantes = @(
    @{ Caso = 1;  Serie = 'F081'; Numero = '26782';  Fecha = '20/10/2026'; PV = '081'; Estado = '1'; Tipo = 'Factura' }
    @{ Caso = 2;  Serie = 'F079'; Numero = '25805';  Fecha = '19/10/2026'; PV = '079'; Estado = '0'; Tipo = 'Todos' }
    @{ Caso = 3;  Serie = 'F002'; Numero = '301438'; Fecha = '19/10/2026'; PV = '002'; Estado = '0'; Tipo = 'Factura' }
    @{ Caso = 4;  Serie = 'F081'; Numero = '22979';  Fecha = '19/06/2026'; PV = '081'; Estado = '0'; Tipo = 'Factura' }
    @{ Caso = 5;  Serie = 'F005'; Numero = '210574'; Fecha = '29/09/2026'; PV = '005'; Estado = '0'; Tipo = 'Factura' }
    @{ Caso = 6;  Serie = 'B079'; Numero = '5341';   Fecha = '19/10/2026'; PV = '079'; Estado = '0'; Tipo = 'Todos' }
    @{ Caso = 7;  Serie = 'F005'; Numero = '29347';  Fecha = '19/10/2026'; PV = '005'; Estado = '0'; Tipo = 'Nota de credi' }
    @{ Caso = 8;  Serie = 'F005'; Numero = '784';    Fecha = '15/10/2026'; PV = '005'; Estado = '0'; Tipo = 'Nota de debit' }
    @{ Caso = 12; Serie = 'F005'; Numero = '203655'; Fecha = '08/06/2026'; PV = '005'; Estado = '0'; Tipo = 'Factura' }
)
$retencion = @{ Caso = 9;  Serie = 'R001'; Numero = '2004'; Fecha = '15/10/2026'; Estado = '0' }
$baja      = @{ Caso = 10; Fecha = '05/10/2026' }
$reversion = @{ Caso = 11; Fecha = '25/10/2023' }
function Incluido($caso) { return ($Casos.Count -eq 0 -or $Casos -contains $caso) }
$comprobantes = @($comprobantes | Where-Object { Incluido $_.Caso })

# ---------- Utilidades de postback ----------
function Ocultos($html) {
    $h = @{}
    foreach ($n in '__VIEWSTATE','__VIEWSTATEGENERATOR','__VIEWSTATEENCRYPTED','__EVENTVALIDATION','__EVENTTARGET','__EVENTARGUMENT') {
        $m = [regex]::Match($html, 'id="' + $n + '" value="([^"]*)"')
        if ($m.Success) { $h[$n] = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value) }
    }
    return $h
}

function Enviar($sesion, $pagina, $html, $campos) {
    $todo = Ocultos $html
    foreach ($k in $campos.Keys) { $todo[$k] = $campos[$k] }
    # HttpUtility.UrlEncode no tiene el límite de largo de Uri.EscapeDataString (el estado de vista puede ser grande)
    $cuerpo = ($todo.GetEnumerator() | ForEach-Object { [System.Web.HttpUtility]::UrlEncode($_.Key) + '=' + [System.Web.HttpUtility]::UrlEncode([string]$_.Value) }) -join '&'
    try {
        $r = Invoke-WebRequest "$Url/$pagina" -WebSession $sesion -Method Post -Body $cuerpo -ContentType 'application/x-www-form-urlencoded' -UseBasicParsing -TimeoutSec 900
    } catch {
        # Error 500: devuelve la primera línea de la excepción de ASP.NET
        $detalle = ''
        if ($_.Exception.Response) {
            $e = [System.Net.WebUtility]::HtmlDecode((New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd())
            $detalle = [regex]::Match($e, '\[\w*Exception[^\]\r\n]*').Value
        }
        throw "Error de servidor en $pagina $detalle"
    }
    return [string]$r.Content
}

function Etiqueta($html, $id) {
    $m = [regex]::Match($html, 'id="MainContent_' + $id + '"[^>]*>([^<]*)<')
    if ($m.Success) { return [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value).Trim() } else { return '' }
}

# Nombre de la casilla de la fila con esa serie y número (null si no está en la página visible)
function Casilla($html, $grilla, $serie, $numero) {
    $tabla = [regex]::Match($html, '(?s)<table[^>]*data-grilla="' + $grilla + '".*?</table>').Value
    foreach ($fila in [regex]::Matches($tabla, '(?s)<tr.*?</tr>')) {
        $f = $fila.Value
        if ($f -match ('>\s*' + $serie + '\s*<') -and $f -match ('>\s*' + $numero + '\s*<')) {
            $m = [regex]::Match($f, 'name="([^"]*chkAccept)"')
            if ($m.Success) { return $m.Groups[1].Value }
        }
    }
    return $null
}

$p = 'ctl00$MainContent$'

# Textos de las celdas de la fila con esa serie y número (o de la primera fila de datos si no se indica)
function Celdas($html, $grilla, $serie, $numero) {
    $tabla = [regex]::Match($html, '(?s)<table[^>]*data-grilla="' + $grilla + '".*?</table>').Value
    foreach ($fila in [regex]::Matches($tabla, '(?s)<tr.*?</tr>')) {
        $f = $fila.Value
        if ($f -notmatch '<td') { continue }
        if (-not $serie -or ($f -match ('>\s*' + $serie + '\s*<') -and $f -match ('>\s*' + $numero + '\s*<'))) {
            return @([regex]::Matches($f, '(?s)<td[^>]*>(.*?)</td>') | ForEach-Object { [System.Net.WebUtility]::HtmlDecode(($_.Groups[1].Value -replace '<[^>]+>', '')).Trim() })
        }
    }
    return @()
}

# Recorre las páginas de la grilla (50 filas por página) hasta encontrar la fila.
# Devuelve el HTML de la página donde está y el nombre de su casilla.
function BuscarEnPaginas($sesion, $pagina, $html, $grilla, $filtros, $serie, $numero) {
    $casilla = Casilla $html $grilla $serie $numero
    $tabla = [regex]::Match($html, '(?s)<table[^>]*data-grilla="' + $grilla + '".*?</table>').Value
    $selector = [regex]::Match($tabla, 'name="([^"]*paginasDropDownList)"').Groups[1].Value
    $paginas = [regex]::Matches([regex]::Match($tabla, '(?s)<select[^>]*paginasDropDownList.*?</select>').Value, '<option').Count
    $n = 2
    while (-not $casilla -and $selector -and $n -le $paginas) {
        $cambio = $filtros.Clone(); $cambio['__EVENTTARGET'] = $selector; $cambio[$selector] = [string]$n
        $html = Enviar $sesion $pagina $html $cambio
        $casilla = Casilla $html $grilla $serie $numero
        $tabla = [regex]::Match($html, '(?s)<table[^>]*data-grilla="' + $grilla + '".*?</table>').Value
        $selector = [regex]::Match($tabla, 'name="([^"]*paginasDropDownList)"').Groups[1].Value
        $n++
    }
    return @{ Html = $html; Casilla = $casilla }
}
$resultados = New-Object System.Collections.ArrayList
function Anotar($caso, $doc, $mensaje) { [void]$resultados.Add([pscustomobject]@{ Caso = $caso; Comprobante = $doc; Resultado = $mensaje }) }

# ---------- Comprobantes ----------
foreach ($c in $comprobantes) {
    $doc = "$($c.Serie)-$($c.Numero)"
    try {
        $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $html = [string](Invoke-WebRequest "$Url/ProcesarComprobantes.aspx" -WebSession $s -UseBasicParsing -TimeoutSec 300).Content
        # El valor de las opciones de punto de venta lleva un espacio delante (" 005")
        $pv = [regex]::Match($html, 'value="(\s*' + $c.PV + ')"').Groups[1].Value
        $inicial = $html
        $casilla = $null
        # Primero en el estado indicado; si no está (por ejemplo, porque ya se procesó en producción), en el otro
        foreach ($estado in @($c.Estado, $(if ($c.Estado -eq '0') { '1' } else { '0' }))) {
            $filtros = @{ ($p + 'ddlbPuntoVenta1') = $pv; ($p + 'ddlbPuntoVenta2') = $pv; ($p + 'txtFechaInicio') = $c.Fecha; ($p + 'txtFechaFin') = $c.Fecha;
                          ($p + 'rbEstado') = $estado; ($p + 'rblTipoComprobante') = $c.Tipo }
            $consulta = $filtros.Clone(); $consulta[$p + 'btnConsultar'] = 'Consultar'
            $html = Enviar $s 'ProcesarComprobantes.aspx' $inicial $consulta
            $r = BuscarEnPaginas $s 'ProcesarComprobantes.aspx' $html 'comprobantes' $filtros $c.Serie $c.Numero
            $html = $r.Html; $casilla = $r.Casilla
            if ($casilla) { break }
        }
        if (-not $casilla) { Anotar $c.Caso $doc ("NO ENCONTRADO en la consulta (" + (Etiqueta $html 'lblCant') + ")"); continue }
        $proceso = $filtros.Clone(); $proceso[$casilla] = 'on'; $proceso[$p + 'btnProcesar'] = 'Procesar'
        $html = Enviar $s 'ProcesarComprobantes.aspx' $html $proceso
        # Columnas de la grilla: ... 8 Estado, 9 CDR, 10 Mensaje SUNAT/OSE
        $celdas = Celdas $html 'comprobantes' $c.Serie $c.Numero
        $respuesta = if ($celdas.Count -ge 11) { "estado=$($celdas[8]) CDR=$($celdas[9]) · $($celdas[10])" } else { '' }
        Anotar $c.Caso $doc ((@((Etiqueta $html 'lblCantRegAcep'), (Etiqueta $html 'lblCantRegRec'), (Etiqueta $html 'lblCant'), $respuesta) | Where-Object { $_ }) -join ' · ')
    } catch { Anotar $c.Caso $doc ("ERROR: " + $_.Exception.Message) }
}

# ---------- Comunicación de baja ----------
if (Incluido $baja.Caso) { try {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $html = [string](Invoke-WebRequest "$Url/ProcesarComprobantes.aspx" -WebSession $s -UseBasicParsing -TimeoutSec 300).Content
    $html = Enviar $s 'ProcesarComprobantes.aspx' $html @{ ($p + 'txtFechaCB') = $baja.Fecha; ($p + 'btnConsultarCB') = 'Consultar' }
    $html = Enviar $s 'ProcesarComprobantes.aspx' $html @{ ($p + 'txtFechaCB') = $baja.Fecha; ($p + 'btnProcesarCB') = 'Procesar baja' }
    # Columnas de la grilla de bajas: ... 6 CDR, 7 Ticket
    $celdas = Celdas $html 'bajas' $null $null
    $ticket = if ($celdas.Count -ge 8) { " · CDR=$($celdas[6]) ticket=$($celdas[7])" } else { '' }
    Anotar $baja.Caso "Baja $($baja.Fecha)" ((Etiqueta $html 'lblComunicacionBaja') + $ticket)
} catch { Anotar $baja.Caso "Baja $($baja.Fecha)" ("ERROR: " + $_.Exception.Message) } }

# ---------- Retención ----------
$doc = "$($retencion.Serie)-$($retencion.Numero)"
if (Incluido $retencion.Caso) { try {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $html = [string](Invoke-WebRequest "$Url/ProcesarRetenciones.aspx" -WebSession $s -UseBasicParsing -TimeoutSec 300).Content
    $filtros = @{ ($p + 'txtFechaInicio') = $retencion.Fecha; ($p + 'txtFechaFin') = $retencion.Fecha; ($p + 'rbEstado') = $retencion.Estado; ($p + 'txtFechaReversion') = $reversion.Fecha }
    $consulta = $filtros.Clone(); $consulta[$p + 'btnConsultar'] = 'Consultar'
    $html = Enviar $s 'ProcesarRetenciones.aspx' $html $consulta
    $r = BuscarEnPaginas $s 'ProcesarRetenciones.aspx' $html 'retenciones' $filtros $retencion.Serie $retencion.Numero
    $html = $r.Html; $casilla = $r.Casilla
    if (-not $casilla) { Anotar $retencion.Caso $doc ("NO ENCONTRADA en la consulta (" + (Etiqueta $html 'lblCant') + ")") }
    else {
        $proceso = $filtros.Clone(); $proceso[$casilla] = 'on'; $proceso[$p + 'btnProcesar'] = 'Procesar'
        $html = Enviar $s 'ProcesarRetenciones.aspx' $html $proceso
        Anotar $retencion.Caso $doc ((@((Etiqueta $html 'lblCantRegAcep'), (Etiqueta $html 'lblCantRegRec'), (Etiqueta $html 'lblCant')) | Where-Object { $_ }) -join ' · ')
    }
} catch { Anotar $retencion.Caso $doc ("ERROR: " + $_.Exception.Message) } }

# ---------- Reversión ----------
if (Incluido $reversion.Caso) { try {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $html = [string](Invoke-WebRequest "$Url/ProcesarRetenciones.aspx" -WebSession $s -UseBasicParsing -TimeoutSec 300).Content
    $html = Enviar $s 'ProcesarRetenciones.aspx' $html @{ ($p + 'txtFechaReversion') = $reversion.Fecha; ($p + 'btnConsultarReversion') = 'Consultar' }
    $html = Enviar $s 'ProcesarRetenciones.aspx' $html @{ ($p + 'txtFechaReversion') = $reversion.Fecha; ($p + 'btnProcesarReversion') = 'Procesar' }
    Anotar $reversion.Caso "Reversión $($reversion.Fecha)" (Etiqueta $html 'lblReversion')
} catch { Anotar $reversion.Caso "Reversión $($reversion.Fecha)" ("ERROR: " + $_.Exception.Message) } }

$resultados | Sort-Object Caso | Format-Table -AutoSize -Wrap | Out-String -Width 200
