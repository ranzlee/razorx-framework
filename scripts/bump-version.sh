#!/bin/bash

# Version Bump Script for RazorX.Framework
# Usage: ./scripts/bump-version.sh [major|minor|patch|prerelease] [prerelease-label]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$PROJECT_DIR/RazorX.Framework/RazorX.Framework.csproj"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Display usage
show_usage() {
    echo "Usage: $0 [major|minor|patch|prerelease] [prerelease-label]"
    echo ""
    echo "Examples:"
    echo "  $0 patch                    # 1.0.0 -> 1.0.1"
    echo "  $0 minor                    # 1.0.0 -> 1.1.0"
    echo "  $0 major                    # 1.0.0 -> 2.0.0"
    echo "  $0 prerelease alpha         # 1.0.0 -> 1.0.1-alpha"
    echo "  $0 prerelease beta          # 1.0.0-alpha -> 1.0.0-beta"
    echo ""
    echo "Current version: $(get_current_version)"
}

# Get current version from project file
get_current_version() {
    if [[ -f "$PROJECT_FILE" ]]; then
        grep -oP '<PackageVersion>\K[^<]+' "$PROJECT_FILE" | head -1 || true
    else
        log_error "Project file not found: $PROJECT_FILE"
        exit 1
    fi
}

# Parse semantic version
parse_version() {
    local version=$1
    local regex='^([0-9]+)\.([0-9]+)\.([0-9]+)(-([a-zA-Z0-9.-]+))?$'
    
    if [[ "$version" =~ $regex ]]; then
        MAJOR="${BASH_REMATCH[1]}"
        MINOR="${BASH_REMATCH[2]}"
        PATCH="${BASH_REMATCH[3]}"
        PRERELEASE="${BASH_REMATCH[5]}"
    else
        log_error "Invalid version format: $version"
        log_info "Expected format: MAJOR.MINOR.PATCH[-PRERELEASE]"
        exit 1
    fi
}

# Increment version
increment_version() {
    local increment_type=$1
    local prerelease_label=${2:-alpha}
    
    case "$increment_type" in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            PRERELEASE=""
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            PRERELEASE=""
            ;;
        patch)
            PATCH=$((PATCH + 1))
            PRERELEASE=""
            ;;
        prerelease)
            if [[ -z "$PRERELEASE" ]]; then
                PATCH=$((PATCH + 1))
                PRERELEASE="$prerelease_label"
            else
                PRERELEASE="$prerelease_label"
            fi
            ;;
        *)
            log_error "Invalid increment type: $increment_type"
            show_usage
            exit 1
            ;;
    esac
}

# Build new version string
build_version_string() {
    if [[ -n "$PRERELEASE" ]]; then
        NEW_VERSION="$MAJOR.$MINOR.$PATCH-$PRERELEASE"
    else
        NEW_VERSION="$MAJOR.$MINOR.$PATCH"
    fi
}

# Update version in project file
update_project_file() {
    local old_version=$1
    local new_version=$2
    
    log_info "Updating project file: $PROJECT_FILE"
    
    # Create backup
    cp "$PROJECT_FILE" "$PROJECT_FILE.backup"
    
    # Update version
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' "s/<PackageVersion>$old_version<\/PackageVersion>/<PackageVersion>$new_version<\/PackageVersion>/" "$PROJECT_FILE"
    else
        # Linux
        sed -i "s/<PackageVersion>$old_version<\/PackageVersion>/<PackageVersion>$new_version<\/PackageVersion>/" "$PROJECT_FILE"
    fi
    
    # Verify update
    local updated_version
    updated_version=$(get_current_version)
    
    if [[ "$updated_version" == "$new_version" ]]; then
        log_success "Version updated successfully: $old_version -> $new_version"
        rm "$PROJECT_FILE.backup"
    else
        log_error "Version update failed. Restoring backup."
        mv "$PROJECT_FILE.backup" "$PROJECT_FILE"
        exit 1
    fi
}

