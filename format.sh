#!/bin/bash

# Format C# files using CSharpier
dotnet csharpier .

# Format other files using Prettier
npx prettier --write .