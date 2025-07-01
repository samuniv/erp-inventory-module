# Integration Test Runner Script for Windows
param(
    [Parameter(HelpMessage="Test suite to run (all|database|messaging|end-to-end)")]
    [ValidateSet("all", "database", "messaging", "end-to-end")]
    [string]$Suite = "all",
    
    [Parameter(HelpMessage="Don't cleanup containers after tests")]
    [switch]$NoCleanup,
    
    [Parameter(HelpMessage="Enable parallel test execution")]
    [switch]$Parallel,
    
    [Parameter(HelpMessage="Enable verbose output")]
    [switch]$Verbose,
    
    [Parameter(HelpMessage="Show help information")]
    [switch]$Help
)

if ($Help) {
    Write-Host "ERP Inventory Module - Integration Test Runner" -ForegroundColor Cyan
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\run-integration-tests.ps1 [OPTIONS]" -ForegroundColor White
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Yellow
    Write-Host "  -Suite <suite>     Test suite to run (all|database|messaging|end-to-end)" -ForegroundColor Gray
    Write-Host "  -NoCleanup         Don't cleanup containers after tests" -ForegroundColor Gray
    Write-Host "  -Parallel          Enable parallel test execution" -ForegroundColor Gray
    Write-Host "  -Verbose           Enable verbose output" -ForegroundColor Gray
    Write-Host "  -Help              Show this help message" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\run-integration-tests.ps1                              # Run all integration tests" -ForegroundColor Gray
    Write-Host "  .\run-integration-tests.ps1 -Suite database              # Run only database tests" -ForegroundColor Gray
    Write-Host "  .\run-integration-tests.ps1 -Parallel -Verbose           # Run with parallel execution and verbose output" -ForegroundColor Gray
    exit 0
}

Write-Host "🧪 ERP Inventory Module - Integration Test Runner" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan

# Configuration
$CleanupAfter = -not $NoCleanup.IsPresent

Write-Host "📋 Test Configuration:" -ForegroundColor Yellow
Write-Host "   Suite: $Suite" -ForegroundColor Gray
Write-Host "   Parallel: $($Parallel.IsPresent)" -ForegroundColor Gray
Write-Host "   Cleanup: $CleanupAfter" -ForegroundColor Gray
Write-Host "   Verbose: $($Verbose.IsPresent)" -ForegroundColor Gray
Write-Host ""

# Check if Docker is running
try {
    docker info | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker not running"
    }
} catch {
    Write-Host "❌ Error: Docker is not running. Please start Docker and try again." -ForegroundColor Red
    exit 1
}

# Check if .NET SDK is available
try {
    dotnet --version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw ".NET SDK not found"
    }
} catch {
    Write-Host "❌ Error: .NET SDK is not installed. Please install .NET 9 SDK." -ForegroundColor Red
    exit 1
}

# Cleanup function
function Cleanup {
    if ($CleanupAfter) {
        Write-Host "🧹 Cleaning up containers and resources..." -ForegroundColor Yellow
        docker-compose -f docker-compose.integration.yml down -v --remove-orphans 2>$null
        docker system prune -f | Out-Null
        Write-Host "✅ Cleanup completed" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Skipping cleanup (containers left running for debugging)" -ForegroundColor Yellow
    }
}

# Register cleanup function
$ExecutionContext.SessionState.Module.OnRemove = {
    Cleanup
}

