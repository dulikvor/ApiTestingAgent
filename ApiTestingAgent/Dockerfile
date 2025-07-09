# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Install debugger for remote debugging
RUN apt-get update && apt-get install -y curl
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

EXPOSE 5991
EXPOSE 4024

ENV ASPNETCORE_URLS=http://+:5991
ENTRYPOINT ["dotnet", "ApiTestingAgent.dll"]