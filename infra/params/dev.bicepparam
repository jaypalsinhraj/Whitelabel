using '../main.bicep'

param environment = 'dev'
param acrName = 'acrwhitelabeldev001'
param containerAppsEnvName = 'cae-whitelabel-dev'
param logRetentionDays = 30
param frontendImageTag = 'latest'
param backendImageTag = 'latest'
param minReplicas = 1
param maxReplicas = 3