# Update changelog
update_changelog() {
    local new_version=$1
    local changelog_file="$PROJECT_DIR/CHANGELOG.md"
    
    if [[ ! -f "$changelog_file" ]]; then
        log_warning "CHANGELOG.md not found. Skipping changelog update."
        return 0
    fi
    
    log_info "Updating CHANGELOG.md..."
    
    local date
    date=$(date +%Y-%m-%d)
    
    # Create new changelog entry
    local temp_file
    temp_file=$(mktemp)
    
    # Read existing changelog and insert new version
    awk -v version="$new_version" -v date="$date" '
        /^## \[Unreleased\]/ {
            print $0
            print ""
            print "### Added"
            print "### Changed" 
            print "### Fixed"
            print ""
            print "## [" version "] - " date
            next
        }
        { print }
    ' "$changelog_file" > "$temp_file"
    
    mv "$temp_file" "$changelog_file"
    log_success "CHANGELOG.md updated with version $new_version"
}

# Create git tag
create_git_tag() {
    local version=$1
    
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        log_warning "Not in a git repository. Skipping tag creation."
        return 0
    fi
    
    # Check if there are uncommitted changes
    if ! git diff-index --quiet HEAD --; then
        log_warning "There are uncommitted changes. Please commit them first."
        log_info "You can create the tag manually later with: git tag v$version"
        return 0
    fi
    
    local tag_name="v$version"
    
    # Check if tag already exists
    if git tag -l | grep -q "^$tag_name$"; then
        log_warning "Tag $tag_name already exists. Skipping tag creation."
        return 0
    fi
    
    # Create annotated tag
    log_info "Creating git tag: $tag_name"
    git tag -a "$tag_name" -m "Release version $version"
    
    log_success "Git tag created: $tag_name"
    log_info "Push the tag with: git push origin $tag_name"
}

# Validate environment
validate_environment() {
    # Check if project file exists
    if [[ ! -f "$PROJECT_FILE" ]]; then
        log_error "Project file not found: $PROJECT_FILE"
        exit 1
    fi
    
    # Check if we can get current version
    local current_version
    current_version=$(get_current_version)
    
    if [[ -z "$current_version" ]]; then
        log_error "Could not determine current version from project file"
        exit 1
    fi
    
    log_info "Current version: $current_version"
}

# Main execution
main() {
    local increment_type=${1:-}
    local prerelease_label=${2:-alpha}
    
    # Show usage if no arguments
    if [[ -z "$increment_type" ]]; then
        show_usage
        exit 0
    fi
    
    # Validate environment
    validate_environment
    
    # Get and parse current version
    local current_version
    current_version=$(get_current_version)
    parse_version "$current_version"
    
    log_info "Current version components:"
    log_info "  Major: $MAJOR"
    log_info "  Minor: $MINOR" 
    log_info "  Patch: $PATCH"
    log_info "  Prerelease: ${PRERELEASE:-none}"
    
    # Calculate new version
    increment_version "$increment_type" "$prerelease_label"
    build_version_string
    
    log_info "New version will be: $NEW_VERSION"
    
    # Confirm with user
    read -p "Proceed with version bump? (y/N): " -n 1 -r
    echo
    if [[ ! "$REPLY" =~ ^[Yy]$ ]]; then
        log_info "Version bump cancelled."
        exit 0
    fi
    
    # Perform updates
    update_project_file "$current_version" "$NEW_VERSION"
    update_changelog "$NEW_VERSION"
    
    # Only create tag for release versions (not pre-release)
    if [[ -z "$PRERELEASE" ]]; then
        create_git_tag "$NEW_VERSION"
    else
        log_info "Skipping tag creation for pre-release version"
    fi
    
    log_success "Version bump completed! 🎉"
    log_info "Version: $current_version -> $NEW_VERSION"
    log_info ""
    log_info "Next steps:"
    log_info "1. Review the changes"
    log_info "2. Commit the version bump: git add . && git commit -m 'Bump version to $NEW_VERSION'"
    if [[ -z "$PRERELEASE" ]]; then
        log_info "3. Push the tag: git push origin v$NEW_VERSION"
        log_info "4. The GitHub Actions workflow will automatically publish the package"
    else
        log_info "3. Push to trigger pre-release build: git push origin main"
    fi
}

# Run main function
main "$@"