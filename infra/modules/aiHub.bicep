// AI Foundry Hub module
// Creates Azure AI Foundry Hub for agent orchestration

@description('Name of the AI Hub')
param name string

@description('Location for the AI Hub')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('Storage account ID for the AI Hub')
param storageAccountId string

@description('Key Vault ID for the AI Hub')
param keyVaultId string

@description('Application Insights ID for the AI Hub (optional)')
param applicationInsightsId string = ''

resource aiHub 'Microsoft.MachineLearningServices/workspaces@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'Hub'
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    friendlyName: name
    storageAccount: storageAccountId
    keyVault: keyVaultId
    applicationInsights: !empty(applicationInsightsId) ? applicationInsightsId : null
    publicNetworkAccess: 'Enabled'
    managedNetwork: {
      isolationMode: 'Disabled'
    }
  }
}

output name string = aiHub.name
output id string = aiHub.id
output principalId string = aiHub.identity.principalId
