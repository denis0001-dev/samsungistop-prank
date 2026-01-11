@echo off
dotnet publish samsungistop.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o "./output"
explorer output