# Use the official .NET 9 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

# Use the official .NET 9 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["Auth.Service.csproj", "."]
RUN dotnet restore "Auth.Service.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "Auth.Service.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Auth.Service.csproj" -c Release -o /app/publish

# Create the final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Auth.Service.dll"]
