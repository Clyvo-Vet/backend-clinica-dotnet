-- ============================================================================
-- KURA — Flyway V5
-- Versão: v5 (alinhamento .NET/EF — PASSO 7)
-- Data: 2026-05-13
-- Autor: Felipe Ferrete
-- ============================================================================
-- CONTEXTO:
--   Este script alinha o esquema Oracle com o modelo de domínio .NET após
--   as seguintes correções (PASSOS 5-7):
--
--   [1] PKs renomeadas de "ID" para o nome canônico do schema Flyway
--       (ex: ID → ID_VETERINARIO, ID → ID_PET, etc.)
--   [2] ST_ATIVA → ST_ATIVO nas tabelas VETERINARIO, TUTOR, PET, INVITE_TUTOR
--   [3] ST_ATIVA removida de tabelas que não possuem essa coluna no schema:
--       VACINA, TIPO_EVENTO, RACA, PRESCRICAO, NOTIFICACAO, MEDICAMENTO,
--       LEITURA_TEMPERATURA, EXAME, EVENTO_CLINICO, ESPECIE, DOCUMENTO,
--       DISPOSITIVO_IOT, ALERTA_TEMPERATURA, AGENDAMENTO
--   [4] LOG_ERRO removida do .NET (tabela permanece no Oracle; Java grava nela)
--   [5] CLINICA: renomeia/corrige colunas para alinhar ao schema v3
--   [6] Sequence LEITURA_TEMPERATURA usa SEQ_LEITURA_TEMP (não SEQ_LEITURA_TEMPERATURA)
--   [7] Sequence ALERTA_TEMPERATURA usa SEQ_ALERTA_TEMP (não SEQ_ALERTA_TEMPERATURA)
-- ============================================================================
-- NOTA: Como o EF Core gerencia o histórico via __EFMigrationsHistory,
--       este script Flyway é o espelho DDL do EF Migration
--       "20260513140711_Schema_v4_BoolColumns_PKRename".
-- ============================================================================

-- ============================================================================
-- 1. RENOMEAR PKs — "ID" → nome canônico
-- ============================================================================

-- VETERINARIO: ID → ID_VETERINARIO  +  ST_ATIVA → ST_ATIVO
ALTER TABLE "VETERINARIO" RENAME COLUMN "ID" TO "ID_VETERINARIO"
/
ALTER TABLE "VETERINARIO" RENAME COLUMN "ST_ATIVA" TO "ST_ATIVO"
/

-- TUTOR: ID → ID_TUTOR  +  ST_ATIVA → ST_ATIVO
ALTER TABLE "TUTOR" RENAME COLUMN "ID" TO "ID_TUTOR"
/
ALTER TABLE "TUTOR" RENAME COLUMN "ST_ATIVA" TO "ST_ATIVO"
/

-- PET: ID → ID_PET  +  ST_ATIVA → ST_ATIVO
ALTER TABLE "PET" RENAME COLUMN "ID" TO "ID_PET"
/
ALTER TABLE "PET" RENAME COLUMN "ST_ATIVA" TO "ST_ATIVO"
/

-- ESPECIE: ID → ID_ESPECIE
ALTER TABLE "ESPECIE" RENAME COLUMN "ID" TO "ID_ESPECIE"
/

-- RACA: ID → ID_RACA
ALTER TABLE "RACA" RENAME COLUMN "ID" TO "ID_RACA"
/

-- TIPO_EVENTO: ID → ID_TIPO_EVENTO
ALTER TABLE "TIPO_EVENTO" RENAME COLUMN "ID" TO "ID_TIPO_EVENTO"
/

-- EVENTO_CLINICO: ID → ID_EVENTO
ALTER TABLE "EVENTO_CLINICO" RENAME COLUMN "ID" TO "ID_EVENTO"
/

-- VACINA: ID → ID_VACINA
ALTER TABLE "VACINA" RENAME COLUMN "ID" TO "ID_VACINA"
/

-- PRESCRICAO: ID → ID_PRESCRICAO
ALTER TABLE "PRESCRICAO" RENAME COLUMN "ID" TO "ID_PRESCRICAO"
/

-- EXAME: ID → ID_EXAME
ALTER TABLE "EXAME" RENAME COLUMN "ID" TO "ID_EXAME"
/

-- DOCUMENTO: ID → ID_DOCUMENTO
ALTER TABLE "DOCUMENTO" RENAME COLUMN "ID" TO "ID_DOCUMENTO"
/

-- NOTIFICACAO: ID → ID_NOTIFICACAO
ALTER TABLE "NOTIFICACAO" RENAME COLUMN "ID" TO "ID_NOTIFICACAO"
/

-- MEDICAMENTO: ID → ID_MEDICAMENTO
ALTER TABLE "MEDICAMENTO" RENAME COLUMN "ID" TO "ID_MEDICAMENTO"
/

-- DISPOSITIVO_IOT: ID → ID_DISPOSITIVO
ALTER TABLE "DISPOSITIVO_IOT" RENAME COLUMN "ID" TO "ID_DISPOSITIVO"
/

-- LEITURA_TEMPERATURA: ID → ID_LEITURA
ALTER TABLE "LEITURA_TEMPERATURA" RENAME COLUMN "ID" TO "ID_LEITURA"
/

-- ALERTA_TEMPERATURA: ID → ID_ALERTA
ALTER TABLE "ALERTA_TEMPERATURA" RENAME COLUMN "ID" TO "ID_ALERTA"
/

