// SQL Server module
// Creates Azure SQL Server and database

@description('Name of the SQL Server')
param serverName string

@description('Name of the SQL Database')
param databaseName string = 'sessionsight'

@description('Location for the SQL Server')
param location string = resourceGroup().location

@description('Tags to apply to the resources')
param tags object = {}

@description('SQL administrator login name')
param adminLogin string = 'sessionsightadmin'

@description('SQL administrator password')
@secure()
param adminPassword string

@description('Enable free tier (32GB limit)')
param enableFreeTier bool = true

@description('Database SKU name')
param skuName string = 'GP_S_Gen5_1'

@description('Database SKU tier')
param skuTier string = 'GeneralPurpose'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure services to connect
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: enableFreeTier ? 34359738368 : 1073741824 // 32GB for free tier, 1GB otherwise
    useFreeLimit: enableFreeTier
    freeLimitExhaustionBehavior: 'AutoPause'
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
    isLedgerOn: false
  }
}

output serverName string = sqlServer.name
output serverId string = sqlServer.id
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
output databaseId string = sqlDatabase.id
// Note: Connection string contains secrets, retrieve via Azure CLI or Key Vault
