// SessionSight Infrastructure
// Main entry point for deploying all Azure resources
// Deploys at subscription scope, creates resource group and all resources

targetScope = 'subscription'

// === Parameters ===

@description('Environment name (dev, prod, etc.)')
@allowed(['dev', 'prod', 'test'])
param environmentName string

@description('Location for all resources')
param location string = 'eastus2'

@description('SQL administrator password')
@secure()
param sqlAdminPassword string

@description('Object ID of the service principal for RBAC assignments')
param servicePrincipalObjectId string = ''

// === Variables ===

var prefix = 'sessionsight'
var resourceGroupName = 'rg-${prefix}-${environmentName}'
var tags = {
  project: 'SessionSight'
  environment: environmentName
  managedBy: 'Bicep'
}

// Storage account name must be lowercase alphanumeric, 3-24 chars
var storageAccountName = '${prefix}storage${environmentName}'

// === Resource Group ===

module rg 'modules/resourceGroup.bicep' = {
  name: 'resourceGroup'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

// === Key Vault ===
// Deployed first as other resources may store secrets here

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-kv-${environmentName}'
    location: location
    tags: tags
    adminObjectId: servicePrincipalObjectId
  }
  dependsOn: [rg]
}

// === Storage Account ===

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: storageAccountName
    location: location
    tags: tags
    contributorObjectId: servicePrincipalObjectId
  }
  dependsOn: [rg]
}

// === SQL Server and Database ===

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: resourceGroup(resourceGroupName)
  params: {
    serverName: '${prefix}-sql-${environmentName}'
    databaseName: 'sessionsight'
    location: location
    tags: tags
    adminPassword: sqlAdminPassword
    enableFreeTier: environmentName == 'dev' || environmentName == 'test'
  }
  dependsOn: [rg]
}

// === Azure OpenAI ===

module openai 'modules/openai.bicep' = {
  name: 'openai'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-openai-${environmentName}'
    location: location
    tags: tags
  }
  dependsOn: [rg]
}

// === Azure AI Search ===

module search 'modules/search.bicep' = {
  name: 'search'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-search-${environmentName}'
    location: location
    tags: tags
    skuName: environmentName == 'prod' ? 'basic' : 'free'
  }
  dependsOn: [rg]
}

// === Document Intelligence ===

module docIntelligence 'modules/docintell.bicep' = {
  name: 'docIntelligence'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-docint-${environmentName}'
    location: location
    tags: tags
    skuName: environmentName == 'prod' ? 'S0' : 'F0'
  }
  dependsOn: [rg]
}

// === AI Foundry Hub ===

module aiHub 'modules/aiHub.bicep' = {
  name: 'aiHub'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-aihub-${environmentName}'
    location: location
    tags: tags
    storageAccountId: storage.outputs.id
    keyVaultId: keyVault.outputs.id
  }
  dependsOn: [rg]
}

// === AI Foundry Project ===

module aiProject 'modules/aiProject.bicep' = {
  name: 'aiProject'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-aiproject-${environmentName}'
    location: location
    tags: tags
    hubId: aiHub.outputs.id
  }
}

// === Outputs ===

output resourceGroupName string = rg.outputs.name
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.vaultUri
output storageAccountName string = storage.outputs.name
output storageBlobEndpoint string = storage.outputs.blobEndpoint
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output openaiName string = openai.outputs.name
output openaiEndpoint string = openai.outputs.endpoint
output searchName string = search.outputs.name
output searchEndpoint string = search.outputs.endpoint
output docIntelligenceName string = docIntelligence.outputs.name
output docIntelligenceEndpoint string = docIntelligence.outputs.endpoint
output aiHubName string = aiHub.outputs.name
output aiProjectName string = aiProject.outputs.name
