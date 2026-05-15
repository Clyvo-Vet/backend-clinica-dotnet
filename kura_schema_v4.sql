-- ============================================================================
-- KURA — Sistema de Gestão Veterinária
-- Cliente: Clyvo Vet (Challenge FIAP 2026)
-- Banco: Oracle 19c+
-- Notação: 3FN | Constraints nomeadas | Sequences para PKs
-- ============================================================================
-- VERSÃO: v4
-- ALTERAÇÕES EM RELAÇÃO À v3:
--   [1] CLINICA         → colunas DS_EMAIL_ACESSO (UNIQUE), DS_SENHA_HASH
--                         (autenticação .NET da clínica)
--   [2] INVITE_TUTOR    → estrutura redefinida pelo modelo .NET:
--                         NR_TOKEN VARCHAR2(36), ST_UTILIZADO, ST_ATIVO,
--                         DT_CRIACAO, DT_ATUALIZACAO
--   [3] CONSULTA        → nova tabela (subtipo de EVENTO_CLINICO)
--   [4] TRIAGEM_LUNA    → nova tabela (triagem IA/bot Luna)
--   [5] DISPOSITIVO_IOT → colunas alinhadas ao modelo .NET:
--                         CD_DISPOSITIVO (UNIQUE), DS_DESCRICAO, DS_LOCALIZACAO
--   [6] LEITURA_TEMPERATURA → FK para ID_DISPOSITIVO_IOT; colunas VL_TEMPERATURA,
--                             VL_UMIDADE; sem ST_DENTRO_FAIXA (calculado na app)
--   [7] ALERTA_TEMPERATURA  → FK para ID_LEITURA_TEMPERATURA; VL_LIMITE
--   [8] PKs canônicas   → ID_<TABELA> em todas as tabelas .NET
--   [9] ST_ATIVA/ST_ATIVO → removida de tabelas sem soft-delete;
--                           renomeada para ST_ATIVO em VETERINARIO, TUTOR, PET
--   [10] Sequences novas → SEQ_CONSULTA, SEQ_TRIAGEM_LUNA
-- ============================================================================
-- ARQUITETURA DE DOMÍNIOS:
--   .NET (Backend Clínica — Felipe) — escreve
--     CLINICA, VETERINARIO, TUTOR, PET, ESPECIE, RACA,
--     EVENTO_CLINICO, TIPO_EVENTO, CONSULTA, VACINA, PRESCRICAO,
--     MEDICAMENTO, EXAME, DOCUMENTO, NOTIFICACAO,
--     DISPOSITIVO_IOT, LEITURA_TEMPERATURA, ALERTA_TEMPERATURA,
--     TRIAGEM_LUNA, INVITE_TUTOR, TUTOR_PET
--
--   Java (Backend Tutor — Nikolas) — escreve
--     CONTA_TUTOR, CONSENTIMENTO, AGENDAMENTO, IDEMPOTENCY_KEY
--
--   Leitura cruzada:
--     .NET lê CONTA_TUTOR, CONSENTIMENTO, AGENDAMENTO
--     Java lê INVITE_TUTOR, TUTOR, PET, VETERINARIO, CLINICA
-- ============================================================================

-- ============================================================================
-- 0. LIMPEZA (executar em ambiente de dev — ordem reversa de dependência)
-- ============================================================================

-- DROP TABLE IDEMPOTENCY_KEY              CASCADE CONSTRAINTS;
-- DROP TABLE LOG_ERRO                     CASCADE CONSTRAINTS;
-- DROP TABLE TRIAGEM_LUNA                 CASCADE CONSTRAINTS;
-- DROP TABLE ALERTA_TEMPERATURA           CASCADE CONSTRAINTS;
-- DROP TABLE LEITURA_TEMPERATURA          CASCADE CONSTRAINTS;
-- DROP TABLE DISPOSITIVO_IOT              CASCADE CONSTRAINTS;
-- DROP TABLE NOTIFICACAO                  CASCADE CONSTRAINTS;
-- DROP TABLE CONSENTIMENTO                CASCADE CONSTRAINTS;
-- DROP TABLE AGENDAMENTO                  CASCADE CONSTRAINTS;
-- DROP TABLE CONTA_TUTOR                  CASCADE CONSTRAINTS;
-- DROP TABLE INVITE_TUTOR                 CASCADE CONSTRAINTS;
-- DROP TABLE DOCUMENTO                    CASCADE CONSTRAINTS;
-- DROP TABLE EXAME                        CASCADE CONSTRAINTS;
-- DROP TABLE PRESCRICAO                   CASCADE CONSTRAINTS;
-- DROP TABLE CONSULTA                     CASCADE CONSTRAINTS;
-- DROP TABLE VACINA                       CASCADE CONSTRAINTS;
-- DROP TABLE EVENTO_CLINICO               CASCADE CONSTRAINTS;
-- DROP TABLE TIPO_EVENTO                  CASCADE CONSTRAINTS;
-- DROP TABLE TUTOR_PET                    CASCADE CONSTRAINTS;
-- DROP TABLE PET                          CASCADE CONSTRAINTS;
-- DROP TABLE RACA                         CASCADE CONSTRAINTS;
-- DROP TABLE ESPECIE                      CASCADE CONSTRAINTS;
-- DROP TABLE TUTOR                        CASCADE CONSTRAINTS;
-- DROP TABLE VETERINARIO                  CASCADE CONSTRAINTS;
-- DROP TABLE CLINICA                      CASCADE CONSTRAINTS;
-- DROP TABLE MEDICAMENTO                  CASCADE CONSTRAINTS;

-- DROP SEQUENCE SEQ_CLINICA;
-- DROP SEQUENCE SEQ_VETERINARIO;
-- DROP SEQUENCE SEQ_TUTOR;
-- DROP SEQUENCE SEQ_ESPECIE;
-- DROP SEQUENCE SEQ_RACA;
-- DROP SEQUENCE SEQ_PET;
-- DROP SEQUENCE SEQ_TIPO_EVENTO;
-- DROP SEQUENCE SEQ_EVENTO_CLINICO;
-- DROP SEQUENCE SEQ_CONSULTA;
-- DROP SEQUENCE SEQ_VACINA;
-- DROP SEQUENCE SEQ_MEDICAMENTO;
-- DROP SEQUENCE SEQ_PRESCRICAO;
-- DROP SEQUENCE SEQ_EXAME;
-- DROP SEQUENCE SEQ_DOCUMENTO;
-- DROP SEQUENCE SEQ_AGENDAMENTO;
-- DROP SEQUENCE SEQ_NOTIFICACAO;
-- DROP SEQUENCE SEQ_CONTA_TUTOR;
-- DROP SEQUENCE SEQ_CONSENTIMENTO;
-- DROP SEQUENCE SEQ_LOG_ERRO;
-- DROP SEQUENCE SEQ_DISPOSITIVO_IOT;
-- DROP SEQUENCE SEQ_LEITURA_TEMP;
-- DROP SEQUENCE SEQ_ALERTA_TEMP;
-- DROP SEQUENCE SEQ_TRIAGEM_LUNA;
-- DROP SEQUENCE SEQ_IDEMPOTENCY_KEY;
-- DROP SEQUENCE SEQ_INVITE_TUTOR;

-- ============================================================================
-- 1. SEQUENCES (auto-incremento)
-- ============================================================================

