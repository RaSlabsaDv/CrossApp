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

| RID | Mode | Size publish | Runtime |
|-----|-------|-----------------|-------------------|
| linux-x64 | self-contained | 80M | no |
| linux-x64 | framework-dependent | 132K | yes (.NET 10) |
| win-x64 | self-contained | 77M | no |
| win-x64 | framework-dependent | 212K | yes (.NET 10) |

#### Self-contained publish bundles the entire .NET runtime together with the application code, so it takes up significantly more space, but it doesn't require .NET to be installed on the target machine — it can run out of the box.

#### Framework-dependent publish contains only the application's own code and dependencies, making it much smaller, but it requires a compatible .NET Runtime to already be installed on the user's machine.

---

#### Additional publish options Comparison

| RID | Mode | Size publish | Runtime |
|-----|-------|-----------------|---------|
| linux-x64 | self-contained + SingleFile | 74M | no |
| linux-x64 | self-contained + Trimmed | 24M | no |

### Multi-targeting Build Note (optional task)

`Core/EnvironmentInfo.cs` uses conditional compilation to show which
TFM the assembly was built for:

```csharp
#if NET10_0_OR_GREATER
    private const string BuildNote = "збірка під net10.0";
#else
    private const string BuildNote = "збірка під net9.0";
#endif
```

Verified with:

```bash
dotnet run --project src/Cli -f net9.0
dotnet run --project src/Cli -f net10.0
```

Each run prints a different `Примітка збірки` line, confirming the
conditional compilation works per-TFM.


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