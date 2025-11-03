#!/bin/bash
set -euo pipefail

echo "Running all RazorX.Framework tests..."
echo ""

# Navigate to project root
cd "$(dirname "$0")/.."

# Build the project
echo "========================================"
echo "Building RazorX.Framework"
echo "========================================"
dotnet build

echo ""
echo "========================================"
echo "Running .NET Tests (MSTest)"
echo "========================================"
echo "Testing RazorX.Framework.Tests..."
dotnet test RazorX.Framework.Tests --no-build

echo ""
echo "Testing RazorX.Framework.OpenTelemetry.Tests..."
dotnet test RazorX.Framework.OpenTelemetry.Tests --no-build

echo ""
echo "Testing RazorX.Framework.Redis.Tests..."
dotnet test RazorX.Framework.Redis.Tests --no-build

echo ""
echo "Testing RazorX.Framework.Azure.Tests..."
dotnet test RazorX.Framework.Azure.Tests --no-build

echo ""
echo "========================================"
echo "Running JavaScript Tests (Vitest)"
echo "========================================"

# Run JavaScript tests
cd RazorX.Framework.Tests/Node
npm run test:coverage

echo ""
echo "========================================"
echo "All Tests Complete!"
echo "========================================"