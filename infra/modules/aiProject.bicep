// AI Foundry Project module
// Creates Azure AI Foundry Project for SessionSight agents

@description('Name of the AI Project')
param name string

@description('Location for the AI Project')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('AI Hub ID that this project belongs to')
param hubId string

resource aiProject 'Microsoft.MachineLearningServices/workspaces@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'Project'
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    friendlyName: name
    hubResourceId: hubId
    publicNetworkAccess: 'Enabled'
  }
}

output name string = aiProject.name
output id string = aiProject.id
output principalId string = aiProject.identity.principalId
