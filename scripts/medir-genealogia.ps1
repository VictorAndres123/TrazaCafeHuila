<#
.SYNOPSIS
  QA3 · Mide la latencia de GET /api/lotes/{id}/genealogia con un lote de N entregas (por defecto 200).
  También reporta el tiempo de sincronización y la ventana de consistencia eventual (QA2).
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\medir-genealogia.ps1
#>
param([string]$Base = "http://localhost:5080", [int]$Entregas = 200, [int]$Repeticiones = 50)
$ErrorActionPreference = "Stop"

$fincas = @("f1000000-0000-4000-8000-000000000001", "f1000000-0000-4000-8000-000000000004", "f1000000-0000-4000-8000-000000000005")
$lista = 1..$Entregas | ForEach-Object {
    @{ entregaId = [guid]::NewGuid().ToString(); fincaId = $fincas[$_ % 3]; kilos = 50.5; fechaEntrega = "2026-10-25" }
}

function Post([string]$ruta, $cuerpo) {
    $bytes = [Text.Encoding]::UTF8.GetBytes(($cuerpo | ConvertTo-Json -Depth 6))
    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Base$ruta" -ContentType "application/json; charset=utf-8" -Body $bytes
}

$sw = [Diagnostics.Stopwatch]::StartNew()
$r = Post "/api/entregas/sync" @{ puntoAcopio = "Acopio Medición"; entregas = $lista }
$sw.Stop()
$sync = $r.Content | ConvertFrom-Json
"Sincronización de $Entregas entregas: HTTP $([int]$r.StatusCode) · creadas=$($sync.creadas) · $($sw.ElapsedMilliseconds) ms"

$lote = (Post "/api/lotes" @{ entregaIds = @($lista | ForEach-Object { $_.entregaId }) }).Content | ConvertFrom-Json
"Lote $($lote.codigo) conformado (202) con $Entregas entregas · $($lote.kilosTotales) kg"

$url = "$Base/api/lotes/$($lote.loteId)/genealogia"
do { Start-Sleep -Milliseconds 300; $g = Invoke-RestMethod $url } while ($g.estado -eq "PendienteVerificacion")
"Estado final: $($g.estado) · ventana de consistencia eventual (QA2): $($g.segundosHastaVerificacion) s"

1..5 | ForEach-Object { Invoke-WebRequest -UseBasicParsing $url | Out-Null }   # calentamiento
$tiempos = 1..$Repeticiones | ForEach-Object {
    $t = [Diagnostics.Stopwatch]::StartNew(); Invoke-WebRequest -UseBasicParsing $url | Out-Null; $t.Stop(); $t.Elapsed.TotalMilliseconds
} | Sort-Object

function P([double]$p) { $tiempos[[math]::Max(0, [math]::Ceiling($p * $tiempos.Count) - 1)] }
""
"QA3 · GET genealogía ($Entregas entregas, $Repeticiones peticiones, medido desde el cliente)"
"  mín = {0:N1} ms · p50 = {1:N1} ms · p95 = {2:N1} ms · máx = {3:N1} ms · meta < 500 ms" -f $tiempos[0], (P 0.50), (P 0.95), $tiempos[-1]
