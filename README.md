# Energy API 🌱

ASP.NET Core Web API providing UK energy mix data and optimal EV charging window calculations.

## Live API
🔗 https://energy-api-r80i.onrender.com/api/energy/mix

## Endpoints

### GET /api/energy/mix
Returns the energy mix for today, tomorrow, and the day after tomorrow with clean energy percentages.

**Response example:**
```json
[
  {
    "date": "2026-07-01",
    "averageMix": { "wind": 19.0, "solar": 11.7, "gas": 38.4 },
    "cleanEnergyPercentage": 46.97
  }
]
```

### GET /api/energy/optimal-window?durationHours=3
Returns the optimal time window to charge an EV based on clean energy availability.

**Parameters:**
- `durationHours` (int, 1–6): desired charging duration in hours

**Response example:**
```json
{
  "startDateTime": "2026-07-02T09:30:00",
  "endDateTime": "2026-07-02T12:30:00",
  "averageCleanEnergyPercentage": 88.78
}
```

## Tech Stack
- ASP.NET Core Web API (.NET 10)
- C#
- xUnit + Moq (unit tests)
- Docker
- Deployed on Render

## Run Locally

```bash
cd EnergyApi
dotnet run
```

API will be available at `http://localhost:5252`

## Run Tests

```bash
cd EnergyApi.Tests
dotnet test
```

## External API
Data source: [Carbon Intensity API](https://carbon-intensity.github.io/api-definitions/)