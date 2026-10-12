// Reelr in Azure: one resource group in Sweden Central with Azure SQL (free offer), Key Vault and Container Apps.
// Deploy with infra/deploy.ps1, or by hand:
//   az deployment sub create --location swedencentral --template-file infra/main.bicep --parameters infra/main.bicepparam
targetScope = 'subscription'

@description('Azure region for every resource.')
param location string = 'swedencentral'

@description('Name of the resource group that holds everything.')
param resourceGroupName string = 'rg-reelr'

@description('Object id of the Entra user or group that administers the database and may write Key Vault secrets.')
param adminObjectId string

@description('Sign-in name shown for that database administrator.')
param adminLogin string

@description('Image tag to run, e.g. a commit SHA. Empty runs a placeholder image until CI has pushed the real ones.')
param imageTag string = ''

@description('Registry and owner the images are pulled from.')
param imageRepository string = 'ghcr.io/thesuperfalcon'

@description('Smallest number of backend replicas. 0 lets it sleep when unused (cold start of a few seconds); 1 keeps it warm.')
@minValue(0)
@maxValue(1)
param backendMinReplicas int = 0

@description('SMTP server for account emails.')
param emailHost string = 'smtp-relay.brevo.com'

@description('SMTP user name. Its password is the Key Vault secret "email-password".')
param emailUserName string = ''

@description('Sender address of account emails.')
param emailFromAddress string = ''

@description('User ids that get admin access, e.g. [\'1\'].')
param adminUserIds array = []

@description('User ids of shared demo accounts, which cannot change or delete the account itself.')
param demoUserIds array = []

@description('True lets only people with an invite code register. False opens registration, e.g. to create the first admin account.')
param inviteOnly bool = true

@description('Address that gets an email when the backend fails. Empty turns the alerts off.')
param alertEmail string = ''

@description('False creates only the base resources, so the Key Vault secrets and database users can be set up before the apps start.')
param deployApps bool = true

var tags = {
  project: 'reelr'
  env: 'beta'
}

resource group 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'reelr-resources'
  scope: group
  params: {
    location: location
    tags: tags
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    imageTag: imageTag
    imageRepository: imageRepository
    backendMinReplicas: backendMinReplicas
    emailHost: emailHost
    emailUserName: emailUserName
    emailFromAddress: emailFromAddress
    adminUserIds: adminUserIds
    demoUserIds: demoUserIds
    alertEmail: alertEmail
    inviteOnly: inviteOnly
    deployApps: deployApps
  }
}

output siteUrl string = resources.outputs.siteUrl
output keyVaultName string = resources.outputs.keyVaultName
output sqlServerName string = resources.outputs.sqlServerName
output databaseName string = resources.outputs.databaseName
output backendIdentityName string = resources.outputs.backendIdentityName
output migrateIdentityName string = resources.outputs.migrateIdentityName
output migrateJobName string = resources.outputs.migrateJobName