CREATE SEQUENCE SEQ_CLINICA          START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_VETERINARIO      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_TUTOR            START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_ESPECIE          START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_RACA             START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_PET              START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_TIPO_EVENTO      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_EVENTO_CLINICO   START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_CONSULTA         START WITH 1 INCREMENT BY 1 NOCACHE;  -- [v4]
CREATE SEQUENCE SEQ_VACINA           START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_MEDICAMENTO      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_PRESCRICAO       START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_EXAME            START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_DOCUMENTO        START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_AGENDAMENTO      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_NOTIFICACAO      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_CONTA_TUTOR      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_CONSENTIMENTO    START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_LOG_ERRO         START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_DISPOSITIVO_IOT  START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_LEITURA_TEMP     START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_ALERTA_TEMP      START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_TRIAGEM_LUNA     START WITH 1 INCREMENT BY 1 NOCACHE;  -- [v4]
CREATE SEQUENCE SEQ_IDEMPOTENCY_KEY  START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE SEQUENCE SEQ_INVITE_TUTOR     START WITH 1 INCREMENT BY 1 NOCACHE;

-- ============================================================================
-- 2. ENTIDADES INDEPENDENTES (sem FKs)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- CLINICA  [v4 — adicionado DS_EMAIL_ACESSO, DS_SENHA_HASH]
-- Domínio: .NET | Autenticação própria via JWT
-- ----------------------------------------------------------------------------
CREATE TABLE CLINICA (
    ID_CLINICA       NUMBER(10)       DEFAULT SEQ_CLINICA.NEXTVAL NOT NULL,
    NM_CLINICA       VARCHAR2(120)    NOT NULL,
    NR_CNPJ          VARCHAR2(18)     NOT NULL,
    NM_RAZAO_SOCIAL  VARCHAR2(150),
    DS_ENDERECO      VARCHAR2(200)    NOT NULL,
    NM_CIDADE        VARCHAR2(80)     NOT NULL,
    SG_UF            CHAR(2)          NOT NULL,
    NR_CEP           VARCHAR2(9)      NOT NULL,
    DS_TELEFONE      VARCHAR2(20),
    DS_EMAIL         VARCHAR2(120)    NOT NULL,
    DT_CADASTRO      TIMESTAMP        DEFAULT SYSTIMESTAMP NOT NULL,
    ST_ATIVA         CHAR(1)          DEFAULT 'S' NOT NULL,
    -- [v4] Credenciais de acesso da clínica ao backend .NET
    DS_EMAIL_ACESSO  VARCHAR2(120)    NOT NULL,
    DS_SENHA_HASH    VARCHAR2(256)    NOT NULL,
    CONSTRAINT PK_CLINICA             PRIMARY KEY (ID_CLINICA),
    CONSTRAINT UK_CLINICA_CNPJ        UNIQUE (NR_CNPJ),
    CONSTRAINT UK_CLINICA_EMAIL_ACESSO UNIQUE (DS_EMAIL_ACESSO),
    CONSTRAINT CK_CLINICA_ATIVA       CHECK (ST_ATIVA IN ('S','N')),
    CONSTRAINT CK_CLINICA_UF          CHECK (LENGTH(SG_UF) = 2)
);

COMMENT ON TABLE  CLINICA                    IS 'Clínicas e hospitais veterinários cadastrados no KURA';
COMMENT ON COLUMN CLINICA.ID_CLINICA         IS 'PK auto-incremento via SEQ_CLINICA';
COMMENT ON COLUMN CLINICA.NR_CNPJ            IS 'CNPJ formatado XX.XXX.XXX/0001-XX';
COMMENT ON COLUMN CLINICA.ST_ATIVA           IS 'S=ativa, N=inativa (soft delete)';
COMMENT ON COLUMN CLINICA.DS_EMAIL_ACESSO    IS 'E-mail de login da clínica no sistema .NET — UNIQUE';
COMMENT ON COLUMN CLINICA.DS_SENHA_HASH      IS 'BCrypt hash da senha de acesso — NUNCA texto plano';

-- ----------------------------------------------------------------------------
-- ESPECIE
-- Domínio: .NET | Tabela de domínio (lookup)
-- ----------------------------------------------------------------------------
CREATE TABLE ESPECIE (
    ID_ESPECIE    NUMBER(5)      DEFAULT SEQ_ESPECIE.NEXTVAL NOT NULL,
    NM_ESPECIE    VARCHAR2(200)  NOT NULL,
    DT_CRIACAO    TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO TIMESTAMP,
    CONSTRAINT PK_ESPECIE      PRIMARY KEY (ID_ESPECIE),
    CONSTRAINT UK_ESPECIE_NOME UNIQUE (NM_ESPECIE)
);

COMMENT ON TABLE ESPECIE IS 'Tipos de animais atendidos: Cão, Gato, Ave, Réptil etc.';

-- ----------------------------------------------------------------------------
-- MEDICAMENTO
-- Domínio: .NET | Catálogo central de medicamentos
-- ----------------------------------------------------------------------------
CREATE TABLE MEDICAMENTO (
    ID_MEDICAMENTO    NUMBER(10)     DEFAULT SEQ_MEDICAMENTO.NEXTVAL NOT NULL,
    NM_MEDICAMENTO    VARCHAR2(200)  NOT NULL,
    DS_PRINCIPIO_ATIVO VARCHAR2(500) NOT NULL,
    DS_APRESENTACAO   VARCHAR2(500)  NOT NULL,
    DT_CRIACAO        TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO    TIMESTAMP,
    CONSTRAINT PK_MEDICAMENTO PRIMARY KEY (ID_MEDICAMENTO)
);

COMMENT ON COLUMN MEDICAMENTO.DS_PRINCIPIO_ATIVO IS 'Princípio ativo (ex: Oclacitinib)';
COMMENT ON COLUMN MEDICAMENTO.DS_APRESENTACAO    IS 'Apresentação (ex: comprimido 16mg, frasco 50ml)';

-- ----------------------------------------------------------------------------
-- TIPO_EVENTO
-- Domínio: .NET | Tabela de domínio
-- ----------------------------------------------------------------------------
CREATE TABLE TIPO_EVENTO (
    ID_TIPO_EVENTO  NUMBER(5)      DEFAULT SEQ_TIPO_EVENTO.NEXTVAL NOT NULL,
    CD_TIPO         VARCHAR2(20)   NOT NULL,
    NM_TIPO         VARCHAR2(200)  NOT NULL,
    DT_CRIACAO      TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO  TIMESTAMP,
    CONSTRAINT PK_TIPO_EVENTO      PRIMARY KEY (ID_TIPO_EVENTO),
    CONSTRAINT UK_TIPO_EVENTO_CD   UNIQUE (CD_TIPO)
);

COMMENT ON TABLE  TIPO_EVENTO    IS 'Tipos: CONSULTA, TELEORIENTACAO, VACINA, PRESCRICAO, EXAME, PROCEDIMENTO, RETORNO';
COMMENT ON COLUMN TIPO_EVENTO.CD_TIPO IS 'Código de negócio imutável (ex: CONSULTA, VACINA)';

-- ============================================================================
-- 3. ENTIDADES DEPENDENTES — NÍVEL 1
-- ============================================================================

-- ----------------------------------------------------------------------------
-- VETERINARIO
-- Domínio: .NET | FK CLINICA
-- ----------------------------------------------------------------------------
CREATE TABLE VETERINARIO (
    ID_VETERINARIO   NUMBER(10)     DEFAULT SEQ_VETERINARIO.NEXTVAL NOT NULL,
    ID_CLINICA       NUMBER(10)     NOT NULL,
    NM_VETERINARIO   VARCHAR2(200)  NOT NULL,
    NR_CRMV          VARCHAR2(20)   NOT NULL,
    DS_EMAIL         VARCHAR2(150)  NOT NULL,
    NR_TELEFONE      VARCHAR2(20)   NOT NULL,
    ST_ATIVO         CHAR(1)        DEFAULT 'S' NOT NULL,
    DT_CRIACAO       TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_VETERINARIO PRIMARY KEY (ID_VETERINARIO),
    CONSTRAINT FK_VET_CLINICA FOREIGN KEY (ID_CLINICA) REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT UK_VET_CRMV    UNIQUE (NR_CRMV),
    CONSTRAINT UK_VET_EMAIL   UNIQUE (DS_EMAIL),
    CONSTRAINT CK_VET_ATIVO   CHECK (ST_ATIVO IN ('S','N'))
);

