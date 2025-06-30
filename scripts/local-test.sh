#!/bin/bash

# Local Development Testing Script
# Mirrors the CI pipeline for local development

set -e  # Exit on any error

echo "🚀 Starting ERP Inventory Module Local Test Pipeline"
echo "=================================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Step 1: Clean previous builds
print_status "Cleaning previous builds..."
dotnet clean
rm -rf ./TestResults
rm -rf ./coverage

# Step 2: Restore dependencies
print_status "Restoring dependencies..."
dotnet restore

# Step 3: Build solution
print_status "Building solution..."
if dotnet build --no-restore --configuration Release; then
    print_success "Build completed successfully"
else
    print_error "Build failed"
    exit 1
fi

# Step 4: Run unit tests (excluding integration tests)
print_status "Running unit tests..."
if dotnet test --no-build --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults \
    --logger trx \
    --verbosity normal \
    --filter "Category!=Integration" \
    --settings tests.runsettings; then
    print_success "Unit tests passed"
else
    print_warning "Some unit tests failed"
fi

# Step 5: Check Docker availability
print_status "Checking Docker availability..."
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed or not in PATH"
    exit 1
fi

if ! docker info &> /dev/null; then
    print_error "Docker daemon is not running"
    exit 1
fi

print_success "Docker is available"

# Step 6: Run integration tests
print_status "Running integration tests..."
if dotnet test --no-build --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults \
    --logger trx \
    --verbosity normal \
    --filter "Category=Integration" \
    --settings tests.runsettings; then
    print_success "Integration tests passed"
else
    print_warning "Some integration tests failed"
fi

# Step 7: Run end-to-end tests
print_status "Running end-to-end tests..."
if dotnet test --no-build --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults \
    --logger trx \
    --verbosity normal \
    tests/EndToEnd.Integration.Tests/ \
    --settings tests.runsettings; then
    print_success "End-to-end tests passed"
else
    print_warning "Some end-to-end tests failed"
fi

# Step 8: Security scan
print_status "Running security audit..."
dotnet list package --vulnerable --include-transitive || print_warning "Security audit completed with warnings"

# Step 9: Check for outdated packages
print_status "Checking for outdated packages..."
dotnet list package --outdated --include-transitive || print_warning "Some packages are outdated"

# Step 10: Generate test report
print_status "Generating test report..."
if [ -d "./TestResults" ]; then
    echo "Test results generated in ./TestResults"
    find ./TestResults -name "*.trx" -exec echo "TRX file: {}" \;
    find ./TestResults -name "*.xml" -exec echo "Coverage file: {}" \;
else
    print_warning "No test results found"
fi

print_success "Local test pipeline completed!"
print_status "Check ./TestResults for detailed test reports and coverage data"

# Optional: Open test results
if command -v code &> /dev/null; then
    print_status "Opening test results in VS Code..."
    code ./TestResults
fi
