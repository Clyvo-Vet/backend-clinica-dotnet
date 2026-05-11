#!/bin/bash
# teardown-kura.sh — Deleta TODOS os recursos Azure para evitar custos.
# ATENÇÃO: Esta operação é IRREVERSÍVEL. Execute após a apresentação FIAP.

set -e

RESOURCE_GROUP="rg-kura-api"

echo "==> Iniciando teardown do Resource Group '$RESOURCE_GROUP'..."
echo "    Todos os recursos (VM, IP público, discos, NSG) serão deletados."
read -r -p "    Confirmar? (s/N): " confirm

if [[ "$confirm" != "s" && "$confirm" != "S" ]]; then
  echo "Teardown cancelado."
  exit 0
fi

az group delete --name "$RESOURCE_GROUP" --yes --no-wait

echo ""
echo "Resource Group '$RESOURCE_GROUP' em processo de exclusão."
echo "A exclusão pode levar alguns minutos. Nenhum custo adicional será gerado."
echo ""
echo "Para verificar o status:"
echo "  az group show --name $RESOURCE_GROUP --query properties.provisioningState -o tsv"
