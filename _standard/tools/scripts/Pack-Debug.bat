dotnet pack ..\..\..\client\Watchdog.Client\Watchdog.Client.csproj --output d:\nuget --configuration Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
for /D %%x in ("%userprofile%\.nuget\packages\watchdog*") do (rmdir %%x /s /q)

