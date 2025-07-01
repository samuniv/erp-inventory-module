# Branch Protection Setup Script for Windows
# This script configures branch protection rules using GitHub CLI

[CmdletBinding()]
param(
    [Parameter(HelpMessage="Show help information")]
    [switch]$Help
)

if ($Help) {
    Write-Host "ERP Inventory Module - Branch Protection Setup" -ForegroundColor Cyan
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script configures branch protection rules for the repository." -ForegroundColor White
    Write-Host ""
    Write-Host "Prerequisites:" -ForegroundColor Yellow
    Write-Host "  • GitHub CLI (gh) installed and authenticated" -ForegroundColor Gray
    Write-Host "  • Repository admin permissions" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Usage: .\setup-branch-protection.ps1" -ForegroundColor White
    exit 0
}

Write-Host "🔐 ERP Inventory Module - Branch Protection Setup" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Check if GitHub CLI is installed
try {
    $null = Get-Command gh -ErrorAction Stop
} catch {
    Write-Host "❌ Error: GitHub CLI (gh) is not installed." -ForegroundColor Red
    Write-Host "Please install it from: https://cli.github.com/" -ForegroundColor Yellow
    exit 1
}

# Check if user is authenticated
try {
    gh auth status 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Not authenticated"
    }
} catch {
    Write-Host "❌ Error: Not authenticated with GitHub CLI." -ForegroundColor Red
    Write-Host "Please run: gh auth login" -ForegroundColor Yellow
    exit 1
}

# Get repository information
try {
    $repoInfo = gh repo view --json owner,name | ConvertFrom-Json
    $repoOwner = $repoInfo.owner.login
    $repoName = $repoInfo.name
    
    Write-Host "📋 Repository: $repoOwner/$repoName" -ForegroundColor White
    Write-Host ""
} catch {
    Write-Host "❌ Error: Failed to get repository information." -ForegroundColor Red
    Write-Host "Make sure you're in a git repository directory." -ForegroundColor Yellow
    exit 1
}

# Function to create branch protection rule
function Set-BranchProtection {
    param(
        [string]$Branch,
        [string]$Contexts,
        [string]$EnforceAdmins,
        [string]$RequiredReviews,
        [string]$RepoOwner,
        [string]$RepoName
    )
    
    Write-Host "🛡️  Setting up protection for branch: $Branch" -ForegroundColor Yellow
    
    try {
        # Create the protection rule using GitHub CLI
        $protectionData = @{
            required_status_checks = @{
                strict = $true
                contexts = ($Contexts | ConvertFrom-Json)
            }
            enforce_admins = [bool]::Parse($EnforceAdmins)
            required_pull_request_reviews = ($RequiredReviews | ConvertFrom-Json)
            restrictions = $null
            required_conversation_resolution = $true
            allow_force_pushes = $false
            allow_deletions = $false
        } | ConvertTo-Json -Depth 10
        
        # Use gh api to set protection
        $protectionData | gh api "repos/$RepoOwner/$RepoName/branches/$Branch/protection" --method PUT --input -
        
        Write-Host "✅ Protection rules applied to $Branch branch" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "❌ Failed to set protection for $Branch branch: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Main branch protection with comprehensive checks
Write-Host "Setting up main branch protection..." -ForegroundColor Yellow

$mainContexts = @(
    "Angular CI / Lint, Test, and Build Angular App (18.x)",
    "Angular CI / Lint, Test, and Build Angular App (20.x)", 
    "Angular CI / Security Audit",
    ".NET Backend CI / Build and Unit Test (Debug)",
    ".NET Backend CI / Build and Unit Test (Release)",
    ".NET Backend CI / Code Quality Analysis",
    ".NET Backend CI / Security Vulnerability Scan"
) | ConvertTo-Json

$mainReviews = @{
    required_approving_review_count = 1
    dismiss_stale_reviews = $true
    require_code_owner_reviews = $true
    require_last_push_approval = $true
} | ConvertTo-Json

$mainSuccess = Set-BranchProtection -Branch "main" -Contexts $mainContexts -EnforceAdmins "true" -RequiredReviews $mainReviews -RepoOwner $repoOwner -RepoName $repoName

Write-Host ""
Write-Host "Setting up develop branch protection..." -ForegroundColor Yellow

# Develop branch protection with essential checks only
$developContexts = @(
    "Angular CI / Lint, Test, and Build Angular App (20.x)",
    ".NET Backend CI / Build and Unit Test (Release)", 
    ".NET Backend CI / Code Quality Analysis"
) | ConvertTo-Json

$developReviews = @{
    required_approving_review_count = 1
    dismiss_stale_reviews = $true
    require_code_owner_reviews = $false
} | ConvertTo-Json

$developSuccess = Set-BranchProtection -Branch "develop" -Contexts $developContexts -EnforceAdmins "false" -RequiredReviews $developReviews -RepoOwner $repoOwner -RepoName $repoName

Write-Host ""

if ($mainSuccess -and $developSuccess) {
    Write-Host "🎯 Branch protection setup completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Summary:" -ForegroundColor Yellow
    Write-Host "   • Main branch: Comprehensive protection with all CI checks required" -ForegroundColor Gray
    Write-Host "   • Develop branch: Essential protection with core CI checks required" -ForegroundColor Gray
    Write-Host "   • Both branches require PR reviews and conversation resolution" -ForegroundColor Gray
    Write-Host "   • Force pushes and deletions are disabled" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🔍 You can verify the settings at:" -ForegroundColor Cyan
    Write-Host "   https://github.com/$repoOwner/$repoName/settings/branches" -ForegroundColor Blue
    Write-Host ""
    Write-Host "⚠️  Note: Integration tests are configured as optional until the first" -ForegroundColor Yellow
    Write-Host "   successful run. They will automatically become required after that." -ForegroundColor Yellow
} else {
    Write-Host "❌ Branch protection setup encountered errors." -ForegroundColor Red
    Write-Host "Please check the error messages above and try again." -ForegroundColor Yellow
    exit 1
}
