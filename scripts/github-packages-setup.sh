#!/bin/bash

# GitHub Packages Setup Script for RazorX.Framework
# This script helps configure GitHub Packages for both publishing and consuming

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
GITHUB_OWNER="ranzlee"
GITHUB_REPO="razorx-framework"
PACKAGE_SOURCE_NAME="github-razorx"
PACKAGE_SOURCE_URL="https://nuget.pkg.github.com/${GITHUB_OWNER}/index.json"

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_step() { echo -e "${CYAN}[STEP]${NC} $1"; }

# Display banner
show_banner() {
    echo -e "${CYAN}"
    echo "╔════════════════════════════════════════════════╗"
    echo "║     GitHub Packages Setup for RazorX.Framework ║"
    echo "╚════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

# Display usage
show_usage() {
    echo "Usage: $0 [command] [options]"
    echo ""
    echo "Commands:"
    echo "  setup-source    Configure NuGet source for GitHub Packages"
    echo "  test-auth       Test GitHub Packages authentication"
    echo "  list-packages   List available packages"
    echo "  install         Install RazorX.Framework from GitHub Packages"
    echo "  publish-local   Publish package from local build"
    echo "  clean-sources   Remove GitHub Packages sources"
    echo "  help            Show this help message"
    echo ""
    echo "Options:"
    echo "  --token TOKEN   GitHub Personal Access Token (PAT)"
    echo "  --version VER   Package version to install"
    echo ""
    echo "Examples:"
    echo "  $0 setup-source --token ghp_xxxxxxxxxxxx"
    echo "  $0 install --version 1.0.0-beta.1"
    echo "  $0 list-packages"
}

# Check prerequisites
check_prerequisites() {
    log_step "Checking prerequisites..."
    
    local missing_tools=()
    
    if ! command -v dotnet &> /dev/null; then
        missing_tools+=("dotnet")
    fi
    
    if ! command -v git &> /dev/null; then
        missing_tools+=("git")
    fi
    
    if ! command -v curl &> /dev/null; then
        missing_tools+=("curl")
    fi
    
    if [ ${#missing_tools[@]} -gt 0 ]; then
        log_error "Missing required tools: ${missing_tools[*]}"
        echo "Please install the missing tools and try again."
        exit 1
    fi
    
    log_success "All prerequisites met"
}

# Get or prompt for GitHub token
get_github_token() {
    local token=""
    
    # Check command line argument
    for i in "$@"; do
        case $i in
            --token=*)
                token="${i#*=}"
                shift
                ;;
            --token)
                shift
                token="$1"
                shift
                ;;
        esac
    done
    
    # Check environment variable
    if [[ -z "$token" && -n "$GITHUB_TOKEN" ]]; then
        token="$GITHUB_TOKEN"
        log_info "Using token from GITHUB_TOKEN environment variable"
    fi
    
    # Check gh CLI authentication
    if [[ -z "$token" ]] && command -v gh &> /dev/null; then
        if gh auth status &> /dev/null; then
            token=$(gh auth token)
            log_info "Using token from GitHub CLI"
        fi
    fi
    
    # Prompt if still no token
    if [[ -z "$token" ]]; then
        echo ""
        log_warning "No GitHub token found. You need a Personal Access Token (PAT) with 'read:packages' scope."
        echo "Create one at: https://github.com/settings/tokens/new"
        echo ""
        read -s -p "Enter your GitHub PAT: " token
        echo ""
    fi
    
    if [[ -z "$token" ]]; then
        log_error "GitHub token is required"
        exit 1
    fi
    
    echo "$token"
}

# Setup NuGet source for GitHub Packages
setup_nuget_source() {
    log_step "Setting up NuGet source for GitHub Packages..."
    
    local token
    token=$(get_github_token "$@")
    
    # Check if source already exists
    if dotnet nuget list source | grep -q "$PACKAGE_SOURCE_NAME"; then
        log_warning "Source '$PACKAGE_SOURCE_NAME' already exists. Removing..."
        dotnet nuget remove source "$PACKAGE_SOURCE_NAME" || true
    fi
    
    # Get GitHub username
    local github_user
    if command -v gh &> /dev/null && gh auth status &> /dev/null; then
        github_user=$(gh api user --jq .login)
    else
        read -p "Enter your GitHub username: " github_user
    fi
    
    # Add the source
    log_info "Adding NuGet source: $PACKAGE_SOURCE_NAME"
    dotnet nuget add source "$PACKAGE_SOURCE_URL" \
        --name "$PACKAGE_SOURCE_NAME" \
        --username "$github_user" \
        --password "$token" \
        --store-password-in-clear-text
    
    if [[ $? -eq 0 ]]; then
        log_success "NuGet source configured successfully!"
        echo ""
        echo "Source Name: $PACKAGE_SOURCE_NAME"
        echo "Source URL:  $PACKAGE_SOURCE_URL"
        echo ""
        
        # Create NuGet.config if it doesn't exist
        create_nuget_config "$github_user" "$token"
    else
        log_error "Failed to configure NuGet source"
        exit 1
    fi
}

# Create NuGet.config file
create_nuget_config() {
    local username=$1
    local token=$2
    
    log_step "Creating NuGet.config file..."
    
    cat > NuGet.config << EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github" value="https://nuget.pkg.github.com/${GITHUB_OWNER}/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="${username}" />
      <add key="ClearTextPassword" value="${token}" />
    </github>
  </packageSourceCredentials>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="github">
      <package pattern="RazorX.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF
    
    log_success "NuGet.config created"
    log_warning "Note: This file contains your token. Add it to .gitignore!"
    
    # Add to .gitignore if not already there
    if [[ -f .gitignore ]] && ! grep -q "NuGet.config" .gitignore; then
        echo "NuGet.config" >> .gitignore
        log_info "Added NuGet.config to .gitignore"
    fi
}

# Test GitHub Packages authentication
test_authentication() {
    log_step "Testing GitHub Packages authentication..."
    
    local token
    token=$(get_github_token "$@")
    
    # Test API access
    log_info "Testing GitHub API access..."
    local response
    response=$(curl -s -H "Authorization: token $token" \
        "https://api.github.com/user/packages?package_type=nuget" \
        -w "\n%{http_code}")
    
    local http_code
    http_code=$(echo "$response" | tail -n 1)
    
    if [[ "$http_code" == "200" ]]; then
        log_success "GitHub API authentication successful!"
        
        # Parse and display packages
        local packages
        packages=$(echo "$response" | head -n -1 | jq -r '.[] | .name' 2>/dev/null || echo "")
        
        if [[ -n "$packages" ]]; then
            echo "Your accessible packages:"
            echo "$packages" | while read -r pkg; do
                echo "  - $pkg"
            done
        else
            echo "No packages found (this is normal if you haven't published any yet)"
        fi
    elif [[ "$http_code" == "401" ]]; then
        log_error "Authentication failed. Check your token."
        echo "Ensure your PAT has 'read:packages' scope."
        exit 1
    else
        log_error "Unexpected response: HTTP $http_code"
        exit 1
    fi
    
    # Test NuGet source
    log_info "Testing NuGet source access..."
    if dotnet nuget list source | grep -q "$PACKAGE_SOURCE_NAME"; then
        log_success "NuGet source is configured"
    else
        log_warning "NuGet source not configured. Run: $0 setup-source"
    fi
}

# List available packages
list_packages() {
    log_step "Listing available RazorX.Framework packages..."
    
    # Use GitHub API to get package versions
    local token
    token=$(get_github_token "$@")
    
    log_info "Fetching package information from GitHub..."
    
    local response
    response=$(curl -s -H "Authorization: token $token" \
        -H "Accept: application/vnd.github.v3+json" \
        "https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/packages/nuget/RazorX.Framework/versions")
    
    if echo "$response" | jq -e '.message' &> /dev/null; then
        local message
        message=$(echo "$response" | jq -r '.message')
        log_error "API Error: $message"
        exit 1
    fi
    
    local count
    count=$(echo "$response" | jq '. | length')
    
    if [[ "$count" -eq 0 ]]; then
        log_warning "No packages found in GitHub Packages"
        echo "This is normal if no packages have been published yet."
    else
        log_success "Found $count package version(s):"
        echo ""
        echo "$response" | jq -r '.[] | "  - v\(.name) (Published: \(.created_at | split("T")[0]))"'
        echo ""
        
        # Get latest version
        local latest
        latest=$(echo "$response" | jq -r '.[0].name')
        echo "Latest version: $latest"
    fi
}

# Install package
install_package() {
    log_step "Installing RazorX.Framework from GitHub Packages..."
    
    local version=""
    for i in "$@"; do
        case $i in
            --version=*)
                version="${i#*=}"
                shift
                ;;
            --version)
                shift
                version="$1"
                shift
                ;;
        esac
    done
    
    # Check if we're in a .NET project
    if [[ ! -f *.csproj ]]; then
        log_error "No .csproj file found in current directory"
        echo "Please run this command from a .NET project directory"
        exit 1
    fi
    
    # Build install command
    local install_cmd="dotnet add package RazorX.Framework"
    
    if [[ -n "$version" ]]; then
        install_cmd="$install_cmd --version $version"
    fi
    
    install_cmd="$install_cmd --source $PACKAGE_SOURCE_NAME"
    
    log_info "Running: $install_cmd"
    
    if $install_cmd; then
        log_success "Package installed successfully!"
        
        # Check if client files were copied
        if [[ -f "wwwroot/js/razorx.js" ]]; then
            log_success "Client files copied to wwwroot/js/"
        else
            log_info "Client files will be copied on build"
        fi
    else
        log_error "Package installation failed"
        echo "Troubleshooting tips:"
        echo "1. Ensure NuGet source is configured: $0 setup-source"
        echo "2. Check available versions: $0 list-packages"
        echo "3. Verify your GitHub token has 'read:packages' scope"
        exit 1
    fi
}

