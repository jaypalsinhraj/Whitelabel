param location string
param acrName string
param tags object

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
    publicNetworkAccess: 'Enabled'
  }
  tags: tags
}

var creds = acr.listCredentials()

output loginServer string = acr.properties.loginServer
output username string = creds.username
@secure()
output password string = creds.passwords[0].value
output acrId string = acr.id
