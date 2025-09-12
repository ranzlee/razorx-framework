#!/bin/bash

# Test script for Dependabot dev-first configuration
# This script helps verify the Dependabot configuration is working correctly

set -euo pipefail

echo "🔍 Dependabot Configuration Test Script"
echo "========================================"
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI (gh) is not installed."
    echo "   Install it from: https://cli.github.com/"
    exit 1
fi

echo "✅ GitHub CLI is installed"
echo ""

# Check authentication
if ! gh auth status &> /dev/null; then
    echo "❌ Not authenticated with GitHub CLI"
    echo "   Run: gh auth login"
    exit 1
fi

echo "✅ Authenticated with GitHub"
echo ""

# Check current branch
current_branch=$(git branch --show-current || echo "unknown")
echo "📍 Current branch: $current_branch"
echo ""

# Check if there are uncommitted changes
if [[ -n $(git status -s) ]]; then
    echo "⚠️  You have uncommitted changes:"
    git status -s
    echo ""
    read -p "Do you want to continue anyway? (y/N) " -n 1 -r
    echo ""
    if [[ ! "$REPLY" =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# List current Dependabot PRs
echo "📋 Current Dependabot PRs:"
echo "--------------------------"
gh pr list --label dependencies --json number,title,headRefName,baseRefName,state --template '{{range .}}#{{.number}}: {{.title}}
  Branch: {{.headRefName}} → {{.baseRefName}}
  State: {{.state}}
{{end}}'

if [ $? -ne 0 ]; then
    echo "No Dependabot PRs found"
fi
echo ""

# Check Dependabot configuration
echo "🔧 Dependabot Configuration Summary:"
echo "------------------------------------"
echo "All package ecosystems configured to target: dev branch"
echo ""
grep -A 1 "target-branch" .github/dependabot.yml | grep -v "^--$" || echo "❌ No target-branch found!"
echo ""

# Check CI/CD triggers
echo "🚀 CI/CD Workflow Triggers:"
echo "---------------------------"
echo "Pull request branches:"
grep -A 1 "pull_request:" .github/workflows/ci-cd.yml | grep "branches:" | sed 's/.*branches: /  /'
echo ""

# Provide testing options
echo "📝 Testing Options:"
echo "-------------------"
echo "1. Wait for next scheduled Dependabot run (Mondays at 08:00 UTC)"
echo "2. Manually check for updates via GitHub Settings > Security > Dependabot"
echo "3. Create a test PR to dev branch to verify CI/CD triggers"
echo ""

echo "🎯 Next Steps:"
echo "--------------"
echo "1. Commit these changes to your dev branch:"
echo "   git add .github/dependabot.yml .github/workflows/ci-cd.yml"
echo "   git commit -m 'feat: configure Dependabot to target dev branch'"
echo "   git push origin dev"
echo ""
echo "2. Monitor for Dependabot PRs targeting the dev branch"
echo "3. Verify CI/CD pipeline runs on those PRs"
echo ""

echo "✨ Configuration test complete!"