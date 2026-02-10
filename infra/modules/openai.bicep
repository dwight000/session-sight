// Azure OpenAI module
// Creates Azure OpenAI service with model deployments
// Updated Feb 2026: Migrated from deprecated gpt-4o/gpt-4o-mini to gpt-4.1-mini/gpt-4.1-nano

@description('Name of the Azure OpenAI resource')
param name string

@description('Location for the Azure OpenAI resource')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU name for the Azure OpenAI resource')
param skuName string = 'S0'

@description('Deploy GPT-4.1-mini model (replaces gpt-4o)')
param deployGpt41Mini bool = true

@description('Deploy GPT-4.1-nano model (replaces gpt-4o-mini)')
param deployGpt41Nano bool = true

@description('Deploy GPT-4.1 model')
param deployGpt41 bool = true

@description('Deploy text-embedding-3-large model')
param deployEmbeddings bool = true

@description('GPT-4.1-mini deployment capacity (TPM in thousands)')
param gpt41MiniCapacity int = 10

@description('GPT-4.1-nano deployment capacity (TPM in thousands)')
param gpt41NanoCapacity int = 10

@description('GPT-4.1 deployment capacity (TPM in thousands)')
param gpt41Capacity int = 10

@description('Embeddings deployment capacity (TPM in thousands)')
param embeddingsCapacity int = 10

@description('Principal ID to grant Cognitive Services User role (e.g., AI Project managed identity)')
param cognitiveServicesUserPrincipalId string = ''

resource openai 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'OpenAI'
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

resource gpt41MiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41Mini) {
  parent: openai
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
  dependsOn: [gpt41Deployment] // Sequential deployment to avoid conflicts
}

resource gpt41Deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41) {
  parent: openai
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
}

resource gpt41NanoDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt41Nano) {
  parent: openai
  name: 'gpt-4.1-nano'
  sku: {
    name: 'GlobalStandard'  // nano only supports GlobalStandard, not Standard
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
  dependsOn: [gpt41MiniDeployment] // Sequential deployment to avoid conflicts
}

resource embeddingsDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployEmbeddings) {
  parent: openai
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
  dependsOn: [gpt41NanoDeployment] // Sequential deployment to avoid conflicts
}

// Grant Cognitive Services User role if principal provided
// This allows the principal to call OpenAI APIs using Azure AD auth
resource cognitiveServicesUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(cognitiveServicesUserPrincipalId)) {
  name: guid(openai.id, cognitiveServicesUserPrincipalId, 'Cognitive Services User')
  scope: openai
  properties: {
    principalId: cognitiveServicesUserPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908') // Cognitive Services User
    principalType: 'ServicePrincipal'
  }
}

output name string = openai.name
output id string = openai.id
output endpoint string = openai.properties.endpoint
// Note: API key is a secret, retrieve via Azure CLI: az cognitiveservices account keys list
