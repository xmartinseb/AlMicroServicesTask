FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore Alza.AggregationBackendService/Alza.AggregationBackendService.csproj

RUN dotnet publish Alza.AggregationBackendService/Alza.AggregationBackendService.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Alza.AggregationBackendService.dll"]