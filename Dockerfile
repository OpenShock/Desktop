FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app
COPY ./publish/Web-Linux .

ENTRYPOINT ["dotnet", "OpenShock.Desktop.dll"]