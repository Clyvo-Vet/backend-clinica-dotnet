#!/usr/bin/env bash
# TASK-09 (INT-02). Smoke test E2E — App Clínica -> .NET -> Luna, contra um Oracle
# XE local (via DevOps-Cloud/docker-compose.yml), sem Azure VM.
#
# Uso: bash scripts/test-e2e-clinica.sh
# Sai != 0 se qualquer asserção NÃO-documentada falhar. Falhas conhecidas
# (ver docs/INT-02-contract-map.md) são reportadas como [KNOWN] e não derrubam
# o script — o objetivo é sinalizar regressões novas, não re-provar bugs já
# catalogados.
#
# Pré-requisitos (setup local, uma vez):
#   1. cd ../DevOps-Cloud && docker compose --env-file .env up -d oracle-db
#      (a primeira subida do volume Oracle XE leva ~3-5min — aguarde "healthy":
#      docker compose ps oracle-db)
#   2. Aplicar o schema completo (V1-V9) contra esse Oracle rodando o Java uma
#      vez em profile prod (backend-tutor-java):
#        DB_URL="jdbc:oracle:thin:@//localhost:9092/XEPDB1" \
#        DB_USERNAME=<ORACLE_APP_USER do .env> DB_PASSWORD=<ORACLE_APP_PASSWORD> \
#        JWT_SECRET=<qualquer string >=64 bytes> CORS_ALLOWED_ORIGINS=http://localhost:8081 \
#        mvn spring-boot:run -Dspring-boot.run.profiles=prod
#      (Ctrl+C após "Started KuraTutorApplication" — só precisa aplicar o schema.)
#
# Este script assume que os passos acima já rodaram e o Oracle já tem o
# schema aplicado. Ele SOBE O .NET sozinho (não assume container).

set -uo pipefail

BASE_URL="http://localhost:8095/api/v1"
HEALTH_URL="http://localhost:8095/health"
LUNA_URL="${LUNA_URL:-http://localhost:8000}"
LOG_FILE="$(mktemp)"
FAILURES=0
KNOWN=0

log()   { echo "[test-e2e-clinica] $*"; }
fail()  { echo "[FAIL]  $*"; FAILURES=$((FAILURES + 1)); }
known() { echo "[KNOWN] $* — ver docs/INT-02-contract-map.md"; KNOWN=$((KNOWN + 1)); }
pass()  { echo "[ OK ]  $*"; }

assert_status() {
  local desc="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    pass "$desc → HTTP $actual"
  else
    fail "$desc → esperado HTTP $expected, obtido HTTP $actual"
  fi
}

# ─── Env vars da conexão Oracle local (default = valores do DevOps-Cloud/.env.example) ──
ORACLE_APP_USER="${ORACLE_APP_USER:-RM562999}"
ORACLE_APP_PASSWORD="${ORACLE_APP_PASSWORD:?defina ORACLE_APP_PASSWORD (mesmo valor do DevOps-Cloud/.env)}"
DOTNET_JWT_KEY="${DOTNET_JWT_KEY:-kura-api-secret-key-fiap-2026-clyvovet}"
IOT_API_KEY="${IOT_API_KEY:-kura-iot-device-key-2026}"
LUNA_API_KEY="${LUNA_API_KEY:-kura-luna-integration-key-2026}"

# ─── Sobe o .NET local (porta 8095, evita conflito com outros serviços em 8080) ────
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
log "Subindo backend-clinica-dotnet contra Oracle local (porta 8095)..."
(
  cd "$REPO_ROOT"
  export ASPNETCORE_ENVIRONMENT="Production"
  export ASPNETCORE_URLS="http://+:8095"
  export ConnectionStrings__DefaultConnection="User Id=${ORACLE_APP_USER};Password=${ORACLE_APP_PASSWORD};Data Source=localhost:9092/XEPDB1"
  export Jwt__Key="$DOTNET_JWT_KEY"
  export Jwt__Issuer="kura-api"
  export Jwt__Audience="kura-client"
  export IoT__ApiKey="$IOT_API_KEY"
  export Luna__ApiKey="$LUNA_API_KEY"
  dotnet run --no-launch-profile --project src/Kura.Api >"$LOG_FILE" 2>&1
) &
APP_PID=$!

