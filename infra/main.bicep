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

@description('Object ID of a developer user for local dev RBAC assignments (e.g., for DefaultAzureCredential)')
param developerUserObjectId string = ''

@description('GitHub PAT with read:packages scope for pulling images from ghcr.io')
@secure()
param ghcrToken string = ''

@description('Enable Container Apps deployment (requires ghcrToken)')
param deployContainerApps bool = false

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

// === AI Hub to OpenAI Connection ===

module aiHubConnection 'modules/aiHubConnection.bicep' = {
  name: 'aiHubConnection'
  scope: resourceGroup(resourceGroupName)
  params: {
    hubName: aiHub.outputs.name
    connectionName: 'openai-connection'
    openaiResourceId: openai.outputs.id
    openaiEndpoint: openai.outputs.endpoint
  }
}

// === Role Assignments for AI Project Managed Identity ===
// Grant Cognitive Services User role on OpenAI and Doc Intelligence
// so the AI Project can call these APIs using Azure AD authentication

module openaiRoleAssignment 'modules/openai.bicep' = {
  name: 'openai-role-assignment'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-openai-${environmentName}'
    location: location
    tags: tags
    // Skip model deployments - they already exist
    deployGpt41: false
    deployGpt41Mini: false
    deployGpt41Nano: false
    deployEmbeddings: false
    cognitiveServicesUserPrincipalId: aiProject.outputs.principalId
  }
  dependsOn: [openai, aiProject]
}

module docIntelligenceRoleAssignment 'modules/docintell.bicep' = {
  name: 'docIntelligence-role-assignment'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-docint-${environmentName}'
    location: location
    tags: tags
    cognitiveServicesUserPrincipalId: aiProject.outputs.principalId
  }
  dependsOn: [docIntelligence, aiProject]
}

// === Search Role Assignment for AI Project ===
// Grant Search Index Data Contributor so the API can index session embeddings

module searchRoleAssignment 'modules/search.bicep' = {
  name: 'search-role-assignment-aiproject'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-search-${environmentName}'
    location: location
    tags: tags
    skuName: environmentName == 'prod' ? 'basic' : 'free'
    searchIndexDataContributorPrincipalId: aiProject.outputs.principalId
    searchIndexDataContributorPrincipalType: 'ServicePrincipal'
  }
  dependsOn: [search, aiProject]
}

// === Search Role Assignment for Developer ===
// Grant Search Index Data Contributor for local dev with DefaultAzureCredential

module searchRoleAssignmentDeveloper 'modules/search.bicep' = if (!empty(developerUserObjectId)) {
  name: 'search-role-assignment-developer'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-search-${environmentName}'
    location: location
    tags: tags
    skuName: environmentName == 'prod' ? 'basic' : 'free'
    searchIndexDataContributorPrincipalId: developerUserObjectId
    searchIndexDataContributorPrincipalType: 'User'
  }
  dependsOn: [search]
}

// === Container Apps ===
// Deploys API and Web frontend to Azure Container Apps (pulls from ghcr.io)

module containerApps 'modules/containerApps.bicep' = if (deployContainerApps) {
  name: 'containerApps'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-${environmentName}'
    location: location
    tags: tags
    ghcrToken: ghcrToken
    // Pass Azure service endpoints
    sqlConnectionString: 'Server=${sql.outputs.serverFqdn};Database=${sql.outputs.databaseName};User Id=sessionsightadmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
    openaiEndpoint: openai.outputs.endpoint
    searchEndpoint: search.outputs.endpoint
    docIntelligenceEndpoint: docIntelligence.outputs.endpoint
    storageBlobEndpoint: storage.outputs.blobEndpoint
  }
  dependsOn: [rg]
}

// === Container Apps Role Assignments ===
// Grant the API managed identity access to Azure services

module containerAppsOpenaiRole 'modules/openai.bicep' = if (deployContainerApps) {
  name: 'containerApps-openai-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-openai-${environmentName}'
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
    name: '${prefix}-docint-${environmentName}'
    location: location
    tags: tags
    cognitiveServicesUserPrincipalId: containerApps.outputs.apiPrincipalId
  }
}

module containerAppsSearchRole 'modules/search.bicep' = if (deployContainerApps) {
  name: 'containerApps-search-role'
  scope: resourceGroup(resourceGroupName)
  params: {
    name: '${prefix}-search-${environmentName}'
    location: location
    tags: tags
    skuName: environmentName == 'prod' ? 'basic' : 'free'
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
output aiProjectEndpoint string = 'https://${location}.api.azureml.ms/agents/v1.0/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroupName}/providers/Microsoft.MachineLearningServices/workspaces/${aiProject.outputs.name}'

// Container Apps outputs (only when deployed)
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output containerAppsEnvName string = deployContainerApps ? containerApps.outputs.envName : ''
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output apiUrl string = deployContainerApps ? containerApps.outputs.apiUrl : ''
#disable-next-line outputs-should-not-contain-secrets  // False positive: outputs contain URLs, not secrets
output webUrl string = deployContainerApps ? containerApps.outputs.webUrl : ''
