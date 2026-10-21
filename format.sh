#!/bin/bash

# Format C# files using CSharpier
dotnet tool restore && dotnet dotnet-csharpier .

# Format other files using Prettier
npx prettier --write .