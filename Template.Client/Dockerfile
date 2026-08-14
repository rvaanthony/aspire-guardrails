# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "./"]
COPY ["Template.Client/Template.Client.csproj", "Template.Client/"]
COPY ["Template.Business/Template.Business.csproj", "Template.Business/"]
COPY ["Template.Common/Template.Common.csproj", "Template.Common/"]
COPY ["Template.Data/Template.Data.csproj", "Template.Data/"]
COPY ["Template.ServiceDefaults/Template.ServiceDefaults.csproj", "Template.ServiceDefaults/"]
# Central Package Management resolves versions from Directory.Packages.props next
# to these csproj files, and every ProjectReference target must exist before
# restore, or MSBuild fails resolving the project graph — hence the full project
# skeleton here rather than just Template.Client's own csproj.
RUN dotnet restore "./Template.Client/Template.Client.csproj"
COPY . .
WORKDIR "/src/Template.Client"
RUN dotnet build "./Template.Client.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Template.Client.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Template.Client.dll"]