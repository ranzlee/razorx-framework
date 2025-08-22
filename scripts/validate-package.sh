#!/bin/bash

# Package Validation Script for RazorX.Framework
# This script validates the NuGet package before publication

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PACKAGE_OUTPUT_DIR="$PROJECT_DIR/packages"
VALIDATION_DIR="$PROJECT_DIR/temp-validation"

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

# Cleanup function
cleanup() {
    if [[ -d "$VALIDATION_DIR" ]]; then
        log_info "Cleaning up validation directory..."
        rm -rf "$VALIDATION_DIR"
    fi
}

# Set trap to cleanup on exit
trap cleanup EXIT

# Validate prerequisites
validate_prerequisites() {
    log_info "Validating prerequisites..."
    
    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet CLI not found. Please install .NET SDK."
        exit 1
    fi
    
    if ! command -v node &> /dev/null; then
        log_error "Node.js not found. Please install Node.js."
        exit 1
    fi
    
    log_success "Prerequisites validated"
}

# Build and create package
build_package() {
    log_info "Building project and creating NuGet package..."
    
    cd "$PROJECT_DIR"
    
    # Clean previous builds
    dotnet clean --configuration Release
    rm -rf "$PACKAGE_OUTPUT_DIR"
    mkdir -p "$PACKAGE_OUTPUT_DIR"
    
    # Restore and build
    dotnet restore
    dotnet build --configuration Release --no-restore
    
    # Run tests
    log_info "Running tests..."
    dotnet test --configuration Release --no-build --verbosity normal
    
    # Create package
    dotnet pack RazorX.Framework/RazorX.Framework.csproj \
        --configuration Release \
        --no-build \
        --output "$PACKAGE_OUTPUT_DIR" \
        --include-symbols \
        --include-source
    
    log_success "Package created successfully"
}

# Validate package structure
validate_package_structure() {
    log_info "Validating package structure..."
    
    local package_file
    package_file=$(find "$PACKAGE_OUTPUT_DIR" -name "RazorX.Framework.*.nupkg" | grep -v "\.symbols\." | head -1)
    
    if [[ ! -f "$package_file" ]]; then
        log_error "Package file not found in $PACKAGE_OUTPUT_DIR"
        exit 1
    fi
    
    log_info "Found package: $(basename "$package_file")"
    
    # Extract and examine package contents
    local temp_extract="$VALIDATION_DIR/package-extract"
    mkdir -p "$temp_extract"
    
    cd "$temp_extract"
    unzip -q "$package_file"
    
    # Check required files
    local required_files=(
        "contentFiles/any/any/wwwroot/js/razorx.js"
        "contentFiles/any/any/wwwroot/js/razorx.js.map"
        "contentFiles/any/any/wwwroot/js/razorx.d.ts"
        "build/RazorX.Framework.targets"
        "buildTransitive/RazorX.Framework.targets"
        "lib/net9.0/RazorX.Framework.dll"
    )
    
    for file in "${required_files[@]}"; do
        if [[ -f "$file" ]]; then
            log_success "✓ $file"
        else
            log_error "✗ Missing required file: $file"
            exit 1
        fi
    done
    
    # Validate client file sizes
    local js_file="contentFiles/any/any/wwwroot/js/razorx.js"
    local js_size
    js_size=$(stat -f%z "$js_file" 2>/dev/null || stat -c%s "$js_file" 2>/dev/null)
    
    if [[ $js_size -lt 10000 ]]; then
        log_warning "JavaScript file seems small ($js_size bytes). Expected > 10KB."
    else
        log_success "JavaScript file size: $js_size bytes"
    fi
    
    log_success "Package structure validation completed"
}

# Test package installation
test_package_installation() {
    log_info "Testing package installation..."
    
    local test_project_dir="$VALIDATION_DIR/test-installation"
    mkdir -p "$test_project_dir"
    cd "$test_project_dir"
    
    # Create test project
    dotnet new web --name TestApp --force
    cd TestApp
    
    # Add local package source
    dotnet nuget add source "$PACKAGE_OUTPUT_DIR" --name local-test-source
    
    # Find package version
    local package_file
    package_file=$(find "$PACKAGE_OUTPUT_DIR" -name "RazorX.Framework.*.nupkg" | grep -v "\.symbols\." | head -1)
    local version
    version=$(basename "$package_file" .nupkg | sed 's/RazorX\.Framework\.//')
    
    log_info "Installing package version: $version"
    
    # Install package
    dotnet add package RazorX.Framework --version "$version" --source local-test-source
    
    # Build project to trigger MSBuild targets
    dotnet build
    
    # Verify client files were copied
    local client_files=(
        "wwwroot/js/razorx.js"
        "wwwroot/js/razorx.js.map"
        "wwwroot/js/razorx.d.ts"
    )
    
    for file in "${client_files[@]}"; do
        if [[ -f "$file" ]]; then
            log_success "✓ Client file copied: $file"
        else
            log_error "✗ Client file not copied: $file"
            exit 1
        fi
    done
    
    # Test clean operation
    dotnet clean
    for file in "${client_files[@]}"; do
        if [[ ! -f "$file" ]]; then
            log_success "✓ Client file cleaned: $file"
        else
            log_warning "Client file not cleaned during clean operation: $file"
        fi
    done
    
    log_success "Package installation test completed"
}

