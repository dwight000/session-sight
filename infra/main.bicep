// SessionSight Infrastructure
// Main entry point for deploying all Azure resources
// Deploys at subscription scope, creates resource group and all resources
//
// Resource sharing strategy:
// - Dev creates all resources (AI services, SQL server, Container Apps env)
// - Stage shares dev's stateless AI services and SQL server, gets its own
//   database, storage, key vault, and container apps within the same RG

targetScope = 'subscription'

// === Parameters ===

@description('Environment name (dev or stage)')
@allowed(['dev', 'stage', 'test'])
param environmentName string

@description('Location for all resources')
param location string = 'eastus2'

@description('SQL administrator password')
@secure()
param sqlAdminPassword string

@description('Object ID of the service principal for RBAC assignments')
param servicePrincipalObjectId string = ''

@description('Object ID of a developer user for local dev RBAC assignments (e.g., for DefaultAzureCredential)')
param developerUserObjectId string = ''

@description('GitHub PAT with read:packages scope for pulling images from ghcr.io')
@secure()
param ghcrToken string = ''

@description('Enable Container Apps deployment (requires ghcrToken)')
param deployContainerApps bool = false

// === Variables ===

var prefix = 'sessionsight'
var isDevEnvironment = environmentName == 'dev'

// All environments share a single resource group
var resourceGroupName = 'rg-${prefix}-dev'

var tags = {
  project: 'SessionSight'
  environment: environmentName
  managedBy: 'Bicep'
}

// Per-env resource names
var storageAccountName = '${prefix}storage${environmentName}'

// Shared resource names (always dev-suffixed, created by dev deployment)
var sharedSqlServerName = '${prefix}-sql-dev'
var sharedOpenaiName = '${prefix}-openai-dev'
var sharedSearchName = '${prefix}-search-dev'
var sharedDocIntName = '${prefix}-docint-dev'

// Per-env database and search index names
var sqlDatabaseName = isDevEnvironment ? 'sessionsight' : 'sessionsight-${environmentName}'
var searchIndexName = isDevEnvironment ? 'sessionsight-sessions' : 'sessionsight-sessions-${environmentName}'

// Computed endpoints for shared AI services (predictable Azure naming)
var openaiEndpointValue = 'https://${sharedOpenaiName}.openai.azure.com/'
var searchEndpointValue = 'https://${sharedSearchName}.search.windows.net'
var docIntelligenceEndpointValue = 'https://${sharedDocIntName}.cognitiveservices.azure.com/'

// === Resource Group ===
// Only dev creates the RG; stage deploys into the existing dev RG

module rg 'modules/resourceGroup.bicep' = if (isDevEnvironment) {
  name: 'resourceGroup'
  params: {
    name: resourceGroupName
    location: location
    tags: tags
  }
}

// === Key Vault ===
// Per-env (different connection strings per environment)

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
// Per-env (PHI isolation — document blobs separated by environment)

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
// Server is shared (created by dev only); each env gets its own database

module sql 'modules/sql.bicep' = {
  name: 'sql'
  scope: resourceGroup(resourceGroupName)
  params: {
    serverName: sharedSqlServerName
    databaseName: sqlDatabaseName
    location: location
    tags: tags
    adminPassword: sqlAdminPassword
    createServer: isDevEnvironment
    enableFreeTier: isDevEnvironment
  }
  dependsOn: [rg]
}

// === Shared AI Services (dev only) ===
// These are stateless — stage reuses dev's instances via computed endpoints

module openai 'modules/openai.bicep' = if (isDevEnvironment) {
  name: 'openai'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedOpenaiName
    location: location
    tags: tags
  }
  dependsOn: [rg]
}

module search 'modules/search.bicep' = if (isDevEnvironment) {
  name: 'search'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedSearchName
    location: location
    tags: tags
    skuName: 'free'
  }
  dependsOn: [rg]
}

module docIntelligence 'modules/docintell.bicep' = if (isDevEnvironment) {
  name: 'docIntelligence'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedDocIntName
    location: location
    tags: tags
    skuName: 'F0'
  }
  dependsOn: [rg]
}

// === AI Foundry Hub + Project (dev only) ===

module aiHub 'modules/aiHub.bicep' = if (isDevEnvironment) {
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

module aiProject 'modules/aiProject.bicep' = if (isDevEnvironment) {
  name: 'aiProject'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-aiproject-${environmentName}'
    location: location
    tags: tags
    hubId: aiHub.outputs.id
  }
}

module aiHubConnection 'modules/aiHubConnection.bicep' = if (isDevEnvironment) {
  name: 'aiHubConnection'
  scope: resourceGroup(resourceGroupName)
  params: {
    hubName: aiHub.outputs.name
    connectionName: 'openai-connection'
    openaiResourceId: openai.outputs.id
    openaiEndpoint: openai.outputs.endpoint
  }
}

// === Role Assignments for AI Project Managed Identity (dev only) ===

