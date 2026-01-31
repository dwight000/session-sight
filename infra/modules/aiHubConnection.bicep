// AI Hub to Azure OpenAI connection
// Creates a connection from AI Foundry Hub to Azure OpenAI for agent access

@description('Name of the AI Hub workspace')
param hubName string

@description('Name of the connection')
param connectionName string

@description('Azure OpenAI resource ID')
param openaiResourceId string

@description('Azure OpenAI endpoint')
param openaiEndpoint string

resource aiHub 'Microsoft.MachineLearningServices/workspaces@2024-10-01' existing = {
  name: hubName
}

resource openaiConnection 'Microsoft.MachineLearningServices/workspaces/connections@2024-10-01' = {
  parent: aiHub
  name: connectionName
  properties: {
    category: 'AzureOpenAI'
    target: openaiEndpoint
    authType: 'AAD'
    metadata: {
      ApiType: 'Azure'
      ResourceId: openaiResourceId
    }
  }
}

output connectionName string = openaiConnection.name