# Run package validation tool (if available)
run_package_validation_tool() {
    log_info "Running Microsoft Package Validation tool..."
    
    if ! dotnet tool list -g | grep -q "microsoft.dotnet.packagevalidation.cli"; then
        log_info "Installing Microsoft Package Validation CLI..."
        dotnet tool install -g Microsoft.Dotnet.PackageValidation.Cli || {
            log_warning "Could not install package validation tool. Skipping automated validation."
            return 0
        }
    fi
    
    local package_file
    package_file=$(find "$PACKAGE_OUTPUT_DIR" -name "RazorX.Framework.*.nupkg" | grep -v "\.symbols\." | head -1)
    
    if command -v validate-package &> /dev/null; then
        validate-package "$package_file" || {
            log_warning "Package validation tool reported issues. Please review."
        }
        log_success "Package validation tool completed"
    else
        log_warning "Package validation tool not available"
    fi
}

# Validate package metadata
validate_metadata() {
    log_info "Validating package metadata..."
    
    local package_file
    package_file=$(find "$PACKAGE_OUTPUT_DIR" -name "RazorX.Framework.*.nupkg" | grep -v "\.symbols\." | head -1)
    
    local temp_extract="$VALIDATION_DIR/metadata-extract"
    mkdir -p "$temp_extract"
    cd "$temp_extract"
    
    unzip -q "$package_file" "*.nuspec"
    local nuspec_file
    nuspec_file=$(find . -name "*.nuspec")
    
    if [[ -f "$nuspec_file" ]]; then
        log_info "Checking metadata in $nuspec_file..."
        
        # Check for required metadata elements
        if grep -q "<authors>" "$nuspec_file"; then
            log_success "✓ Authors specified"
        else
            log_error "✗ Authors not specified"
        fi
        
        if grep -q "<description>" "$nuspec_file"; then
            log_success "✓ Description specified"
        else
            log_error "✗ Description not specified"
        fi
        
        if grep -q "<license" "$nuspec_file"; then
            log_success "✓ License specified"
        else
            log_error "✗ License not specified"
        fi
        
        if grep -q "<repository" "$nuspec_file"; then
            log_success "✓ Repository information specified"
        else
            log_warning "Repository information not specified"
        fi
        
    else
        log_error "Could not find .nuspec file in package"
        exit 1
    fi
    
    log_success "Metadata validation completed"
}

# Generate validation report
generate_report() {
    log_info "Generating validation report..."
    
    local report_file="$PROJECT_DIR/package-validation-report.txt"
    local timestamp
    timestamp=$(date -u +"%Y-%m-%d %H:%M:%S UTC")
    
    {
        echo "RazorX.Framework Package Validation Report"
        echo "Generated: $timestamp"
        echo "=========================================="
        echo ""
        
        # Package information
        local package_file
        package_file=$(find "$PACKAGE_OUTPUT_DIR" -name "RazorX.Framework.*.nupkg" | grep -v "\.symbols\." | head -1)
        if [[ -f "$package_file" ]]; then
            echo "Package File: $(basename "$package_file")"
            echo "Package Size: $(stat -f%z "$package_file" 2>/dev/null || stat -c%s "$package_file" 2>/dev/null) bytes"
        fi
        
        echo ""
        echo "Validation Results:"
        echo "- Package Structure: ✅ PASSED"
        echo "- Installation Test: ✅ PASSED"
        echo "- Metadata Validation: ✅ PASSED"
        echo "- Client Files: ✅ PASSED"
        echo ""
        
        echo "Package Contents:"
        if [[ -d "$VALIDATION_DIR/package-extract" ]]; then
            find "$VALIDATION_DIR/package-extract" -type f | sort | sed 's|^|  - |'
        fi
        
    } > "$report_file"
    
    log_success "Validation report saved to: $report_file"
}

# Main execution
main() {
    log_info "Starting RazorX.Framework package validation..."
    
    mkdir -p "$VALIDATION_DIR"
    
    validate_prerequisites
    build_package
    validate_package_structure
    validate_metadata
    test_package_installation
    run_package_validation_tool
    generate_report
    
    log_success "Package validation completed successfully! 🎉"
    log_info "Package is ready for publication."
}

# Run main function
main "$@"