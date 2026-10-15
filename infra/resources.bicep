// Everything inside the resource group. See main.bicep for the parameters.
param location string
param tags object
param adminObjectId string
param adminLogin string
param imageTag string
param imageRepository string
param backendMinReplicas int
param frontendMinReplicas int
param emailHost string
param emailUserName string
param emailFromAddress string
param adminUserIds array
param demoUserIds array
param inviteOnly bool
param alertEmail string
param customDomain string
param deployApps bool

// Short, stable suffix for names that must be unique across Azure (SQL server, Key Vault).
var suffix = take(uniqueString(resourceGroup().id), 8)
var databaseName = 'reelr'

// Until CI has pushed real images, a public sample image keeps the apps deployable. It listens on port 80.
var usePlaceholder = empty(imageTag)
var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var backendImage = usePlaceholder ? placeholderImage : '${imageRepository}/reelr-backend:${imageTag}'
var migrateImage = usePlaceholder ? placeholderImage : '${imageRepository}/reelr-migrate:${imageTag}'
var frontendImage = usePlaceholder ? placeholderImage : '${imageRepository}/reelr-frontend:${imageTag}'
var appPort = usePlaceholder ? 80 : 8080

// Built-in role ids.
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

// ---------- Identities ----------
// The backend reads and writes data; the migration job may also change the schema. Neither has a password:
// both sign in to SQL and Key Vault as managed identities.

resource backendIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-reelr-backend'
  location: location
  tags: tags
}

resource migrateIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-reelr-migrate'
  location: location
  tags: tags
}

// ---------- Key Vault ----------
// Holds jwt-key, tmdb-token and email-password. They are set with infra/deploy.ps1, never in this template.

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-reelr-${suffix}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
  }
}

resource backendReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, backendIdentity.id, keyVaultSecretsUser)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
    principalId: backendIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource adminWritesSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, adminObjectId, keyVaultSecretsOfficer)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficer)
    principalId: adminObjectId
  }
}

// ---------- Azure SQL ----------
// Entra sign-in only: no SQL passwords exist. The database uses the free offer and pauses when idle;
// if the monthly free amount runs out it pauses until the next month instead of billing.

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-reelr-${suffix}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
    }
  }
}

// The apps have no fixed outbound address, so connections from Azure services are allowed.
// Every sign-in still needs an Entra identity that has a user in the database.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ---------- Container Apps ----------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-reelr'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.2')
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-reelr'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// The site's own domain, e.g. reelr.se, with a free certificate Azure renews itself. Its DNS needs
// a CNAME to the app's default address and a TXT record asuid.<name> with the environment's verification id.
// The default address keeps working next to it.
resource domainCertificate 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (!empty(customDomain)) {
  parent: environment
  name: 'mc-reelr-domain'
  location: location
  tags: tags
  properties: {
    subjectName: customDomain
    domainControlValidation: 'CNAME'
  }
}

var siteUrl = empty(customDomain) ? 'https://reelr.${environment.properties.defaultDomain}' : 'https://${customDomain}'

// Connection strings carry no secret: the identity's client id says which managed identity signs in.
// The long timeout covers the database waking up from a pause.
var sqlConnection = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${databaseName};Encrypt=True;Connect Timeout=90;Authentication=Active Directory Managed Identity'

var healthProbes = usePlaceholder ? [] : [
  {
    type: 'Startup'
    httpGet: {
      path: '/health/live'
      port: appPort
    }
    periodSeconds: 3
    failureThreshold: 20
  }
  {
    // Not /health/ready: that one queries the database and TMDB, and would keep the database from pausing.
    type: 'Liveness'
    httpGet: {
      path: '/health/live'
      port: appPort
    }
    periodSeconds: 30
  }
]

var adminSettings = [for (id, i) in adminUserIds: {
  name: 'Admin__UserIds__${i}'
  value: string(id)
}]

var demoSettings = [for (id, i) in demoUserIds: {
  name: 'Demo__UserIds__${i}'
  value: string(id)
}]