module openaiRoleAssignment 'modules/openai.bicep' = if (isDevEnvironment) {
  name: 'openai-role-assignment'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedOpenaiName
    location: location
    tags: tags
    deployGpt41: false
    deployGpt41Mini: false
    deployGpt41Nano: false
    deployEmbeddings: false
    cognitiveServicesUserPrincipalId: aiProject.outputs.principalId
  }
  dependsOn: [openai]
}

module docIntelligenceRoleAssignment 'modules/docintell.bicep' = if (isDevEnvironment) {
  name: 'docIntelligence-role-assignment'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedDocIntName
    location: location
    tags: tags
    cognitiveServicesUserPrincipalId: aiProject.outputs.principalId
  }
  dependsOn: [docIntelligence]
}

module searchRoleAssignment 'modules/search.bicep' = if (isDevEnvironment) {
  name: 'search-role-assignment-aiproject'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedSearchName
    location: location
    tags: tags
    skuName: 'free'
    searchIndexDataContributorPrincipalId: aiProject.outputs.principalId
    searchIndexDataContributorPrincipalType: 'ServicePrincipal'
  }
  dependsOn: [search]
}

// === Search Role Assignment for Developer (dev only — same search service covers all indexes) ===

module searchRoleAssignmentDeveloper 'modules/search.bicep' = if (isDevEnvironment && !empty(developerUserObjectId)) {
  name: 'search-role-assignment-developer'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedSearchName
    location: location
    tags: tags
    skuName: 'free'
    searchIndexDataContributorPrincipalId: developerUserObjectId
    searchIndexDataContributorPrincipalType: 'User'
  }
  dependsOn: [search]
}

// === Container Apps ===
// Environment is shared (created by dev); each env gets its own API + Web apps

module containerApps 'modules/containerApps.bicep' = if (deployContainerApps) {
  name: 'containerApps'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-${environmentName}'
    location: location
    tags: tags
    ghcrToken: ghcrToken
    createEnvironment: isDevEnvironment
    existingEnvName: '${prefix}-dev-env'
    searchIndexName: searchIndexName
    // Pass Azure service endpoints (shared AI services, per-env storage)
    sqlConnectionString: 'Server=${sharedSqlServerName}.database.windows.net;Database=${sqlDatabaseName};User Id=sessionsightadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
    openaiEndpoint: openaiEndpointValue
    searchEndpoint: searchEndpointValue
    docIntelligenceEndpoint: docIntelligenceEndpointValue
    storageBlobEndpoint: storage.outputs.blobEndpoint
  }
  dependsOn: [rg]
}

// === Container Apps Role Assignments ===
// Grant the API managed identity access to shared AI services + per-env storage
// Role assignment modules are idempotent — re-deploying shared resources is a no-op

module containerAppsOpenaiRole 'modules/openai.bicep' = if (deployContainerApps) {
  name: 'containerApps-openai-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedOpenaiName
    location: location
    tags: tags
    deployGpt41: false
    deployGpt41Mini: false
    deployGpt41Nano: false
    deployEmbeddings: false
    cognitiveServicesUserPrincipalId: containerApps.outputs.apiPrincipalId
  }
}

module containerAppsDocIntelRole 'modules/docintell.bicep' = if (deployContainerApps) {
  name: 'containerApps-docintell-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedDocIntName
    location: location
    tags: tags
    cognitiveServicesUserPrincipalId: containerApps.outputs.apiPrincipalId
  }
}

module containerAppsSearchRole 'modules/search.bicep' = if (deployContainerApps) {
  name: 'containerApps-search-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: sharedSearchName
    location: location
    tags: tags
    skuName: 'free'
    searchIndexDataContributorPrincipalId: containerApps.outputs.apiPrincipalId
    searchIndexDataContributorPrincipalType: 'ServicePrincipal'
  }
}

module containerAppsStorageRole 'modules/storage.bicep' = if (deployContainerApps) {
  name: 'containerApps-storage-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: storageAccountName
    location: location
    tags: tags
    contributorObjectId: containerApps.outputs.apiPrincipalId
  }
}

// === Outputs ===

output resourceGroupName string = resourceGroupName
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.vaultUri
output storageAccountName string = storage.outputs.name
output storageBlobEndpoint string = storage.outputs.blobEndpoint
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output openaiName string = sharedOpenaiName
output openaiEndpoint string = openaiEndpointValue
output searchName string = sharedSearchName
output searchEndpoint string = searchEndpointValue
output searchIndexName string = searchIndexName
output docIntelligenceName string = sharedDocIntName
output docIntelligenceEndpoint string = docIntelligenceEndpointValue
output aiHubName string = isDevEnvironment ? aiHub.outputs.name : ''
output aiProjectName string = isDevEnvironment ? aiProject.outputs.name : ''
output aiProjectEndpoint string = isDevEnvironment ? 'https://${location}.api.azureml.ms/agents/v1.0/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroupName}/providers/Microsoft.MachineLearningServices/workspaces/${aiProject.outputs.name}' : ''

// Container Apps outputs (only when deployed)
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output containerAppsEnvName string = deployContainerApps ? containerApps.outputs.envName : ''
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output apiUrl string = deployContainerApps ? containerApps.outputs.apiUrl : ''
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output webUrl string = deployContainerApps ? containerApps.outputs.webUrl : ''
