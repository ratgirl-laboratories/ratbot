FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY RatBot.Application/RatBot.Application.csproj Ratbot.Application/
COPY RatBot.Host/RatBot.Host.csproj RatBot.Host/
COPY RatBot.Infrastructure/RatBot.Infrastructure.csproj RatBot.Infrastructure/
COPY RatBot.Interactions/RatBot.Interactions.csproj RatBot.Interactions/
COPY RatBot.Domain/RatBot.Domain.csproj RatBot.Domain/

RUN dotnet restore RatBot.Host/RatBot.Host.csproj

COPY . .
RUN dotnet publish RatBot.Host/RatBot.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "RatBot.dll"]
