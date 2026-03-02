#!/bin/bash
# deploy-with-chat.sh - Deploy Expense Management System INCLUDING GenAI (OpenAI + AI Search)
# Usage: Set variables below then run: bash deploy-with-chat.sh
set -e

# ============================================================
# VARIABLES - SET THESE BEFORE RUNNING
# ============================================================
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
ADMIN_OBJECT_ID=""   # Your Azure AD Object ID: az ad signed-in-user show --query id -o tsv
ADMIN_LOGIN=""       # Your email/UPN: az account show --query user.name -o tsv

# ============================================================
# VALIDATION
# ============================================================
if [ -z "$ADMIN_OBJECT_ID" ] || [ -z "$ADMIN_LOGIN" ]; then
    echo "ERROR: Please set ADMIN_OBJECT_ID and ADMIN_LOGIN before running this script."
    echo "  ADMIN_OBJECT_ID: az ad signed-in-user show --query id -o tsv"
    echo "  ADMIN_LOGIN:     az account show --query user.name -o tsv"
    exit 1
fi

echo "=== Deploying Expense Management System WITH GenAI ==="
echo "Resource Group : $RESOURCE_GROUP"
echo "Location       : $LOCATION"
echo "Admin Login    : $ADMIN_LOGIN"
echo "Note: Azure OpenAI will be deployed to swedencentral (GPT-4o quota)"
echo ""

# ============================================================
# 1. Create resource group
# ============================================================
echo "Step 1: Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
echo "  ✓ Resource group ready"

# ============================================================
# 2. Deploy infrastructure including GenAI
# ============================================================
echo ""
echo "Step 2: Deploying infrastructure (App Service + SQL + GenAI)..."
echo "  This may take 5-10 minutes..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infra/main.bicep \
  --parameters adminObjectId="$ADMIN_OBJECT_ID" adminLogin="$ADMIN_LOGIN" deployGenAI=true \
  --query properties.outputs \
  --output json)

APP_SERVICE_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.sqlServerFqdn.value')
SQL_SERVER_NAME=$(echo "$SQL_SERVER_FQDN" | cut -d'.' -f1)
MANAGED_IDENTITY_CLIENT_ID=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.managedIdentityName.value')
OPENAI_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo "$DEPLOYMENT_OUTPUT" | jq -r '.searchEndpoint.value')

echo "  ✓ App Service   : $APP_SERVICE_NAME"
echo "  ✓ SQL Server    : $SQL_SERVER_FQDN"
echo "  ✓ MI Name       : $MANAGED_IDENTITY_NAME"
echo "  ✓ MI Client ID  : $MANAGED_IDENTITY_CLIENT_ID"
echo "  ✓ OpenAI        : $OPENAI_ENDPOINT"
echo "  ✓ AI Search     : $SEARCH_ENDPOINT"

# ============================================================
# 3. Configure App Service settings (including OpenAI)
# ============================================================
echo ""
echo "Step 3: Configuring App Service settings..."
az webapp config appsettings set \
  --name "$APP_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    "ConnectionStrings__DefaultConnection=Server=tcp:${SQL_SERVER_FQDN},1433;Database=Northwind;Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
    "AZURE_CLIENT_ID=${MANAGED_IDENTITY_CLIENT_ID}" \
    "ManagedIdentityClientId=${MANAGED_IDENTITY_CLIENT_ID}" \
    "OpenAI__Endpoint=${OPENAI_ENDPOINT}" \
    "OpenAI__DeploymentName=${OPENAI_MODEL_NAME}" \
    "Search__Endpoint=${SEARCH_ENDPOINT}" \
  --output none
echo "  ✓ App Service settings configured (including OpenAI)"

# ============================================================
# 4. Wait for SQL Server to be ready
# ============================================================
echo ""
echo "Step 4: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30

# ============================================================
# 5. Add firewall rules
# ============================================================
echo ""
echo "Step 5: Adding firewall rules..."
MY_IP=$(curl -s https://api.ipify.org)

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowAllAzureIPs" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  --output none

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER_NAME" \
  --name "AllowDeploymentIP" \
  --start-ip-address "$MY_IP" \
  --end-ip-address "$MY_IP" \
  --output none

echo "  ✓ Firewall rules added (Azure services + IP: $MY_IP)"
echo "  Waiting 15 seconds for firewall rules to propagate..."
sleep 15

# ============================================================
# 6. Install Python dependencies
# ============================================================
echo ""
echo "Step 6: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "  ✓ Python packages installed"

# ============================================================
# 7. Update Python scripts with actual server details
# ============================================================
echo ""
echo "Step 7: Updating Python scripts with server details..."
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s|SERVER = \"example.database.windows.net\"|SERVER = \"${SQL_SERVER_FQDN}\"|g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
echo "  ✓ Scripts updated with server: $SQL_SERVER_FQDN"

# ============================================================
# 8. Import database schema
# ============================================================
echo ""
echo "Step 8: Importing database schema..."
python3 run-sql.py
echo "  ✓ Database schema imported"

# ============================================================
# 9. Configure database roles for managed identity
# ============================================================
echo ""
echo "Step 9: Configuring database roles for managed identity..."
sed -i.bak "s/mid-AppModAssist-020317/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak
python3 run-sql-dbrole.py
echo "  ✓ Database roles configured"

# ============================================================
# 10. Create stored procedures
# ============================================================
echo ""
echo "Step 10: Creating stored procedures..."
python3 run-sql-stored-procs.py
echo "  ✓ Stored procedures created"

# ============================================================
# 11. Deploy application code
# ============================================================
echo ""
echo "Step 11: Deploying application code..."
if [ ! -f "app.zip" ]; then
    echo "  Building app first..."
    cd app
    dotnet publish -c Release -o /tmp/publish-output --nologo -q
    cd /tmp/publish-output
    zip -r /tmp/app.zip . > /dev/null
    cd -
    mv /tmp/app.zip ./app.zip
    echo "  ✓ app.zip created"
fi

az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_SERVICE_NAME" \
  --src-path ./app.zip \
  --type zip \
  --output none
echo "  ✓ Application deployed"

# ============================================================
# DONE
# ============================================================
echo ""
echo "=========================================="
echo "=== Full Deployment Complete! ============"
echo "=========================================="
echo ""
echo "  App URL     : ${APP_SERVICE_URL}/Index"
echo "  Swagger     : ${APP_SERVICE_URL}/swagger"
echo "  SQL Server  : $SQL_SERVER_FQDN"
echo "  OpenAI      : $OPENAI_ENDPOINT"
echo "  AI Search   : $SEARCH_ENDPOINT"
echo ""
echo "  NOTE: Navigate to ${APP_SERVICE_URL}/Index to view the app"
echo "        (not the root URL which shows a default page)"
echo ""
echo "  GenAI Chat available via the Chat UI application"
echo "  Set ExpenseApiBaseUrl in chatui/appsettings.json to: ${APP_SERVICE_URL}"
echo "=========================================="
