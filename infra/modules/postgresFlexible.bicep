param location string
@description('Server name: 3–63 chars, lowercase letters, digits, hyphens; globally unique.')
param serverName string
param administratorLogin string
@secure()
param administratorLoginPassword string
param databaseName string = 'whitelabel'
param tags object

// https://learn.microsoft.com/azure/postgresql/flexible-server/quickstart-create-server
resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01' = {
  name: serverName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
  tags: tags
}

resource db 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01' = {
  parent: server
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Allow Azure services (incl. Container Apps) to reach the server — tighten for production (VNet/private).
resource firewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01' = {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('PostgreSQL FQDN for connection strings.')
output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName

output databaseNameOut string = databaseName

@description('Npgsql connection string for the app (contains password).')
@secure()
output connectionString string = 'Host=${server.properties.fullyQualifiedDomainName};Database=${databaseName};Username=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;Trust Server Certificate=true'