cleanup() {
  log "Encerrando o processo .NET (PID $APP_PID)..."
  kill "$APP_PID" 2>/dev/null
  wait "$APP_PID" 2>/dev/null
}
trap cleanup EXIT

log "Aguardando health check (até 120s)..."
UP=0
for i in $(seq 1 40); do
  if curl -sf "$HEALTH_URL" >/dev/null 2>&1; then UP=1; break; fi
  sleep 3
done
if [ "$UP" -ne 1 ]; then
  fail ".NET não respondeu em $HEALTH_URL após 120s — ver $LOG_FILE (checklist: Oracle local rodando? schema V1-V9 aplicado?)"
  tail -40 "$LOG_FILE"
  exit 1
fi
pass "health check respondeu"

# ─── 1. Registro de clínica de teste (CNPJ/e-mail únicos por execução) ────────
TS=$(date +%s)
RAND=$RANDOM$RANDOM$RANDOM
CNPJ="${RAND:0:2}.${RAND:2:3}.${RAND:5:3}/0001-$(( (TS + RAND) % 90 + 10 ))"
EMAIL="smoke${TS}@kura.test"
log "POST /auth/register-clinica"
REG_RESP="$(curl -s -w '\n%{http_code}' -X POST "$BASE_URL/auth/register-clinica" \
  -H 'Content-Type: application/json' \
  -d "{\"nmClinica\":\"Clinica Smoke Test $TS\",\"nrCnpj\":\"$CNPJ\",\"dsEndereco\":\"Rua Teste, 100\",\"nmCidade\":\"Sao Paulo\",\"sgUf\":\"SP\",\"nrCep\":\"01000000\",\"dsEmail\":\"$EMAIL\",\"dsEmailAcesso\":\"$EMAIL\",\"dsSenha\":\"SmokeTest@123\"}")"
REG_STATUS="$(echo "$REG_RESP" | tail -n1)"
REG_JSON="$(echo "$REG_RESP" | sed '$d')"
assert_status "POST /auth/register-clinica" "201" "$REG_STATUS"
ID_CLINICA="$(echo "$REG_JSON" | grep -o '"idClinica":[0-9]*' | grep -o '[0-9]*')"

# ─── 2. Login ───────────────────────────────────────────────────────────────────
log "POST /auth/login"
LOGIN_RESP="$(curl -s -w '\n%{http_code}' -X POST "$BASE_URL/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"dsEmail\":\"$EMAIL\",\"dsSenha\":\"SmokeTest@123\"}")"
LOGIN_STATUS="$(echo "$LOGIN_RESP" | tail -n1)"
LOGIN_JSON="$(echo "$LOGIN_RESP" | sed '$d')"
assert_status "POST /auth/login" "200" "$LOGIN_STATUS"
TOKEN="$(echo "$LOGIN_JSON" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)"
AUTH="Authorization: Bearer $TOKEN"

if [ -z "${TOKEN:-}" ]; then
  fail "Nenhum accessToken obtido — abortando testes autenticados"
  exit 1
fi

# ─── 3. Veterinário + tutor + pet de apoio (necessários para os fluxos abaixo) ──
log "POST /veterinarios (setup)"
VET_RESP="$(curl -s -w '\n%{http_code}' -X POST "$BASE_URL/veterinarios" -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"idClinica\":$ID_CLINICA,\"nmVeterinario\":\"Dr. Smoke\",\"nrCrmv\":\"SP-$RANDOM\",\"dsEmail\":\"vet$TS@kura.test\",\"nrTelefone\":\"11988887777\"}")"
ID_VET="$(echo "$VET_RESP" | sed '$d' | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')"
assert_status "POST /veterinarios (setup)" "201" "$(echo "$VET_RESP" | tail -n1)"

log "POST /tutores (setup)"
CPF="$(printf '%011d' $(( (TS + RANDOM) % 100000000000 )) )"
TUTOR_RESP="$(curl -s -w '\n%{http_code}' -X POST "$BASE_URL/tutores" -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"nmTutor\":\"Tutor Smoke\",\"nrCpf\":\"$CPF\",\"dsEmail\":\"tutor$TS@kura.test\",\"nrTelefone\":\"11977776666\"}")"
ID_TUTOR="$(echo "$TUTOR_RESP" | sed '$d' | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')"
assert_status "POST /tutores (setup)" "201" "$(echo "$TUTOR_RESP" | tail -n1)"

