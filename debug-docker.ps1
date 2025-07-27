# debug-docker.ps1
# PowerShell script to start and debug Docker containers

param(
    [string]$Action = "start"
)

switch ($Action.ToLower()) {
    "start" {
        Write-Host "Stopping any existing containers..." -ForegroundColor Yellow
        docker-compose -f docker-compose.debug.yml down --remove-orphans
        Write-Host "Starting containers in debug mode..." -ForegroundColor Green
        docker-compose -f docker-compose.debug.yml up --build -d
        Start-Sleep -Seconds 3
        Write-Host "Containers started. You can now attach debugger." -ForegroundColor Green
        Write-Host "Container status:" -ForegroundColor Yellow
        docker-compose -f docker-compose.debug.yml ps
        Write-Host "Services available:" -ForegroundColor Cyan
        Write-Host "  ApiTestingAgent: http://localhost:5991" -ForegroundColor White
        Write-Host "  LocalChat: http://localhost:3000" -ForegroundColor White
        Write-Host "  Debug Port: 4024" -ForegroundColor White
    }
    "stop" {
        Write-Host "Stopping containers..." -ForegroundColor Red
        docker-compose -f docker-compose.debug.yml down --remove-orphans
    }
    "logs" {
        Write-Host "Showing logs..." -ForegroundColor Cyan
        docker-compose -f docker-compose.debug.yml logs -f apitestingagent
    }
    "attach" {
        Write-Host "Container processes:" -ForegroundColor Yellow
        docker exec -it apitestingagent-apitestingagent-1 ps aux
        Write-Host "To attach debugger, use VS Code or run:" -ForegroundColor Green
        Write-Host "docker exec -it apitestingagent-apitestingagent-1 /bin/bash" -ForegroundColor Cyan
    }
    default {
        Write-Host "Usage: ./debug-docker.ps1 [start|stop|logs|attach]" -ForegroundColor Yellow
        Write-Host "  start  - Start containers in debug mode" -ForegroundColor White
        Write-Host "  stop   - Stop containers" -ForegroundColor White
        Write-Host "  logs   - Show container logs" -ForegroundColor White
        Write-Host "  attach - Show processes and attach instructions" -ForegroundColor White
    }
}
