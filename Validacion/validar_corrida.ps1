# Valida los archivos generados en una corrida y los copia a Validacion\linea-base\<corrida>.
# Uso: validar_corrida.ps1 -Origen E:\SEE_PRUEBAS\SunatFiles -Destino <carpeta>
param([string]$Origen, [string]$Destino)
Add-Type -AssemblyName System.Security
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ns_ds = 'http://www.w3.org/2000/09/xmldsig#'
$archivos = Get-ChildItem $Origen -Recurse -File | Where-Object { $_.Name -like '20100072751-*' }
$filas = foreach ($a in $archivos | Where-Object Extension -eq '.xml') {
    $x = New-Object System.Xml.XmlDocument; $x.PreserveWhitespace = $true; $x.Load($a.FullName)
    $ns = New-Object System.Xml.XmlNamespaceManager $x.NameTable; $ns.AddNamespace('ds', $ns_ds)
    $sig = $x.SelectSingleNode('//ds:Signature', $ns)
    $valida = $false; $alg = ''
    if ($sig) {
        $sx = New-Object System.Security.Cryptography.Xml.SignedXml $x; $sx.LoadXml($sig)
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 (,[Convert]::FromBase64String($x.SelectSingleNode('//ds:X509Certificate', $ns).InnerText))
        $valida = $sx.CheckSignature($cert, $true)
        $alg = ($x.SelectSingleNode('//ds:SignatureMethod', $ns).GetAttribute('Algorithm') -replace '.*#','') + '/' + ($x.SelectSingleNode('//ds:DigestMethod', $ns).GetAttribute('Algorithm') -replace '.*#','')
    }
    $base = $a.FullName -replace '\.xml$',''
    $zip = '-'
    if (Test-Path "$base.zip") { $z = [IO.Compression.ZipFile]::OpenRead("$base.zip"); $zip = (($z.Entries | ForEach-Object Name) -join ','); $z.Dispose(); $zip = if ($zip -eq $a.Name) { 'OK' } else { "?? $zip" } }
    $pdf = '-'
    if (Test-Path "$base.pdf") { $t = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes("$base.pdf")); $pdf = "$([regex]::Matches($t,'/Type\s*/Page[^s]').Count) pag" + $(if ($t -match '/ByteRange') { ', firmado' } else { ', SIN FIRMA' }) }
    [pscustomobject]@{ Comprobante = $a.BaseName -replace '^20100072751-',''; Raiz = $x.DocumentElement.LocalName; Firma = $alg; Valida = $valida; ZIP = $zip; PDF = $pdf }
}
$filas | Sort-Object Comprobante | Format-Table -AutoSize | Out-String -Width 200

New-Item -ItemType Directory -Force $Destino | Out-Null
foreach ($a in $archivos) { Copy-Item $a.FullName (Join-Path $Destino $a.Name) -Force }
Get-ChildItem $Destino -File | Where-Object Name -ne 'SHA256.txt' | Sort-Object Name | Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $(Split-Path $_.Path -Leaf)" } | Set-Content (Join-Path $Destino 'SHA256.txt') -Encoding UTF8
"Copiados $(@($archivos).Count) archivos a $Destino (con SHA256.txt)"
