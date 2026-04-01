param location string
param workspaceName string
param retentionInDays int
param tags object

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
  }
  tags: tags
}

var keys = workspace.listKeys()

output workspaceId string = workspace.id
output customerId string = workspace.properties.customerId
@secure()
output sharedKey string = keys.primarySharedKey
