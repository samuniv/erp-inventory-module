#!/bin/bash

# Integration Test Runner Script
set -e

echo "🧪 ERP Inventory Module - Integration Test Runner"
echo "================================================="

# Default values
TEST_SUITE="all"
CLEANUP_AFTER="true"
PARALLEL="false"
VERBOSE="false"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --suite)
            TEST_SUITE="$2"
            shift 2
            ;;
        --no-cleanup)
            CLEANUP_AFTER="false"
            shift
            ;;
        --parallel)
            PARALLEL="true"
            shift
            ;;
        --verbose)
            VERBOSE="true"
            shift
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --suite <suite>     Test suite to run (all|database|messaging|end-to-end)"
            echo "  --no-cleanup        Don't cleanup containers after tests"
            echo "  --parallel          Enable parallel test execution"
            echo "  --verbose           Enable verbose output"
            echo "  --help              Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                              # Run all integration tests"
            echo "  $0 --suite database             # Run only database tests"
            echo "  $0 --parallel --verbose         # Run with parallel execution and verbose output"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Error: Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if .NET SDK is available
if ! dotnet --version > /dev/null 2>&1; then
    echo "❌ Error: .NET SDK is not installed. Please install .NET 9 SDK."
    exit 1
fi

echo "📋 Test Configuration:"
echo "   Suite: $TEST_SUITE"
echo "   Parallel: $PARALLEL"
echo "   Cleanup: $CLEANUP_AFTER"
echo "   Verbose: $VERBOSE"
echo ""

# Cleanup function
cleanup() {
    if [ "$CLEANUP_AFTER" = "true" ]; then
        echo "🧹 Cleaning up containers and resources..."
        docker-compose -f docker-compose.integration.yml down -v --remove-orphans 2>/dev/null || true
        docker system prune -f > /dev/null 2>&1 || true
        echo "✅ Cleanup completed"
    else
        echo "⚠️  Skipping cleanup (containers left running for debugging)"
    fi
}

# Set trap for cleanup on exit
trap cleanup EXIT

# Start infrastructure services
echo "🚀 Starting test infrastructure..."
docker-compose -f docker-compose.integration.yml up -d postgres-test kafka-test zookeeper-test redis-test

# Wait for services to be healthy
echo "⏳ Waiting for services to be ready..."
timeout=180
counter=0

while [ $counter -lt $timeout ]; do
    if docker-compose -f docker-compose.integration.yml ps --services --filter "status=running" | wc -l | grep -q "4"; then
        echo "✅ All services are running"
        break
    fi
    sleep 2
    counter=$((counter + 2))
    if [ $((counter % 20)) -eq 0 ]; then
        echo "   Still waiting... ($counter/$timeout seconds)"
    fi
done

if [ $counter -ge $timeout ]; then
    echo "❌ Timeout waiting for services to start"
    docker-compose -f docker-compose.integration.yml logs
    exit 1
fi

# Additional wait for service health
echo "🔍 Verifying service health..."
sleep 10

# Restore NuGet packages
echo "📦 Restoring NuGet packages..."
dotnet restore ERPInventoryModule.sln

# Build solution
echo "🔨 Building solution..."
if [ "$VERBOSE" = "true" ]; then
    dotnet build ERPInventoryModule.sln --configuration Release --no-restore
else
    dotnet build ERPInventoryModule.sln --configuration Release --no-restore > /dev/null
fi

# Prepare test results directory
rm -rf TestResults
mkdir -p TestResults

# Set test environment variables
export DOCKER_HOST=unix:///var/run/docker.sock
export TESTCONTAINERS_RYUK_DISABLED=true
export TESTCONTAINERS_CHECKS_DISABLE=true
export TESTCONTAINERS_HOST_OVERRIDE=localhost

# Build test filter based on suite
case $TEST_SUITE in
    "database")
        TEST_FILTER="FullyQualifiedName~IntegrationTests&Category!=EndToEnd&Category!=Messaging"
        echo "🗄️  Running database integration tests..."
        ;;
    "messaging")
        TEST_FILTER="FullyQualifiedName~IntegrationTests&Category=Messaging"
        echo "📨 Running messaging integration tests..."
        ;;
    "end-to-end")
        TEST_FILTER="FullyQualifiedName~IntegrationTests&Category=EndToEnd"
        echo "🔄 Running end-to-end integration tests..."
        ;;
    "all")
        TEST_FILTER="FullyQualifiedName~IntegrationTests"
        echo "🧪 Running all integration tests..."
        ;;
    *)
        echo "❌ Invalid test suite: $TEST_SUITE"
        echo "Valid options: all, database, messaging, end-to-end"
        exit 1
        ;;
esac

# Build dotnet test command
TEST_CMD="dotnet test ERPInventoryModule.sln --configuration Release --no-build --filter \"$TEST_FILTER\" --logger trx --collect:\"XPlat Code Coverage\" --results-directory ./TestResults --settings ./tests/integration.runsettings"

if [ "$PARALLEL" = "false" ]; then
    TEST_CMD="$TEST_CMD -- RunConfiguration.MaxCpuCount=1"
fi

if [ "$VERBOSE" = "true" ]; then
    TEST_CMD="$TEST_CMD --verbosity normal"
else
    TEST_CMD="$TEST_CMD --verbosity minimal"
fi

# Run the tests
echo "🏃‍♂️ Executing integration tests..."
echo "Command: $TEST_CMD"
echo ""

if eval $TEST_CMD; then
    echo ""
    echo "✅ Integration tests completed successfully!"
    
    # Display test results summary if available
    if find TestResults -name "*.trx" | head -1 | xargs -r test -f; then
        echo ""
        echo "📊 Test Results Summary:"
        echo "========================"
        
        # Count test files and basic stats
        TRX_FILES=$(find TestResults -name "*.trx" | wc -l)
        echo "   Test result files: $TRX_FILES"
        
        if [ $TRX_FILES -gt 0 ]; then
            echo "   Results location: ./TestResults/"
            
            # Try to extract basic stats from TRX files
            TOTAL_TESTS=$(grep -h "<Counters " TestResults/*.trx 2>/dev/null | sed 's/.*total="\([0-9]*\)".*/\1/' | awk '{sum+=$1} END {print sum}' || echo "Unknown")
            PASSED_TESTS=$(grep -h "<Counters " TestResults/*.trx 2>/dev/null | sed 's/.*passed="\([0-9]*\)".*/\1/' | awk '{sum+=$1} END {print sum}' || echo "Unknown")
            FAILED_TESTS=$(grep -h "<Counters " TestResults/*.trx 2>/dev/null | sed 's/.*failed="\([0-9]*\)".*/\1/' | awk '{sum+=$1} END {print sum}' || echo "Unknown")
            
            echo "   Total tests: $TOTAL_TESTS"
            echo "   Passed: $PASSED_TESTS"
            echo "   Failed: $FAILED_TESTS"
        fi
    fi
    
    # Display coverage information if available
    if find TestResults -name "coverage.cobertura.xml" | head -1 | xargs -r test -f; then
        echo ""
        echo "📈 Code coverage reports generated in TestResults/"
    fi
    
    exit 0
else
    echo ""
    echo "❌ Integration tests failed!"
    echo ""
    echo "🔍 Troubleshooting tips:"
    echo "   1. Check TestResults/ directory for detailed test logs"
    echo "   2. Verify all required services are running:"
    echo "      docker-compose -f docker-compose.integration.yml ps"
    echo "   3. Check service logs for errors:"
    echo "      docker-compose -f docker-compose.integration.yml logs"
    echo "   4. Run with --verbose flag for more detailed output"
    echo ""
    exit 1
fi
