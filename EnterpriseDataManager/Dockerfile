FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["EnterpriseDataManager/EnterPriceDataManager.csproj", "EnterpriseDataManager/"]
COPY ["EnterpriseDataManager.Application/EnterpriseDataManager.Application.csproj", "EnterpriseDataManager.Application/"]
COPY ["EnterpriseDataManager.Common/EnterpriseDataManager.Common.csproj", "EnterpriseDataManager.Common/"]
COPY ["EnterpriseDataManager.Core/EnterpriseDataManager.Core.csproj", "EnterpriseDataManager.Core/"]
COPY ["EnterpriseDataManager.Data/EnterpriseDataManager.Data.csproj", "EnterpriseDataManager.Data/"]
COPY ["EnterpriseDataManager.Infrastructure/EnterpriseDataManager.Infrastructure.csproj", "EnterpriseDataManager.Infrastructure/"]

RUN dotnet restore "EnterpriseDataManager/EnterPriceDataManager.csproj" -r linux-x64

COPY . .

RUN dotnet publish "EnterpriseDataManager/EnterPriceDataManager.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "EnterpriseDataManager.dll"]
