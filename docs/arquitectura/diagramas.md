# TrazaCafé Huila — Diagramas de arquitectura (Mermaid)

> Se renderizan directamente en GitHub. Para exportar a PNG: `npx @mermaid-js/mermaid-cli -i diagramas.md -o out.md`.

## Figura 1. C4 — Contexto

```mermaid
flowchart TB
  operario["Operario de acopio<br/>[Persona]<br/>Registra entregas de café, a veces sin conexión"]
  analista["Analista de cumplimiento del exportador<br/>[Persona]<br/>Conforma lotes y consulta su trazabilidad"]
  sistema["TrazaCafé Huila<br/>[Sistema de software]<br/>Trazabilidad finca → lote y verificación de no deforestación"]
  geo["Proveedor de análisis geoespacial<br/>[Sistema externo · simulado]<br/>Áreas con pérdida de bosque y su fecha"]
  ue["Operador en la UE<br/>[Externo · fuera de alcance]<br/>Presenta la declaración de diligencia debida"]

  operario -->|"Sincroniza entregas<br/>[HTTPS/JSON]"| sistema
  analista -->|"Conforma lotes y consulta genealogía<br/>[HTTPS/JSON]"| sistema
  sistema -->|"Consulta pérdidas de bosque por finca"| geo
  sistema -.->|"Entrega el insumo de la declaración<br/>(geolocalización + genealogía)"| ue

  classDef person fill:#08427b,color:#fff,stroke:#052e56
  classDef system fill:#1168bd,color:#fff,stroke:#0b4884
  classDef external fill:#999,color:#fff,stroke:#6b6b6b
  class operario,analista person
  class sistema system
  class geo,ue external
```

## Figura 2. C4 — Contenedores

```mermaid
flowchart LR
  operario["Operario de acopio"]
  analista["Analista de cumplimiento"]

  subgraph sys["TrazaCafé Huila"]
    acopio["Acopio y Lotes API<br/>[ASP.NET Core 10]<br/>Entregas, lotes, balance de masa, genealogía"]
    dbA[("acopio_db<br/>[PostgreSQL 17]<br/>Entregas, lotes, outbox, inbox, vw_genealogia_lote")]
    mq{{"RabbitMQ 4<br/>[exchange topic trazacafe.events]"}}
    cump["Cumplimiento Worker<br/>[.NET 10 + endpoint de salud]<br/>Verifica fincas con NetTopologySuite"]
    dbC[("cumplimiento_db<br/>[PostgreSQL 17]<br/>Réplica de fincas, verificaciones, outbox, inbox")]
  end

  operario -->|"POST /api/entregas/sync"| acopio
  analista -->|"POST /api/lotes<br/>GET /api/lotes/{id}/genealogia"| acopio
  acopio -->|"EF Core (escritura) · Npgsql (lectura)"| dbA
  acopio -->|"FincaRegistrada, EntregaRegistrada, LoteConformado<br/>(vía outbox)"| mq
  mq -->|"LoteVerificado → cola acopio.eventos"| acopio
  mq -->|"FincaRegistrada, LoteConformado → cola cumplimiento.eventos"| cump
  cump -->|"LoteVerificado (vía outbox)"| mq
  cump -->|"EF Core"| dbC
```

## Figura 3. Componentes — Acopio y Lotes (hexagonal + CQRS ligero)

```mermaid
flowchart LR
  subgraph sgIn["Adaptadores de entrada"]
    ep1["EntregasEndpoints<br/>POST /api/entregas/sync"]
    ep2["LotesEndpoints<br/>POST /api/lotes · GET /api/lotes"]
    ep3["LotesEndpoints<br/>GET /api/lotes/{id}/genealogia"]
    cons["LoteVerificadoManejador<br/>[RabbitMqConsumidor]"]
  end
  subgraph sgApp["Aplicación"]
    c1["SincronizarEntregasHandler<br/>(comando)"]
    c2["ConformarLoteHandler<br/>(comando)"]
    c3["AplicarVerificacionHandler<br/>(comando)"]
    q1["ConsultarGenealogiaHandler<br/>(consulta)"]
  end
  subgraph sgDom["Dominio"]
    lote["Lote (agregado)<br/>estados · balance de masa · I1–I5"]
    ent["Entrega<br/>hash canónico"]
    finca["Finca<br/>consentimiento"]
  end
  subgraph sgPorts["Puertos de salida"]
    p1["IEntregaRepository · IFincaRepository · ILoteRepository"]
    p3["IUnitOfWork · IEventOutbox · IInbox"]
    p4["IGenealogiaReadModel"]
  end
  subgraph sgOut["Adaptadores de salida"]
    ef["AcopioDbContext + repositorios EF<br/>xmin · outbox · inbox"]
    sql["NpgsqlGenealogiaReadModel<br/>vw_genealogia_lote"]
    relay["OutboxRelay&lt;AcopioDbContext&gt;<br/>→ RabbitMQ"]
  end

  ep1 --> c1
  ep2 --> c2
  ep3 --> q1
  cons --> c3
  c1 --> ent
  c2 --> lote
  c2 --> finca
  c3 --> lote
  c1 --> p1
  c2 --> p1
  c3 --> p1
  c1 --> p3
  c2 --> p3
  c3 --> p3
  q1 --> p4
  ef -.->|implementa| p1
  ef -.->|implementa| p3
  sql -.->|implementa| p4
  ef -->|"tabla outbox_messages"| relay
```