-- CLINICA: ID → ID_CLINICA  +  NR_TELEFONE → DS_TELEFONE  +  DT_CRIACAO → DT_CADASTRO
ALTER TABLE "CLINICA" RENAME COLUMN "ID" TO "ID_CLINICA"
/
ALTER TABLE "CLINICA" RENAME COLUMN "NR_TELEFONE" TO "DS_TELEFONE"
/
ALTER TABLE "CLINICA" RENAME COLUMN "DT_CRIACAO" TO "DT_CADASTRO"
/

-- INVITE_TUTOR: ST_ATIVA → ST_ATIVO
ALTER TABLE "INVITE_TUTOR" RENAME COLUMN "ST_ATIVA" TO "ST_ATIVO"
/

-- ============================================================================
-- 2. REMOVER ST_ATIVA de tabelas que não possuem essa coluna no schema Flyway
--    (foram adicionadas erroneamente pelo .NET antes deste alinhamento)
-- ============================================================================

DECLARE
    PROCEDURE drop_col_if_exists(p_table VARCHAR2, p_col VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count
        FROM user_tab_columns
        WHERE table_name = p_table AND column_name = p_col;
        IF v_count > 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE "' || p_table || '" DROP COLUMN "' || p_col || '"';
        END IF;
    END;
BEGIN
    drop_col_if_exists('VACINA',              'ST_ATIVA');
    drop_col_if_exists('TIPO_EVENTO',         'ST_ATIVA');
    drop_col_if_exists('RACA',                'ST_ATIVA');
    drop_col_if_exists('PRESCRICAO',          'ST_ATIVA');
    drop_col_if_exists('NOTIFICACAO',         'ST_ATIVA');
    drop_col_if_exists('MEDICAMENTO',         'ST_ATIVA');
    drop_col_if_exists('LEITURA_TEMPERATURA', 'ST_ATIVA');
    drop_col_if_exists('EXAME',               'ST_ATIVA');
    drop_col_if_exists('EVENTO_CLINICO',      'ST_ATIVA');
    drop_col_if_exists('ESPECIE',             'ST_ATIVA');
    drop_col_if_exists('DOCUMENTO',           'ST_ATIVA');
    drop_col_if_exists('DISPOSITIVO_IOT',     'ST_ATIVA');
    drop_col_if_exists('ALERTA_TEMPERATURA',  'ST_ATIVA');
    drop_col_if_exists('AGENDAMENTO',         'ST_ATIVA');
END;
/

-- ============================================================================
-- 3. CORRIGIR CLINICA — remover colunas transitórias e adicionar schema v3
-- ============================================================================

-- Remover DS_SENHA (coluna transitória — PASSO 5 adicionou, substituída por DS_SENHA_HASH)
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'DS_SENHA';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" DROP COLUMN "DS_SENHA"';
    END IF;
END;
/

-- Remover DT_ATUALIZACAO (CLINICA usa DT_CADASTRO, sem DT_ATUALIZACAO no schema)
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'DT_ATUALIZACAO';
    IF v_count > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" DROP COLUMN "DT_ATUALIZACAO"';
    END IF;
END;
/

-- Adicionar colunas v3 que faltavam (idempotente via verificação prévia)
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'NM_CIDADE';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" ADD "NM_CIDADE" NVARCHAR2(80) DEFAULT '''' NOT NULL';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'NM_RAZAO_SOCIAL';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" ADD "NM_RAZAO_SOCIAL" NVARCHAR2(150)';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'NR_CEP';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" ADD "NR_CEP" NVARCHAR2(9) DEFAULT '''' NOT NULL';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'SG_UF';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" ADD "SG_UF" NVARCHAR2(2) DEFAULT '''' NOT NULL';
    END IF;
END;
/

-- Alterar DS_EMAIL_ACESSO: tamanho 100 → 120
DECLARE
    v_len NUMBER;
BEGIN
    SELECT data_length INTO v_len FROM user_tab_columns
    WHERE table_name = 'CLINICA' AND column_name = 'DS_EMAIL_ACESSO';
    IF v_len < 120 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "CLINICA" MODIFY "DS_EMAIL_ACESSO" NVARCHAR2(120) NOT NULL';
    END IF;
EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
END;
/

-- Criar índice único em DS_EMAIL_ACESSO se não existir
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM user_indexes
    WHERE table_name = 'CLINICA' AND index_name = 'IX_CLINICA_DS_EMAIL_ACESSO';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX "IX_CLINICA_DS_EMAIL_ACESSO" ON "CLINICA" ("DS_EMAIL_ACESSO")';
    END IF;
END;
/

-- ============================================================================
-- 4. TRIAGEM_LUNA — corrigir ST_ENCAMINHADO_VET: NUMBER(10) → CHAR(1)
-- ============================================================================
DECLARE
    v_type VARCHAR2(20);
BEGIN
    SELECT data_type INTO v_type FROM user_tab_columns
    WHERE table_name = 'TRIAGEM_LUNA' AND column_name = 'ST_ENCAMINHADO_VET';
    IF v_type = 'NUMBER' THEN
        EXECUTE IMMEDIATE 'ALTER TABLE "TRIAGEM_LUNA" MODIFY "ST_ENCAMINHADO_VET" CHAR(1) NOT NULL';
    END IF;
EXCEPTION WHEN NO_DATA_FOUND THEN NULL;
END;
/

-- ============================================================================
-- 5. Registrar migração EF no histórico Flyway (informativo)
-- ============================================================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES (N'20260513140711_Schema_v4_BoolColumns_PKRename', N'10.0.7')
/