CREATE INDEX IDX_VET_CLINICA ON VETERINARIO(ID_CLINICA);

COMMENT ON COLUMN VETERINARIO.NR_CRMV IS 'Número do CRMV (registro profissional)';

-- ----------------------------------------------------------------------------
-- TUTOR
-- Domínio: .NET (cadastro) | Java (lê para vincular CONTA_TUTOR)
-- ----------------------------------------------------------------------------
CREATE TABLE TUTOR (
    ID_TUTOR         NUMBER(10)     DEFAULT SEQ_TUTOR.NEXTVAL NOT NULL,
    ID_CLINICA       NUMBER(10)     NOT NULL,
    NM_TUTOR         VARCHAR2(200)  NOT NULL,
    NR_CPF           VARCHAR2(11)   NOT NULL,
    DT_NASCIMENTO    DATE,
    DS_EMAIL         VARCHAR2(150)  NOT NULL,
    NR_TELEFONE      VARCHAR2(20)   NOT NULL,
    DS_WHATSAPP      VARCHAR2(20),
    DS_ENDERECO      VARCHAR2(200),
    NM_CIDADE        VARCHAR2(80),
    SG_UF            CHAR(2),
    NR_CEP           VARCHAR2(9),
    ST_ATIVO         CHAR(1)        DEFAULT 'S' NOT NULL,
    -- Transparência LGPD (art. 6º VI)
    ST_AVISO_PRIVACIDADE CHAR(1)    DEFAULT 'N' NOT NULL,
    DT_AVISO_PRIVACIDADE TIMESTAMP,
    DS_VERSAO_AVISO  VARCHAR2(20),
    DT_CRIACAO       TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_TUTOR         PRIMARY KEY (ID_TUTOR),
    CONSTRAINT FK_TUTOR_CLINICA FOREIGN KEY (ID_CLINICA) REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT UK_TUTOR_CPF     UNIQUE (NR_CPF),
    CONSTRAINT UK_TUTOR_EMAIL   UNIQUE (DS_EMAIL),
    CONSTRAINT CK_TUTOR_ATIVO   CHECK (ST_ATIVO IN ('S','N')),
    CONSTRAINT CK_TUTOR_AVISO   CHECK (ST_AVISO_PRIVACIDADE IN ('S','N'))
);

CREATE INDEX IDX_TUTOR_CLINICA ON TUTOR(ID_CLINICA);

COMMENT ON TABLE  TUTOR                      IS 'Responsável pelo pet. Cadastrado pela clínica (.NET). Conta de acesso gerenciada pelo Java (CONTA_TUTOR).';
COMMENT ON COLUMN TUTOR.DS_WHATSAPP          IS 'Número usado pela Luna (bot) para comunicação — pode ser igual ao telefone';
COMMENT ON COLUMN TUTOR.ST_AVISO_PRIVACIDADE IS 'S = tutor recebeu aviso de privacidade (transparência LGPD art. 6º VI)';
COMMENT ON COLUMN TUTOR.DS_VERSAO_AVISO      IS 'Versão do aviso apresentado (ex: v1.0, v1.1)';

-- ----------------------------------------------------------------------------
-- RACA
-- Domínio: .NET | FK ESPECIE
-- ----------------------------------------------------------------------------
CREATE TABLE RACA (
    ID_RACA          NUMBER(5)      DEFAULT SEQ_RACA.NEXTVAL NOT NULL,
    ID_ESPECIE       NUMBER(5)      NOT NULL,
    NM_RACA          VARCHAR2(200)  NOT NULL,
    DT_CRIACAO       TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_RACA         PRIMARY KEY (ID_RACA),
    CONSTRAINT FK_RACA_ESPECIE FOREIGN KEY (ID_ESPECIE) REFERENCES ESPECIE(ID_ESPECIE),
    CONSTRAINT UK_RACA_ESPECIE UNIQUE (NM_RACA, ID_ESPECIE)
);

CREATE INDEX IDX_RACA_ESPECIE ON RACA(ID_ESPECIE);

-- ============================================================================
-- 4. ENTIDADE CENTRAL — PET
-- ============================================================================

-- ----------------------------------------------------------------------------
-- PET
-- Domínio: .NET | FKs ESPECIE, RACA, VETERINARIO
-- ----------------------------------------------------------------------------
CREATE TABLE PET (
    ID_PET               NUMBER(10)    DEFAULT SEQ_PET.NEXTVAL NOT NULL,
    ID_CLINICA           NUMBER(10)    NOT NULL,
    ID_ESPECIE           NUMBER(5)     NOT NULL,
    ID_RACA              NUMBER(5)     NOT NULL,
    ID_VETERINARIO_RESP  NUMBER(10),
    NM_PET               VARCHAR2(200) NOT NULL,
    DT_NASCIMENTO        DATE          NOT NULL,
    SG_SEXO              CHAR(1)       NOT NULL,
    SG_PORTE             CHAR(1)       NOT NULL,
    ST_ATIVO             CHAR(1)       DEFAULT 'S' NOT NULL,
    DT_CRIACAO           TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO       TIMESTAMP,
    CONSTRAINT PK_PET             PRIMARY KEY (ID_PET),
    CONSTRAINT FK_PET_CLINICA     FOREIGN KEY (ID_CLINICA)         REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT FK_PET_ESPECIE     FOREIGN KEY (ID_ESPECIE)         REFERENCES ESPECIE(ID_ESPECIE),
    CONSTRAINT FK_PET_RACA        FOREIGN KEY (ID_RACA)            REFERENCES RACA(ID_RACA),
    CONSTRAINT FK_PET_VETERINARIO FOREIGN KEY (ID_VETERINARIO_RESP) REFERENCES VETERINARIO(ID_VETERINARIO),
    CONSTRAINT CK_PET_SEXO        CHECK (SG_SEXO  IN ('M','F')),
    CONSTRAINT CK_PET_PORTE       CHECK (SG_PORTE IN ('P','M','G')),
    CONSTRAINT CK_PET_ATIVO       CHECK (ST_ATIVO IN ('S','N'))
);

CREATE INDEX IDX_PET_CLINICA     ON PET(ID_CLINICA);
CREATE INDEX IDX_PET_ESPECIE     ON PET(ID_ESPECIE);
CREATE INDEX IDX_PET_RACA        ON PET(ID_RACA);
CREATE INDEX IDX_PET_VETERINARIO ON PET(ID_VETERINARIO_RESP);

COMMENT ON TABLE  PET                     IS 'Animal atendido. ID_PET = prontuário único do paciente.';
COMMENT ON COLUMN PET.SG_PORTE            IS 'P=pequeno, M=médio, G=grande';
COMMENT ON COLUMN PET.ID_VETERINARIO_RESP IS 'Veterinário responsável principal — pode ser NULL';

-- ----------------------------------------------------------------------------
-- TUTOR_PET  (associativa N:N entre TUTOR e PET)
-- Domínio: .NET
-- ----------------------------------------------------------------------------
CREATE TABLE TUTOR_PET (
    ID_TUTOR     NUMBER(10)    NOT NULL,
    ID_PET       NUMBER(10)    NOT NULL,
    DS_VINCULO   VARCHAR2(50)  DEFAULT 'PROPRIETARIO' NOT NULL,
    ST_PRINCIPAL CHAR(1)       DEFAULT 'S' NOT NULL,
    DT_VINCULO   TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_TUTOR_PET       PRIMARY KEY (ID_TUTOR, ID_PET),
    CONSTRAINT FK_TUTOR_PET_TUTOR FOREIGN KEY (ID_TUTOR) REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT FK_TUTOR_PET_PET   FOREIGN KEY (ID_PET)   REFERENCES PET(ID_PET),
    CONSTRAINT CK_TP_PRINCIPAL    CHECK (ST_PRINCIPAL IN ('S','N'))
);

