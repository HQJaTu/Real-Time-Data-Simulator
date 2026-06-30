# Real-Time Data Simulator
Windows desktop app **and** command-line tool (`rtdsim`) that generate dynamic data from a JSON template and stream it to Azure messaging and analytics targets: **Azure Event Hubs**, **Azure Service Bus**, and **Azure Data Explorer (Kusto) / Microsoft Fabric Eventhouse**.

# Screenshot
<img src='.\media\real-time-data-simulator-ver-0.6-main-screen.png'>

# Main Features
- Multiple target [connectors](#connectors): Azure Event Hubs, Azure Service Bus, and Azure Data Explorer (Kusto) / Fabric Eventhouse
- Desktop GUI and a scriptable [CLI](#cli) (`rtdsim`)
- Configurable size of workload (amount of messages to be sent) and parallelism
- Easily editable JSON payload
- Load/Save payload as a file locally
- Runtime evaluation of pre-defined variables
- Runtime evaluation of expression in C# (slower)
- Built-in expression helpers (e.g. random strings, sub-second timestamps)
- Microsoft Entra ID (Interactive Browser) Authentication

# Scenarios
1. Generate dynamic data from a JSON template and send it to a **Microsoft Fabric EventStream** custom endpoint (Event Hubs-compatible).
2. Generate dynamic data and send it to an **Azure Event Hub**.
3. Generate dynamic data and send it to an **Azure Service Bus** queue or topic (e.g. to load-test consumers or messaging pipelines).
4. Generate dynamic data and ingest it into an **Azure Data Explorer (Kusto)** table or a **Microsoft Fabric Eventhouse** for KQL analytics / dashboards.

# Connectors
The simulator can send generated load to several targets. Pick the connector in the GUI (**Destination → Service**) or with `--target` on the CLI; each connector declares its own parameters.

| Connector | `--target` | Sends to | Authentication |
|--|--|--|--|
| [Azure Event Hubs](#azure-event-hubs) | `eventhubs` | An event hub (incl. Fabric EventStream custom endpoints) | SAS connection string **or** Microsoft Entra ID |
| [Azure Service Bus](#azure-service-bus) | `servicebus` | A queue or topic | SAS connection string **or** Microsoft Entra ID |
| [Azure Data Explorer / Kusto — Streaming](#azure-data-explorer-kusto--fabric-eventhouse) | `kusto-streaming` | A database table (incl. Fabric Eventhouse) | Microsoft Entra ID |
| [Azure Data Explorer / Kusto — Queued](#azure-data-explorer-kusto--fabric-eventhouse) | `kusto-queued` | A database table (incl. Fabric Eventhouse) | Microsoft Entra ID |

Two authentication styles are supported by the messaging connectors:
- **SAS connection string** — paste the full `Endpoint=sb://...;SharedAccessKey=...` string into the connection field. No sign-in needed.
- **Microsoft Entra ID** — click **Azure: Sign in** in the GUI (Interactive Browser). The connection field can then be just the fully-qualified namespace (`<namespace>.servicebus.windows.net`); a full connection string is also accepted, the host is extracted from it. The sign-in is **cached across runs** in an encrypted token cache (DPAPI), so you only log in once — until the refresh token expires. While signed in, the button shows **Azure: Sign out** (hover shows the signed-in user); clicking it (or **File → Azure: Sign out**) clears the cached sign-in. (The CLI uses `DefaultAzureCredential`, which reuses your `az login` session, so it also doesn't prompt per run.)

## Azure Event Hubs
Sends each generated message as an Event Hubs event (batched).

| Parameter | GUI field | CLI | Description |
|--|--|--|--|
| `connection` | Connection string / Namespace | `-p connection=` | SAS connection string, or the namespace when using Entra ID |
| `eventHub` | Event Hub name | `-p eventHub=` | Target event hub (entity) name |

**Connection string (SAS):**
```
Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<policy>;SharedAccessKey=<key>
```
Set **Event Hub name** to the hub you are sending to.

**Microsoft Fabric (EventStream custom endpoint):** the Fabric portal's *custom endpoint → SAS key* view gives a connection string that already contains an `EntityPath`, e.g.
```
Endpoint=sb://<...>.servicebus.windows.net/;SharedAccessKeyName=key_***;SharedAccessKey=***;EntityPath=es_5c952fa4-***
```
Put that same `es_***` value in the **Event Hub name** field (it must match the `EntityPath`).

**CLI example:**
```powershell
rtdsim --target eventhubs `
  -p connection="Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=***" `
  -p eventHub=my-hub `
  --template .\payload.json --messages 100
```

## Azure Service Bus
Sends each generated message to a queue or topic (batched).

| Parameter | GUI field | CLI | Description |
|--|--|--|--|
| `connection` | Connection string / Namespace | `-p connection=` | SAS connection string, or the namespace when using Entra ID |
| `entity` | Queue / Topic name | `-p entity=` | Target queue or topic name |

**Connection string (SAS):**
```
Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<policy>;SharedAccessKey=<key>
```
Set **Queue / Topic name** to the destination entity. A namespace-level policy (e.g. `RootManageSharedAccessKey`) can send to any entity; an entity-scoped policy's connection string contains `;EntityPath=<entity>` and only works for that entity.

**CLI example:**
```powershell
rtdsim --target servicebus `
  -p connection="Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=send;SharedAccessKey=***" `
  -p entity=my-queue `
  --template .\payload.json --messages 100
```

## Azure Data Explorer (Kusto) / Fabric Eventhouse
Ingests each batch into a database **table** as `multijson`. Two ingestion modes are available as separate connectors:

| `--target` | GUI service | Ingestion | Setup required |
|--|--|--|--|
| `kusto-streaming` | Azure Data Explorer — Streaming ingestion | [Streaming](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/management/streaming-ingestion-policy) — low latency, sends to the engine endpoint | **Streaming ingestion policy must be enabled** on the cluster and target table |
| `kusto-queued` | Azure Data Explorer — Queued ingestion | [Queued](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/api/netfx/about-kusto-ingest#queued-ingestion) — batched server-side, sends to the `ingest-` data-management endpoint | **None** — works out of the box |

Use **streaming** for near-real-time delivery; use **queued** when you don't want to (or can't) enable the streaming policy, e.g. for throughput measurements on a stock cluster. Both take the same parameters:

| Parameter | GUI field | CLI | Description |
|--|--|--|--|
| `clusterUri` | Cluster URI | `-p clusterUri=` | The cluster's query/engine URI (see below) |
| `database` | Database | `-p database=` | Target database name |
| `table` | Table | `-p table=` | Target table name |

**Cluster URI** — provide the query/engine URI for **both** connectors; the queued connector derives the `ingest-` data-management endpoint automatically (and either connector also accepts a URI already in the other form):
- **Azure Data Explorer:** `https://<cluster>.<region>.kusto.windows.net`
- **Microsoft Fabric Eventhouse:** the **Query URI** shown on the Eventhouse / KQL Database page (form `https://<...>.kusto.fabric.microsoft.com`).

**Authentication — Microsoft Entra ID only:**
- **GUI:** click **Azure: Sign in** before running.
- **CLI:** uses the ambient credential chain (`DefaultAzureCredential`) — e.g. `az login`, environment variables, or a managed identity.

The identity needs at least **Table/Database Ingestor** rights on the target.

**Payload format, schema & mappings:** messages are ingested as `multijson`. Without an ingestion mapping, Kusto maps JSON **by property name (case-sensitive)** to columns of the same name — any property that doesn't match a column is dropped, and any column without a matching property is set to **null**. So if your field names don't line up with the table, ingestion *succeeds* but every row is **blank/null** (this is not an ingestion failure, so `--verify-ingestion` won't flag it). Two ways to get real data in:

1. **Match the names** — make each JSON property name equal the target column name, e.g.
   ```json
   { "StringColumn": "test-{{$RandomString(6)}}", "EventTime": "{{$DateTime.UtcNow.ToString("O")}}" }
   ```
2. **Use an ingestion mapping** — create a JSON mapping on the table and pass its name via the optional **Ingestion mapping** field (`-p mapping=<name>`). This lets arbitrary field names map to the intended columns:
   ```kusto
   .create table MyTable ingestion json mapping "MyMapping"
   '[{"column":"Fuel","Properties":{"Path":"$.FuelType"}}, {"column":"Gen","Properties":{"Path":"$.Generation"}}]'
   ```

**Prerequisite (streaming only) — enable streaming ingestion** on both the database/cluster policy and the target table, otherwise `kusto-streaming` ingestion fails. `kusto-queued` needs no such setup.
```kusto
.alter table <table> policy streamingingestion enable
```

**Error handling & verification.** Payloads are checked to be valid JSON before sending (a malformed template fails immediately with a clear error). For **streaming**, a schema/data failure is reported by the service on the send call and surfaced as an error. For **queued**, ingestion happens asynchronously server-side, so a schema mismatch is *not* visible at send time — the run can report success while rows are rejected. To catch that, pass `--verify-ingestion` (CLI) or tick **Verify ingestion** (GUI): after the run it queries `.show ingestion failures` for the target table and reports any failures. Queued failures can lag, so re-check later if in doubt:
```kusto
.show ingestion failures | where Table == "<table>" | order by FailedOn desc
```

**CLI examples:**
```powershell
# Streaming ingestion (requires the streaming policy)
rtdsim --target kusto-streaming `
  -p clusterUri=https://mycluster.westeurope.kusto.windows.net `
  -p database=Telemetry -p table=Events `
  --template .\payload.json --messages 100 --parallelism 3

# Queued ingestion (no setup required)
rtdsim --target kusto-queued `
  -p clusterUri=https://mycluster.westeurope.kusto.windows.net `
  -p database=Telemetry -p table=Events `
  --template .\payload.json --messages 100 --parallelism 3
```

# Tokens
The biggest advantage of the application is the ability to generate value for the tokens listed in the PAYLOAD definition. Data generation takes place on the fly for each message prepared for sending.

## Variables
Format of a variable: `{{VariableName}}`  
Variables are **defined in a TOML file** (see [Variable definitions](#variable-definitions-toml)), so you can add, remove or retune them without rebuilding. The built-in defaults are:

|Variable | Generates |
|--|--|
| `{{UserId}}` | Numeric (int) value from range 5000-5100 |
| `{{ProductId}}` | Numeric (int) value from range 700-999 |
| `{{Device}}` | Random item from array of: mobile,tablet,pc |
| `{{DateTime.Now}}` | Current date & time (now) in [round-trip ("O") format](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#Roundtrip).  |
| `{{FuelType(MessageIndex)}}` | Value selected on `MessageIndex` from list: BIOMASS,CCGT,COAL,INTELEC,INTEW,INTFR,INTIFA2,INTIRL,INTNED,INTNEM,INTNSL,INTVKL,NPSHYD,NUCLEAR,OCGT,OIL,OTHER,PS,WIND |
| `{{SettlementPeriod}}` | Hardcoded Int value: `48` |

### Variable definitions (TOML)
Definitions live in a `variables.toml` file shipped next to the executable. Each **table** defines one variable; the table name is the token (quote names that contain dots or parentheses, e.g. `["DateTime.Now"]`).

```toml
[UserId]
type = "randomInt"     # integer in [min, max) — max exclusive
min = 5000
max = 5100

[Device]
type = "randomItem"    # a random element of items
items = ["mobile", "tablet", "pc"]

["FuelType(MessageIndex)"]
type = "indexedItem"   # items[messageIndex % count]
items = ["BIOMASS", "CCGT", "COAL"]

["DateTime.Now"]
type = "dateTimeNow"   # current local time; optional `format` (default "O")

[SettlementPeriod]
type = "literal"       # the fixed string `value`
value = "48"
```

| type | Parameters | Produces |
|--|--|--|
| `randomInt` | `min`, `max` | Random integer in `[min, max)` (max exclusive) |
| `randomItem` | `items` | A random element of `items` |
| `indexedItem` | `items` | `items[messageIndex % items.Count]` |
| `dateTimeNow` | `format` (optional, default `"O"`) | Current local date/time |
| `literal` | `value` | The fixed string `value` |

**How the file is resolved:** the apps load `--variables <file>` if given (CLI) or a file chosen via **File → Load Variable Definitions...** (GUI); otherwise `variables.toml` next to the executable; otherwise the built-in defaults.

## C# Expressions
Format of an expression: `{{$Expression}}`  
Expressions are **slower** in generating values, but more flexible. Use can use any C# compatible code from preloaded namespaces (for example: `System`).
Expressions are being evaluated using [Microsoft.CodeAnalysis.CSharp.Scripting](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md).

   
|Expression | Generates |
|--|--|
| `{{$DateTime.Now}}` | Current date & time (now) |
| `{{$new Random().Next(100,200)}}` | Random numeric (int) value from range 100-199 |
| `{{$DateTime.Now.AddSeconds(-5).ToString("s")+"Z"}}` | Date & time 5 seconds ago in [Sortable date/time pattern](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#Sortable) with 'Z' at the end. |
| `{{$DateTime.Now.ToString("dd/MM/yyyy")}}` | Current date & time formatted to `31/12/2020` |
| `{{$DateTime.Now.Hour*2+Math.Floor(DateTime.Now.Minute/30d)}}` | Calculation based on Current Time  |
| `{{$DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")+"Z"}}` | UTC timestamp with **microsecond** precision (see [Sub-second timestamps](#sub-second-timestamps)) |
| `{{$RandomString(10)}}` | 10-character random alphanumeric string (see [Expression helpers](#expression-helpers)) |

## Expression helpers
In addition to the `System` namespace, expressions can call these built-in helper functions.

### RandomString
`RandomString(length, charset?)` generates a random string.

| Argument | Required | Description |
|--|--|--|
| `length` | yes | Number of characters to generate. `0` (or less) yields an empty string. |
| `charset` | no | The set of characters to choose from. Defaults to `A–Z`, `a–z`, `0–9`. |

| Expression | Generates |
|--|--|
| `{{$RandomString(8)}}` | 8 random alphanumeric characters, e.g. `wD4QUgYn` |
| `{{$RandomString(6, "ABCDEF0123456789")}}` | 6 random hex characters, e.g. `79DEF1` |
| `id-{{$RandomString(4)}}` | Composes with literals, e.g. `id-LxRU` |

> **Note:** each *distinct* expression is evaluated once per message, and all identical occurrences are replaced with the same value. To emit two independent random values in one message, make the tokens differ slightly — e.g. `{{$RandomString(8)}}` and `{{$RandomString( 8)}}`.

## Sub-second timestamps
The sortable format `"s"` has **no** fractional seconds. For higher resolution, use custom [date/time format specifiers](https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings) — `ffffff` = microseconds (6 digits), `fffffff` = 100-nanosecond ticks (7 digits, .NET's maximum):

| Expression | Generates |
|--|--|
| `{{$DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")+"Z"}}` | `2026-07-01T09:20:15.123456Z` (microseconds) |
| `{{$DateTime.UtcNow.ToString("O")}}` | `2026-07-01T09:20:15.1234567Z` (round-trip, 100-ns ticks) |

> **Use `DateTime.UtcNow` (not `DateTime.Now`) when appending `Z`.** `DateTime.Now` returns *local* time; tagging it with `Z` mislabels it as UTC. Also note that the actual precision is bounded by the OS clock (~1–15 ms on Windows), so trailing microsecond digits may be padding rather than distinct values.

# CLI

## Usage
```text
rtdsim - Real-Time Data Simulator CLI

Generates and sends load to a target service.

Usage:
  rtdsim --template <file> [--target <kind>] [--param key=value ...] [options]

Required:
  -t, --template <file>           Path to the message template file.

Options:
      --target <kind>             Target service (default 'eventhubs').
  -p, --param key=value           Connector parameter (repeatable). See per-target list below.
      --variables <file>          Variable definitions TOML (default: variables.toml next to the exe).
      --parallelism <n>           Number of concurrent senders (default 1).
      --batches <n>               Batches per sender (default 1).
      --messages <n>              Messages per batch (default 1).
      --wait <seconds>            Seconds to wait between batches (default 0).
      --verify-ingestion          After the run, report Kusto ingestion failures (queued targets).
  -h, --help                      Show this help.

Targets and their parameters:
  --target eventhubs    (Event Hubs)
      -p connection=<value>       Connection string / Namespace [required]
          SAS connection string, or the fully-qualified namespace when signed in with Azure Identity.
      -p eventHub=<value>         Event Hub name [required]
  --target servicebus   (Azure Service Bus)
      -p connection=<value>       Connection string / Namespace [required]
          SAS connection string, or the fully-qualified namespace when signed in with Azure Identity.
      -p entity=<value>           Queue / Topic name [required]
  --target kusto-streaming (Azure Data Explorer - Streaming ingestion)
      -p clusterUri=<value>       Cluster URI [required]
          Engine/query cluster URI. Streaming ingestion must be enabled on the cluster and target table.
      -p database=<value>         Database [required]
      -p table=<value>            Table [required]
  --target kusto-queued (Azure Data Explorer - Queued ingestion)
      -p clusterUri=<value>       Cluster URI [required]
          Engine/query cluster URI (the 'ingest-' data-management endpoint is derived automatically). No streaming policy required.
      -p database=<value>         Database [required]
      -p table=<value>            Table [required]
```

## Example CLI run
```text
& rtdsim.exe --target kusto-streaming `
  -p clusterUri=https://mycluster.westeurope.kusto.windows.net `
  -p database=test `
  -p table=test `
  --template .\test.template `
  --messages 100 --parallelism 3
Target     : Azure Data Explorer - Streaming ingestion
Cluster URI : https://mycluster.westeurope.kusto.windows.net
Database    : test
Table       : test
Parallelism: 3
Batches    : 1 per sender (3 total)
Messages   : 100 per batch (300 total)
Wait time  : 0s between batches

Batches: 2/3 | Messages: 200 | TPS: 61.69 | 36,600 bytes (0.06 Mbps)

Completed: sent 300 messages in 3 batches.
Total time: 00:00:03.2427018 | TPS: 92.52 | 36,600 bytes (0.09 Mbps)
```

# References
- [Get a custom endpoint without creating an EventHub for EventStreams](https://www.youtube.com/watch?v=ftb2nN3eukg)  
- [Azure Service Bus connection strings & SAS](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas)
- [Azure Data Explorer — streaming ingestion policy](https://learn.microsoft.com/en-us/azure/data-explorer/kusto/management/streaming-ingestion-policy)
- [Microsoft Fabric Eventhouse overview](https://learn.microsoft.com/en-us/fabric/real-time-intelligence/eventhouse)
- Similar project: [Mockingbird](https://www.tinybird.co/blog-posts/mockingbird-announcement-mock-data-generator)

# Release Notes
New features, bug fixes and changes [can be found here](changelog.md).