// Azure AI Services module
// Creates Azure AI Services resource (kind: AIServices) for non-OpenAI model deployments
// (Mistral, Grok, Llama, etc.) which cannot be deployed on kind: OpenAI resources.

@description('Name of the Azure AI Services resource')
param name string

@description('Location for the Azure AI Services resource')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU name for the Azure AI Services resource')
param skuName string = 'S0'

@description('Deploy Mistral-Large-3 model')
param deployMistralLarge3 bool = true

@description('Mistral-Large-3 deployment capacity (TPM in thousands)')
param mistralLarge3Capacity int = 1

@description('Principal ID to grant Cognitive Services User role')
param cognitiveServicesUserPrincipalId string = ''

@description('Principal type for the role assignment')
@allowed(['User', 'ServicePrincipal'])
param cognitiveServicesUserPrincipalType string = 'User'

resource aiServices 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: skuName
  }
  properties: {
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource mistralLarge3Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployMistralLarge3) {
  parent: aiServices
  name: 'Mistral-Large-3'
  sku: {
    name: 'GlobalStandard'
    capacity: mistralLarge3Capacity
  }
  properties: {
    model: {
      format: 'Mistral AI'
      name: 'Mistral-Large-3'
    }
  }
}

// Grant Cognitive Services User role if principal provided
resource cognitiveServicesUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(cognitiveServicesUserPrincipalId)) {
  name: guid(aiServices.id, cognitiveServicesUserPrincipalId, 'Cognitive Services User')
  scope: aiServices
  properties: {
    principalId: cognitiveServicesUserPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908') // Cognitive Services User
    principalType: cognitiveServicesUserPrincipalType
  }
}

output name string = aiServices.name
output id string = aiServices.id
output endpoint string = aiServices.properties.endpoint
