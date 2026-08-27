@echo off
setlocal

set "CONFIGURATION=%~2"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Debug"

if /I "%CONFIGURATION%"=="Debug" goto configuration-valid
if /I "%CONFIGURATION%"=="Staging" goto configuration-valid
if /I "%CONFIGURATION%"=="Release" goto configuration-valid

echo Invalid configuration: "%CONFIGURATION%"
echo Usage: %~nx0 [clean^|restore^|build^|test] [Debug^|Staging^|Release]
exit /b 1

:configuration-valid
if "%~1"=="" goto all

if /I "%~1"=="clean"   goto run-clean
if /I "%~1"=="restore" goto run-restore
if /I "%~1"=="build"   goto run-build
if /I "%~1"=="test"    goto run-test

echo Invalid section: "%~1"
echo Usage: %~nx0 [clean^|restore^|build^|test] [Debug^|Staging^|Release]
exit /b 1


:all
call :clean   || exit /b 1
call :restore || exit /b 1
call :build   || exit /b 1
call :test    || exit /b 1
exit /b 0


:run-clean
call :clean
exit /b %errorlevel%


:run-restore
call :restore
exit /b %errorlevel%


:run-build
call :build
exit /b %errorlevel%


:run-test
call :test
exit /b %errorlevel%


:clean
echo.
echo === Clean (%CONFIGURATION%) ===
dotnet clean Icod.CoreUtils.sln -c "%CONFIGURATION%"
exit /b %errorlevel%


:restore
echo.
echo === Restore ===
dotnet restore Icod.CoreUtils.sln
exit /b %errorlevel%


:build
echo.
echo === Build (%CONFIGURATION%) ===
dotnet build Icod.CoreUtils.sln -c "%CONFIGURATION%" --no-restore
exit /b %errorlevel%


:test
echo.
echo === Test (%CONFIGURATION%) ===
dotnet test Icod.CoreUtils.sln -c "%CONFIGURATION%" --no-build
exit /b %errorlevel%
