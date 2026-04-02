targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Logical environment name (dev, prod, etc.).')
param environment string

@description('Globally unique name for Azure Container Registry (5–50 alphanumeric).')
param acrName string

@description('Log Analytics retention in days.')
param logRetentionDays int = 30

@description('Container Apps environment name suffix (unique within region).')
param containerAppsEnvName string

@description('Frontend container image tag (pushed to ACR).')
param frontendImageTag string = 'latest'

@description('Backend container image tag (pushed to ACR).')
param backendImageTag string = 'latest'

@description('Minimum replicas per app.')
param minReplicas int = 1

@description('Maximum replicas per app.')
param maxReplicas int = 3

@description('Optional: Entra/Azure AD settings for the API (use Key Vault or App Config in production).')
param azureAdTenantId string = ''
param azureAdApiClientId string = ''
param azureAdAudience string = ''
param azureAdDomain string = ''

@description('PostgreSQL flexible server admin login (no @ or spaces).')
param postgresAdminLogin string = 'whitelabel'

@description('PostgreSQL flexible server admin password (required for API database).')
@secure()
param postgresAdminPassword string

var tags = {
  environment: environment
  application: 'whitelabel-saas'
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    acrName: acrName
    tags: tags
  }
}

module logs 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: {
    location: location
    workspaceName: 'law-${environment}-${uniqueString(resourceGroup().id)}'
    retentionInDays: logRetentionDays
    tags: tags
  }
}

module cae 'modules/containerAppsEnv.bicep' = {
  name: 'containerAppsEnv'
  params: {
    location: location
    environmentName: containerAppsEnvName
    logAnalyticsCustomerId: logs.outputs.customerId
    logAnalyticsSharedKey: logs.outputs.sharedKey
    tags: tags
  }
}

var acrLoginServer = acr.outputs.loginServer

var postgresServerName = take(
  toLower(replace('psql-${environment}-${uniqueString(resourceGroup().id)}', '_', '-')),
  50
)

module postgres 'modules/postgresFlexible.bicep' = {
  name: 'postgres'
  params: {
    location: location
    serverName: postgresServerName
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    databaseName: 'whitelabel'
    tags: tags
  }
}

module fe 'modules/containerApp.bicep' = {
  name: 'frontendApp'
  params: {
    location: location
    appName: 'ca-fe-${environment}-${take(uniqueString(resourceGroup().id), 8)}'
    environmentId: cae.outputs.environmentId
    acrLoginServer: acrLoginServer
    acrUsername: acr.outputs.username
    acrPassword: acr.outputs.password
    image: '${acrLoginServer}/whitelabel-frontend:${frontendImageTag}'
    targetPort: 3000
    ingressExternal: true
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    envVars: []
    tags: tags
  }
}

module api 'modules/containerApp.bicep' = {
  name: 'backendApp'
  params: {
    location: location
    appName: 'ca-api-${environment}-${take(uniqueString(resourceGroup().id), 8)}'
    environmentId: cae.outputs.environmentId
    acrLoginServer: acrLoginServer
    acrUsername: acr.outputs.username
    acrPassword: acr.outputs.password
    image: '${acrLoginServer}/whitelabel-backend:${backendImageTag}'
    targetPort: 5000
    ingressExternal: true
    minReplicas: minReplicas
    maxReplicas: maxReplicas
    includePostgresConnection: true
    postgresConnectionString: postgres.outputs.connectionString
    envVars: concat(
      [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'AzureAd__Instance'
          value: az.environment().authentication.loginEndpoint
        }
        {
          name: 'Cors__Origins__0'
          value: fe.outputs.fqdn != '' ? 'https://${fe.outputs.fqdn}' : '*'
        }
      ],
      !empty(azureAdDomain)
        ? [
            {
              name: 'AzureAd__Domain'
              value: azureAdDomain
            }
          ]
        : [],
      !empty(azureAdTenantId)
        ? [
            {
              name: 'AzureAd__TenantId'
              value: azureAdTenantId
            }
            {
              name: 'AzureAd__ClientId'
              value: azureAdApiClientId
            }
            {
              name: 'AzureAd__Audience'
              value: azureAdAudience
            }
          ]
        : []
    )
    tags: tags
  }
}

@description('FQDN for the frontend Container App (HTTPS).')
output frontendFqdn string = fe.outputs.fqdn

@description('FQDN for the backend Container App (HTTPS).')
output backendFqdn string = api.outputs.fqdn

@description('Azure Container Registry login server.')
output acrLoginServerOut string = acrLoginServer

@description('Log Analytics workspace resource id.')
output logAnalyticsWorkspaceId string = logs.outputs.workspaceId

@description('PostgreSQL flexible server FQDN.')
output postgresFqdn string = postgres.outputs.fullyQualifiedDomainName
