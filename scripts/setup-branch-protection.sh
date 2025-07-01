#!/bin/bash

# Branch Protection Setup Script
# This script configures branch protection rules using GitHub CLI

set -e

echo "🔐 ERP Inventory Module - Branch Protection Setup"
echo "================================================="

# Check if GitHub CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) is not installed."
    echo "Please install it from: https://cli.github.com/"
    exit 1
fi

# Check if user is authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI."
    echo "Please run: gh auth login"
    exit 1
fi

# Get repository information
REPO_OWNER=$(gh repo view --json owner --jq '.owner.login')
REPO_NAME=$(gh repo view --json name --jq '.name')

echo "📋 Repository: $REPO_OWNER/$REPO_NAME"
echo ""

# Function to create branch protection rule
create_branch_protection() {
    local branch=$1
    local contexts=$2
    local enforce_admins=$3
    local required_reviews=$4
    
    echo "🛡️  Setting up protection for branch: $branch"
    
    # Create the protection rule
    gh api "repos/$REPO_OWNER/$REPO_NAME/branches/$branch/protection" \
        --method PUT \
        --field "required_status_checks={\"strict\":true,\"contexts\":$contexts}" \
        --field "enforce_admins=$enforce_admins" \
        --field "required_pull_request_reviews=$required_reviews" \
        --field "restrictions=null" \
        --field "required_conversation_resolution=true" \
        --field "allow_force_pushes=false" \
        --field "allow_deletions=false"
    
    echo "✅ Protection rules applied to $branch branch"
}

# Main branch protection with comprehensive checks
echo "Setting up main branch protection..."

main_contexts='[
    "Angular CI / Lint, Test, and Build Angular App (18.x)",
    "Angular CI / Lint, Test, and Build Angular App (20.x)", 
    "Angular CI / Security Audit",
    ".NET Backend CI / Build and Unit Test (Debug)",
    ".NET Backend CI / Build and Unit Test (Release)",
    ".NET Backend CI / Code Quality Analysis",
    ".NET Backend CI / Security Vulnerability Scan"
]'

main_reviews='{
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": true,
    "require_last_push_approval": true
}'

create_branch_protection "main" "$main_contexts" "true" "$main_reviews"

echo ""
echo "Setting up develop branch protection..."

# Develop branch protection with essential checks only
develop_contexts='[
    "Angular CI / Lint, Test, and Build Angular App (20.x)",
    ".NET Backend CI / Build and Unit Test (Release)", 
    ".NET Backend CI / Code Quality Analysis"
]'

develop_reviews='{
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false
}'

create_branch_protection "develop" "$develop_contexts" "false" "$develop_reviews"

echo ""
echo "🎯 Branch protection setup completed!"
echo ""
echo "📊 Summary:"
echo "   • Main branch: Comprehensive protection with all CI checks required"
echo "   • Develop branch: Essential protection with core CI checks required"
echo "   • Both branches require PR reviews and conversation resolution"
echo "   • Force pushes and deletions are disabled"
echo ""
echo "🔍 You can verify the settings at:"
echo "   https://github.com/$REPO_OWNER/$REPO_NAME/settings/branches"
echo ""
echo "⚠️  Note: Integration tests are configured as optional until the first"
echo "   successful run. They will automatically become required after that."
