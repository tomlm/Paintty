dotnet tool uninstall -g Paintty
dotnet pack -c Release
dotnet tool install -g Paintty --source nupkg