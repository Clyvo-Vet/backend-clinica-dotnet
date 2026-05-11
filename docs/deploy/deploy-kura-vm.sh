#!/bin/bash
# deploy-kura-vm.sh — Provisiona VM Linux Ubuntu 22.04 na Azure, instala Docker e sobe a KURA API
# Pré-requisito: az login realizado na máquina local

set -e

RESOURCE_GROUP="rg-kura-api"
LOCATION="brazilsouth"
VM_NAME="vm-kura-api"
VM_USER="kura"
VM_SIZE="Standard_B2s"
REPO_URL="https://github.com/FelipeFerrete/kura-api.git"

echo "==> [1/6] Criando Resource Group '$RESOURCE_GROUP' em '$LOCATION'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

echo "==> [2/6] Provisionando VM Linux Ubuntu 22.04..."
az vm create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --image Ubuntu2204 \
  --admin-username "$VM_USER" \
  --generate-ssh-keys \
  --size "$VM_SIZE" \
  --public-ip-sku Standard

echo "==> [3/6] Abrindo porta 8080 (API)..."
az vm open-port \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --port 8080 \
  --priority 1001

echo "==> [4/6] Capturando IP público da VM..."
VM_IP=$(az vm show -d \
  --resource-group "$RESOURCE_GROUP" \
  --name "$VM_NAME" \
  --query publicIps -o tsv)

echo "    VM provisionada em: $VM_IP"

echo "==> [5/6] Instalando Docker, Docker Compose, Git e nano via SSH..."
ssh -o StrictHostKeyChecking=no "$VM_USER@$VM_IP" <<'REMOTE'
  set -e
  sudo apt-get update -qq
  sudo apt-get install -y docker.io docker-compose git nano
  sudo usermod -aG docker "$USER"
  sudo systemctl enable docker
  sudo systemctl start docker
  echo "Docker instalado: $(docker --version)"
REMOTE

echo "==> [6/6] Clonando repositório e subindo API com Docker Compose..."
ssh -o StrictHostKeyChecking=no "$VM_USER@$VM_IP" <<REMOTE
  set -e
  if [ ! -d "kura-api" ]; then
    git clone $REPO_URL kura-api
  else
    cd kura-api && git pull
  fi
  cd kura-api
  # Variáveis de ambiente obrigatórias — ajuste antes de executar
  export ConnectionStrings__DefaultConnection="User Id=RM562999;Password=SUASENHA;Data Source=oracle.fiap.com.br:1521/orcl"
  export Jwt__Key="kura-api-secret-key-fiap-2026-clyvovet"
  export Jwt__Issuer="kura-api"
  export Jwt__Audience="kura-client"
  export IoT__ApiKey="kura-iot-device-key-2026"
  sudo -E docker-compose up -d --build
REMOTE

echo ""
echo "=========================================="
echo "Deploy concluído com sucesso!"
echo "  Swagger: http://$VM_IP:8080/swagger"
echo "  Health:  http://$VM_IP:8080/health"
echo "  Metrics: http://$VM_IP:8080/metrics"
echo "=========================================="
echo "Para encerrar e evitar custos, execute: ./teardown-kura.sh"
