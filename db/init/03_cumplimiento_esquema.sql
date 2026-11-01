-- TrazaCafé Huila · 03 · Esquema del contexto Cumplimiento
\connect cumplimiento_db

-- Réplica local alimentada por FincaRegistrada (ADR-003). Sin datos personales del caficultor.
CREATE TABLE fincas_replica (
    finca_id    uuid PRIMARY KEY,
    codigo      text        NOT NULL,
    geometria   jsonb       NOT NULL,
    recibida_en timestamptz NOT NULL
);

CREATE TABLE verificaciones (
    lote_id       uuid PRIMARY KEY,           -- idempotencia de negocio: un lote se verifica una sola vez
    bloqueado     boolean     NOT NULL,
    verificado_en timestamptz NOT NULL
);

CREATE TABLE verificacion_fincas (
    lote_id  uuid    NOT NULL REFERENCES verificaciones (lote_id),
    finca_id uuid    NOT NULL,
    conforme boolean NOT NULL,
    detalle  text    NOT NULL,
    PRIMARY KEY (lote_id, finca_id)
);

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

CREATE TABLE inbox_messages (
    message_id   uuid PRIMARY KEY,
    tipo         text        NOT NULL,
    procesado_en timestamptz NOT NULL
);
