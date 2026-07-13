-- seed_10m.sql — dataset sintético para prueba de volumen/memoria contra Oracle real.
-- Ver docs/07-testing-strategy.md §4.
-- Uso: ejecutar en SQL*Plus / SQLcl con la cuenta de pruebas.

-- 1) Tabla destino
CREATE TABLE hexporter_stress (
    id       NUMBER        NOT NULL,
    fecha    DATE          NOT NULL,
    monto    NUMBER(12,2)  NOT NULL,
    cliente  VARCHAR2(64)  NOT NULL
);

-- 2) Sembrar 10M filas por lotes de 1M (evita un solo INSERT gigante / undo enorme).
--    CONNECT BY LEVEL genera las filas del lote; el offset desplaza el id.
BEGIN
    FOR lote IN 0 .. 9 LOOP
        INSERT /*+ APPEND */ INTO hexporter_stress (id, fecha, monto, cliente)
        SELECT lote * 1000000 + LEVEL,
               DATE '2000-01-01' + MOD(LEVEL, 3650),
               ROUND(DBMS_RANDOM.VALUE(1, 10000), 2),
               'cliente_' || (lote * 1000000 + LEVEL)
        FROM   dual
        CONNECT BY LEVEL <= 1000000;
        COMMIT;
    END LOOP;
END;
/

-- 3) Estadísticas (mejora los planes durante el export)
BEGIN
    DBMS_STATS.GATHER_TABLE_STATS(USER, 'HEXPORTER_STRESS');
END;
/

-- 4) Verificación
SELECT COUNT(*) AS filas FROM hexporter_stress;

-- Limpieza (cuando termine la prueba):
-- DROP TABLE hexporter_stress PURGE;
