// azure-sql.bicep
// Deploys Azure SQL Server (Entra ID only auth) and Northwind Database

@description('Azure region for deployment')
param location string = 'uksouth'

@description('Object ID of the Azure AD admin for SQL Server')
param adminObjectId string

@description('Login / UPN of the Azure AD admin for SQL Server')
param adminLogin string

@description('Name of the user-assigned managed identity')
param managedIdentityName string

@description('Resource ID of the user-assigned managed identity')
param managedIdentityId string

// Generate unique SQL server name (must be lowercase)
var sqlServerName = 'sql-${toLower(uniqueString(resourceGroup().id))}'
var databaseName = 'Northwind'

// Azure SQL Server - Entra ID only authentication
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Northwind Database - Basic tier
resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

// Firewall rule: Allow all Azure services
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output databaseName string = database.name
