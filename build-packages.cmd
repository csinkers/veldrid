@echo off
@setlocal

dotnet restore src\Veldrid.sln

set UseStableVersions=true

dotnet pack -c Release src\Veldrid\Veldrid.csproj
dotnet pack -c Release src\Veldrid.Utilities\Veldrid.Utilities.csproj
dotnet pack -c Release src\Veldrid.VirtualReality\Veldrid.VirtualReality.csproj
