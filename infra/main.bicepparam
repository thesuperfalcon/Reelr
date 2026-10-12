using 'main.bicep'

// The database administrator is whoever deploys. The values come from the environment so they are not in the
// repository; infra/deploy.ps1 sets them from the signed-in Azure CLI user.
param adminObjectId = readEnvironmentVariable('REELR_ADMIN_OBJECT_ID')
param adminLogin = readEnvironmentVariable('REELR_ADMIN_LOGIN', 'Reelr admin')

param imageTag = readEnvironmentVariable('REELR_IMAGE_TAG', '')
param imageRepository = readEnvironmentVariable('REELR_IMAGE_REPOSITORY', 'ghcr.io/thesuperfalcon')
param deployApps = bool(readEnvironmentVariable('REELR_DEPLOY_APPS', 'true'))

// Comma-separated user ids, e.g. "1" or "1,4". Empty for none.
param adminUserIds = map(filter(split(readEnvironmentVariable('REELR_ADMIN_USER_IDS', ''), ','), id => !empty(trim(id))), id => trim(id))
param demoUserIds = map(filter(split(readEnvironmentVariable('REELR_DEMO_USER_IDS', ''), ','), id => !empty(trim(id))), id => trim(id))
param inviteOnly = bool(readEnvironmentVariable('REELR_INVITE_ONLY', 'true'))
param alertEmail = readEnvironmentVariable('REELR_ALERT_EMAIL', '')
param customDomain = readEnvironmentVariable('REELR_CUSTOM_DOMAIN', '')

param emailUserName = readEnvironmentVariable('REELR_EMAIL_USER', '')
param emailFromAddress = readEnvironmentVariable('REELR_EMAIL_FROM', '')