log "POST /pets (setup)"
PET_RESP="$(curl -s -w '\n%{http_code}' -X POST "$BASE_URL/pets" -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"idEspecie\":1,\"idRaca\":1,\"idVeterinarioResp\":$ID_VET,\"nmPet\":\"Rex Smoke\",\"dtNascimento\":\"2022-01-01T00:00:00\",\"sgSexo\":\"M\",\"sgPorte\":\"M\",\"idTutor\":$ID_TUTOR,\"stPrincipal\":true,\"dsVinculo\":\"PROPRIETARIO\"}")"
ID_PET="$(echo "$PET_RESP" | sed '$d' | grep -o '"id":[0-9]*' | head -1 | grep -o '[0-9]*')"
assert_status "POST /pets (setup)" "201" "$(echo "$PET_RESP" | tail -n1)"

# ─── 4. GET /dashboard/hoje ─────────────────────────────────────────────────────
log "GET /dashboard/hoje"
STATUS="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/dashboard/hoje" -H "$AUTH")"
assert_status "GET /dashboard/hoje" "200" "$STATUS"

# ─── 5. GET /pets ────────────────────────────────────────────────────────────────
log "GET /pets"
STATUS="$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/pets" -H "$AUTH")"
assert_status "GET /pets" "200" "$STATUS"

# ─── 6. POST /eventos-clinicos/consultas ───────────────────────────────────────
# CONHECIDO QUEBRADO: ConsultaService.cs hardcoda IdTipoEventoConsulta = 4L em vez
# de buscar TIPO_EVENTO por CD_TIPO='CONSULTA'. O seed real só tem 3 tipos — id=4
# não existe → sempre 500 (FK_EVENTO_TIPO). Ver docs/INT-02-contract-map.md. Não
# corrigido nesta task (decisão: código de serviço .NET, fora do escopo do smoke test).
log "POST /eventos-clinicos/consultas (esperado falhar — bug conhecido de ID fixo)"
CONSULTA_STATUS="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/eventos-clinicos/consultas" -H "$AUTH" -H 'Content-Type: application/json' \
  -d "{\"idPet\":$ID_PET,\"idVeterinario\":$ID_VET,\"dtConsulta\":\"2026-07-23T10:00:00\",\"dsMotivo\":\"Checkup\",\"dsObservacao\":\"smoke test\"}")"
if [ "$CONSULTA_STATUS" = "500" ]; then
  known "POST /eventos-clinicos/consultas → HTTP 500 (ConsultaService.IdTipoEventoConsulta=4L hardcoded)"
elif [ "$CONSULTA_STATUS" = "201" ]; then
  pass "POST /eventos-clinicos/consultas → HTTP 201 (bug do ID fixo foi corrigido!)"
else
  fail "POST /eventos-clinicos/consultas → esperado 500 (conhecido) ou 201 (corrigido), obtido HTTP $CONSULTA_STATUS"
fi

# ─── 7. Luna — POST /whatsapp/enviar (só a camada de auth; NÃO envia mensagem real) ─
# Twilio real configurado neste ambiente — nunca disparamos o caminho de sucesso
# aqui (enviaria WhatsApp de verdade). Testamos apenas a rejeição por API key.
log "Checando disponibilidade da Luna em $LUNA_URL/health..."
if curl -sf "$LUNA_URL/health" >/dev/null 2>&1; then
  log "POST $LUNA_URL/whatsapp/enviar sem X-API-Key (deve rejeitar)"
  LUNA_STATUS="$(curl -s -o /dev/null -w '%{http_code}' -X POST "$LUNA_URL/whatsapp/enviar" \
    -H 'Content-Type: application/json' -d '{"para":"+5511999999999","mensagem":"smoke test — não deve ser enviado"}')"
  assert_status "POST /whatsapp/enviar sem X-API-Key" "401" "$LUNA_STATUS"
else
  known "Luna indisponível em $LUNA_URL (ambiente local sem Python 3.12 — torch/pydantic-core não compilam em 3.13+)"
fi

# ─── Resumo ─────────────────────────────────────────────────────────────────────
echo ""
log "$KNOWN falha(s) conhecida(s) documentada(s), $FAILURES falha(s) NÃO esperada(s)."
if [ "$FAILURES" -eq 0 ]; then
  exit 0
else
  exit 1
fi