try {
    # Start infrastructure services
    Write-Host "🚀 Starting test infrastructure..." -ForegroundColor Yellow
    docker-compose -f docker-compose.integration.yml up -d postgres-test kafka-test zookeeper-test redis-test
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start infrastructure services"
    }

    # Wait for services to be healthy
    Write-Host "⏳ Waiting for services to be ready..." -ForegroundColor Yellow
    $timeout = 180
    $counter = 0

    while ($counter -lt $timeout) {
        $runningServices = docker-compose -f docker-compose.integration.yml ps --services --filter "status=running" | Measure-Object -Line
        if ($runningServices.Lines -eq 4) {
            Write-Host "✅ All services are running" -ForegroundColor Green
            break
        }
        Start-Sleep 2
        $counter += 2
        if ($counter % 20 -eq 0) {
            Write-Host "   Still waiting... ($counter/$timeout seconds)" -ForegroundColor Gray
        }
    }

    if ($counter -ge $timeout) {
        Write-Host "❌ Timeout waiting for services to start" -ForegroundColor Red
        docker-compose -f docker-compose.integration.yml logs
        exit 1
    }

    # Additional wait for service health
    Write-Host "🔍 Verifying service health..." -ForegroundColor Yellow
    Start-Sleep 10

    # Restore NuGet packages
    Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore ERPInventoryModule.sln
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore NuGet packages"
    }

    # Build solution
    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    if ($Verbose) {
        dotnet build ERPInventoryModule.sln --configuration Release --no-restore
    } else {
        dotnet build ERPInventoryModule.sln --configuration Release --no-restore | Out-Null
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build solution"
    }

    # Prepare test results directory
    if (Test-Path "TestResults") {
        Remove-Item "TestResults" -Recurse -Force
    }
    New-Item -ItemType Directory -Path "TestResults" | Out-Null

    # Set test environment variables
    $env:DOCKER_HOST = "unix:///var/run/docker.sock"
    $env:TESTCONTAINERS_RYUK_DISABLED = "true"
    $env:TESTCONTAINERS_CHECKS_DISABLE = "true"
    $env:TESTCONTAINERS_HOST_OVERRIDE = "localhost"

    # Build test filter based on suite
    switch ($Suite) {
        "database" {
            $testFilter = "FullyQualifiedName~IntegrationTests&Category!=EndToEnd&Category!=Messaging"
            Write-Host "🗄️  Running database integration tests..." -ForegroundColor Cyan
        }
        "messaging" {
            $testFilter = "FullyQualifiedName~IntegrationTests&Category=Messaging"
            Write-Host "📨 Running messaging integration tests..." -ForegroundColor Cyan
        }
        "end-to-end" {
            $testFilter = "FullyQualifiedName~IntegrationTests&Category=EndToEnd"
            Write-Host "🔄 Running end-to-end integration tests..." -ForegroundColor Cyan
        }
        "all" {
            $testFilter = "FullyQualifiedName~IntegrationTests"
            Write-Host "🧪 Running all integration tests..." -ForegroundColor Cyan
        }
    }

    # Build dotnet test command arguments
    $testArgs = @(
        "test", "ERPInventoryModule.sln",
        "--configuration", "Release",
        "--no-build",
        "--filter", $testFilter,
        "--logger", "trx",
        "--collect:XPlat Code Coverage",
        "--results-directory", "./TestResults",
        "--settings", "./tests/integration.runsettings"
    )

    if (-not $Parallel) {
        $testArgs += @("--", "RunConfiguration.MaxCpuCount=1")
    }

    if ($Verbose) {
        $testArgs += @("--verbosity", "normal")
    } else {
        $testArgs += @("--verbosity", "minimal")
    }

    # Run the tests
    Write-Host "🏃‍♂️ Executing integration tests..." -ForegroundColor Yellow
    Write-Host "Command: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
    Write-Host ""

    & dotnet @testArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ Integration tests completed successfully!" -ForegroundColor Green
        
        # Display test results summary if available
        $trxFiles = Get-ChildItem -Path "TestResults" -Filter "*.trx" -Recurse
        if ($trxFiles.Count -gt 0) {
            Write-Host ""
            Write-Host "📊 Test Results Summary:" -ForegroundColor Yellow
            Write-Host "========================" -ForegroundColor Yellow
            Write-Host "   Test result files: $($trxFiles.Count)" -ForegroundColor Gray
            Write-Host "   Results location: ./TestResults/" -ForegroundColor Gray
            
            # Try to extract basic stats from TRX files
            try {
                $totalTests = 0
                $passedTests = 0
                $failedTests = 0
                
                foreach ($trxFile in $trxFiles) {
                    $content = Get-Content $trxFile.FullName -Raw
                    if ($content -match '<Counters.*?total="(\d+)".*?passed="(\d+)".*?failed="(\d+)"') {
                        $totalTests += [int]$matches[1]
                        $passedTests += [int]$matches[2]
                        $failedTests += [int]$matches[3]
                    }
                }
                
                if ($totalTests -gt 0) {
                    Write-Host "   Total tests: $totalTests" -ForegroundColor Gray
                    Write-Host "   Passed: $passedTests" -ForegroundColor Green
                    Write-Host "   Failed: $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Gray" })
                }
            } catch {
                Write-Host "   (Could not parse detailed statistics)" -ForegroundColor Gray
            }
        }
        
        # Display coverage information if available
        $coverageFiles = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse
        if ($coverageFiles.Count -gt 0) {
            Write-Host ""
            Write-Host "📈 Code coverage reports generated in TestResults/" -ForegroundColor Cyan
        }
        
    } else {
        Write-Host ""
        Write-Host "❌ Integration tests failed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "🔍 Troubleshooting tips:" -ForegroundColor Yellow
        Write-Host "   1. Check TestResults/ directory for detailed test logs" -ForegroundColor Gray
        Write-Host "   2. Verify all required services are running:" -ForegroundColor Gray
        Write-Host "      docker-compose -f docker-compose.integration.yml ps" -ForegroundColor Gray
        Write-Host "   3. Check service logs for errors:" -ForegroundColor Gray
        Write-Host "      docker-compose -f docker-compose.integration.yml logs" -ForegroundColor Gray
        Write-Host "   4. Run with -Verbose flag for more detailed output" -ForegroundColor Gray
        Write-Host ""
        
        throw "Integration tests failed"
    }

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Cleanup
}
