FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY --link *.props *.props ./
COPY --link Desktop/*.csproj Desktop/
COPY --link ModuleBase/*.csproj ModuleBase/

RUN dotnet restore Desktop/Desktop.csproj /p:Configuration=Release-Web

COPY . .
RUN dotnet publish Desktop/Desktop.csproj -c Release-Web -o /publish/Web-Linux

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS runtime
WORKDIR /app
LABEL org.opencontainers.image.authors="team@openshock.org"

EXPOSE 8080
ENV OPENSHOCK_DATA_FOLDER=/data
ENV HOME=$OPENSHOCK_DATA_FOLDER
VOLUME ["/data"]

COPY --link --from=build ./publish/Web-Linux .

ENTRYPOINT ["dotnet", "OpenShock.Desktop.dll"]