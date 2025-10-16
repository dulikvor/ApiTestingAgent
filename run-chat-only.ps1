#!/usr/bin/env pwsh

# Run LocalChat only to connect to Visual Studio ApiTestingAgent
Write-Host "Starting LocalChat to connect to Visual Studio ApiTestingAgent..." -ForegroundColor Green
Write-Host "Make sure your ApiTestingAgent is running in Visual Studio on http://localhost:5991" -ForegroundColor Yellow
Write-Host "Launch Profile: 'Dline'" -ForegroundColor Yellow
Write-Host "" 

# Navigate to LocalChat directory
Set-Location "$PSScriptRoot\LocalChat"

# Check if node_modules exists
if (-not (Test-Path "node_modules")) {
    Write-Host "Installing dependencies..." -ForegroundColor Cyan
    npm install
}

# Start the LocalChat development server
Write-Host "Starting LocalChat frontend on http://localhost:3000" -ForegroundColor Cyan
Write-Host "Connecting to ApiTestingAgent at http://localhost:5991" -ForegroundColor Cyan
Write-Host "" 
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host "" 

npm run dev
