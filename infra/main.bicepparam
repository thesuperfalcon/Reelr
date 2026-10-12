using 'main.bicep'

// The database administrator is whoever deploys. The values come from the environment so they are not in the
// repository; infra/deploy.ps1 sets them from the signed-in Azure CLI user.
param adminObjectId = readEnvironmentVariable('REELR_ADMIN_OBJECT_ID')
param adminLogin = readEnvironmentVariable('REELR_ADMIN_LOGIN', 'Reelr admin')

param imageTag = readEnvironmentVariable('REELR_IMAGE_TAG', '')
param imageRepository = readEnvironmentVariable('REELR_IMAGE_REPOSITORY', 'ghcr.io/thesuperfalcon')
param deployApps = bool(readEnvironmentVariable('REELR_DEPLOY_APPS', 'true'))

param emailUserName = readEnvironmentVariable('REELR_EMAIL_USER', '')
param emailFromAddress = readEnvironmentVariable('REELR_EMAIL_FROM', '')
