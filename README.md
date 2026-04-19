# RatBot

A Discord bot built with .NET 10.0 and Discord.Net, featuring a modular architecture and optional observability.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/get-started) and [Docker Compose](https://docs.docker.com/compose/install/)
- A Discord Bot Token (from the [Discord Developer Portal](https://discord.com/developers/applications))

## Configuration

The bot is configured via environment variables. For local development, you can create a `.env` file in the root directory.

### Required Configuration

| Variable | Description |
|----------|-------------|
| `Discord__Token` | Your Discord Bot Token |
| `Discord__GuildId` | The ID of the primary guild for slash commands |

### Optional Database Configuration

The `docker-compose.yml` provides a PostgreSQL instance with the following defaults. You only need to change these if you want to use a different setup.

| Variable | Description | Default |
|----------|-------------|---------|
| `DB__Host` | PostgreSQL host | `localhost` |
| `DB__Database` | PostgreSQL database name | `ratbot` |
| `DB__User` | PostgreSQL username | `postgres` |
| `DB__Password` | PostgreSQL password | `postgres` |

## Running Locally

### 1. Start the Database

Run the following command to start the PostgreSQL database in the background:

```bash
docker compose up db -d
```

### 2. Run the Bot

#### Option A: Running with .NET SDK (Recommended for development)

```bash
dotnet run --project RatBot.Host
```

The bot will automatically apply database migrations on startup.

#### Option B: Running via Docker

```bash
docker build -t ratbot .
docker run --env-file .env ratbot
```

## Optional: Observability

The project includes an optional observability stack (Grafana, Loki, OpenTelemetry) for monitoring and logging.

### 1. Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `OTEL__Logs__Endpoint` | OpenTelemetry Collector endpoint | (Disabled if empty) |
| `GRAFANA__ADMIN__USER` | Grafana admin username | `admin` |
| `GRAFANA__ADMIN__PASSWORD` | Grafana admin password | `admin` |

To enable logs export, set `OTEL__Logs__Endpoint` to `http://localhost:4317` (if running bot locally) or `http://otel-collector:4317` (if running bot in docker).

### 2. Start Observability Stack

```bash
docker compose up loki grafana otel-collector -d
```

Grafana will be accessible at `http://localhost:3000` (Default: `admin`/`admin`).

## Development

### Project Structure

- `RatBot.Application`: Business logic and service interfaces.
- `RatBot.Domain`: Core domain models and logic.
- `RatBot.Infrastructure`: Database persistence (EF Core) and external service implementations.
- `RatBot.Discord`: Discord command modules and interaction handlers.
- `RatBot.Host`: Entry point and DI configuration.

### Running Tests

The infrastructure integration tests use Testcontainers and require access to a running Docker daemon.

```bash
dotnet test
```
