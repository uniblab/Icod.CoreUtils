#!/usr/bin/env sh
set -eu

dotnet clean Icod.CoreUtils.sln -c Debug
dotnet restore Icod.CoreUtils.sln
dotnet build Icod.CoreUtils.sln -c Debug --no-restore
dotnet test tests/Shared.Tests/Icod.CoreUtils.Shared.Tests.csproj -c Debug --no-build
