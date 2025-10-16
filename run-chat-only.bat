@echo off
echo Starting LocalChat to connect to Visual Studio ApiTestingAgent...
echo Make sure your ApiTestingAgent is running in Visual Studio on http://localhost:5991
echo Launch Profile: 'Dline'
echo.

cd /d "%~dp0LocalChat"

if not exist "node_modules" (
    echo Installing dependencies...
    npm install
)

echo Starting LocalChat frontend on http://localhost:3000
echo Connecting to ApiTestingAgent at http://localhost:5991
echo.
echo Press Ctrl+C to stop
echo.

npm run dev
