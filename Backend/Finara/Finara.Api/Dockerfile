# Bu imaj tek başına yeterli değil — gerçek sırlar (ConnectionStrings__DefaultConnection,
# Jwt__Key, Smtp__*, Twilio__*, Garanti__*) container'a ortam değişkeni olarak dışarıdan
# verilmeli (bkz. ../PRODUCTION.md). appsettings*.json dosyalarında hiçbir sır tutulmuyor.
#
# Build context bu Dockerfile'ın bulunduğu Finara.Api/ klasörü DEĞİL, çözümün kökü
# (Backend/Finara/) olmalı — diğer katman projelerine (Domain/Application/Infrastructure)
# referans verdiği için: `docker build -f Finara.Api/Dockerfile .` (Backend/Finara içinden).

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Finara.slnx ./
COPY Finara.Domain/*.csproj Finara.Domain/
COPY Finara.Application/*.csproj Finara.Application/
COPY Finara.Infrastructure/*.csproj Finara.Infrastructure/
COPY Finara.Api/*.csproj Finara.Api/
RUN dotnet restore Finara.Api/Finara.Api.csproj

COPY Finara.Domain/ Finara.Domain/
COPY Finara.Application/ Finara.Application/
COPY Finara.Infrastructure/ Finara.Infrastructure/
COPY Finara.Api/ Finara.Api/
RUN dotnet publish Finara.Api/Finara.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Migration'lar burada OTOMATIK uygulanmıyor (bilinçli) — dağıtım script'i/CD adımı
# `dotnet ef database update` ile ayrı bir adım olarak çalıştırmalı, container her
# yeniden başladığında migration denemesin diye.
ENTRYPOINT ["dotnet", "Finara.Api.dll"]
