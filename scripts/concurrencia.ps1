<#
.SYNOPSIS
  Demuestra la concurrencia optimista (ADR-005): dos analistas intentan asignar la MISMA entrega a la vez.
  Resultado esperado: uno recibe 202 y el otro 409 (carrera real, xmin) o 422 (si llegó cuando el primero ya confirmó).
#>
param([string]$Base = "http://localhost:5080")

$entrega = [guid]::NewGuid().ToString()
$cuerpo = @{ puntoAcopio = "Acopio Garzón"; entregas = @(@{ entregaId = $entrega; fincaId = "f1000000-0000-4000-8000-000000000004"; kilos = 33; fechaEntrega = "2026-10-25" }) } | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "$Base/api/entregas/sync" -ContentType "application/json" -Body $cuerpo | Out-Null

$bloque = {
    param($Base, $Entrega)
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Base/api/lotes" -ContentType "application/json" -Body (@{ entregaIds = @($Entrega) } | ConvertTo-Json)
        "HTTP $([int]$r.StatusCode)"
    } catch {
        $resp = $_.Exception.Response; $texto = $_.ErrorDetails.Message
        if (-not $texto) { try { $s = $resp.GetResponseStream(); if ($s.CanSeek) { $s.Position = 0 }; $texto = (New-Object IO.StreamReader($s)).ReadToEnd() } catch { } }
        $titulo = if ($texto) { try { ($texto | ConvertFrom-Json).title } catch { $texto } } else { "" }
        "HTTP $([int]$resp.StatusCode) · $titulo"
    }
}
$jobs = 1..2 | ForEach-Object { Start-Job -ScriptBlock $bloque -ArgumentList $Base, $entrega }
$jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
