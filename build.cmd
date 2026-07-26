@echo off
setlocal

dotnet clean Icod.CoreUtils.sln -c Debug || exit /b 1
dotnet restore Icod.CoreUtils.sln || exit /b 1
dotnet build Icod.CoreUtils.sln -c Debug --no-restore || exit /b 1
dotnet test tests\Shared.Tests\Icod.CoreUtils.Shared.Tests.csproj -c Debug --no-build || exit /b 1
