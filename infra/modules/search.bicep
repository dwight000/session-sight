// Azure AI Search module
// Creates Azure AI Search service for RAG

@description('Name of the Azure AI Search resource')
param name string

@description('Location for the Azure AI Search resource')
param location string = resourceGroup().location

@description('Tags to apply to the resource')
param tags object = {}

@description('SKU name for the Azure AI Search resource')
@allowed(['free', 'basic', 'standard', 'standard2', 'standard3', 'storage_optimized_l1', 'storage_optimized_l2'])
param skuName string = 'free'

@description('Number of replicas (only for paid tiers)')
param replicaCount int = 1

@description('Number of partitions (only for paid tiers)')
param partitionCount int = 1

resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  properties: {
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    partitionCount: skuName == 'free' ? 1 : partitionCount
    replicaCount: skuName == 'free' ? 1 : replicaCount
    semanticSearch: skuName == 'free' ? 'disabled' : 'free'
  }
}

output name string = search.name
output id string = search.id
output endpoint string = 'https://${search.name}.search.windows.net'
// Note: Admin key is a secret, retrieve via Azure CLI: az search admin-key show
