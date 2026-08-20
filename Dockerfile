FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY TeacherOS.slnx Directory.Build.props Directory.Packages.props global.json ./
COPY src/TeacherOS.Domain/TeacherOS.Domain.csproj src/TeacherOS.Domain/
COPY src/TeacherOS.Application/TeacherOS.Application.csproj src/TeacherOS.Application/
COPY src/TeacherOS.Infrastructure/TeacherOS.Infrastructure.csproj src/TeacherOS.Infrastructure/
COPY src/TeacherOS.Api/TeacherOS.Api.csproj src/TeacherOS.Api/
COPY tests/TeacherOS.Domain.Tests/TeacherOS.Domain.Tests.csproj tests/TeacherOS.Domain.Tests/
COPY tests/TeacherOS.Application.Tests/TeacherOS.Application.Tests.csproj tests/TeacherOS.Application.Tests/
COPY tests/TeacherOS.IntegrationTests/TeacherOS.IntegrationTests.csproj tests/TeacherOS.IntegrationTests/
COPY tests/TeacherOS.ArchitectureTests/TeacherOS.ArchitectureTests.csproj tests/TeacherOS.ArchitectureTests/
RUN dotnet restore TeacherOS.slnx

COPY . .
RUN dotnet publish src/TeacherOS.Api/TeacherOS.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeacherOS.Api.dll"]
