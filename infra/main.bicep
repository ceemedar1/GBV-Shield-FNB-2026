targetScope = 'resourceGroup'

@description('Globally unique lowercase prefix, for example gbvshieldprod')
param namePrefix string
param location string = resourceGroup().location
@secure()
param firebaseApiKey string
@description('Published container image, for example ghcr.io/org/gbvshield-api:sha')
param containerImage string

var storageName = take(replace('${namePrefix}tracking', '-', ''), 24)
var workspaceName = '${namePrefix}-logs'
var environmentName = '${namePrefix}-env'
var apiName = '${namePrefix}-api'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: { retentionInDays: 30 }
  sku: { name: 'PerGB2018' }
}

resource managedEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: managedEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: { external: true, targetPort: 8080, allowInsecure: false }
      secrets: [
        { name: 'firebase-api-key', value: firebaseApiKey }
        { name: 'storage-connection', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
      ]
    }
    template: {
      containers: [{
        name: 'api'
        image: containerImage
        resources: { cpu: json('0.25'), memory: '0.5Gi' }
        env: [
          { name: 'Firebase__ApiKey', secretRef: 'firebase-api-key' }
          { name: 'Storage__ConnectionString', secretRef: 'storage-connection' }
          { name: 'Tracking__PublicBaseUrl', value: 'https://${apiName}.${managedEnv.properties.defaultDomain}/track.html' }
        ]
        probes: [{ type: 'Liveness', httpGet: { path: '/health', port: 8080 }, initialDelaySeconds: 10, periodSeconds: 30 }]
      }]
      scale: { minReplicas: 0, maxReplicas: 2, rules: [{ name: 'http', http: { metadata: { concurrentRequests: '25' } } }] }
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
