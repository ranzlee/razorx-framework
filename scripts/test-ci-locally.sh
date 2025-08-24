#!/bin/bash
# Test CI build steps locally to ensure they work

set -e  # Exit on error
set -o pipefail  # Exit if any command in a pipeline fails

echo "Testing CI build steps locally..."
echo "================================"

# Navigate to project root
cd "$(dirname "$0")/.."

# Check prerequisites
echo "Checking prerequisites..."
if ! command -v node &> /dev/null; then
    echo "❌ Node.js is not installed"
    exit 1
fi

if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is not installed"
    exit 1
fi

echo "✅ Node.js version: $(node --version)"
echo "✅ .NET SDK version: $(dotnet --version)"
echo ""

# Clean previous builds
echo "Cleaning previous builds..."

# Check if we should clean node_modules (only if explicitly requested or they're corrupted)
CLEAN_NODE_MODULES=${CLEAN_NODE_MODULES:-false}

if [ "$CLEAN_NODE_MODULES" = "true" ]; then
    echo "Force cleaning node_modules (CLEAN_NODE_MODULES=true)..."
    
    # Function to safely remove node_modules
    safe_remove_node_modules() {
        local dir="$1"
        if [ -d "$dir" ]; then
            echo "Removing $dir..."
            # Try normal removal first
            rm -rf "$dir" 2>/dev/null || {
                # If that fails, try with find to remove deeply nested directories
                find "$dir" -type d -name ".git" -prune -o -type d -print0 2>/dev/null | xargs -0 chmod 755 2>/dev/null || true
                find "$dir" -type f -print0 2>/dev/null | xargs -0 chmod 644 2>/dev/null || true
                rm -rf "$dir" 2>/dev/null || {
                    # Last resort: use sudo (will prompt for password if needed)
                    echo "Need elevated permissions to remove $dir"
                    sudo rm -rf "$dir"
                }
            }
        fi
    }
    
    # Remove node_modules directories
    safe_remove_node_modules "RazorX.Framework/Node/node_modules"
    safe_remove_node_modules "RazorX.Framework.Tests/Node/node_modules"
    
    # Remove package-lock files to ensure fresh installs
    rm -f RazorX.Framework/Node/package-lock.json
    rm -f RazorX.Framework.Tests/Node/package-lock.json
else
    echo "Keeping existing node_modules (set CLEAN_NODE_MODULES=true to force clean)"
fi

# Remove other build artifacts
rm -rf RazorX.Framework/Node/dist
rm -rf artifacts/

# Clean .NET build
dotnet clean --configuration Release 2>/dev/null || true
echo ""

# Install RazorX.Framework npm dependencies
echo "Installing RazorX.Framework npm dependencies..."
cd RazorX.Framework/Node

# Check if node_modules exists and has content
if [ -d "node_modules" ] && [ "$(ls -A node_modules)" ]; then
    echo "node_modules exists, skipping npm install..."
else
    echo "Running npm install..."
    npm install || {
        echo "npm install failed, trying with cache clean..."
        npm cache clean --force
        npm install || {
            echo "❌ Failed to install npm dependencies"
            exit 1
        }
    }
fi

# Verify TypeScript is installed
TSC_PATH="./node_modules/.bin/tsc"
if [ ! -f "$TSC_PATH" ]; then
    echo "❌ TypeScript binary not found at $TSC_PATH"
    exit 1
fi

TSC_VERSION=$($TSC_PATH --version 2>&1)
if [ $? -ne 0 ]; then
    echo "❌ TypeScript not working properly: $TSC_VERSION"
    exit 1
fi
echo "✅ TypeScript installed: $TSC_VERSION"

cd ../..
echo ""

# Restore .NET dependencies
echo "Restoring .NET dependencies..."
dotnet restore
echo ""

# Build solution
echo "Building solution..."
dotnet build --configuration Release --no-restore
echo ""

# Install test npm dependencies
echo "Installing RazorX.Framework.Tests npm dependencies..."
cd RazorX.Framework.Tests/Node

# Check if node_modules exists and has content
if [ -d "node_modules" ] && [ "$(ls -A node_modules)" ]; then
    echo "node_modules exists, skipping npm install..."
else
    echo "Running npm install..."
    npm install || {
        echo "npm install failed, trying with cache clean..."
        npm cache clean --force
        npm install || {
            echo "❌ Failed to install test npm dependencies"
            exit 1
        }
    }
fi

cd ../..
echo ""

# Run .NET tests
echo "Running .NET tests..."
dotnet test --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory ./TestResults
echo ""

# Run JavaScript tests
echo "Running JavaScript tests..."
cd RazorX.Framework.Tests/Node
npm test
cd ../..
echo ""

# Pack NuGet package
echo "Packing NuGet package..."
dotnet pack RazorX.Framework/RazorX.Framework.csproj --configuration Release --no-build --output ./artifacts
echo ""

# Verify artifacts were created
echo "================================"
if [ -d "artifacts" ] && [ "$(ls -A artifacts/*.nupkg 2>/dev/null)" ]; then
    echo "✅ All CI steps completed successfully!"
    echo ""
    echo "Artifacts created:"
    ls -la artifacts/*.nupkg 2>/dev/null
    exit 0
else
    echo "❌ CI steps completed but no artifacts were created"
    echo "Check the build output above for any issues."
    exit 1
fi