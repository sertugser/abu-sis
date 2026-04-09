@echo off
echo =========================================
echo SIS Project - Starting in Integrated Terminal...
echo =========================================

set "PATH=%PATH%;C:\Program Files\nodejs;%AppData%\npm"

echo.
echo [1/3] Backend (SIS.API) setup...
cd SIS.API
call dotnet restore
call dotnet build
cd ..

echo.
echo [2/3] Frontend (SIS.Web) setup...
cd SIS.Web
call npm install
cd ..

echo.
echo =========================================
echo Preparation complete! Starting projects...
echo Both API and Web outputs will be shown here.
echo (Press CTRL+C once to stop both later)
echo =========================================

:: Start Backend in background (same terminal window)
start /b cmd /c "cd SIS.API && dotnet run"

:: Start Frontend in background (same terminal window)
start /b cmd /c "cd SIS.Web && npm run dev"

echo.
echo Waiting for servers to be ready...
timeout /t 10 /nobreak >nul

echo Opening browser: http://localhost:5173/
start http://localhost:5173/

echo.
echo =========================================
echo ALL DONE! Projects are running in this window.
echo =========================================
