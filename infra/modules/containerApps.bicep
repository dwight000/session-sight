// Container Apps module
// Creates Azure Container Apps environment (optionally) and apps for API and Web frontend
// Pulls images from GitHub Container Registry (ghcr.io)
// When createEnvironment is false, references an existing shared environment

@description('Base name for resources')
param name string

@description('Location for resources')
param location string = resourceGroup().location

@description('Tags to apply to resources')
param tags object = {}

@description('GitHub Container Registry username')
param ghcrUsername string = 'dwight000'

@description('API container image tag')
param apiImageTag string = 'latest'

@description('Web container image tag')
param webImageTag string = 'latest'

@description('GitHub PAT with read:packages scope for pulling from ghcr.io')
@secure()
param ghcrToken string

@description('Create the Container Apps Environment (false = reference existing shared env)')
param createEnvironment bool = true

@description('Name of existing Container Apps Environment (used when createEnvironment is false)')
param existingEnvName string = ''

@description('Azure AI Search index name (env-specific to isolate data)')
param searchIndexName string = 'sessionsight-sessions'

@description('ASP.NET Core environment name (Staging for cloud dev, Production for stage)')
param aspnetEnvironment string = 'Production'

// === Azure service endpoints (passed from main.bicep) ===

@description('SQL Server connection string')
@secure()
param sqlConnectionString string

@description('Azure OpenAI endpoint')
param openaiEndpoint string

@description('Azure AI Search endpoint')
param searchEndpoint string

@description('Document Intelligence endpoint')
param docIntelligenceEndpoint string

@description('Azure Blob Storage endpoint')
param storageBlobEndpoint string

// === Container Apps Environment ===

resource newEnv 'Microsoft.App/managedEnvironments@2023-05-01' = if (createEnvironment) {
  name: '${name}-env'
  location: location
  tags: tags
  properties: {
    zoneRedundant: false  // Dev doesn't need HA
  }
}

resource existingEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = if (!createEnvironment) {
  name: existingEnvName
}

var managedEnvId = createEnvironment ? newEnv.id : existingEnv.id

// === API Container App ===

resource apiApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${name}-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: 'ghcr.io'
          username: ghcrUsername
          passwordSecretRef: 'ghcr-token'
        }
      ]
      secrets: [
        { name: 'ghcr-token', value: ghcrToken }
        { name: 'sql-connection-string', value: sqlConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: 'ghcr.io/${ghcrUsername}/sessionsight-api:${apiImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            // Connection strings
            { name: 'ConnectionStrings__sessionsight', secretRef: 'sql-connection-string' }
            // Azure service endpoints (uses managed identity for auth)
            { name: 'AzureOpenAI__Endpoint', value: openaiEndpoint }
            { name: 'AzureSearch__Endpoint', value: searchEndpoint }
            { name: 'AzureSearch__IndexName', value: searchIndexName }
            { name: 'DocumentIntelligence__Endpoint', value: docIntelligenceEndpoint }
            { name: 'ConnectionStrings__documents', value: storageBlobEndpoint }
            // ASP.NET Core settings
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
          ]
        }
      ]
      scale: {
        minReplicas: 1  // Keep at least 1 replica for reliable internal DNS + faster response
        maxReplicas: 3
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

// === Web (Frontend) Container App ===

resource webApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${name}-web'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: managedEnvId
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: 'ghcr.io'
          username: ghcrUsername
          passwordSecretRef: 'ghcr-token'
        }
      ]
      secrets: [
        { name: 'ghcr-token', value: ghcrToken }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: 'ghcr.io/${ghcrUsername}/sessionsight-web:${webImageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            // API URL for nginx proxy
            // Use external HTTPS URL - nginx config handles SSL verification
            { name: 'API_URL', value: 'https://${apiApp.properties.configuration.ingress.fqdn}' }
          ]
        }
      ]
      scale: {
        minReplicas: 1  // Keep at least 1 replica for reliable response
        maxReplicas: 2
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
}

// === Outputs ===

output envId string = managedEnvId
output envName string = createEnvironment ? newEnv.name : existingEnvName
output apiAppName string = apiApp.name
output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output apiPrincipalId string = apiApp.identity.principalId
output webAppName string = webApp.name
output webUrl string = 'https://${webApp.properties.configuration.ingress.fqdn}'
