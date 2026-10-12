<#
.SYNOPSIS
    Sets up Reelr in Azure, or updates it. Safe to run again.

.DESCRIPTION
    1. Creates the base resources (identities, Key Vault, Azure SQL, Container Apps environment).
    2. Stores the secrets in Key Vault. The JWT key is generated; the TMDB token and the SMTP key are asked for
       with hidden input. Secrets that already exist are kept unless -ResetSecrets is given.
    3. Creates the database users for the backend and migration identities.
    4. Creates or updates the apps and the migration job, and runs the migrations when real images are used.

    Needs the Azure CLI (signed in with "az login") and the .NET 10 SDK.

.EXAMPLE
    ./infra/deploy.ps1 -EmailUserName 'xxxx@smtp-brevo.com' -EmailFromAddress 'reelr.beta@outlook.com'

.EXAMPLE
    ./infra/deploy.ps1 -EmailUserName 'xxxx@smtp-brevo.com' -EmailFromAddress 'reelr.beta@outlook.com' -ImageTag 3f2c1ab
#>
param(
    [string] $Subscription = 'reelr',
    [Parameter(Mandatory)] [string] $EmailUserName,
    [Parameter(Mandatory)] [string] $EmailFromAddress,
    # Empty runs placeholder images until CI has pushed the real ones.
    [string] $ImageTag = '',
    [switch] $ResetSecrets
)

$ErrorActionPreference = 'Stop'
$template = Join-Path $PSScriptRoot 'main.bicep'
$parameters = Join-Path $PSScriptRoot 'main.bicepparam'

function Invoke-Deployment([bool] $deployApps) {
    $env:REELR_DEPLOY_APPS = "$deployApps".ToLower()
    $result = az deployment sub create --name "reelr-$(Get-Date -Format yyyyMMddHHmmss)" --location swedencentral `
        --template-file $template --parameters $parameters --query properties.outputs -o json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Deployment failed.' }
    return $result
}

function Set-SecretFromText([string] $vault, [string] $name, [string] $value) {
    # Through a file, so the value never appears on a command line.
    $file = New-TemporaryFile
    try {
        [System.IO.File]::WriteAllText($file, $value)
        # A new role assignment can take a minute to apply.
        for ($try = 1; ; $try++) {
            az keyvault secret set --vault-name $vault --name $name --file $file --encoding utf-8 --output none 2>$null
            if ($LASTEXITCODE -eq 0) { break }
            if ($try -ge 10) { throw "Could not write secret $name to $vault." }
            Write-Host "  waiting for Key Vault access..."
            Start-Sleep -Seconds 15
        }
    }
    finally {
        Remove-Item $file -Force
    }
}

function Set-SecretIfMissing([string] $vault, [string] $name, [scriptblock] $getValue) {
    $exists = az keyvault secret list --vault-name $vault --query "[?name=='$name'] | length(@)" -o tsv 2>$null
    if ($exists -eq '1' -and -not $ResetSecrets) {
        Write-Host "  $name already set"
        return
    }
    Set-SecretFromText $vault $name (& $getValue)
    Write-Host "  $name set"
}

function Read-Hidden([string] $prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $value = [System.Net.NetworkCredential]::new('', $secure).Password
    if ([string]::IsNullOrWhiteSpace($value)) { throw "$prompt is required." }
    return $value
}

az account set --subscription $Subscription
$me = az ad signed-in-user show --query '{id:id, upn:userPrincipalName}' -o json | ConvertFrom-Json

$env:REELR_ADMIN_OBJECT_ID = $me.id
$env:REELR_ADMIN_LOGIN = $me.upn
$env:REELR_EMAIL_USER = $EmailUserName
$env:REELR_EMAIL_FROM = $EmailFromAddress
$env:REELR_IMAGE_TAG = $ImageTag

Write-Host '1/4 Base resources'
$out = Invoke-Deployment $false
$vault = $out.keyVaultName.value
$sqlServer = $out.sqlServerName.value

Write-Host '2/4 Secrets'
Set-SecretIfMissing $vault 'jwt-key' {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes)
}
Set-SecretIfMissing $vault 'tmdb-token' { Read-Hidden 'TMDB API Read Access Token' }
Set-SecretIfMissing $vault 'email-password' { Read-Hidden 'Brevo SMTP key' }

Write-Host '3/4 Database users'
# Lets this machine reach the server for a moment.
$myIp = (Invoke-RestMethod -Uri 'https://api.ipify.org').Trim()
az sql server firewall-rule create --resource-group rg-reelr --server $sqlServer --name deploy-client `
    --start-ip-address $myIp --end-ip-address $myIp --output none
try {
    dotnet run (Join-Path $PSScriptRoot 'sql-users.cs') -- "$sqlServer.database.windows.net" $out.databaseName.value `
        $out.backendIdentityName.value $out.migrateIdentityName.value
}
finally {
    az sql server firewall-rule delete --resource-group rg-reelr --server $sqlServer --name deploy-client --output none
}

Write-Host '4/4 Apps'
$out = Invoke-Deployment $true

if ($ImageTag) {
    Write-Host 'Running the migrations...'
    $execution = az containerapp job start --resource-group rg-reelr --name $out.migrateJobName.value --query name -o tsv
    do {
        Start-Sleep -Seconds 10
        $status = az containerapp job execution show --resource-group rg-reelr --name $out.migrateJobName.value `
            --job-execution-name $execution --query properties.status -o tsv
        Write-Host "  $status"
    } while ($status -in 'Running', 'Processing')
    if ($status -ne 'Succeeded') { throw "Migrations ended with status $status." }
}

Write-Host "Done: $($out.siteUrl.value)"
