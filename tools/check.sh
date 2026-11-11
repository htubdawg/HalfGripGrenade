#!/bin/bash
# Compiles the mod against the game, BepInEx and HalfGripTools without starting the game, the way HalfGripTools' check-mods.ps1 does
# (C# 10, netstandard2.1), on Linux. Nothing is written to the repo.
#   tools/check.sh <game folder> <HalfGripTools.dll>
#   tools/check.sh <game folder> --stub     (tools/stub/HalfGripTools.cs stands in for HalfGripTools, when it cannot be built)
# The game folder can be the HalfGripRefs repo's game/ folder. HalfGripTools.dll comes from building HalfGripTools:
#   dotnet build HalfGripTools/HalfGripTools.csproj -c Release -p:GameDir=<game folder>
# which puts it in HalfGripTools/dist/BepInEx/plugins/HalfGripTools/.
set -euo pipefail
game="$(realpath "${1:?game folder}")"
repo="$(cd "$(dirname "$0")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
managed="$game/HalfGrip_Data/Managed"
if [ "${2:?HalfGripTools.dll or --stub}" = "--stub" ]; then
  tools_item="<Compile Include=\"$repo/tools/stub/HalfGripTools.cs\" />"
else
  tools_item="<Reference Include=\"$(realpath "$2")\" />"
fi
cat > "$work/check.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>10.0</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>HalfGripMod.Grenade</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$repo/Grenade/src/**/*.cs" />
    $tools_item
    <Reference Include="$managed/*.dll" Exclude="$managed/mscorlib.dll;$managed/netstandard.dll;$managed/System*.dll" />
    <Reference Include="$game/BepInEx/core/BepInEx.dll" />
    <Reference Include="$game/BepInEx/core/0Harmony.dll" />
  </ItemGroup>
</Project>
XML
DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet build "$work/check.csproj" -c Release -v q --nologo -p:OutDir="$work/out/"
