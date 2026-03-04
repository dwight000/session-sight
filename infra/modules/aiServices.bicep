// Azure AI Services module
// Creates Azure AI Services resource (kind: AIServices) for all model deployments:
// - OpenAI models (gpt-4.1-*, text-embedding-3-large) with format: 'OpenAI'
// - Non-OpenAI models (Mistral, Grok, Llama, etc.) with vendor-specific formats
// AIServices kind supports both, eliminating the need for a separate OpenAI resource.

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

@description('Mistral-Large-3 deployment capacity (TPM in thousands). Debate needs 3+ sequential calls; capacity 1 allows only 1 req/60s.')
param mistralLarge3Capacity int = 8

@description('Deploy GPT-4.1 model')
param deployGpt41 bool = true

@description('GPT-4.1 deployment capacity (TPM in thousands)')
param gpt41Capacity int = 50

@description('Deploy GPT-4.1-mini model')
param deployGpt41Mini bool = true

@description('GPT-4.1-mini deployment capacity (TPM in thousands)')
param gpt41MiniCapacity int = 50

@description('Deploy GPT-4.1-nano model')
param deployGpt41Nano bool = true

@description('GPT-4.1-nano deployment capacity (TPM in thousands)')
param gpt41NanoCapacity int = 50

@description('Deploy text-embedding-3-large model')
param deployEmbeddings bool = true

@description('Embeddings deployment capacity (TPM in thousands)')
param embeddingsCapacity int = 50

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
      version: '1'
    }
  }
}

// === OpenAI model deployments (format: 'OpenAI') ===
// Sequential dependsOn chain avoids concurrent deployment conflicts

resource gpt41Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41) {
  parent: aiServices
  name: 'gpt-4.1'
  sku: {
    name: 'GlobalStandard'
    capacity: gpt41Capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1'
      version: '2025-04-14'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [mistralLarge3Deployment]
}

resource gpt41MiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41Mini) {
  parent: aiServices
  name: 'gpt-4.1-mini'
  sku: {
    name: 'Standard'
    capacity: gpt41MiniCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1-mini'
      version: '2025-04-14'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [gpt41Deployment]
}

resource gpt41NanoDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41Nano) {
  parent: aiServices
  name: 'gpt-4.1-nano'
  sku: {
    name: 'GlobalStandard'
    capacity: gpt41NanoCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4.1-nano'
      version: '2025-04-14'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [gpt41MiniDeployment]
}

resource embeddingsDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployEmbeddings) {
  parent: aiServices
  name: 'text-embedding-3-large'
  sku: {
    name: 'Standard'
    capacity: embeddingsCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
  }
  dependsOn: [gpt41NanoDeployment]
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
