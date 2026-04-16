# SunTime

A WebAssembly app that visualises the sun's current position relative to the horizon, built with [Uno Platform](https://platform.uno/) and SkiaSharp.

## Features

- **Sun dial visualisation** – a circular view with a sky/ground split. The sun icon travels along the outer arc from sunrise to sunset, and dims below the horizon at night.
- **24-hour analog clock** – tick marks and hour labels (0–23) are drawn inside the orbit ring, with a live hour hand.
- **Solar calculations** – NOAA solar position algorithm computing sunrise, sunset, solar noon, and current altitude for any location.
- **Geolocation** – uses the browser's Geolocation API via `Windows.Devices.Geolocation.Geolocator`. Falls back to Stockholm (59.33°N, 18.07°E) if permission is denied.
- **Auto-refresh** – a 1-minute timer keeps the dial in sync with the current time.

## Tech Stack

| | |
|---|---|
| Framework | [Uno Platform](https://platform.uno/) with `Uno.Sdk` 6.5.31 |
| Target | `net10.0-browserwasm` |
| Rendering | SkiaSharp (`SKXamlCanvas`) via the `SkiaRenderer` UnoFeature |

## Project Structure

```
.
├── SunTime/                  # Uno WASM + Android frontend
│   ├── Controls/
│   │   └── SunDial.cs        # SkiaSharp custom control
│   ├── Pages/
│   │   ├── LoginPage              # Google/Apple sign-in
│   │   ├── PendingApprovalPage    # Shown while awaiting admin approval
│   │   └── UserManagementPage     # Admin / SuperAdmin only
│   ├── Services/
│   │   ├── ApiClient.cs      # HTTP client for SunTime.Server
│   │   ├── LocationService.cs
│   │   ├── SettingsService.cs
│   │   └── SolarCalculator.cs
│   ├── Platforms/WebAssembly/Program.cs
│   ├── MainPage.xaml/.cs     # Dial + refresh timer (gated on sign-in)
│   ├── SplashPage.xaml/.cs   # Splash → LoginPage
│   └── App.xaml/.cs
├── SunTime.Server/           # ASP.NET Core API
│   ├── Controllers/
│   │   ├── AuthController.cs    # /api/auth/login, /login/password, /me, /refresh
│   │   └── UsersController.cs   # /api/users CRUD (Admin / SuperAdmin)
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Models/
│   │   ├── AppUser.cs
│   │   └── Dtos.cs
│   └── Program.cs            # Identity + JWT + Google OIDC + PostgreSQL
├── Dockerfile.wasm
├── Dockerfile.api
├── docker-compose.yml        # wasm + api + postgres
└── nginx.conf                # serves WASM + reverse-proxies /api/*
```

## User handling

The frontend is gated behind a login flow modeled after the VirgoBoule project:

1. `SplashPage` plays the sun-rise animation, then navigates to `LoginPage`.
2. `LoginPage` triggers Google or Apple OIDC via `WebAuthenticationBroker` and
   exchanges the returned `id_token` with `SunTime.Server` for a JWT.
3. New users are created in `PendingApproval` status and sent to
   `PendingApprovalPage` until an Admin/SuperAdmin approves them.
4. `Active` users land on `MainPage` (the sun dial). Admin/SuperAdmin users also
   see a "Manage Users" button that opens `UserManagementPage`.
5. A seeded SuperAdmin (`admin@suntime.local` / `Admin123!`) is created in
   development so the first approvals can happen without any external IdP setup.

## Running the full stack locally

```bash
docker compose up --build
```

Services:

- WASM frontend: http://localhost:8080
- API backend:   http://localhost:8080/api/* (reverse-proxied by nginx)
- PostgreSQL:    localhost:5432 (user `postgres`, db `suntime`)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Uno Platform workload](https://platform.uno/docs/articles/get-started.html)

```bash
dotnet workload install uno-wasm
```

### Run

```bash
dotnet run
```

The app will be served at `http://localhost:5001`.

## Download build output from GitHub Actions

Each CI run uploads a downloadable artifact named `suntime-browserwasm`.

1. Open the **Actions** tab in GitHub.
2. Select a **Build** workflow run.
3. Download the `suntime-browserwasm` artifact from the **Artifacts** section.

## Cloud environment (Oz)

This repo is wired up to a Warp Oz cloud environment (team-scoped, ID `DMT1HA3vyEQmfeYplc8Xo0`) so cloud agents can build and run against both target frameworks declared in `SunTime.csproj` (`net10.0-browserwasm;net10.0-android`).

### Base image

`mcr.microsoft.com/dotnet/sdk:10.0`

### Setup commands (order matters)

1. `apt-get update && apt-get install -y python3`
2. `dotnet workload install wasm-tools`
3. `dotnet workload install android`
4. `cd /workspace/SunTime && dotnet restore`

The `android` workload install is required because `dotnet restore` with no `-f` flag resolves every TFM in the csproj, and the `net10.0-android` target fails without the workload. Symptom when it's missing:

```
Environment setup failed: Failed to run setup command: cd /workspace/SunTime && dotnet restore.
```

### Updating the environment

Use the `oz` CLI (ordering is append-only, so you may need to remove and re-add the `dotnet restore` step to keep it last):

```bash
oz environment update DMT1HA3vyEQmfeYplc8Xo0 \
  --remove-setup-command "cd /workspace/SunTime && dotnet restore" \
  -c "dotnet workload install android" \
  -c "cd /workspace/SunTime && dotnet restore"
```

Verify:

```bash
oz environment get DMT1HA3vyEQmfeYplc8Xo0 --output-format text
```