## Figura 4. Despliegue local (Docker Compose)

```mermaid
flowchart TB
  cli["Swagger UI · http/trazacafe.http · scripts/demo.ps1"]
  subgraph host["Equipo del desarrollador · Docker Desktop"]
    subgraph net["Red de compose (trazacafe_default)"]
      pg[("postgres<br/>postgres:17-alpine<br/>acopio_db + cumplimiento_db<br/>host :55432")]
      rmq{{"rabbitmq<br/>rabbitmq:4-management-alpine<br/>:5672 · consola :15672"}}
      api["acopio-api<br/>:8080 → host :5080<br/>/swagger · /health/ready"]
      wk["cumplimiento-worker<br/>:8080 → host :5081<br/>/health/ready · geodata embebida"]
    end
    vol[("volumen pgdata")]
    init["db/init/*.sql<br/>(esquema + seed)"]
  end
  cli -->|"HTTP :5080"| api
  api --> pg
  api --> rmq
  wk --> pg
  wk --> rmq
  pg --- vol
  init -.->|"primer arranque"| pg
```

## Figura 5. Secuencia — Sincronización idempotente (QA1)

```mermaid
sequenceDiagram
  autonumber
  actor Op as Operario (cliente offline)
  participant API as Acopio y Lotes API
  participant H as SincronizarEntregasHandler
  participant DB as acopio_db

  Op->>API: POST /api/entregas/sync {puntoAcopio, entregas[{entregaId (UUID del cliente), fincaId, kilos, fechaEntrega}]}
  API->>H: EjecutarAsync(comando)
  H->>H: Entrega.Registrar(...) valida y calcula hash SHA-256 canónico
  H->>DB: SELECT id, hash FROM entregas WHERE id = ANY(...)
  alt Algún id existe con hash distinto
    H-->>API: Conflicto(ids)
    API-->>Op: 409 Conflict (ProblemDetails + entregasEnConflicto) · no se persiste nada
  else Ids nuevos y/o repetidos con el mismo hash
    H->>DB: BEGIN · INSERT entregas nuevas · INSERT outbox(EntregaRegistrada) · COMMIT
    Note over H,DB: PRIMARY KEY(id) resuelve la carrera entre dos reenvíos simultáneos (la API reintenta una vez)
    H-->>API: Aceptada(creadas, duplicadas)
    API-->>Op: 200 OK {creadas, duplicadas}
  end
  Note over Op,API: Reenvío del mismo lote → 200 OK {creadas: 0, duplicadas: n}
```

## Figura 6. Secuencia — Conformación y verificación asíncrona del lote (QA2)

```mermaid
sequenceDiagram
  autonumber
  actor An as Analista
  participant A as Acopio y Lotes API
  participant DA as acopio_db
  participant RA as OutboxRelay Acopio
  participant MQ as RabbitMQ
  participant C as Cumplimiento Worker
  participant DC as cumplimiento_db
  participant G as NtsVerificadorDeforestacion
  participant RC as OutboxRelay Cumplimiento

  An->>A: POST /api/lotes {entregaIds}
  A->>DA: BEGIN · Lote.Conformar() · UPDATE entregas … WHERE xmin = @v · INSERT lote PendienteVerificacion · INSERT outbox(LoteConformado) · COMMIT
  alt Otra transacción tomó una entrega
    A-->>An: 409 Conflict
  else Éxito
    A-->>An: 202 Accepted · Location /api/lotes/{id}/genealogia
  end
  RA->>DA: SELECT … FROM outbox_messages WHERE publicado_en IS NULL FOR UPDATE SKIP LOCKED
  RA->>MQ: publish LoteConformado (persistente, publisher confirms)
  RA->>DA: UPDATE outbox_messages SET publicado_en = now()
  MQ->>C: deliver LoteConformado (messageId)
  C->>DC: ¿messageId en inbox_messages?
  alt Ya procesado
    C->>MQ: ack sin efectos
  else Nuevo
    C->>DC: Leer geometrías de fincas_replica
    C->>G: BuscarPerdidasAsync(geometría) por finca
    G-->>C: pérdidas de bosque (área, fecha)
    C->>C: ResultadoFinca.Evaluar → regla de corte 31-12-2020 (dominio)
    C->>DC: BEGIN · INSERT verificaciones · INSERT inbox · INSERT outbox(LoteVerificado) · COMMIT
    C->>MQ: ack
  end
  Note over C,MQ: Finca aún no replicada → cumplimiento.eventos.retry (TTL 5 s) · tras 5 intentos → .dlq
  RC->>MQ: publish LoteVerificado
  MQ->>A: deliver LoteVerificado
  A->>DA: BEGIN · inbox · Lote.AplicarVerificacion() → Declarable o Bloqueado · COMMIT
  An->>A: GET /api/lotes/{id}/genealogia
  A-->>An: 200 {estado, kilosTotales, fincas[entregas, conforme, detalle], segundosHastaVerificacion}
```
