param location string
param appName string
param environmentId string
param acrLoginServer string
param acrUsername string
@secure()
param acrPassword string
param image string
param targetPort int
param ingressExternal bool
param minReplicas int
param maxReplicas int
param envVars array = []
param tags object

@description('When set, adds ConnectionStrings__DefaultConnection via Container Apps secret (Npgsql).')
param includePostgresConnection bool = false
@secure()
param postgresConnectionString string = ''

var registrySecretName = 'acr-password'
var postgresSecretName = 'postgres-connection-string'

var registrySecrets = [
  {
    name: registrySecretName
    value: acrPassword
  }
]

var postgresSecrets = includePostgresConnection
  ? [
      {
        name: postgresSecretName
        value: postgresConnectionString
      }
    ]
  : []

var allSecrets = concat(registrySecrets, postgresSecrets)

var postgresEnv = includePostgresConnection
  ? [
      {
        name: 'ConnectionStrings__DefaultConnection'
        secretRef: postgresSecretName
      }
      {
        name: 'Database__Provider'
        value: 'PostgreSQL'
      }
    ]
  : []

var allEnv = concat(envVars, postgresEnv)

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: ingressExternal
        targetPort: targetPort
        transport: 'auto'
      }
      registries: [
        {
          server: acrLoginServer
          username: acrUsername
          passwordSecretRef: registrySecretName
        }
      ]
      secrets: allSecrets
    }
    template: {
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
      containers: [
        {
          name: 'main'
          image: image
          env: allEnv
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
  tags: tags
}

output fqdn string = ingressExternal ? app.properties.configuration.ingress.fqdn : ''
output appId string = app.id
