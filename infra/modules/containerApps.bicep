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

@description('IP security restrictions for web ingress (empty = allow all). API is unrestricted — accessed via web proxy.')
param webIpSecurityRestrictions array = []

@description('Minimum API replicas. 0 = scale to zero when idle (saves costs). Max is always 1.')
param minApiReplicas int = 0

// === Azure service endpoints (passed from main.bicep) ===

@description('SQL Server connection string (Managed Identity — no secrets)')
param sqlConnectionString string

@description('Azure AI Search endpoint')
param searchEndpoint string

@description('Document Intelligence endpoint')
param docIntelligenceEndpoint string

@description('Azure Blob Storage endpoint')
param storageBlobEndpoint string

@description('Azure AI Services endpoint (consolidated — hosts all models: OpenAI + Mistral)')
param aiServicesEndpoint string

// === Container Apps Environment ===

resource newEnv 'Microsoft.App/managedEnvironments@2024-10-02-preview' = if (createEnvironment) {
  name: '${name}-env'
  location: location
  tags: tags
  properties: {
    zoneRedundant: false  // Dev doesn't need HA
  }
}

resource existingEnv 'Microsoft.App/managedEnvironments@2024-10-02-preview' existing = if (!createEnvironment) {
  name: existingEnvName
}

var managedEnvId = createEnvironment ? newEnv.id : existingEnv.id

// === API Container App ===

resource apiApp 'Microsoft.App/containerApps@2024-10-02-preview' = {
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
            // Connection strings (MI auth — no secrets)
            { name: 'ConnectionStrings__sessionsight', value: sqlConnectionString }
            // Azure AI Services endpoint (consolidated — all models: OpenAI + Mistral)
            { name: 'AzureAIServices__Endpoint', value: aiServicesEndpoint }
            { name: 'AzureSearch__Endpoint', value: searchEndpoint }
            { name: 'AzureSearch__IndexName', value: searchIndexName }
            { name: 'DocumentIntelligence__Endpoint', value: docIntelligenceEndpoint }
            { name: 'ConnectionStrings__documents', value: storageBlobEndpoint }
            // Feature flags
            { name: 'RiskDebate__Enabled', value: 'true' }
            { name: 'RiskDebate__TriggerMode', value: 'Always' }
            { name: 'PipelineDiagnostics__StoreLlmTraces', value: 'true' }
            // ASP.NET Core settings
            { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetEnvironment }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
          ]
        }
      ]
      scale: {
        minReplicas: minApiReplicas
        maxReplicas: 1
        cooldownPeriod: 1800  // 30 min of no traffic before scaling to zero
        pollingInterval: 30
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

resource webApp 'Microsoft.App/containerApps@2024-10-02-preview' = {
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
        ipSecurityRestrictions: webIpSecurityRestrictions
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
        minReplicas: 0  // Scale to zero when idle to save costs
        maxReplicas: 1
        cooldownPeriod: 1800  // 30 min of no traffic before scaling to zero
        pollingInterval: 30
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