CREATE INDEX IDX_TP_PET ON TUTOR_PET(ID_PET);

COMMENT ON TABLE  TUTOR_PET              IS 'Vínculo N:N tutores ↔ pets. Suporta casal compartilhando guarda.';
COMMENT ON COLUMN TUTOR_PET.ST_PRINCIPAL IS 'S = tutor principal (recebe notificações por padrão)';

-- ============================================================================
-- 5. EVENTOS CLÍNICOS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- EVENTO_CLINICO
-- Domínio: .NET | FKs PET, VETERINARIO, TIPO_EVENTO
-- Tabela-mãe da timeline do pet. Subtypes: CONSULTA, VACINA, PRESCRICAO, EXAME.
-- ----------------------------------------------------------------------------
CREATE TABLE EVENTO_CLINICO (
    ID_EVENTO        NUMBER(10)    DEFAULT SEQ_EVENTO_CLINICO.NEXTVAL NOT NULL,
    ID_CLINICA       NUMBER(10)    NOT NULL,
    ID_PET           NUMBER(10)    NOT NULL,
    ID_VETERINARIO   NUMBER(10)    NOT NULL,
    ID_TIPO_EVENTO   NUMBER(5)     NOT NULL,
    DT_EVENTO        TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DS_OBSERVACAO    VARCHAR2(1000) NOT NULL,
    DT_CRIACAO       TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_EVENTO_CLINICO PRIMARY KEY (ID_EVENTO),
    CONSTRAINT FK_EV_CLINICA     FOREIGN KEY (ID_CLINICA)      REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT FK_EV_PET         FOREIGN KEY (ID_PET)          REFERENCES PET(ID_PET),
    CONSTRAINT FK_EV_VETERINARIO FOREIGN KEY (ID_VETERINARIO)  REFERENCES VETERINARIO(ID_VETERINARIO),
    CONSTRAINT FK_EV_TIPO        FOREIGN KEY (ID_TIPO_EVENTO)  REFERENCES TIPO_EVENTO(ID_TIPO_EVENTO)
);

CREATE INDEX IDX_EV_CLINICA     ON EVENTO_CLINICO(ID_CLINICA);
CREATE INDEX IDX_EV_PET         ON EVENTO_CLINICO(ID_PET);
CREATE INDEX IDX_EV_VETERINARIO ON EVENTO_CLINICO(ID_VETERINARIO);
CREATE INDEX IDX_EV_TIPO        ON EVENTO_CLINICO(ID_TIPO_EVENTO);
CREATE INDEX IDX_EV_DATA        ON EVENTO_CLINICO(DT_EVENTO DESC);

COMMENT ON TABLE EVENTO_CLINICO IS 'Núcleo da timeline do pet. Cada interação clínica = 1 evento.';

-- ----------------------------------------------------------------------------
-- CONSULTA  [v4 — novo subtipo de EVENTO_CLINICO]
-- Domínio: .NET | FK EVENTO_CLINICO (1:1 quando TIPO_EVENTO = CONSULTA)
-- ----------------------------------------------------------------------------
CREATE TABLE CONSULTA (
    ID_CONSULTA      NUMBER(10)    DEFAULT SEQ_CONSULTA.NEXTVAL NOT NULL,
    ID_EVENTO_CLINICO NUMBER(10)   NOT NULL,
    DS_MOTIVO        VARCHAR2(200) NOT NULL,
    DS_ANAMNESE      VARCHAR2(2000),
    DS_EXAME_FISICO  VARCHAR2(2000),
    DS_DIAGNOSTICO   VARCHAR2(1000),
    DT_CONSULTA      TIMESTAMP     NOT NULL,
    ST_ATIVA         CHAR(1)       DEFAULT 'S' NOT NULL,
    DT_CRIACAO       TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_CONSULTA         PRIMARY KEY (ID_CONSULTA),
    CONSTRAINT FK_CONSULTA_EVENTO  FOREIGN KEY (ID_EVENTO_CLINICO) REFERENCES EVENTO_CLINICO(ID_EVENTO),
    CONSTRAINT UK_CONSULTA_EVENTO  UNIQUE (ID_EVENTO_CLINICO),
    CONSTRAINT CK_CONSULTA_ATIVA   CHECK (ST_ATIVA IN ('S','N'))
);

COMMENT ON TABLE CONSULTA IS 'Detalhes clínicos de uma consulta. 1:1 com EVENTO_CLINICO (soft delete independente).';

-- ----------------------------------------------------------------------------
-- VACINA
-- Domínio: .NET | FK EVENTO_CLINICO (1:1)
-- ----------------------------------------------------------------------------
CREATE TABLE VACINA (
    ID_VACINA          NUMBER(10)    DEFAULT SEQ_VACINA.NEXTVAL NOT NULL,
    ID_EVENTO_CLINICO  NUMBER(10)    NOT NULL,
    NM_VACINA          VARCHAR2(200) NOT NULL,
    NR_LOTE            VARCHAR2(50)  NOT NULL,
    DS_FABRICANTE      VARCHAR2(200) NOT NULL,
    DT_PROXIMA_DOSE    TIMESTAMP,
    DT_CRIACAO         TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO     TIMESTAMP,
    CONSTRAINT PK_VACINA        PRIMARY KEY (ID_VACINA),
    CONSTRAINT FK_VACINA_EVENTO FOREIGN KEY (ID_EVENTO_CLINICO) REFERENCES EVENTO_CLINICO(ID_EVENTO)
);

COMMENT ON COLUMN VACINA.DT_PROXIMA_DOSE IS 'Calculado pelo backend — alimenta lembretes da Luna';

-- ----------------------------------------------------------------------------
-- PRESCRICAO
-- Domínio: .NET | FK EVENTO_CLINICO, MEDICAMENTO
-- ----------------------------------------------------------------------------
CREATE TABLE PRESCRICAO (
    ID_PRESCRICAO      NUMBER(10)    DEFAULT SEQ_PRESCRICAO.NEXTVAL NOT NULL,
    ID_EVENTO_CLINICO  NUMBER(10)    NOT NULL,
    ID_MEDICAMENTO     NUMBER(10)    NOT NULL,
    DS_POSOLOGIA       VARCHAR2(500) NOT NULL,
    NR_DURACAO_DIAS    NUMBER(4)     NOT NULL,
    DT_CRIACAO         TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO     TIMESTAMP,
    CONSTRAINT PK_PRESCRICAO       PRIMARY KEY (ID_PRESCRICAO),
    CONSTRAINT FK_PRESC_EVENTO     FOREIGN KEY (ID_EVENTO_CLINICO) REFERENCES EVENTO_CLINICO(ID_EVENTO),
    CONSTRAINT FK_PRESC_MEDICAMENTO FOREIGN KEY (ID_MEDICAMENTO)   REFERENCES MEDICAMENTO(ID_MEDICAMENTO),
    CONSTRAINT CK_PRESC_DURACAO    CHECK (NR_DURACAO_DIAS > 0)
);

-- ----------------------------------------------------------------------------
-- EXAME
-- Domínio: .NET | FK EVENTO_CLINICO
-- ----------------------------------------------------------------------------
CREATE TABLE EXAME (
    ID_EXAME           NUMBER(10)     DEFAULT SEQ_EXAME.NEXTVAL NOT NULL,
    ID_EVENTO_CLINICO  NUMBER(10)     NOT NULL,
    NM_EXAME           VARCHAR2(200)  NOT NULL,
    DS_RESULTADO       VARCHAR2(1000) NOT NULL,
    DT_REALIZACAO      DATE           NOT NULL,
    DT_CRIACAO         TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO     TIMESTAMP,
    CONSTRAINT PK_EXAME        PRIMARY KEY (ID_EXAME),
    CONSTRAINT FK_EXAME_EVENTO FOREIGN KEY (ID_EVENTO_CLINICO) REFERENCES EVENTO_CLINICO(ID_EVENTO)
);

