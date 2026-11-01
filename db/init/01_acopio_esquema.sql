-- TrazaCafé Huila · 01 · Esquema del contexto Acopio y Lotes
\connect acopio_db

CREATE TABLE fincas (
    id                   uuid PRIMARY KEY,
    codigo               text        NOT NULL UNIQUE,
    nombre               text        NOT NULL,
    municipio            text        NOT NULL,
    vereda               text        NOT NULL,
    geometria            jsonb       NOT NULL,              -- Polygon GeoJSON (WGS84)
    consentimiento_datos boolean     NOT NULL DEFAULT false, -- autorización del caficultor para compartir datos
    registrada_en        timestamptz NOT NULL
);

CREATE TABLE lotes (
    id            uuid PRIMARY KEY,
    codigo        text          NOT NULL UNIQUE,
    estado        text          NOT NULL CHECK (estado IN ('PendienteVerificacion', 'Declarable', 'Bloqueado')),
    kilos_totales numeric(14,3) NOT NULL CHECK (kilos_totales > 0),
    conformado_en timestamptz   NOT NULL,
    verificado_en timestamptz   NULL,
    CHECK ((estado = 'PendienteVerificacion') = (verificado_en IS NULL))
);

CREATE TABLE entregas (
    id            uuid PRIMARY KEY,                        -- generado por el cliente (idempotencia)
    finca_id      uuid          NOT NULL REFERENCES fincas (id),
    punto_acopio  text          NOT NULL,
    kilos         numeric(12,3) NOT NULL CHECK (kilos > 0),
    fecha_entrega date          NOT NULL,
    hash          char(64)      NOT NULL,                  -- SHA-256 del contenido canónico
    lote_id       uuid          NULL REFERENCES lotes (id) DEFERRABLE INITIALLY DEFERRED,
    registrada_en timestamptz   NOT NULL
);
CREATE INDEX ix_entregas_lote ON entregas (lote_id);
CREATE INDEX ix_entregas_finca ON entregas (finca_id);

-- Composición inmutable del lote (genealogía). UNIQUE(entrega_id) es una segunda defensa del invariante I2.
CREATE TABLE lote_entregas (
    lote_id    uuid          NOT NULL REFERENCES lotes (id) DEFERRABLE INITIALLY DEFERRED,
    entrega_id uuid          NOT NULL UNIQUE REFERENCES entregas (id) DEFERRABLE INITIALLY DEFERRED,
    finca_id   uuid          NOT NULL REFERENCES fincas (id),
    kilos      numeric(12,3) NOT NULL CHECK (kilos > 0),
    PRIMARY KEY (lote_id, entrega_id)
);

CREATE TABLE lote_verificaciones (
    lote_id  uuid    NOT NULL REFERENCES lotes (id) DEFERRABLE INITIALLY DEFERRED,
    finca_id uuid    NOT NULL REFERENCES fincas (id),
    conforme boolean NOT NULL,
    detalle  text    NOT NULL,
    PRIMARY KEY (lote_id, finca_id)
);

-- Transactional Outbox (ADR-002). No se purga en el MVP: diario de auditoría append-only.
CREATE TABLE outbox_messages (
    id           uuid PRIMARY KEY,
    tipo         text        NOT NULL,
    payload      jsonb       NOT NULL,
    ocurrido_en  timestamptz NOT NULL,
    publicado_en timestamptz NULL,
    intentos     integer     NOT NULL DEFAULT 0,
    ultimo_error text        NULL
);
CREATE INDEX ix_outbox_pendientes ON outbox_messages (ocurrido_en, id) WHERE publicado_en IS NULL;

-- Inbox: consumidor idempotente.
CREATE TABLE inbox_messages (
    message_id   uuid PRIMARY KEY,
    tipo         text        NOT NULL,
    procesado_en timestamptz NOT NULL
);

-- Modelo de lectura (CQRS): una fila por entrega del lote, con su finca y el resultado de verificación.
CREATE VIEW vw_genealogia_lote AS
SELECT le.lote_id,
       f.id             AS finca_id,
       f.codigo         AS finca_codigo,
       f.nombre         AS finca_nombre,
       f.municipio,
       f.vereda,
       f.geometria,
       v.conforme,
       v.detalle        AS detalle_verificacion,
       e.id             AS entrega_id,
       e.fecha_entrega,
       e.punto_acopio,
       le.kilos
FROM lote_entregas le
JOIN entregas e ON e.id = le.entrega_id
JOIN fincas   f ON f.id = le.finca_id
LEFT JOIN lote_verificaciones v ON v.lote_id = le.lote_id AND v.finca_id = le.finca_id;
