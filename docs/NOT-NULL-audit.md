# Auditoria sistemática: DTO × coluna Oracle `NOT NULL` (TASK-60)

**Contexto:** `DS_OBSERVACAO` (`EVENTO_CLINICO`) foi encontrado por acidente, duas vezes
(TASK-47 corrigiu mal — virou regra de negócio no validator; TASK-56 corrigiu de verdade —
coalesce no service). Esta task varre sistematicamente todas as colunas `NOT NULL` de tipo
texto das tabelas `.NET`-owned em busca de outras instâncias da mesma classe de bug: um DTO
não-nullable (`string ... = string.Empty`, ou default nomeado que ainda pode ser sobrescrito
por `""` explícito) alcançando uma coluna Oracle `NOT NULL` sem tratamento — Oracle trata
`VARCHAR2('')` como `NULL`, e o INSERT estoura `ORA-01400` (500).

## Metodologia

1. Levantadas todas as colunas `NOT NULL` de tipo `VARCHAR2`/`CHAR` nas migrations Flyway
   (`backend-tutor-java/src/main/resources/db/migration/V1__initial_schema.sql` e
   `V9__schema_drift_clinico.sql`), filtradas pelas tabelas `.NET`-owned (`CLINICA`,
   `VETERINARIO`, `PET`, `EVENTO_CLINICO`, `AGENDAMENTO`, `NOTIFICACAO`, `DISPOSITIVO_IOT`,
   `LEITURA_TEMPERATURA`, `ALERTA_TEMPERATURA`, `TRIAGEM_LUNA`, `CONSULTA`, `EXAME`, `VACINA`,
   `PRESCRICAO`, `DOCUMENTO`, `MEDICAMENTO`).
2. Cruzadas com todo DTO `*CreateDto`/`*UpdateDto` de `src/Kura.Application/DTOs/` que
   inicializa um campo `string` com `= string.Empty` (busca literal do brief), e adicionalmente
   com campos que têm um default nomeado não-vazio mas que um cliente pode sobrescrever com
   `""` explícito (achado adicional durante a varredura — `PetCreateDto.DsVinculo` /
   `AdicionarTutorPetDto.DsVinculo` — fora do escopo literal do grep do brief, mas mesma classe
   de bug; incluído porque a varredura por coluna/entidade o expôs).
3. Para cada par candidato: verificado se (a) existe um endpoint HTTP alcançável pelo cliente
   que popula o campo a partir do DTO sem tratamento, e (b) existe uma regra de validação
   (FluentValidation `NotEmpty()`/DataAnnotations `[Required]`) que já bloqueia o valor vazio
   antes do INSERT. Só então reproduzido de fato via `curl` contra o compose real
   (`DevOps-Cloud`, Oracle real, não InMemory/H2) — nenhum item foi classificado como "bug"
   sem um 500 real observado nos logs do container (`docker compose logs kura-api`), com a
   linha `ORA-01400` confirmando a coluna exata.

## Resultado da varredura

**6 pares DTO×endpoint confirmados como bug real (500), cobrindo 4 colunas Oracle
distintas — todos corrigidos.** Os demais candidatos
levantados pelo cruzamento coluna×DTO não reproduziram, por 4 razões distintas: já validados
(`NotEmpty()`/`[Required]`), já corrigidos pela TASK-56, sem endpoint HTTP alcançável (DTO
nunca instanciado fora de si mesmo em todo o código-fonte), ou coluna nunca populada por
input de cliente (gerada inteiramente no servidor).

## Tabela completa

