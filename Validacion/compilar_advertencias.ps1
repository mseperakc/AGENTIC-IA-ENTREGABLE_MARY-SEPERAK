# Rebuild Debug y Release de las dos soluciones y guarda la lista de advertencias (sin duplicados).
# Uso: .\Validacion\compilar_advertencias.ps1 -Salida Validacion\advertencias-4.0.txt
param([Parameter(Mandatory = $true)][string]$Salida)

$raiz = Split-Path $PSScriptRoot -Parent
$msb = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
$soluciones = @(
    'APISEE\eFACT-COMPONENTE\EDITORAPERU.FACTELECT\EDITORAPERU.FACTELECT.sln',
    'WFRSEE\FacturacionElectronica\FacturacionElectronica.sln'
)
$advertencias = New-Object System.Collections.ArrayList
$errores = 0
foreach ($sln in $soluciones) {
    foreach ($conf in 'Debug', 'Release') {
        $log = Join-Path $env:TEMP ('msbuild-' + [IO.Path]::GetFileNameWithoutExtension($sln) + "-$conf.log")
        & $msb (Join-Path $raiz $sln) /t:Rebuild /p:Configuration=$conf /nologo /v:minimal /clp:NoSummary "/flp:LogFile=$log;Verbosity=minimal" | Out-Null
        $codigo = $LASTEXITCODE
        $lineas = Get-Content $log -Encoding UTF8
        $err = @($lineas | Where-Object { $_ -match ': error ' })
        $errores += $err.Count
        "{0} {1}: salida {2}, {3} errores" -f [IO.Path]::GetFileNameWithoutExtension($sln), $conf, $codigo, $err.Count
        $err | Select-Object -First 5 | ForEach-Object { '  ' + $_.Trim() }
        foreach ($l in $lineas | Where-Object { $_ -match ': warning ' }) {
            # Normaliza: ruta relativa, sin el proyecto entre corchetes ni la configuración
            $n = ($l.Trim() -replace '\s*\[[^\]]+\.csproj\]$', '').Replace($raiz + '\', '')
            [void]$advertencias.Add("$conf | $n")
        }
    }
}
$unicas = $advertencias | ForEach-Object { $_ -replace '^(Debug|Release) \| ', '' } | Sort-Object -Unique
$unicas | Set-Content (Join-Path $raiz $Salida) -Encoding UTF8
"Advertencias distintas: $(@($unicas).Count). Errores: $errores. Guardadas en $Salida"
"Por código: " + (($unicas | ForEach-Object { [regex]::Match($_, 'warning (\w+)').Groups[1].Value } | Group-Object | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', ')
