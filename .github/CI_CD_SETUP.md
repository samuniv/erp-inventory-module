# CI/CD Setup Guide

This guide provides step-by-step instructions for setting up the complete CI/CD pipeline for the ERP Inventory Module.

## Quick Start Checklist

- [ ] 🔧 Configure Repository Settings
- [ ] 🛡️ Set Up Branch Protection Rules
- [ ] 🔐 Configure Repository Secrets
- [ ] 🧪 Validate Workflow Configuration
- [ ] 📊 Set Up Monitoring and Alerts
- [ ] 🚀 Test Complete Pipeline

## Prerequisites

### Required Tools

- **GitHub CLI**: [Install from here](https://cli.github.com/)
- **Git**: Latest version
- **Repository Admin Access**: Required for branch protection setup

### Repository Requirements

- GitHub repository with admin permissions
- Main and develop branches created
- All workflow files committed to repository

## Step 1: Repository Settings Configuration

### 1.1 General Settings

Navigate to **Settings** → **General**:

- **Features**:

  - ✅ Wikis (for documentation)
  - ✅ Issues (for bug tracking)
  - ✅ Projects (for project management)
  - ✅ Discussions (for team communication)

- **Pull Requests**:
  - ✅ Allow merge commits
  - ✅ Allow squash merging
  - ✅ Allow rebase merging
  - ✅ Always suggest updating pull request branches
  - ✅ Allow auto-merge
  - ✅ Automatically delete head branches

### 1.2 Actions Settings

Navigate to **Settings** → **Actions** → **General**:

- **Actions permissions**: ✅ Allow all actions and reusable workflows
- **Artifact and log retention**: 90 days (or as per organization policy)
- **Fork pull request workflows**: ✅ Require approval for first-time contributors

### 1.3 Code Security and Analysis

Navigate to **Settings** → **Code security and analysis**:

- **Dependency graph**: ✅ Enabled
- **Dependabot alerts**: ✅ Enabled
- **Dependabot security updates**: ✅ Enabled
- **Dependabot version updates**: ✅ Enabled (configure with `.github/dependabot.yml`)
- **Code scanning**: ✅ Set up CodeQL analysis
- **Secret scanning**: ✅ Enabled

## Step 2: Repository Secrets Configuration

### 2.1 Required Secrets

Navigate to **Settings** → **Secrets and variables** → **Actions**:

#### SonarCloud Integration (Optional but Recommended)

```
SONAR_TOKEN=your_sonarcloud_token
```

#### Container Registry (GitHub Packages - Automatic)

```
GITHUB_TOKEN=automatically_provided
```

#### Notification Integration (Optional)

```
SLACK_WEBHOOK=your_slack_webhook_url
TEAMS_WEBHOOK=your_teams_webhook_url
```

#### Code Coverage (Optional)

```
CODECOV_TOKEN=your_codecov_token
```

### 2.2 Environment Variables

Create environment variables for different deployment stages:

**Development Environment**:

```
DB_CONNECTION_STRING=development_database_connection
REDIS_CONNECTION_STRING=development_redis_connection
KAFKA_BOOTSTRAP_SERVERS=development_kafka_servers
```

## Step 3: Branch Protection Rules Setup

### 3.1 Automated Setup (Recommended)

#### For Linux/macOS:

```bash
# Ensure you're in the repository directory
cd /path/to/erp-inventory-module

# Make script executable
chmod +x scripts/setup-branch-protection.sh

# Run the setup script
./scripts/setup-branch-protection.sh
```

#### For Windows (PowerShell):

```powershell
# Ensure you're in the repository directory
cd C:\path\to\erp-inventory-module

# Run the setup script
.\scripts\setup-branch-protection.ps1
```

### 3.2 Manual Setup

If you prefer manual setup, follow the detailed instructions in [BRANCH_PROTECTION.md](./.github/BRANCH_PROTECTION.md).

### 3.3 Verification

After setup, verify branch protection rules:

1. Go to **Settings** → **Branches**
2. Confirm rules exist for `main` and `develop` branches
3. Test by creating a test PR (should require status checks)

## Step 4: Workflow Validation

### 4.1 Validate Configuration

Run the CI/CD validation workflow:

1. Go to **Actions** → **CI/CD Validation**
2. Click **Run workflow**
3. Select "all" for validation type
4. Click **Run workflow**

### 4.2 Check Workflow Syntax

Locally validate workflow files:

```bash
# Install act (GitHub Actions local runner) - optional
curl https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash

# Validate workflow syntax
python -c "import yaml; [yaml.safe_load(open(f)) for f in ['.github/workflows/angular.yml', '.github/workflows/dotnet.yml', '.github/workflows/integration-tests.yml']]"
```

## Step 5: Test Complete Pipeline

### 5.1 Create Test Pull Request

1. **Create Feature Branch**:

   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b test/ci-cd-setup
   ```

2. **Make Small Test Change**:

   ```bash
   # Make a minor change to trigger CI
   echo "# CI/CD Test" >> README.md
   git add README.md
   git commit -m "test: trigger CI/CD pipeline"
   git push origin test/ci-cd-setup
   ```

3. **Create Pull Request**:
   - Target: `develop` branch
   - Watch for workflow triggers
   - Verify all status checks run

### 5.2 Verify Workflow Execution

Monitor the following workflows:

1. **Angular CI**: Should trigger and complete successfully
2. **.NET Backend CI**: Should trigger and complete successfully
3. **Integration Tests**: Should trigger (if configured for develop branch)

### 5.3 Check Status Checks

In the PR, verify:

- ✅ All required status checks appear
- ✅ Status checks must pass before merge
- ✅ Branch protection rules prevent merge if checks fail

## Step 6: Monitoring and Alerts Setup

### 6.1 Slack Integration (Optional)

1. **Create Slack App**: Follow [Slack API documentation](https://api.slack.com/start)
2. **Get Webhook URL**: Create incoming webhook
3. **Add to Repository Secrets**: Store as `SLACK_WEBHOOK`
4. **Test Integration**: Add notification step to workflows

### 6.2 Email Notifications

Configure in **Settings** → **Notifications**:

- ✅ Email notifications for workflow failures
- ✅ Email notifications for security alerts

### 6.3 GitHub Dashboard

Set up monitoring dashboard:

1. Use GitHub Insights to track workflow success rates
2. Monitor build times and performance trends
3. Set up alerts for repeated failures

## Step 7: Advanced Configuration

### 7.1 Dependabot Configuration

Create `.github/dependabot.yml`:

```yaml
version: 2
updates:
  # Enable version updates for npm (Angular)
  - package-ecosystem: "npm"
    directory: "/erp-inventory-angular"
    schedule:
      interval: "weekly"
      day: "sunday"
      time: "04:00"
    open-pull-requests-limit: 10

  # Enable version updates for NuGet (.NET)
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "sunday"
      time: "04:00"
    open-pull-requests-limit: 10

  # Enable version updates for GitHub Actions
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
      day: "sunday"
      time: "04:00"
    open-pull-requests-limit: 5
```

### 7.2 CodeQL Configuration

Create `.github/codeql/codeql-config.yml`:

```yaml
name: "CodeQL Config"

disable-default-queries: false

queries:
  - uses: security-and-quality

paths-ignore:
  - "**/*.generated.cs"
  - "**/Migrations/**"
  - "**/bin/**"
  - "**/obj/**"
  - "erp-inventory-angular/node_modules/**"
  - "erp-inventory-angular/dist/**"

paths:
  - "src/**"
  - "erp-inventory-angular/src/**"
```

### 7.3 Issue Templates

Create `.github/ISSUE_TEMPLATE/`:

**Bug Report** (`.github/ISSUE_TEMPLATE/bug_report.yml`):

```yaml
name: Bug Report
description: File a bug report
title: "[Bug]: "
labels: ["bug", "triage"]
assignees: []
body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to fill out this bug report!
  - type: input
    id: contact
    attributes:
      label: Contact Details
      description: How can we get in touch with you if we need more info?
      placeholder: ex. email@example.com
    validations:
      required: false
  - type: textarea
    id: what-happened
    attributes:
      label: What happened?
      description: Also tell us, what did you expect to happen?
      placeholder: Tell us what you see!
      value: "A bug happened!"
    validations:
      required: true
```

### 7.4 Pull Request Template

Create `.github/pull_request_template.md`:

```markdown
## Description

Brief description of changes made.

## Type of Change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Performance improvement
- [ ] Code refactoring

## Testing

- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed
- [ ] Added new tests for this change

## Checklist

- [ ] Code follows the style guidelines
- [ ] Self-review completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Documentation updated
- [ ] No new warnings introduced
- [ ] Tests added/updated

## Screenshots (if applicable)

Add screenshots to help explain your changes.

## Additional Notes

Any additional information or context about the PR.
```

## Step 8: Performance Optimization

### 8.1 Workflow Optimization

Monitor and optimize workflow performance:

1. **Enable Caching**: Ensure all workflows use appropriate caching
2. **Parallel Execution**: Use matrix strategies where possible
3. **Path Filtering**: Only run workflows when relevant files change
4. **Resource Sizing**: Use appropriate runner sizes for workloads

### 8.2 Build Time Monitoring

Track build performance metrics:

```bash
# Add to workflow for performance monitoring
- name: Report build time
  run: |
    echo "Build completed at: $(date)"
    echo "Total duration: ${{ job.duration }}"
```

### 8.3 Resource Usage Analysis

Regularly analyze:

- Average build times per workflow
- Success/failure rates
- Resource usage patterns
- Queue wait times

## Troubleshooting

### Common Issues

#### Workflow Doesn't Trigger

**Problem**: Workflows not running on PR or push
**Solutions**:

1. Check path filters match changed files
2. Verify branch names match trigger configuration
3. Ensure repository has Actions enabled

#### Status Checks Not Required

**Problem**: PRs can be merged without status checks
**Solutions**:

1. Verify branch protection rules are configured
2. Check status check names match workflow job names
3. Ensure user has proper permissions

#### Integration Tests Fail

**Problem**: Testcontainers tests fail in CI
**Solutions**:

1. Check Docker daemon is available
2. Verify environment variables are set
3. Review timeout settings
4. Check container resource limits

#### Build Performance Issues

**Problem**: Workflows taking too long
**Solutions**:

1. Enable more aggressive caching
2. Use matrix strategies for parallelization
3. Optimize Docker builds with multi-stage builds
4. Consider using larger runners

### Getting Help

1. **GitHub Docs**: [GitHub Actions Documentation](https://docs.github.com/en/actions)
2. **Community Forum**: [GitHub Community](https://github.community/)
3. **Issues**: Create issue in this repository for project-specific help
4. **Team Chat**: Use configured Slack/Teams channels

## Maintenance

### Regular Tasks

#### Weekly

- [ ] Review workflow success rates
- [ ] Update any failing or flaky tests
- [ ] Check for security alerts and updates

#### Monthly

- [ ] Review and update dependencies
- [ ] Analyze build performance metrics
- [ ] Update documentation as needed

#### Quarterly

- [ ] Review branch protection rules
- [ ] Update workflow configurations
- [ ] Evaluate new GitHub Actions features
- [ ] Security audit of CI/CD pipeline

### Updates and Changes

When making changes to CI/CD:

1. **Test in Feature Branch**: Always test changes before merging
2. **Update Documentation**: Keep this guide and related docs current
3. **Communicate Changes**: Notify team of new requirements or processes
4. **Monitor Impact**: Watch for issues after deploying changes

## Best Practices Summary

### Workflow Design

- Use path-based filtering to avoid unnecessary runs
- Implement comprehensive caching strategies
- Set appropriate timeouts for all jobs
- Use matrix strategies for parallel execution
- Keep workflows focused and modular

### Security

- Use minimal required permissions
- Store sensitive data in repository secrets
- Regularly update dependencies and scan for vulnerabilities
- Enable branch protection with required status checks
- Use signed commits for critical branches

### Performance

- Monitor build times and success rates
- Optimize Docker builds with layer caching
- Use appropriate runner sizes for workloads
- Implement efficient test strategies
- Regular performance reviews and optimizations

### Collaboration

- Clear PR and issue templates
- Comprehensive status checks
- Required code reviews
- Automated dependency updates
- Good documentation and communication
