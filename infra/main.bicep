// main.bicep
// Main orchestration template - deploys App Service, Azure SQL, and optionally GenAI resources

@description('Azure region for deployment')
param location string = 'uksouth'

@description('Object ID of the Azure AD admin for SQL Server (run: az ad signed-in-user show --query id -o tsv)')
param adminObjectId string

@description('Login / UPN of the Azure AD admin (your email address)')
param adminLogin string

@description('Whether to deploy GenAI resources (Azure OpenAI + AI Search)')
param deployGenAI bool = false

// Deploy App Service with Managed Identity
module appServiceModule 'app-service.bicep' = {
  name: 'appServiceDeploy'
  params: {
    location: location
  }
}

// Reference the managed identity to get its principalId
resource existingManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: appServiceModule.outputs.managedIdentityName
}

// Deploy Azure SQL with Entra ID authentication
module sqlModule 'azure-sql.bicep' = {
  name: 'azureSqlDeploy'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityName: appServiceModule.outputs.managedIdentityName
    managedIdentityId: appServiceModule.outputs.managedIdentityId
  }
}

// Conditionally deploy GenAI resources
module genAIModule 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeploy'
  params: {
    location: location
    managedIdentityPrincipalId: existingManagedIdentity.properties.principalId
  }
}

// Outputs
output appServiceName string = appServiceModule.outputs.appServiceName
output appServiceUrl string = appServiceModule.outputs.appServiceUrl
output sqlServerFqdn string = sqlModule.outputs.sqlServerFqdn
output sqlServerName string = sqlModule.outputs.sqlServerName
output databaseName string = sqlModule.outputs.databaseName
output managedIdentityClientId string = appServiceModule.outputs.managedIdentityClientId
output managedIdentityName string = appServiceModule.outputs.managedIdentityName

// Conditional GenAI outputs (null-safe operators)
output openAIEndpoint string = deployGenAI ? genAIModule.outputs!.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAIModule.outputs!.openAIModelName : ''
output openAIName string = deployGenAI ? genAIModule.outputs!.openAIName : ''
output searchEndpoint string = deployGenAI ? genAIModule.outputs!.searchEndpoint : ''
output searchName string = deployGenAI ? genAIModule.outputs!.searchName : ''
