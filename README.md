# CrossApp

## Getting Started

### Prerequisites

- .NET SDK 10.0
- Git

### Build & Run

```bash
dotnet build
dotnet run --project src/Cli
```

#### Publish Size Comparison

| RID | Mode | Size publish | runtime need |
|-----|-------|-----------------|-------------------|
| linux-x64 | self-contained | 80M | no |
| linux-x64 | framework-dependent | 132K | yes (.NET 10) |
| win-x64 | self-contained | 77M | no |
| win-x64 | framework-dependent | 212K | yes (.NET 10) |

**Self-contained** publish bundles the entire .NET runtime along with the
app, so it's significantly larger but can run on a machine with no .NET
installed. **Framework-dependent** publish contains only the app's own code
and dependencies, is much smaller, but requires a compatible .NET Runtime
already installed on the target machine.

## Environment

- .NET SDK 10.0
- Tested on: Omarchy 4.0.2 x64

## Project Structure

```
CrossApp/
├── .git/
├── .gitignore
├── CrossApp.sln
├── README.md
└── src/
    ├── Core/
    │   ├── Core.csproj
    │   └── EnvironmentInfo.cs
    └── Cli/
        ├── Cli.csproj
        └── Program.cs
```

- `Core/Dto/` — data transfer record types (week 3): `ProductDto`,
  `BookDto`, `OrderDto`
- `Core/Domain/` — entities with behavior and invariants (week 4)
- `Core/Storage/` — storage implementations (week 5)