# Publish local package
publish_local() {
    log_step "Publishing local package to GitHub Packages..."
    
    local token
    token=$(get_github_token "$@")
    
    # Find package file
    local package_file
    package_file=$(find . -name "RazorX.Framework.*.nupkg" | grep -v ".symbols." | head -1)
    
    if [[ ! -f "$package_file" ]]; then
        log_error "No package file found. Build the package first:"
        echo "  dotnet pack RazorX.Framework/RazorX.Framework.csproj -c Release"
        exit 1
    fi
    
    log_info "Found package: $(basename "$package_file")"
    
    # Publish package
    log_info "Publishing to GitHub Packages..."
    
    if dotnet nuget push "$package_file" \
        --api-key "$token" \
        --source "$PACKAGE_SOURCE_URL" \
        --skip-duplicate; then
        
        log_success "Package published successfully!"
        echo "View your package at:"
        echo "https://github.com/${GITHUB_OWNER}/${GITHUB_REPO}/packages"
    else
        log_error "Failed to publish package"
        exit 1
    fi
}

# Clean NuGet sources
clean_sources() {
    log_step "Cleaning GitHub Packages NuGet sources..."
    
    # Remove configured source
    if dotnet nuget list source | grep -q "$PACKAGE_SOURCE_NAME"; then
        log_info "Removing source: $PACKAGE_SOURCE_NAME"
        dotnet nuget remove source "$PACKAGE_SOURCE_NAME"
        log_success "Source removed"
    else
        log_info "Source not found: $PACKAGE_SOURCE_NAME"
    fi
    
    # Remove NuGet.config if it exists
    if [[ -f "NuGet.config" ]]; then
        log_warning "Found NuGet.config file"
        read -p "Remove NuGet.config? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            rm NuGet.config
            log_success "NuGet.config removed"
        fi
    fi
    
    log_success "Cleanup complete"
}

# Main execution
main() {
    show_banner
    
    local command=${1:-help}
    shift || true
    
    case $command in
        setup-source|setup)
            check_prerequisites
            setup_nuget_source "$@"
            ;;
        test-auth|test)
            check_prerequisites
            test_authentication "$@"
            ;;
        list-packages|list)
            check_prerequisites
            list_packages "$@"
            ;;
        install)
            check_prerequisites
            install_package "$@"
            ;;
        publish-local|publish)
            check_prerequisites
            publish_local "$@"
            ;;
        clean-sources|clean)
            clean_sources
            ;;
        help|--help|-h)
            show_usage
            ;;
        *)
            log_error "Unknown command: $command"
            show_usage
            exit 1
            ;;
    esac
}

# Run main function
main "$@"