-- ----------------------------------------------------------------------------
-- DOCUMENTO
-- Domínio: .NET | FK EVENTO_CLINICO
-- ----------------------------------------------------------------------------
CREATE TABLE DOCUMENTO (
    ID_DOCUMENTO       NUMBER(10)    DEFAULT SEQ_DOCUMENTO.NEXTVAL NOT NULL,
    ID_EVENTO_CLINICO  NUMBER(10)    NOT NULL,
    NM_ARQUIVO         VARCHAR2(200) NOT NULL,
    DS_TIPO_MIME       VARCHAR2(100) NOT NULL,
    DS_CAMINHO         VARCHAR2(500) NOT NULL,
    NR_TAMANHO_BYTES   NUMBER(15)    NOT NULL,
    DT_CRIACAO         TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO     TIMESTAMP,
    CONSTRAINT PK_DOCUMENTO   PRIMARY KEY (ID_DOCUMENTO),
    CONSTRAINT FK_DOC_EVENTO  FOREIGN KEY (ID_EVENTO_CLINICO) REFERENCES EVENTO_CLINICO(ID_EVENTO)
);

CREATE INDEX IDX_DOC_EVENTO ON DOCUMENTO(ID_EVENTO_CLINICO);

COMMENT ON COLUMN DOCUMENTO.DS_CAMINHO IS 'Path no blob storage (Azure Blob/S3) — binário nunca armazenado no banco';

-- ============================================================================
-- 6. NOTIFICAÇÕES
-- ============================================================================

-- ----------------------------------------------------------------------------
-- NOTIFICACAO
-- Domínio: .NET (gera) | FKs CLINICA, TUTOR, VETERINARIO
-- ----------------------------------------------------------------------------
CREATE TABLE NOTIFICACAO (
    ID_NOTIFICACAO   NUMBER(10)     DEFAULT SEQ_NOTIFICACAO.NEXTVAL NOT NULL,
    ID_CLINICA       NUMBER(10)     NOT NULL,
    ID_TUTOR         NUMBER(10),
    ID_VETERINARIO   NUMBER(10),
    DS_TITULO        VARCHAR2(200)  NOT NULL,
    DS_MENSAGEM      VARCHAR2(500)  NOT NULL,
    ST_LIDA          CHAR(1)        NOT NULL,
    DT_LEITURA       TIMESTAMP,
    DT_CRIACAO       TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_NOTIFICACAO        PRIMARY KEY (ID_NOTIFICACAO),
    CONSTRAINT FK_NOTIF_CLINICA      FOREIGN KEY (ID_CLINICA)    REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT FK_NOTIF_TUTOR        FOREIGN KEY (ID_TUTOR)      REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT FK_NOTIF_VETERINARIO  FOREIGN KEY (ID_VETERINARIO) REFERENCES VETERINARIO(ID_VETERINARIO),
    CONSTRAINT CK_NOTIF_LIDA         CHECK (ST_LIDA IN ('S','N'))
);

CREATE INDEX IDX_NOTIF_CLINICA ON NOTIFICACAO(ID_CLINICA);
CREATE INDEX IDX_NOTIF_TUTOR   ON NOTIFICACAO(ID_TUTOR);

COMMENT ON TABLE NOTIFICACAO IS 'Notificações in-app e lembretes para tutores e veterinários.';

-- ============================================================================
-- 7. IOT — MONITORAMENTO DE TEMPERATURA DE VACINAS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- DISPOSITIVO_IOT  [v4 — colunas alinhadas ao modelo .NET]
-- Domínio: .NET | FK CLINICA
-- ----------------------------------------------------------------------------
CREATE TABLE DISPOSITIVO_IOT (
    ID_DISPOSITIVO   NUMBER(10)    DEFAULT SEQ_DISPOSITIVO_IOT.NEXTVAL NOT NULL,
    ID_CLINICA       NUMBER(10)    NOT NULL,
    CD_DISPOSITIVO   VARCHAR2(20)  NOT NULL,
    DS_DESCRICAO     VARCHAR2(500) NOT NULL,
    DS_LOCALIZACAO   VARCHAR2(500) NOT NULL,
    DT_CRIACAO       TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_DISPOSITIVO_IOT   PRIMARY KEY (ID_DISPOSITIVO),
    CONSTRAINT FK_IOT_CLINICA       FOREIGN KEY (ID_CLINICA) REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT UK_IOT_CD_DISPOSITIVO UNIQUE (CD_DISPOSITIVO)
);

CREATE INDEX IDX_IOT_CLINICA ON DISPOSITIVO_IOT(ID_CLINICA);

COMMENT ON COLUMN DISPOSITIVO_IOT.CD_DISPOSITIVO IS 'Código único do hardware (MAC ou ID do ESP32) — usado para autenticar via API key';

-- ----------------------------------------------------------------------------
-- LEITURA_TEMPERATURA  [v4 — FK ajustada, colunas VL_*, sem ST_DENTRO_FAIXA]
-- Domínio: .NET | FK DISPOSITIVO_IOT
-- Time-series: ~1.440 leituras/dia/dispositivo (1 leitura/min)
-- ----------------------------------------------------------------------------
CREATE TABLE LEITURA_TEMPERATURA (
    ID_LEITURA          NUMBER(15)    DEFAULT SEQ_LEITURA_TEMP.NEXTVAL NOT NULL,
    ID_DISPOSITIVO_IOT  NUMBER(10)    NOT NULL,
    VL_TEMPERATURA      NUMBER(5,2)   NOT NULL,
    VL_UMIDADE          NUMBER(5,2),
    DT_LEITURA          TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_CRIACAO          TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO      TIMESTAMP,
    CONSTRAINT PK_LEITURA_TEMP  PRIMARY KEY (ID_LEITURA),
    CONSTRAINT FK_LEITURA_DISP  FOREIGN KEY (ID_DISPOSITIVO_IOT) REFERENCES DISPOSITIVO_IOT(ID_DISPOSITIVO),
    CONSTRAINT CK_LEITURA_RANGE CHECK (VL_TEMPERATURA BETWEEN -40 AND 80)
);

CREATE INDEX IDX_LEITURA_DISP_DATA ON LEITURA_TEMPERATURA(ID_DISPOSITIVO_IOT, DT_LEITURA DESC);

COMMENT ON TABLE  LEITURA_TEMPERATURA           IS 'Time-series de leituras dos sensores IoT.';
COMMENT ON COLUMN LEITURA_TEMPERATURA.VL_UMIDADE IS 'Umidade % — opcional (sensores DHT11/DHT22)';

-- ----------------------------------------------------------------------------
-- ALERTA_TEMPERATURA  [v4 — FK para LEITURA, VL_LIMITE]
-- Domínio: .NET | FK LEITURA_TEMPERATURA
-- 1 alerta por evento (não por leitura) para evitar poluição no dashboard
-- ----------------------------------------------------------------------------
CREATE TABLE ALERTA_TEMPERATURA (
    ID_ALERTA              NUMBER(10)    DEFAULT SEQ_ALERTA_TEMP.NEXTVAL NOT NULL,
    ID_LEITURA_TEMPERATURA NUMBER(15)    NOT NULL,
    DS_TIPO_ALERTA         VARCHAR2(50)  NOT NULL,
    VL_LIMITE              NUMBER(5,2)   NOT NULL,
    DS_MENSAGEM            VARCHAR2(500) NOT NULL,
    ST_RESOLVIDO           CHAR(1)       DEFAULT 'N' NOT NULL,
    DT_CRIACAO             TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO         TIMESTAMP,
    CONSTRAINT PK_ALERTA_TEMP      PRIMARY KEY (ID_ALERTA),
    CONSTRAINT FK_ALERTA_LEITURA   FOREIGN KEY (ID_LEITURA_TEMPERATURA) REFERENCES LEITURA_TEMPERATURA(ID_LEITURA),
    CONSTRAINT CK_ALERTA_RESOLVIDO CHECK (ST_RESOLVIDO IN ('S','N'))
);

