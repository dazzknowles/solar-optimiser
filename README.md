# Solar Optimiser

Phase 1 read-only telemetry capture and retrieval for a FoxESS solar/battery installation. See
`docs/solar-functional-specification.md` and `docs/solar-technical-specification.md` for the authoritative
requirements this implementation follows.

Solar formally adopts [Eceni Governance 1.2.0](docs/governance.md). The adoption record identifies the
applicable portfolio rules, Solar-specific decisions and outstanding conformance work without copying Governance
into this repository.

## Solution layout (SOL-T-101)

```
src/
  SolarOptimiser.Domain                 // Site, Device, TelemetryQuantity, TelemetryQuality - no provider or DB knowledge
  SolarOptimiser.Providers.Abstractions // ITelemetryProvider + provider-neutral DTOs
  SolarOptimiser.Providers.FoxESS       // FoxESS adapter: auth, HTTP, raw JSON contracts, mapping to abstraction DTOs
  SolarOptimiser.Persistence            // Eceni.Core-based data access, repository interfaces/implementations, stored procedures
  SolarOptimiser.Collection             // orchestration: discover -> fetch -> classify -> persist -> record run/attempt
  SolarOptimiser.Host                   // single process: hosts the Collection worker AND the retrieval API
tests/
  SolarOptimiser.Domain.Tests
  SolarOptimiser.Providers.FoxESS.Tests
  SolarOptimiser.Persistence.Tests      // integration, ephemeral MariaDB
  SolarOptimiser.Collection.Tests
  SolarOptimiser.Host.Tests             // integration, proves retrieval
```

`SolarOptimiser.Persistence` consumes the `Eceni.Core.Base`/`Eceni.Core.Database.MySQL` NuGet packages (published from
[eceni.core](https://github.com/dazzknowles/eceni.core)) rather than holding a copy of the DBUtility data-access
layer in this repo.

## Consuming the Eceni.Core packages

`NuGet.config` at the repo root points at the private `eceni-core` feed
(`https://nuget.pkg.github.com/dazzknowles/index.json`). Restoring requires a GitHub personal access token with
`read:packages` scope, supplied via the `ECENI_NUGET_PAT` environment variable:

```bash
export ECENI_NUGET_PAT=<a token with read:packages scope>
dotnet restore
```

CI reads the same variable from the `ECENI_NUGET_PAT` repository secret.

## Build

```bash
dotnet build
dotnet test tests/SolarOptimiser.Domain.Tests
dotnet test tests/SolarOptimiser.Providers.FoxESS.Tests
dotnet test tests/SolarOptimiser.Collection.Tests
```

`SolarOptimiser.Persistence.Tests` and `SolarOptimiser.Host.Tests` are integration tests against a live MariaDB and
are not run in CI.
