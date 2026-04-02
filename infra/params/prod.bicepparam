using '../main.bicep'

param environment = 'prod'
param acrName = 'acrwhitelabelprod001'
param containerAppsEnvName = 'cae-whitelabel-prod'
param logRetentionDays = 90
param frontendImageTag = 'latest'
param backendImageTag = 'latest'
param minReplicas = 2
param maxReplicas = 10

param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD')