CREATE INDEX IDX_ALERTA_LEITURA ON ALERTA_TEMPERATURA(ID_LEITURA_TEMPERATURA);
CREATE INDEX IDX_ALERTA_ATIVOS  ON ALERTA_TEMPERATURA(ST_RESOLVIDO, DT_CRIACAO DESC);

COMMENT ON TABLE  ALERTA_TEMPERATURA             IS 'Alertas agregados de temperatura — 1 alerta por evento, não por leitura.';
COMMENT ON COLUMN ALERTA_TEMPERATURA.VL_LIMITE   IS 'Limite que foi ultrapassado (°C) — registrado no momento do alerta';
COMMENT ON COLUMN ALERTA_TEMPERATURA.ST_RESOLVIDO IS 'S = temperatura voltou à faixa e alerta foi fechado';

-- ============================================================================
-- 8. TRIAGEM LUNA  [v4 — novo]
-- ============================================================================

-- ----------------------------------------------------------------------------
-- TRIAGEM_LUNA
-- Domínio: .NET | FKs CLINICA, TUTOR (opt.), PET (opt.)
-- Registro de triagens realizadas pelo bot Luna (IA)
-- ----------------------------------------------------------------------------
CREATE TABLE TRIAGEM_LUNA (
    ID_TRIAGEM          NUMBER(10)    DEFAULT SEQ_TRIAGEM_LUNA.NEXTVAL NOT NULL,
    ID_CLINICA          NUMBER(10)    NOT NULL,
    ID_TUTOR            NUMBER(10),
    ID_PET              NUMBER(10),
    DS_NIVEL_URGENCIA   VARCHAR2(20)  NOT NULL,
    DS_DESCRICAO        VARCHAR2(2000) NOT NULL,
    ST_ENCAMINHADO_VET  CHAR(1)       NOT NULL,
    DT_TRIAGEM          TIMESTAMP     NOT NULL,
    ST_ATIVA            CHAR(1)       DEFAULT 'S' NOT NULL,
    DT_CRIACAO          TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO      TIMESTAMP,
    CONSTRAINT PK_TRIAGEM_LUNA       PRIMARY KEY (ID_TRIAGEM),
    CONSTRAINT FK_TRIAGEM_CLINICA    FOREIGN KEY (ID_CLINICA) REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT FK_TRIAGEM_TUTOR      FOREIGN KEY (ID_TUTOR)   REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT FK_TRIAGEM_PET        FOREIGN KEY (ID_PET)     REFERENCES PET(ID_PET),
    CONSTRAINT CK_TRIAGEM_ENCAM      CHECK (ST_ENCAMINHADO_VET IN ('S','N')),
    CONSTRAINT CK_TRIAGEM_ATIVA      CHECK (ST_ATIVA           IN ('S','N'))
);

CREATE INDEX IDX_TRIAGEM_CLINICA ON TRIAGEM_LUNA(ID_CLINICA);
CREATE INDEX IDX_TRIAGEM_PET     ON TRIAGEM_LUNA(ID_PET);

COMMENT ON TABLE  TRIAGEM_LUNA                   IS 'Triagens realizadas pela Luna (IA). Tutor e Pet são opcionais (usuário pode triagem sem cadastro).';
COMMENT ON COLUMN TRIAGEM_LUNA.DS_NIVEL_URGENCIA IS 'BAIXA, MEDIA, ALTA, CRITICA — classificação do bot';
COMMENT ON COLUMN TRIAGEM_LUNA.ST_ENCAMINHADO_VET IS 'S = bot indicou consulta presencial com veterinário';

-- ============================================================================
-- 9. DOMÍNIO JAVA — IDENTIDADE E AGENDAMENTO
-- ============================================================================
-- ORDEM DE DECLARAÇÃO: INVITE_TUTOR → CONTA_TUTOR → CONSENTIMENTO → AGENDAMENTO
-- ============================================================================

-- ----------------------------------------------------------------------------
-- INVITE_TUTOR  [v4 — estrutura redefinida pelo modelo .NET]
-- Domínio: .NET (gera) | Java (consulta para validar onboarding)
-- Convite invite-based: clínica gera token → envia ao tutor → Java valida ao criar conta
-- ----------------------------------------------------------------------------
CREATE TABLE INVITE_TUTOR (
    ID_INVITE        NUMBER(10)    DEFAULT SEQ_INVITE_TUTOR.NEXTVAL NOT NULL,
    ID_TUTOR         NUMBER(10)    NOT NULL,
    NR_TOKEN         VARCHAR2(36)  NOT NULL,
    DT_EXPIRACAO     TIMESTAMP     NOT NULL,
    DS_CANAL         VARCHAR2(20)  NOT NULL,
    ST_UTILIZADO     CHAR(1)       DEFAULT 'N' NOT NULL,
    ST_ATIVO         CHAR(1)       DEFAULT 'S' NOT NULL,
    DT_CRIACAO       TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ATUALIZACAO   TIMESTAMP,
    CONSTRAINT PK_INVITE_TUTOR   PRIMARY KEY (ID_INVITE),
    CONSTRAINT FK_INVITE_TUTOR   FOREIGN KEY (ID_TUTOR) REFERENCES TUTOR(ID_TUTOR) ON DELETE CASCADE,
    CONSTRAINT UK_INVITE_TOKEN   UNIQUE (NR_TOKEN),
    CONSTRAINT CK_INVITE_CANAL   CHECK (DS_CANAL      IN ('WHATSAPP','EMAIL','SMS')),
    CONSTRAINT CK_INVITE_UTILIZ  CHECK (ST_UTILIZADO  IN ('S','N')),
    CONSTRAINT CK_INVITE_ATIVO   CHECK (ST_ATIVO      IN ('S','N'))
);

CREATE INDEX IDX_INVITE_TOKEN ON INVITE_TUTOR(NR_TOKEN);
CREATE INDEX IDX_INVITE_TUTOR ON INVITE_TUTOR(ID_TUTOR);

COMMENT ON TABLE  INVITE_TUTOR             IS 'Convite de onboarding — gerado pela clínica (.NET), validado pelo Java.';
COMMENT ON COLUMN INVITE_TUTOR.NR_TOKEN    IS 'UUID gerado com SecureRandom — enviado ao tutor pelo canal escolhido';
COMMENT ON COLUMN INVITE_TUTOR.ST_UTILIZADO IS 'S = invite já foi consumido na criação da CONTA_TUTOR';
COMMENT ON COLUMN INVITE_TUTOR.ST_ATIVO    IS 'S = válido para uso (soft delete — N quando expirado ou revogado)';

