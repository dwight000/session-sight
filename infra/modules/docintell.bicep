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

output name string = docIntelligence.name
output id string = docIntelligence.id
output endpoint string = docIntelligence.properties.endpoint
// Note: API key is a secret, retrieve via Azure CLI: az cognitiveservices account keys list
