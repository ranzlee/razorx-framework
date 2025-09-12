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
dotnet test RazorX.Framework.Tests --no-build

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