-- ----------------------------------------------------------------------------
-- CONTA_TUTOR
-- Domínio: Java (Nikolas) | FK TUTOR, FK INVITE_TUTOR
-- Credenciais de acesso ao portal — gerenciada exclusivamente pelo Java
-- ----------------------------------------------------------------------------
CREATE TABLE CONTA_TUTOR (
    ID_CONTA              NUMBER(10)    DEFAULT SEQ_CONTA_TUTOR.NEXTVAL NOT NULL,
    ID_TUTOR              NUMBER(10)    NOT NULL,
    DS_EMAIL_LOGIN        VARCHAR2(200) NOT NULL,
    DS_SENHA_HASH         VARCHAR2(256) NOT NULL,
    DS_SALT               VARCHAR2(64),
    DS_REFRESH_TOKEN_HASH VARCHAR2(256),
    DT_REFRESH_EXPIRA     TIMESTAMP,
    DT_CADASTRO           TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_ULTIMO_LOGIN       TIMESTAMP,
    NR_TENTATIVAS_LOGIN   NUMBER(2)     DEFAULT 0 NOT NULL,
    DT_BLOQUEIO           TIMESTAMP,
    ST_ATIVA              CHAR(1)       DEFAULT 'S' NOT NULL,
    ST_EMAIL_VERIFICADO   CHAR(1)       DEFAULT 'N' NOT NULL,
    DS_TOKEN_RESET        VARCHAR2(256),
    DT_TOKEN_EXPIRA       TIMESTAMP,
    ID_INVITE_USADO       NUMBER(10),
    CONSTRAINT PK_CONTA_TUTOR       PRIMARY KEY (ID_CONTA),
    CONSTRAINT FK_CONTA_TUTOR       FOREIGN KEY (ID_TUTOR)        REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT FK_CONTA_INVITE      FOREIGN KEY (ID_INVITE_USADO) REFERENCES INVITE_TUTOR(ID_INVITE),
    CONSTRAINT UK_CONTA_TUTOR       UNIQUE (ID_TUTOR),
    CONSTRAINT UK_CONTA_EMAIL       UNIQUE (DS_EMAIL_LOGIN),
    CONSTRAINT UK_CONTA_INVITE_USED UNIQUE (ID_INVITE_USADO),
    CONSTRAINT CK_CONTA_ATIVA       CHECK (ST_ATIVA             IN ('S','N')),
    CONSTRAINT CK_CONTA_EMAIL_VERIF CHECK (ST_EMAIL_VERIFICADO  IN ('S','N')),
    CONSTRAINT CK_CONTA_TENTATIVAS  CHECK (NR_TENTATIVAS_LOGIN BETWEEN 0 AND 99)
);

COMMENT ON TABLE  CONTA_TUTOR                       IS 'Credenciais de acesso ao portal. Gerenciada exclusivamente pelo backend Java.';
COMMENT ON COLUMN CONTA_TUTOR.DS_SENHA_HASH         IS 'BCrypt — NUNCA texto plano';
COMMENT ON COLUMN CONTA_TUTOR.DS_REFRESH_TOKEN_HASH IS 'SHA-256 do refresh token — texto plano nunca persiste';
COMMENT ON COLUMN CONTA_TUTOR.ID_INVITE_USADO       IS 'UK garante 1 invite = 1 conta (anti-reuso)';

-- ----------------------------------------------------------------------------
-- CONSENTIMENTO
-- Domínio: Java | FK TUTOR
-- Registro LGPD imutável (histórico por INSERT, nunca UPDATE)
-- ----------------------------------------------------------------------------
CREATE TABLE CONSENTIMENTO (
    ID_CONSENTIMENTO   NUMBER(10)    DEFAULT SEQ_CONSENTIMENTO.NEXTVAL NOT NULL,
    ID_TUTOR           NUMBER(10)    NOT NULL,
    DS_TIPO            VARCHAR2(100) NOT NULL,
    NR_VERSAO_TERMO    VARCHAR2(20)  NOT NULL,
    ST_ACEITO          CHAR(1)       NOT NULL,
    DT_CONSENTIMENTO   TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_CONSENTIMENTO PRIMARY KEY (ID_CONSENTIMENTO),
    CONSTRAINT FK_CONS_TUTOR    FOREIGN KEY (ID_TUTOR) REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT CK_CONS_ACEITO   CHECK (ST_ACEITO IN ('S','N'))
);

CREATE INDEX IDX_CONS_TUTOR ON CONSENTIMENTO(ID_TUTOR);

COMMENT ON TABLE CONSENTIMENTO IS 'Registro LGPD. Cada novo aceite/revogação = nova linha (histórico imutável).';

-- ----------------------------------------------------------------------------
-- AGENDAMENTO
-- Domínio: Java (Nikolas) | FKs CLINICA, TUTOR, PET, VETERINARIO
-- .NET lê apenas; NR_VERSION para optimistic locking JPA @Version
-- ----------------------------------------------------------------------------
CREATE TABLE AGENDAMENTO (
    ID_AGENDAMENTO       NUMBER(10)    DEFAULT SEQ_AGENDAMENTO.NEXTVAL NOT NULL,
    ID_CLINICA           NUMBER(10)    NOT NULL,
    ID_TUTOR             NUMBER(10),
    ID_PET               NUMBER(10),
    ID_VETERINARIO       NUMBER(10),
    NM_PACIENTE          VARCHAR2(200),
    DT_AGENDAMENTO       TIMESTAMP     NOT NULL,
    NR_DURACAO_MINUTOS   NUMBER(4),
    DS_SERVICO           VARCHAR2(500),
    DS_TIPO_CONSULTA     VARCHAR2(200),
    ST_STATUS            VARCHAR2(50),
    DS_STATUS            VARCHAR2(200),
    DS_ORIGEM            VARCHAR2(100),
    NR_VERSION           NUMBER(10)    DEFAULT 0 NOT NULL,
    CONSTRAINT PK_AGENDAMENTO       PRIMARY KEY (ID_AGENDAMENTO),
    CONSTRAINT FK_AGEND_CLINICA     FOREIGN KEY (ID_CLINICA)     REFERENCES CLINICA(ID_CLINICA),
    CONSTRAINT FK_AGEND_TUTOR       FOREIGN KEY (ID_TUTOR)       REFERENCES TUTOR(ID_TUTOR),
    CONSTRAINT FK_AGEND_PET         FOREIGN KEY (ID_PET)         REFERENCES PET(ID_PET),
    CONSTRAINT FK_AGEND_VETERINARIO FOREIGN KEY (ID_VETERINARIO) REFERENCES VETERINARIO(ID_VETERINARIO),
    CONSTRAINT CK_AGEND_VERSION     CHECK (NR_VERSION >= 0)
);

CREATE INDEX IDX_AGEND_CLINICA ON AGENDAMENTO(ID_CLINICA);
CREATE INDEX IDX_AGEND_PET     ON AGENDAMENTO(ID_PET);
CREATE INDEX IDX_AGEND_DATA    ON AGENDAMENTO(DT_AGENDAMENTO);
CREATE INDEX IDX_AGEND_STATUS  ON AGENDAMENTO(ST_STATUS);

COMMENT ON COLUMN AGENDAMENTO.NR_VERSION IS 'Optimistic locking via JPA @Version — incrementado em cada UPDATE pelo Java';

-- ============================================================================
-- 10. AUDITORIA
-- ============================================================================

-- ----------------------------------------------------------------------------
-- LOG_ERRO
-- Domínio: Java (escreve via EXCEPTION WHEN OTHERS das procedures Oracle)
-- .NET usa ILogger estruturado — não escreve nesta tabela
-- Sem FKs: log de erro nunca deve falhar por violação referencial
-- ----------------------------------------------------------------------------
CREATE TABLE LOG_ERRO (
    ID_LOG            NUMBER(15)     DEFAULT SEQ_LOG_ERRO.NEXTVAL NOT NULL,
    NM_PROCEDURE      VARCHAR2(120)  NOT NULL,
    NM_USUARIO        VARCHAR2(60)   NOT NULL,
    DT_ERRO           TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
    NR_CODIGO_ERRO    NUMBER(10)     NOT NULL,
    DS_MENSAGEM_ERRO  VARCHAR2(2000) NOT NULL,
    DS_PARAMETROS     VARCHAR2(2000),
    DS_STACK_TRACE    CLOB,
    CONSTRAINT PK_LOG_ERRO PRIMARY KEY (ID_LOG)
);

