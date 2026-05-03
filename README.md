# KURA API

Backend clínico do sistema de gestão veterinária **Clyvo Vet**. Desenvolvido em .NET 10.0 como parte do Challenge FIAP 2026.

## Stack

| Tecnologia | Versão | Finalidade |
|---|---|---|
| .NET | 10.0 | Framework principal |
| ASP.NET Core | 10.0 | Web API |
| Entity Framework Core | 10.x | ORM / migrations |
| Oracle 19c | — | Banco de dados |
| FluentValidation | 12.x | Validação de DTOs |
| BCrypt.Net | — | Hash de senhas |
| JWT Bearer | — | Autenticação |
| Serilog | — | Logging estruturado |
| Docker | — | Containerização |
| Azure App Service | — | Hospedagem em produção |

## Arquitetura

Clean Architecture em 4 camadas — `Api → Application → Domain ← Infrastructure`.

```mermaid
graph TD
    A[Kura.Api] --> B[Kura.Application]
    A --> C[Kura.Infrastructure]
    B --> D[Kura.Domain]
    C --> D
    C --> E[(Oracle 19c)]
```

| Camada | Responsabilidade |
|---|---|
| **Domain** | Entidades, interfaces de repositório, exceções de domínio |
| **Application** | Services, DTOs, validadores FluentValidation |
| **Infrastructure** | EF Core, repositórios, configurações Fluent API |
| **Api** | Controllers HTTP, filtros, middlewares |

## Como rodar localmente (sem Docker)

```bash
git clone <url-do-repo>
cd backend-clinica-dotnet
```

Criar `src/Kura.Api/appsettings.Development.json` com a connection string Oracle FIAP:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "User Id=RM562999;Password=<sua-senha>;Data Source=oracle.fiap.com.br:1521/orcl"
  },
  "Jwt": {
    "Key": "kura-api-secret-key-fiap-2026-clyvovet",
    "Issuer": "kura-api",
    "Audience": "kura-client"
  },
  "IoT": {
    "ApiKey": "kura-iot-device-key-2026"
  }
}
```

```bash
dotnet restore
dotnet run --project src/Kura.Api
# Swagger: http://localhost:5000/swagger
```

## Como rodar com Docker

```bash
docker-compose up --build
# Swagger: http://localhost:8080/swagger
# Health:  http://localhost:8080/health
```

O Oracle XE sobe automaticamente. Aguarde o healthcheck (~2 min) antes da primeira request.

## Endpoints principais

### Auth
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Autenticação — retorna JWT | Público |

### Clínicas
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/clinicas/{id}` | Buscar clínica por ID | JWT |
| PUT | `/api/v1/clinicas/{id}` | Atualizar clínica | JWT |
| DELETE | `/api/v1/clinicas/{id}` | Soft delete da clínica | JWT |

### Veterinários
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/veterinarios` | Listar veterinários | JWT |
| GET | `/api/v1/veterinarios/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/veterinarios` | Cadastrar veterinário | JWT |
| PUT | `/api/v1/veterinarios/{id}` | Atualizar veterinário | JWT |
| DELETE | `/api/v1/veterinarios/{id}` | Soft delete | JWT |

### Tutores
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/tutores` | Listar tutores | JWT |
| GET | `/api/v1/tutores/{id}` | Buscar por ID | JWT |
| GET | `/api/v1/tutores/{id}/pets` | Pets do tutor | JWT |
| POST | `/api/v1/tutores` | Criar tutor | JWT |
| PUT | `/api/v1/tutores/{id}` | Atualizar tutor | JWT |

### Pets
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/pets` | Listar pets (filtros: tutorId, especieId, porte) | JWT |
| GET | `/api/v1/pets/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/pets` | Cadastrar pet | JWT |
| PUT | `/api/v1/pets/{id}` | Atualizar pet | JWT |
| DELETE | `/api/v1/pets/{id}` | Soft delete | JWT |
| POST | `/api/v1/pets/{id}/tutores` | Vincular tutor adicional | JWT |
| GET | `/api/v1/pets/{id}/timeline` | Timeline de eventos | JWT |
| GET | `/api/v1/pets/{id}/proximas-vacinas` | Próximas doses agendadas | JWT |

### Eventos Clínicos
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/eventos-clinicos` | Listar eventos (filtros: petId, tipo, datas) | JWT |
| GET | `/api/v1/eventos-clinicos/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/eventos-clinicos/vacinas` | Registrar vacina | JWT |
| POST | `/api/v1/eventos-clinicos/prescricoes` | Registrar prescrição | JWT |
| POST | `/api/v1/eventos-clinicos/exames` | Registrar exame | JWT |

### Dashboard
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/dashboard/hoje` | Resumo do dia atual | JWT |
| GET | `/api/v1/dashboard/alertas` | Alertas ativos | JWT |
| GET | `/api/v1/dashboard/recentes` | Agendamentos recentes | JWT |

### Notificações
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/notificacoes` | Listar notificações da clínica | JWT |
| PATCH | `/api/v1/notificacoes/{id}/marcar-lida` | Marcar notificação como lida | JWT |

### IoT
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| POST | `/api/v1/iot/leituras` | Ingerir leitura de temperatura | API Key |
| GET | `/api/v1/iot/dispositivos` | Listar dispositivos | API Key |
| GET | `/api/v1/iot/dispositivos/{id}/leituras` | Histórico de leituras | API Key |
| GET | `/api/v1/iot/dispositivos/{id}/status` | Status do dispositivo | API Key |
| GET | `/api/v1/iot/alertas` | Listar alertas de temperatura | API Key |

### Medicamentos
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/api/v1/medicamentos` | Listar medicamentos | JWT |
| GET | `/api/v1/medicamentos/{id}` | Buscar por ID | JWT |
| POST | `/api/v1/medicamentos` | Cadastrar medicamento | JWT |

### Health
| Método | Rota | Descrição | Auth |
|---|---|---|---|
| GET | `/health` | Health check da API | Público |

## Variáveis de ambiente

| Variável | Descrição | Exemplo |
|---|---|---|
| `ConnectionStrings__OracleConnection` | String de conexão Oracle | `User Id=RM562999;Password=...;Data Source=host:1521/orcl` |
| `Jwt__Key` | Chave secreta para assinar o JWT | `kura-api-secret-key-fiap-2026-clyvovet` |
| `Jwt__Issuer` | Emissor do token JWT | `kura-api` |
| `Jwt__Audience` | Audiência do token JWT | `kura-client` |
| `IoT__ApiKey` | Chave de autenticação dos dispositivos IoT | `kura-iot-device-key-2026` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Development` / `Production` |

## Deploy

Consulte [`docs/azure-deploy.md`](docs/azure-deploy.md) para o passo a passo completo de deploy no Azure.

URL de produção: `https://kura-api-fiap.azurewebsites.net`  
Swagger em produção: `https://kura-api-fiap.azurewebsites.net/swagger`

## Equipe — Clyvo Vet

| Membro | Função |
|---|---|
| **Felipe Ferrete** *(líder técnico)* | .NET · IoT/IA |
| **Nikolas Brisola** | Java · Backend Tutor |
| **Guilherme Sola** | Mobile Tutor · UX |
| **Gustavo Bosak** | Mobile Clínica · QA |
| **Clayton** | DevOps · BD |