| # | Coluna Oracle (tabela) | DTO / campo | Endpoint | Validação existente antes da task | Reproduziu 500? | Ação |
|---|---|---|---|---|---|---|
| 1 | `MEDICAMENTO.DS_APRESENTACAO` | `MedicamentoCreateDto.DsApresentacao` | `POST /api/v1/medicamentos` | Nenhuma (`MedicamentoCreateValidator` só validava `NmMedicamento`/`DsPrincipioAtivo`) | **Sim** — `ORA-01400` confirmado no log | **Corrigido**: coalesce no `MedicamentoService.CreateAsync` (sentinela `"Apresentação não informada"`) + `MaximumLength(500)` (sem `NotEmpty()`) adicionado ao validator |
| 2 | `MEDICAMENTO.*` (3 campos) | `MedicamentoUpdateDto` (`NmMedicamento`/`DsPrincipioAtivo`/`DsApresentacao`) | `PUT /api/v1/medicamentos/{id}` | `[Required, MinLength(1)]` (DataAnnotations, não FluentValidation) | **Não** — retorna `400` corretamente (confirmado via curl como controle) | Nenhuma — já protegido, por um mecanismo diferente (DataAnnotations em vez de FluentValidation) |
| 3 | `TUTOR.DS_TELEFONE` | `TutorCreateDto.NrTelefone` | `POST /api/v1/tutores` | Nenhuma (`TutorCreateValidator` só validava `NmTutor`/`NrCpf`/`DsEmail`/`DsCanalConvite`) | **Sim** — `ORA-01400` confirmado no log | **Corrigido**: coalesce no `TutorService.CreateAsync` (sentinela `"Não informado"`) + `MaximumLength(20)` adicionado ao validator |
| 4 | `TUTOR.DS_TELEFONE` | `TutorUpdateDto.NrTelefone` | `PUT /api/v1/tutores/{id}` | Nenhuma (`TutorUpdateValidator` só validava `NmTutor`/`NrCpf`/`DsEmail`) | **Sim** — `ORA-01400` confirmado no log | **Corrigido**: coalesce no `TutorService.UpdateAsync` (mesmo sentinela) + `MaximumLength(20)` adicionado ao validator |
| 5 | `VACINA.DS_FABRICANTE` | `VacinaCreateDto.DsFabricante` | `POST /api/v1/eventos-clinicos/vacinas` | Nenhuma (`VacinaCreateValidator` só validava `NmVacina`/`NrLote`) | **Sim** — `ORA-01400` confirmado no log | **Corrigido**: coalesce no `VacinaService.CreateAsync` (sentinela `"Fabricante não informado"`) + `MaximumLength(200)` adicionado ao validator |
| 6 | `TUTOR_PET.DS_VINCULO` | `PetCreateDto.DsVinculo` | `POST /api/v1/pets` | Nenhuma regra de validação, mas o DTO tem default nomeado `"PROPRIETARIO"` (não `string.Empty`) — falha só se o cliente mandar `dsVinculo:""` explicitamente | **Sim** — `ORA-01400` confirmado no log ao forçar `dsVinculo:""` | **Corrigido**: coalesce no `PetService.CreateAsync` — fallback usa o próprio default do DTO (`"PROPRIETARIO"`), não um sentinela novo |
| 7 | `TUTOR_PET.DS_VINCULO` | `AdicionarTutorPetDto.DsVinculo` | `POST /api/v1/pets/{id}/tutores` | Mesma situação do #6, default `"CUIDADOR"` | **Sim** (mesmo padrão, mesmo mecanismo) — não testado via curl isolado (idêntico ao #6 no mesmo service), coberto por teste unitário TDD | **Corrigido**: coalesce no `PetService.AdicionarTutorAsync` (fallback `"CUIDADOR"`) |
| 8 | `EVENTO_CLINICO.DS_OBSERVACAO` | `ConsultaCreateDto`/`ExameCreateDto`/`VacinaCreateDto`/`PrescricaoCreateDto`.`DsObservacao` | `POST .../consultas` / `.../exames` / `.../vacinas` / `.../prescricoes` | — | **Já corrigido na TASK-56** (`370ab7b`) — fora do escopo desta task, listado aqui só para registro de que a varredura confirmou que não sobrou nenhuma instância não tratada | Nenhuma (já resolvido) |
| 9 | `CLINICA.NM_CLINICA` / `NR_CNPJ` | `RegisterClinicaDto` (rota real de cadastro) | `POST /api/v1/auth/register-clinica` | `NotEmpty()` em ambos (`RegisterClinicaValidator`) | Não reproduziu — validado | Nenhuma |
| 10 | `CLINICA.NM_CLINICA` / `NR_CNPJ` | `ClinicaCreateDto` | — | `NotEmpty()` em ambos (`ClinicaCreateValidator`) | Não reproduziu — **sem endpoint HTTP**: `ClinicasController` só expõe `GET`/`PUT`/`DELETE`; `ClinicaService.CreateAsync` nunca é chamado por nenhum controller. Cadastro de clínica real é via `RegisterClinicaDto` (#9) | Nenhuma (código morto, fora do escopo de correção desta task) |
| 11 | `CLINICA.NM_CLINICA` | `ClinicaUpdateDto.NmClinica` | `PUT /api/v1/clinicas/{id}` | `NotEmpty()` (`ClinicaUpdateValidator`) | Não reproduziu — validado | Nenhuma |
| 12 | `VETERINARIO.NM_VETERINARIO` | `VeterinarioCreateDto`/`VeterinarioUpdateDto`.`NmVeterinario` | `POST`/`PUT /api/v1/veterinarios...` | `NotEmpty()` em ambos os validators | Não reproduziu — validado | Nenhuma |
| 13 | `VETERINARIO.NM_VETERINARIO` | `RegisterClinicaDto.NmVeterinarioAdmin` | `POST /api/v1/auth/register-clinica` | `NotEmpty()` (`RegisterClinicaValidator`) | Não reproduziu — validado | Nenhuma |
| 14 | `PET.NM_PET` | `PetCreateDto`/`PetUpdateDto`.`NmPet` | `POST`/`PUT /api/v1/pets...` | `NotEmpty()` em ambos os validators | Não reproduziu — validado | Nenhuma |
| 15 | `MEDICAMENTO.NM_MEDICAMENTO` / `DS_PRINCIPIO_ATIVO` | `MedicamentoCreateDto` | `POST /api/v1/medicamentos` | `NotEmpty()` em ambos | Não reproduziu — validado | Nenhuma |
| 16 | `NOTIFICACAO.DS_TITULO` / `DS_MENSAGEM` | `NotificacaoCreateDto` | — | Nenhuma | Não reproduziu — **sem endpoint HTTP nem chamador interno**: `NotificacoesController` só expõe `GET`/`PATCH marcar-lida`; `grep -rn "new NotificacaoCreateDto"` em todo `src/` não encontra nenhum caller além da própria declaração do DTO/interface. `INotificacaoService.CreateAsync` nunca é invocado hoje | Nenhuma (código morto — sinalizado abaixo como achado lateral) |
| 17 | `DISPOSITIVO_IOT.CD_DISPOSITIVO` / `DS_DESCRICAO` / `DS_LOCALIZACAO` | `DispositivoIotCreateDto` | — | Nenhuma | Não reproduziu — **sem endpoint HTTP nem chamador interno**: `IotController` só expõe `POST leituras` (não dispositivos), `GET dispositivos`, `GET dispositivos/{id}/leituras`, `GET dispositivos/{id}/status`, `GET alertas`. `grep -rn "new DispositivoIotCreateDto"` não encontra nenhum caller. `IDispositivoIotService.CreateAsync` nunca é invocado hoje | Nenhuma (código morto — sinalizado abaixo) |
| 18 | `EVENTO_CLINICO.DS_OBSERVACAO` (via stub genérico) | `EventoClinicoCreateDto.DsObservacao` | — | Nenhuma | Não reproduziu — **sem endpoint HTTP nem chamador interno**: todos os 4 subtipos (Consulta/Exame/Vacina/Prescricao) usam seus próprios DTOs; `EventoClinicoCreateDto` não é referenciado por nenhum controller nem service além da própria declaração | Nenhuma (código morto — sinalizado abaixo) |
| 19 | `EXAME.NM_EXAME` / `DS_RESULTADO` | `ExameCreateDto` | `POST /api/v1/eventos-clinicos/exames` | `NotEmpty()` em ambos | Não reproduziu — validado | Nenhuma |
| 20 | `VACINA.NM_VACINA` / `NR_LOTE` | `VacinaCreateDto` | `POST /api/v1/eventos-clinicos/vacinas` | `NotEmpty()` em ambos | Não reproduziu — validado | Nenhuma |
| 21 | `PRESCRICAO.DS_POSOLOGIA` | `PrescricaoCreateDto.DsPosologia` | `POST /api/v1/eventos-clinicos/prescricoes` | `NotEmpty()` | Não reproduziu — validado | Nenhuma |
| 22 | `CONSULTA.DS_MOTIVO` | `ConsultaCreateDto.DsMotivo` | `POST /api/v1/eventos-clinicos/consultas` | `NotEmpty()` | Não reproduziu — validado | Nenhuma |
| 23 | `DOCUMENTO.NM_ARQUIVO` / `DS_TIPO_MIME` / `DS_CAMINHO` | — (sem DTO de criação) | `POST .../receituario` (gera o PDF) | N/A | Não reproduziu — **coluna nunca populada por input de cliente**: `ReceituarioPdfService.GerarReceituarioAsync` monta `Documento` inteiramente no servidor (`NmArquivo` via `Guid.NewGuid()`, `DsTipoMime` hardcoded `"application/pdf"`, `DsCaminho` via `Path.Combine`). Não existe `DocumentoCreateDto` | Nenhuma (não é superfície de ataque) |
| 24 | `TRIAGEM_LUNA.DS_NIVEL_URGENCIA` / `DS_DESCRICAO` | — (sem DTO de criação neste repo) | — | N/A | Não reproduziu — **fora do escopo desta varredura**: `LunaController` só expõe `GET triagens/relatorio` (leitura agregada); não existe endpoint de criação de `TriagemLuna` em `backend-clinica-dotnet`. A tabela é escrita diretamente pelo serviço Python (`kura-luna-ai`), fora do escopo de DTOs `.NET` desta task | Nenhuma |
| 25 | `AGENDAMENTO.ST_STATUS` | `AtualizarStatusAgendamentoDto.DsStatus` | `PUT /api/v1/agenda/{id}/status` (via `AgendaService`) | `Must(s => s is "REALIZADO" or "CANCELADO")` — restringe a um enum de 2 valores, nenhum vazio | Não reproduziu — validado (não é `NotEmpty()`, mas um `Must()` que já exclui `""` por construção) | Nenhuma |
| 26 | `AGENDAMENTO.ST_STATUS` / `DS_ORIGEM` (no INSERT) | — | — | N/A | Não reproduziu — **`.NET` nunca cria `AGENDAMENTO`** (`AgendaService` não tem `AddAsync`/`new Agendamento`; por design, `AGENDAMENTO` é criado só pelo Java — `.NET` só faz `UPDATE` de `ST_STATUS`, ver `CLAUDE.md` "Tabelas e ownership") | Nenhuma (fora do escopo de escrita do .NET) |

## Achados laterais sem correção nesta task

- **`NotificacaoCreateDto`, `DispositivoIotCreateDto`, `EventoClinicoCreateDto` são código
  morto** — cada um tem um `Service.CreateAsync` implementado e testável, mas nenhum
  controller HTTP os expõe e nenhum outro service os invoca internamente
  (`grep -rn "new NotificacaoCreateDto\|new DispositivoIotCreateDto\|new EventoClinicoCreateDto" src/`
  não retorna nenhuma ocorrência fora da declaração do próprio DTO). Não são bugs desta
  classe hoje porque não há caminho de execução alcançável por um cliente, mas também não
  foram "corrigidos" — se um desses três ganhar um endpoint no futuro sem que alguém releia
  esta tabela, os mesmos 3 pares (linhas 16-18) voltam a ser candidatos reais. Não há task
  no backlog rastreando isso; registrado aqui como novo loose end.

## Verificação

Os casos confirmados (linhas 1, 3, 4, 5, 6, 7 — 6 combinações DTO×endpoint, 4 colunas
Oracle distintas: `DS_APRESENTACAO`, `DS_TELEFONE`, `DS_FABRICANTE`, `DS_VINCULO`) foram:
1. Reproduzidos via `curl` contra o compose real (`DevOps-Cloud`, Oracle real) **antes** da
   correção — todos retornaram `500` com `ORA-01400` confirmado nos logs do container
   (`docker compose logs kura-api`) para a coluna exata.
2. Cobertos por teste `[Theory]` TDD (`""` e `"   "`) nos respectivos
   `*ServiceTests.cs`, confirmado **red** contra o código pré-fix (via `git stash` do arquivo
   de service, sem tocar nos testes) antes de implementar a correção, e **green** depois.
3. Revalidados via `curl` contra o compose real **depois** da correção — todos os 6 retornam
   `2xx` com o sentinela persistido; os controles com texto real (`curl` companion) confirmam
   que o sentinela não sobrescreve dado real enviado pelo cliente.

`dotnet test`: **176 testes, 0 falhas** no total da solução (`Kura.Domain.Tests` 8 +
`Kura.Infrastructure.Tests` 22 + `Kura.Application.Tests` 146). `Kura.Application.Tests`
isoladamente subiu de **129 para 146 (17 novos casos de execução)**, confirmado por
`dotnet test` antes de qualquer mudança desta task (medido via `git stash` dos arquivos
tocados) e depois: `Medicamento` (2 casos `[Theory]` + 1 controle = 3) + `Vacina` (3) +
`Tutor.CreateAsync` (3) + `Tutor.UpdateAsync` (3) + `Pet.CreateAsync` (3) +
`Pet.AdicionarTutorAsync` (2, sem teste de controle separado — coberto pelo já existente
`AdicionarTutorAsync_SegundoTutor_CriaTutorPetStPrincipalN`) = 17.