CREATE INDEX IDX_LOG_DATA      ON LOG_ERRO(DT_ERRO DESC);
CREATE INDEX IDX_LOG_PROCEDURE ON LOG_ERRO(NM_PROCEDURE);

COMMENT ON TABLE LOG_ERRO IS 'Erros de procedures Oracle (requisito FIAP — disciplina Banco). .NET usa ILogger estruturado.';

-- ----------------------------------------------------------------------------
-- IDEMPOTENCY_KEY
-- Domínio: Java | Garante que POSTs sensíveis processem exatamente 1 vez
-- ----------------------------------------------------------------------------
CREATE TABLE IDEMPOTENCY_KEY (
    ID_IDEMPOTENCY       NUMBER(10)    DEFAULT SEQ_IDEMPOTENCY_KEY.NEXTVAL NOT NULL,
    DS_KEY               VARCHAR2(64)  NOT NULL,
    NM_RESOURCE          VARCHAR2(60)  NOT NULL,
    ID_RESOURCE_CRIADO   NUMBER(10)    NOT NULL,
    DT_CRIACAO           TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    DT_EXPIRACAO         TIMESTAMP     NOT NULL,
    CONSTRAINT PK_IDEMPOTENCY_KEY    PRIMARY KEY (ID_IDEMPOTENCY),
    CONSTRAINT UK_IDEMPOTENCY_CHAVE  UNIQUE (DS_KEY, NM_RESOURCE),
    CONSTRAINT CK_IDEMPOTENCY_EXPIRA CHECK (DT_EXPIRACAO > DT_CRIACAO)
);

CREATE INDEX IDX_IDEMPOT_EXPIRA ON IDEMPOTENCY_KEY(DT_EXPIRACAO);

COMMENT ON COLUMN IDEMPOTENCY_KEY.DS_KEY           IS 'UUID gerado pelo cliente (header Idempotency-Key)';
COMMENT ON COLUMN IDEMPOTENCY_KEY.NM_RESOURCE      IS 'Recurso alvo (ex: CONSENTIMENTO, AGENDAMENTO)';
COMMENT ON COLUMN IDEMPOTENCY_KEY.ID_RESOURCE_CRIADO IS 'PK do registro criado — retornado em chamadas duplicadas sem reprocessar';

-- ============================================================================
-- 11. VIEWS ÚTEIS
-- ============================================================================

-- Timeline completa do pet (usada pelo Backend Tutor e pela Luna)
CREATE OR REPLACE VIEW VW_TIMELINE_PET AS
SELECT
    p.ID_PET,
    p.NM_PET,
    e.ID_EVENTO,
    te.NM_TIPO               AS NM_TIPO_EVENTO,
    e.DT_EVENTO,
    v.NM_VETERINARIO,
    e.DS_OBSERVACAO,
    te.CD_TIPO               AS CD_TIPO_EVENTO
FROM PET p
JOIN EVENTO_CLINICO e  ON e.ID_PET          = p.ID_PET
JOIN VETERINARIO    v  ON v.ID_VETERINARIO  = e.ID_VETERINARIO
JOIN TIPO_EVENTO   te  ON te.ID_TIPO_EVENTO = e.ID_TIPO_EVENTO
WHERE p.ST_ATIVO = 'S';

-- Vacinas vencendo nos próximos 30 dias (alimenta lembretes da Luna)
CREATE OR REPLACE VIEW VW_VACINAS_VENCENDO AS
SELECT
    p.ID_PET,
    p.NM_PET,
    t.ID_TUTOR,
    t.NM_TUTOR,
    t.DS_WHATSAPP,
    vac.NM_VACINA,
    vac.DT_PROXIMA_DOSE,
    (TRUNC(vac.DT_PROXIMA_DOSE) - TRUNC(SYSDATE)) AS DIAS_RESTANTES,
    c.NM_CLINICA
FROM VACINA vac
JOIN EVENTO_CLINICO e   ON e.ID_EVENTO   = vac.ID_EVENTO_CLINICO
JOIN PET            p   ON p.ID_PET      = e.ID_PET
JOIN TUTOR_PET      tp  ON tp.ID_PET     = p.ID_PET AND tp.ST_PRINCIPAL = 'S'
JOIN TUTOR          t   ON t.ID_TUTOR    = tp.ID_TUTOR
JOIN CLINICA        c   ON c.ID_CLINICA  = e.ID_CLINICA
WHERE vac.DT_PROXIMA_DOSE BETWEEN SYSTIMESTAMP AND SYSTIMESTAMP + INTERVAL '30' DAY
  AND p.ST_ATIVO = 'S';

-- ============================================================================
-- FIM DA DDL — kura_schema_v4.sql
-- ============================================================================
-- RESUMO DAS ALTERAÇÕES (v3 → v4):
--
--   SEQUENCES adicionadas (2):
--     SEQ_CONSULTA, SEQ_TRIAGEM_LUNA
--
--   TABELAS novas (2):
--     CONSULTA       — subtipo de EVENTO_CLINICO (motivo, anamnese, exame físico)
--     TRIAGEM_LUNA   — triagens IA/bot com nível de urgência e encaminhamento
--
--   TABELAS modificadas (.NET):
--     CLINICA        — +DS_EMAIL_ACESSO (UNIQUE), +DS_SENHA_HASH
--     ESPECIE        — colunas alinhadas ao EF (NM_ESPECIE 200, DT_CRIACAO/DT_ATUALIZACAO)
--     MEDICAMENTO    — DS_PRINCIPIO_ATIVO, DS_APRESENTACAO (via EF); sem ST_CONTROLADO
--     TIPO_EVENTO    — +CD_TIPO (UK), NM_TIPO; sem NM_TIPO_EVENTO
--     VETERINARIO    — NR_TELEFONE (era DS_TELEFONE); DT_CRIACAO/DT_ATUALIZACAO
--     TUTOR          — NR_TELEFONE (era DS_TELEFONE); DT_CRIACAO/DT_ATUALIZACAO
--     PET            — +ID_CLINICA; DT_CRIACAO/DT_ATUALIZACAO
--     EVENTO_CLINICO — +ID_CLINICA; DS_OBSERVACAO (era DS_OBSERVACOES/DS_DIAGNOSTICO)
--     VACINA         — ID_EVENTO_CLINICO (era ID_EVENTO); DS_FABRICANTE
--     PRESCRICAO     — ID_EVENTO_CLINICO; +ID_MEDICAMENTO, DS_POSOLOGIA, NR_DURACAO_DIAS
--     EXAME          — ID_EVENTO_CLINICO; DS_RESULTADO, DT_REALIZACAO
--     DOCUMENTO      — ID_EVENTO_CLINICO; NM_ARQUIVO, DS_TIPO_MIME, DS_CAMINHO
--     NOTIFICACAO    — +ID_VETERINARIO; ST_LIDA; sem DS_CANAL/DS_TIPO/ST_STATUS
--     DISPOSITIVO_IOT — CD_DISPOSITIVO, DS_DESCRICAO, DS_LOCALIZACAO; sem thresholds
--     LEITURA_TEMP   — ID_DISPOSITIVO_IOT; VL_TEMPERATURA, VL_UMIDADE; sem ST_DENTRO_FAIXA
--     ALERTA_TEMP    — ID_LEITURA_TEMPERATURA; VL_LIMITE; sem ID_DISPOSITIVO
--     INVITE_TUTOR   — NR_TOKEN VARCHAR2(36), ST_UTILIZADO; sem DS_TOKEN/DT_GERACAO
--
--   PKs canonicais em todas as tabelas: ID_<TABELA>
--   ST_ATIVA/ST_ATIVO removida de tabelas sem soft-delete
--   ST_ATIVA renomeada para ST_ATIVO em VETERINARIO, TUTOR, PET
-- ============================================================================
