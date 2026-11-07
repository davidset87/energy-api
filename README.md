# GridWatch API

[![CI](https://github.com/davidset87/energy-api/actions/workflows/ci.yml/badge.svg)](https://github.com/davidset87/energy-api/actions/workflows/ci.yml)

ASP.NET Core (.NET 10) Web API behind the GridWatch app. It reads half-hourly generation data from the
[UK Carbon Intensity API](https://carbonintensity.org.uk/) and exposes two endpoints:

- the average energy mix for today and the next two days
- the best window to charge an EV, i.e. the one with the highest average share of clean energy

Frontend: https://github.com/davidset87/energy-frontend

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/energy/mix` | Average energy mix per day (3 days) and clean energy percentage |
| GET | `/api/energy/optimal-window?durationHours=3` | Best charging window for a duration of 1 to 6 hours |

Clean energy means biomass, nuclear, hydro, wind and solar.

### Errors

| Status | When |
|--------|------|
| 400 | `durationHours` is missing or outside 1 to 6 (validated with `[Range]`) |
| 502 | The Carbon Intensity API is unreachable, times out, returns an error, or returns empty or malformed data |

## Design notes

- Clean architecture in small pieces: controller, `IEnergyService`/`EnergyService` (typed `HttpClient` registered with `AddHttpClient`), models.
- All dates are handled in UTC, and the clock is injected (`TimeProvider`) so the behaviour is deterministic in tests.
- The charging window is computed on one sorted, continuous list of 30-minute slots, so a window can cross midnight without a special case.
- The upstream API may also return the slot that ends exactly at the requested start. Slots outside the requested range are ignored, so the days are never shifted.
- Upstream failures never become a raw 500: they are mapped to 502, with a 10 second timeout on the `HttpClient`.

## Configuration

| Setting | Environment variable | Default |
|---------|----------------------|---------|
| `CarbonIntensityApi:BaseUrl` | `CarbonIntensityApi__BaseUrl` | `https://api.carbonintensity.org.uk/` |
| `Cors:AllowedOrigin` | `Cors__AllowedOrigin` | `http://localhost:5173` |

CORS only allows the configured origin, with no wildcard.

## Run locally

```bash
dotnet run
```

The API listens on `http://localhost:5252`. `EnergyApi.http` contains ready-to-run requests.

## Tests

```bash
dotnet test EnergyApi.Tests/EnergyApi.Tests.csproj
```

The tests run the real `EnergyService` and mock only the HTTP boundary (`HttpMessageHandler`) with a fixed clock.
They cover averaging, day grouping, window selection, windows across midnight, the requested date range,
and upstream failures (error status, empty body, malformed JSON, missing fields, timeout, unreachable).

## Docker

```bash
docker build -t energy-api .
docker run -p 8080:8080 energy-api
```

Multi-stage