// Azure OpenAI module
// Creates Azure OpenAI service with model deployments

@description('Name of the Azure OpenAI resource')
param name string

@description('Location for the Azure OpenAI resource')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU name for the Azure OpenAI resource')
param skuName string = 'S0'

@description('Deploy GPT-4o model')
param deployGpt4o bool = true

@description('Deploy GPT-4o-mini model')
param deployGpt4oMini bool = true

@description('Deploy text-embedding-3-large model')
param deployEmbeddings bool = true

@description('GPT-4o deployment capacity (TPM in thousands)')
param gpt4oCapacity int = 10

@description('GPT-4o-mini deployment capacity (TPM in thousands)')
param gpt4oMiniCapacity int = 10

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

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt4o) {
  parent: openai
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: gpt4oCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

resource gpt4oMiniDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = if (deployGpt4oMini) {
  parent: openai
  name: 'gpt-4o-mini'
  sku: {
    name: 'Standard'
    capacity: gpt4oMiniCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o-mini'
      version: '2024-07-18'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
  dependsOn: [gpt4oDeployment] // Sequential deployment to avoid conflicts
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
  dependsOn: [gpt4oMiniDeployment] // Sequential deployment to avoid conflicts
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
