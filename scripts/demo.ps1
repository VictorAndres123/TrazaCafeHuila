<#
.SYNOPSIS
  Recorrido completo de la demo de TrazaCafé Huila (compatible con Windows PowerShell 5.1 y PowerShell 7).
.EXAMPLE
  docker compose up --build -d
  .\scripts\demo.ps1
#>
param([string]$Base = "http://localhost:5080")

$ErrorActionPreference = "Stop"

function Invocar([string]$Metodo, [string]$Ruta, $Cuerpo = $null) {
    $p = @{ Method = $Metodo; Uri = "$Base$Ruta"; UseBasicParsing = $true; ContentType = "application/json; charset=utf-8"; Headers = @{ Accept = "application/json, application/problem+json" } }
    if ($null -ne $Cuerpo) { $p.Body = [Text.Encoding]::UTF8.GetBytes(($Cuerpo | ConvertTo-Json -Depth 10)) }
    try {
        $r = Invoke-WebRequest @p
        $json = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $json }
    } catch {
        $resp = $_.Exception.Response
        if (-not $resp) { throw }
        $texto = $_.ErrorDetails.Message
        if (-not $texto) {
            # Windows PowerShell 5.1 no siempre llena ErrorDetails: leer el cuerpo desde el stream de la respuesta
            try { $s = $resp.GetResponseStream(); if ($s.CanSeek) { $s.Position = 0 }; $texto = (New-Object IO.StreamReader($s)).ReadToEnd() } catch { }
        }
        $json = $null
        if ($texto) { try { $json = $texto | ConvertFrom-Json } catch { } }
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Body = $json }
    }
}

function Titulo([string]$t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }

$F = @{
    Esperanza  = "f1000000-0000-4000-8000-000000000001"; Mirador = "f1000000-0000-4000-8000-000000000002"
    Buenavista = "f1000000-0000-4000-8000-000000000003"; Naranjos = "f1000000-0000-4000-8000-000000000004"
    Recuerdo   = "f1000000-0000-4000-8000-000000000006"
}
function Id([int]$n) { "e1000000-0000-4000-8000-{0:D12}" -f $n }
function E([int]$n, [string]$finca, [decimal]$kilos, [string]$fecha) {
    @{ entregaId = (Id $n); fincaId = $finca; kilos = $kilos; fechaEntrega = $fecha }
}
$sync = @{
    puntoAcopio = "Acopio Pitalito"
    entregas    = @(
        (E 1 $F.Esperanza 125.5 "2026-10-20"), (E 2 $F.Naranjos 80 "2026-10-21"), (E 3 $F.Esperanza 60.25 "2026-10-22"),
        (E 4 $F.Mirador 95 "2026-10-22"), (E 5 $F.Buenavista 110 "2026-10-23"), (E 6 $F.Recuerdo 40 "2026-10-23"))
}

Titulo "0. Salud"
(Invocar GET "/health/ready").Body | ConvertTo-Json -Depth 5

Titulo "1. RF1 · Sincronizar 6 entregas"
$r = Invocar POST "/api/entregas/sync" $sync
"HTTP $($r.Status) · creadas=$($r.Body.creadas) duplicadas=$($r.Body.duplicadas)"

Titulo "2. QA1 · Reenviar el mismo lote (idempotencia)"
$r = Invocar POST "/api/entregas/sync" $sync
"HTTP $($r.Status) · creadas=$($r.Body.creadas) duplicadas=$($r.Body.duplicadas)"

Titulo "3. QA1 · Mismo entregaId con kilos alterados"
$r = Invocar POST "/api/entregas/sync" @{ puntoAcopio = "Acopio Pitalito"; entregas = @((E 1 $F.Esperanza 1255 "2026-10-20")) }
"HTTP $($r.Status) · $($r.Body.title): $($r.Body.entregasEnConflicto -join ', ')"

Titulo "4. RF2 · Conformar lotes A, B y C (respuesta 202, verificación asíncrona)"
$lotes = [ordered]@{}
$lotes["A (Esperanza + Naranjos)"]   = Invocar POST "/api/lotes" @{ entregaIds = @((Id 1), (Id 2)) }
$lotes["B (Esperanza + Mirador)"]    = Invocar POST "/api/lotes" @{ entregaIds = @((Id 3), (Id 4)) }
$lotes["C (Buenavista, pérdida 2019)"] = Invocar POST "/api/lotes" @{ entregaIds = @((Id 5)) }
foreach ($k in $lotes.Keys) { "Lote $k → HTTP $($lotes[$k].Status) · $($lotes[$k].Body.codigo) · $($lotes[$k].Body.estado)" }

Titulo "5. Reglas: entrega ya asignada y finca sin consentimiento"
$r = Invocar POST "/api/lotes" @{ entregaIds = @((Id 1)) };  "Reusar entrega → HTTP $($r.Status) · $($r.Body.detail)"
$r = Invocar POST "/api/lotes" @{ entregaIds = @((Id 6)) };  "Sin consentimiento → HTTP $($r.Status) · $($r.Body.detail)"

Titulo "6. Consistencia eventual: esperando a Cumplimiento..."
$limite = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 500
    $pendientes = @((Invocar GET "/api/lotes").Body | Where-Object { $_.estado -eq "PendienteVerificacion" }).Count
} while ($pendientes -gt 0 -and (Get-Date) -lt $limite)

foreach ($k in $lotes.Keys) {
    $g = (Invocar GET "/api/lotes/$($lotes[$k].Body.loteId)/genealogia").Body
    "Lote $k → $($g.estado) en $($g.segundosHastaVerificacion) s · $($g.kilosTotales) kg"
    foreach ($f in $g.fincas) { "    $($f.codigo) $($f.nombre): conforme=$($f.conforme) · $($f.detalleVerificacion)" }
}
