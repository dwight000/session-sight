// Document Intelligence module
// Creates Azure AI Document Intelligence for PDF/OCR processing

@description('Name of the Document Intelligence resource')
param name string

@description('Location for the Document Intelligence resource')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU name for the Document Intelligence resource')
@allowed(['F0', 'S0'])
param skuName string = 'F0'

@description('Principal ID to grant Cognitive Services User role (e.g., AI Project managed identity)')
param cognitiveServicesUserPrincipalId string = ''

resource docIntelligence 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  tags: tags
  kind: 'FormRecognizer'
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

// Grant Cognitive Services User role if principal provided
// This allows the principal to call Document Intelligence APIs using Azure AD auth
resource cognitiveServicesUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(cognitiveServicesUserPrincipalId)) {
  name: guid(docIntelligence.id, cognitiveServicesUserPrincipalId, 'Cognitive Services User')
  scope: docIntelligence
  properties: {
    principalId: cognitiveServicesUserPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908') // Cognitive Services User
    principalType: 'ServicePrincipal'
  }
}

output name string = docIntelligence.name
output id string = docIntelligence.id
output endpoint string = docIntelligence.properties.endpoint
// Note: API key is a secret, retrieve via Azure CLI: az cognitiveservices account keys list
