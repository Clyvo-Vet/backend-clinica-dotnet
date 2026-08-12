# Deploy KURA API — VM Linux na Azure

Este guia cobre o provisionamento de uma Máquina Virtual Linux Ubuntu 22.04 na Azure
e o deploy da KURA API via Docker. Atende à **rubrica DevOps 1.1** (VM Linux Azure).

## Pré-requisitos

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) instalado
- Conta Azure ativa com créditos suficientes
- Login realizado: `az login`

## Ordem de execução

### 1. Deploy

```bash
chmod +x docs/deploy/deploy-kura-vm.sh
./docs/deploy/deploy-kura-vm.sh
```

O script executa automaticamente:

| Etapa | O que faz |
|---|---|
| 1 | Cria Resource Group `rg-kura-api` em `brazilsouth` |
| 2 | Provisiona VM `Standard_B2s` Ubuntu 22.04 com SSH keys geradas |
| 3 | Abre porta 8080 no NSG |
| 4 | Captura IP público |
| 5 | Instala Docker, Docker Compose, Git e nano via SSH |
| 6 | Clona repositório e sobe API com `docker-compose up -d` |

Ao final, exibe:
```
Swagger: http://<IP>:8080/swagger
Health:  http://<IP>:8080/metrics
```

### 2. Variáveis de ambiente

Antes de executar, edite as variáveis no bloco SSH do script:

```bash
ConnectionStrings__DefaultConnection="User Id=RM562999;Password=<YOUR_ORACLE_PASSWORD>;Data Source=oracle.fiap.com.br:1521/orcl"
Jwt__Key="<YOUR_JWT_SECRET_MIN_32_CHARS>"
IoT__ApiKey="<YOUR_IOT_API_KEY>"
```

Ou crie um arquivo `.env` na raiz do repositório clonado e ajuste o `docker-compose.yml`.

### 3. Teardown (OBRIGATÓRIO após apresentação)

Para evitar custos e cumprir a rubrica de cleanup (DevOps item 4):

```bash
chmod +x docs/deploy/teardown-kura.sh
./docs/deploy/teardown-kura.sh
```

O script pede confirmação antes de deletar o Resource Group completo
(VM, disco, IP público, NSG, VNet).

## Recursos criados

| Recurso | Nome | Tipo |
|---|---|---|
| Resource Group | `rg-kura-api` | Agrupamento lógico |
| VM | `vm-kura-api` | Standard_B2s (2 vCPU, 4 GB RAM) |
| SO | Ubuntu 22.04 LTS | Linux |
| IP Público | Gerado automaticamente | Standard SKU |
| Porta aberta | 8080 (TCP) | NSG inbound rule |

## Diagrama

```
Internet
   │  :8080
   ▼
Azure VM (Ubuntu 22.04)
   └── Docker
        └── kura-api container
             └── Oracle FIAP (externo)
```

## Estimativa de custo

Standard_B2s em `brazilsouth`: ~USD 0,05/hora.  
Para uma apresentação de 2 horas: **~USD 0,10**.  
Execute o teardown imediatamente após para zerar o custo.
