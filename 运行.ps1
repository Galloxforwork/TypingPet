$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'TypingPet.csproj')
