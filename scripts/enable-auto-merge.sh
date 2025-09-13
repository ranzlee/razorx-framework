#!/bin/bash
# Enable Auto-merge for RazorX.Framework Repository
# ==================================================
# 
# This script enables auto-merge functionality and helps
# resolve current Dependabot PR issues.

set -e

echo "🚀 RazorX.Framework Auto-merge Configuration Script"
echo "=================================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if gh CLI is available
if ! command -v gh &> /dev/null; then
    echo -e "${RED}❌ GitHub CLI (gh) is not installed${NC}"
    echo "Please install it from: https://cli.github.com/"
    exit 1
fi

# Check if we're in the right repository
REPO_NAME=$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || echo "")
if [[ "$REPO_NAME" != "ranzlee/razorx-framework" ]]; then
    echo -e "${RED}❌ Not in razorx-framework repository${NC}"
    echo "Current repo: $REPO_NAME"
    exit 1
fi

echo "📍 Repository: $REPO_NAME"
echo ""

# Step 1: Enable auto-merge at repository level
echo "1️⃣ Enabling auto-merge for repository..."
gh api -X PATCH repos/ranzlee/razorx-framework \
  --field allow_auto_merge=true \
  --field allow_squash_merge=true \
  --field allow_merge_commit=true \
  --field allow_rebase_merge=true \
  --silent

if [[ $? -eq 0 ]]; then
    echo -e "${GREEN}✅ Auto-merge enabled successfully${NC}"
else
    echo -e "${RED}❌ Failed to enable auto-merge${NC}"
    exit 1
fi

echo ""

# Step 2: Check current Dependabot PRs
echo "2️⃣ Checking current Dependabot PRs..."
PR_COUNT=$(gh pr list --author "app/dependabot" --state open --json number | jq '. | length')
echo "Found $PR_COUNT open Dependabot PRs"

if [[ $PR_COUNT -gt 0 ]]; then
    echo ""
    echo "Current Dependabot PRs:"
    gh pr list --author "app/dependabot" --state open --json number,title,statusCheckRollup \
      --jq '.[] | "  PR #\(.number): \(.title) - \(if .statusCheckRollup | all(.conclusion == "SUCCESS") then "✅ All checks passing" else "⚠️ Some checks failing" end)"'
fi

echo ""

# Step 3: Offer to fix ESLint issue
echo "3️⃣ ESLint Configuration Issue"
echo "PR #57 is failing due to typescript-eslint upgrade."
echo ""
read -p "Would you like to fix the ESLint configuration? (y/n) " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "Fixing ESLint configuration..."
    
    # Check out the PR branch
    gh pr checkout 57 2>/dev/null || {
        echo -e "${YELLOW}⚠️ Could not checkout PR #57. It may be closed or merged.${NC}"
    }
    
    # Create a fix (pin to previous working version)
    cat > RazorX.Framework/Node/package.json.fix << 'EOF'
{
  "devDependencies": {
    "typescript-eslint": "8.42.0"
  }
}
EOF
    
    echo -e "${GREEN}✅ Fix created. Please review and apply manually if needed.${NC}"
    echo "To apply: Update typescript-eslint to 8.42.0 in package.json"
fi

echo ""

# Step 4: Enable auto-merge for existing PRs
echo "4️⃣ Enable auto-merge for existing Dependabot PRs"
read -p "Would you like to enable auto-merge for all passing Dependabot PRs? (y/n) " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    # Get all open Dependabot PRs
    PR_NUMBERS=$(gh pr list --author "app/dependabot" --state open --json number -q '.[].number')
    
    for PR in $PR_NUMBERS; do
        echo "Processing PR #$PR..."
        
        # Check if all checks are passing
        CHECKS_PASSING=$(gh pr view $PR --json statusCheckRollup \
          --jq '.statusCheckRollup | all(.conclusion == "SUCCESS" or .conclusion == "NEUTRAL")')
        
        if [[ "$CHECKS_PASSING" == "true" ]]; then
            # Approve the PR
            gh pr review $PR --approve 2>/dev/null || {
                echo "  Already approved or cannot approve"
            }
            
            # Enable auto-merge
            gh pr merge $PR --auto --merge 2>/dev/null || {
                echo "  Auto-merge already enabled or not available"
            }
            
            echo -e "  ${GREEN}✅ PR #$PR: Auto-merge enabled${NC}"
        else
            echo -e "  ${YELLOW}⚠️ PR #$PR: Checks not passing, skipping${NC}"
        fi
    done
fi

echo ""

# Step 5: Optional - Enable dev branch alpha publishing
echo "5️⃣ Dev Branch Alpha Publishing"
echo "Currently, dev branch pushes don't publish alpha packages."
read -p "Would you like to enable alpha package publishing from dev branch? (y/n) " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}To enable dev branch publishing:${NC}"
    echo "1. Edit .github/workflows/ci-cd.yml"
    echo "2. Change line 124 from:"
    echo "   SHOULD_PUBLISH=false"
    echo "3. To:"
    echo "   SHOULD_PUBLISH=true"
    echo ""
    echo "This will publish alpha packages for testing purposes."
fi

echo ""

# Step 6: Summary
echo "📊 Configuration Summary"
echo "========================"
echo ""

# Check final status
AUTO_MERGE_ENABLED=$(gh api repos/ranzlee/razorx-framework --jq '.allow_auto_merge')
if [[ "$AUTO_MERGE_ENABLED" == "true" ]]; then
    echo -e "${GREEN}✅ Repository auto-merge: ENABLED${NC}"
else
    echo -e "${RED}❌ Repository auto-merge: DISABLED${NC}"
fi

# Check if auto-merge workflow exists
if [[ -f ".github/workflows/auto-merge.yml" ]]; then
    echo -e "${GREEN}✅ Auto-merge workflow: PRESENT${NC}"
else
    echo -e "${YELLOW}⚠️ Auto-merge workflow: NOT FOUND${NC}"
    echo "   Please ensure auto-merge.yml is committed to the repository"
fi

# Count remaining open PRs
OPEN_PRS=$(gh pr list --author "app/dependabot" --state open --json number | jq '. | length')
echo ""
echo "📈 Dependabot PRs remaining: $OPEN_PRS"

echo ""
echo "🎉 Configuration complete!"
echo ""
echo "Next steps:"
echo "1. Commit and push the auto-merge.yml workflow to dev branch"
echo "2. Fix any failing Dependabot PRs"
echo "3. Monitor auto-merge behavior for one week"
echo "4. Adjust settings as needed"
echo ""
echo "The auto-merge workflow will:"
echo "- Automatically approve patch and minor updates"
echo "- Enable auto-merge (merges when checks pass)"
echo "- Require manual review for major updates"
echo "- Add appropriate labels to PRs"