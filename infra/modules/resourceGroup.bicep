// Resource Group module
// Creates a resource group for SessionSight resources

targetScope = 'subscription'

@description('Name of the resource group')
param name string

@description('Location for the resource group')
param location string

@description('Tags to apply to the resource group')
param tags object = {}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: name
  location: location
  tags: tags
}

output name string = rg.name
output id string = rg.id
