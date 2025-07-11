FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app
MAINTAINER team@openshock.org

EXPOSE 8080
ENV OPENSHOCK_DATA_FOLDER=/data
ENV HOME=$OPENSHOCK_DATA_FOLDER
VOLUME ["/data"]

COPY --link ./publish/Web-Linux .

ENTRYPOINT ["dotnet", "OpenShock.Desktop.dll"]