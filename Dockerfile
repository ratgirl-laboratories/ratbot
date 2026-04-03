FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY global.json ./
COPY RatBot/RatBot.csproj RatBot/
COPY RatBot.Infrastructure/RatBot.Infrastructure.csproj RatBot.Infrastructure/
COPY RatBot.Interactions/RatBot.Interactions.csproj RatBot.Interactions/
COPY RatBot.Domain/RatBot.Domain.csproj RatBot.Domain/

RUN dotnet restore RatBot/RatBot.csproj

COPY . .
RUN dotnet publish RatBot/RatBot.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "RatBot.dll"]
