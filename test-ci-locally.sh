#!/bin/bash
# Test CI build steps locally to ensure they work

set -e  # Exit on error

echo "Testing CI build steps locally..."
echo "================================"

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
rm -rf RazorX.Framework/Node/node_modules
rm -rf RazorX.Framework.Tests/Node/node_modules
rm -rf RazorX.Framework/Node/dist
rm -rf artifacts/
dotnet clean --configuration Release 2>/dev/null || true
echo ""

# Install RazorX.Framework npm dependencies
echo "Installing RazorX.Framework npm dependencies..."
cd RazorX.Framework/Node
npm ci || npm install
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
npm ci || npm install
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

echo "================================"
echo "✅ All CI steps completed successfully!"
echo ""
echo "Artifacts created:"
ls -la artifacts/*.nupkg 2>/dev/null || echo "No NuGet packages found"