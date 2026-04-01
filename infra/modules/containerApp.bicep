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

var registrySecretName = 'acr-password'

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
      secrets: [
        {
          name: registrySecretName
          value: acrPassword
        }
      ]
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
          env: envVars
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
