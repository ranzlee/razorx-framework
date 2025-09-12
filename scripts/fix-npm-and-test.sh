#!/bin/bash
# Fix npm issues and run CI tests locally

set -euo pipefail  # Exit on error, undefined variables, and pipe failures

echo "Fixing npm issues and testing CI build steps..."
echo "============================================="

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

# Complete cleanup
echo "Performing complete cleanup..."
echo "Removing all node_modules directories..."
find . -name "node_modules" -type d -prune -exec rm -rf {} + 2>/dev/null || true

echo "Removing all package-lock.json files..."
find . -name "package-lock.json" -type f -delete 2>/dev/null || true

echo "Cleaning npm cache..."
npm cache clean --force 2>/dev/null || true

echo "Cleaning .NET build artifacts..."
dotnet clean --configuration Release 2>/dev/null || true
rm -rf artifacts/
echo ""

# Install RazorX.Framework npm dependencies
echo "Installing RazorX.Framework npm dependencies..."
cd RazorX.Framework/Node

echo "Running fresh npm install..."
npm install --verbose || {
    echo "❌ Failed to install RazorX.Framework npm dependencies"
    echo "Try running: npm config set registry https://registry.npmjs.org/"
    exit 1
}

# Build TypeScript
echo "Building TypeScript..."
npm run build || {
    echo "❌ Failed to build TypeScript"
    exit 1
}

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

echo "Running fresh npm install..."
npm install --verbose || {
    echo "❌ Failed to install test npm dependencies"
    exit 1
}

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
echo "============================================="
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