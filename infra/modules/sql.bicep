// SQL Server module
// Creates Azure SQL Server (optionally) and database
// When createServer is false, references an existing server (shared across environments)

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

@description('Create the SQL Server (false = reference existing shared server)')
param createServer bool = true

// === SQL Server (conditional) ===

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = if (createServer) {
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

// Allow Azure services to connect (only when creating server)
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (createServer) {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// === Existing server reference (for shared server scenarios) ===

resource existingSqlServer 'Microsoft.Sql/servers@2023-08-01-preview' existing = {
  name: serverName
}

// === Database (always created) ===
// Uses existing server reference so it works whether server was just created or already existed

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: existingSqlServer
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
  dependsOn: [sqlServer] // Ensures server is created first when createServer is true
}

output serverName string = serverName
output serverFqdn string = '${serverName}.database.windows.net'
output databaseName string = databaseName
output databaseId string = sqlDatabase.id
