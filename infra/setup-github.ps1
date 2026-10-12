<#
.SYNOPSIS
    Lets the Deploy workflow sign in to Azure without stored keys (OIDC). Run once; safe to run again.

.DESCRIPTION
    Creates the Entra app "reelr-github-deploy", trusts tokens GitHub issues to this repository's "production"
    environment, and gives it what the deploy needs:
      - Contributor on the subscription (the template creates the resource group)
      - Role Based Access Control Administrator on rg-reelr (the template assigns the Key Vault roles)
    Prints the repository variables to add on GitHub. None of them is a secret.
#>
param(
    [string] $Subscription = 'reelr',
    [string] $Repository = 'thesuperfalcon/Reelr',
    [string] $EmailUserName = '',
    [string] $EmailFromAddress = ''
)

$ErrorActionPreference = 'Stop'
$env:AZURE_CORE_ONLY_SHOW_ERRORS = 'true'
$name = 'reelr-github-deploy'

az account set --subscription $Subscription
$account = az account show --query '{sub:id, tenant:tenantId}' -o json | ConvertFrom-Json
$me = az ad signed-in-user show --query '{id:id, upn:userPrincipalName}' -o json | ConvertFrom-Json

$appId = az ad app list --display-name $name --query '[0].appId' -o tsv
if (-not $appId) {
    $appId = az ad app create --display-name $name --query appId -o tsv
    Write-Host "Created app $name"
}

$principalId = az ad sp list --filter "appId eq '$appId'" --query '[0].id' -o tsv
if (-not $principalId) {
    $principalId = az ad sp create --id $appId --query id -o tsv
}

$subject = "repo:${Repository}:environment:production"
$existing = az ad app federated-credential list --id $appId --query "[?subject=='$subject'] | length(@)" -o tsv
if ($existing -ne '1') {
    $credential = @{
        name      = 'github-production'
        issuer    = 'https://token.actions.githubusercontent.com'
        subject   = $subject
        audiences = @('api://AzureADTokenExchange')
    } | ConvertTo-Json
    $file = New-TemporaryFile
    try {
        [System.IO.File]::WriteAllText($file, $credential)
        az ad app federated-credential create --id $appId --parameters "@$file" --output none
    }
    finally {
        Remove-Item $file -Force
    }
    Write-Host "Trusted $subject"
}

$subscriptionScope = "/subscriptions/$($account.sub)"
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal `
    --role Contributor --scope $subscriptionScope --output none
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal `
    --role 'Role Based Access Control Administrator' --scope "$subscriptionScope/resourceGroups/rg-reelr" --output none

Write-Host ''
Write-Host 'Add these on GitHub: Settings > Secrets and variables > Actions > Variables > New repository variable'
$variables = [ordered]@{
    AZURE_CLIENT_ID       = $appId
    AZURE_TENANT_ID       = $account.tenant
    AZURE_SUBSCRIPTION_ID = $account.sub
    REELR_ADMIN_OBJECT_ID = $me.id
    REELR_ADMIN_LOGIN     = $me.upn
    REELR_EMAIL_USER      = $EmailUserName
    REELR_EMAIL_FROM      = $EmailFromAddress
}
$variables.GetEnumerator() | ForEach-Object { '  {0,-22} {1}' -f $_.Key, $_.Value }