resource backend 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: 'backend'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${backendIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      // Reachable only from inside the environment, i.e. through nginx in the frontend app.
      ingress: {
        external: false
        targetPort: appPort
        transport: 'http'
        allowInsecure: true
      }
      secrets: [
        {
          name: 'jwt-key'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/jwt-key'
          identity: backendIdentity.id
        }
        {
          name: 'tmdb-token'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/tmdb-token'
          identity: backendIdentity.id
        }
        {
          name: 'email-password'
          keyVaultUrl: '${vault.properties.vaultUri}secrets/email-password'
          identity: backendIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'backend'
          image: backendImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: healthProbes
          env: concat([
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__ReelrContext', value: '${sqlConnection};User Id=${backendIdentity.properties.clientId}' }
            { name: 'Jwt__Key', secretRef: 'jwt-key' }
            { name: 'TMDB_READ_ACCESS_TOKEN', secretRef: 'tmdb-token' }
            { name: 'Email__Host', value: emailHost }
            { name: 'Email__UserName', value: emailUserName }
            { name: 'Email__Password', secretRef: 'email-password' }
            { name: 'Email__FromAddress', value: emailFromAddress }
            { name: 'App__BaseUrl', value: siteUrl }
            // Requests pass the platform's proxy, nginx and the platform's proxy again, all on private addresses.
            { name: 'ForwardedHeaders__ForwardLimit', value: '3' }
            { name: 'ForwardedHeaders__KnownNetworks__0', value: '10.0.0.0/8' }
            { name: 'ForwardedHeaders__KnownNetworks__1', value: '172.16.0.0/12' }
            { name: 'ForwardedHeaders__KnownNetworks__2', value: '192.168.0.0/16' }
            { name: 'ForwardedHeaders__KnownNetworks__3', value: '100.64.0.0/10' }
            { name: 'Registration__InviteOnly', value: string(inviteOnly) }
          ], adminSettings, demoSettings)
        }
      ]
      // One replica at most: caches, rate limits and the film refresh job live in the process.
      scale: {
        minReplicas: backendMinReplicas
        maxReplicas: 1
      }
    }
  }
  dependsOn: [
    backendReadsSecrets
  ]
}

resource frontend 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: 'reelr'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: appPort
        transport: 'http'
        allowInsecure: false
        customDomains: empty(customDomain) ? [] : [
          {
            name: customDomain
            certificateId: domainCertificate.id
            bindingType: 'SniEnabled'
          }
        ]
      }
    }
    template: {
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            // The backend app's internal address; its ingress listens on port 80.
            { name: 'BACKEND_URL', value: 'http://backend' }
          ]
        }
      ]
      scale: {
        minReplicas: frontendMinReplicas
        maxReplicas: 1
      }
    }
  }
}

// Brings the schema up to date. CI starts it before rolling out a new backend image.
resource migrateJob 'Microsoft.App/jobs@2024-03-01' = if (deployApps) {
  name: 'reelr-migrate'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrateIdentity.id}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 600
      replicaRetryLimit: 1
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: migrateImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ConnectionStrings__ReelrContext', value: '${sqlConnection};User Id=${migrateIdentity.properties.clientId}' }
          ]
        }
      ]
    }
  }
}

// ---------- Alerts ----------
// An email when the backend answers with server errors or keeps restarting (failed liveness checks).
// Off without an address. Each alert costs about 0.10 USD a month.

var alertsOn = deployApps && !empty(alertEmail)

resource alertEmails 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsOn) {
  name: 'ag-reelr'
  location: 'Global'
  tags: tags
  properties: {
    groupShortName: 'reelr'
    enabled: true
    emailReceivers: [
      {
        name: 'owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

resource serverErrorAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsOn) {
  name: 'reelr-backend-5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'The backend answered with more than 5 server errors (5xx) in 15 minutes.'
    severity: 2
    enabled: true
    scopes: [
      backend.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ServerErrors'
          metricNamespace: 'Microsoft.App/containerApps'
          metricName: 'Requests'
          dimensions: [
            {
              name: 'statusCodeCategory'
              operator: 'Include'
              values: [
                '5xx'
              ]
            }
          ]
          operator: 'GreaterThan'
          threshold: 5
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [
      {
        actionGroupId: alertEmails.id
      }
    ]
  }
}

// The liveness probe restarts a backend that stops answering /health/live.
resource restartAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsOn) {
  name: 'reelr-backend-restarts'
  location: 'global'
  tags: tags
  properties: {
    description: 'The backend restarted more than twice in 30 minutes, e.g. because its health check failed.'
    severity: 2
    enabled: true
    scopes: [
      backend.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT30M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'Restarts'
          metricNamespace: 'Microsoft.App/containerApps'
          metricName: 'RestartCount'
          operator: 'GreaterThan'
          threshold: 2
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [
      {
        actionGroupId: alertEmails.id
      }
    ]
  }
}

// When the free amount runs out the database pauses, and the site is down until the 1st of the next month.
resource freeAmountAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (alertsOn) {
  name: 'reelr-sql-free-amount-low'
  location: 'global'
  tags: tags
  properties: {
    description: 'Less than 20 % of the database\'s monthly free amount (20,000 of 100,000 vCore seconds) is left.'
    severity: 2
    enabled: true
    scopes: [
      database.id
    ]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'FreeAmountRemaining'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'free_amount_remaining'
          operator: 'LessThan'
          threshold: 20000
          timeAggregation: 'Minimum'
        }
      ]
    }
    actions: [
      {
        actionGroupId: alertEmails.id
      }
    ]
  }
}

output siteUrl string = siteUrl
output keyVaultName string = vault.name
output sqlServerName string = sqlServer.name
output databaseName string = databaseName
output backendIdentityName string = backendIdentity.name
output migrateIdentityName string = migrateIdentity.name
output migrateJobName string = 'reelr-migrate'
