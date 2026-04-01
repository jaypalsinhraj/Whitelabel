param location string
param environmentName string
param logAnalyticsCustomerId string
@secure()
param logAnalyticsSharedKey string
param tags object

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: logAnalyticsSharedKey
      }
    }
  }
  tags: tags
}

output environmentId string = env.id
