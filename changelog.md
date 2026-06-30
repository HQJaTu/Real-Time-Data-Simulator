# Changelog - Real-Time-Data-Simulator

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]
### Added
* Kusto connectors: optional **Ingestion mapping** parameter (`-p mapping=<name>`) referencing a JSON ingestion mapping on the table. Without a mapping, Kusto maps JSON to columns by name (case-sensitive), so mismatched field names silently ingest as null/blank rows; a mapping lets arbitrary field names land in the right columns.
* Kusto ingestion error handling: payloads are validated as JSON before sending (malformed templates fail fast), and synchronously-reported ingestion failures (e.g. streaming schema mismatches) now surface as errors instead of being ignored. Added an opt-in post-run check for asynchronous (queued) failures — `--verify-ingestion` (CLI) / **Verify ingestion** (GUI) — which queries `.show ingestion failures` and reports them.
* GUI: **persistent Microsoft Entra ID sign-in**. The interactive sign-in is now stored in an encrypted, on-disk token cache with a saved authentication record, so it is silently reused across app runs (until the refresh token expires). The **Azure: Sign in** button toggles to **Azure: Sign out** while signed in (showing the signed-in user on hover), and **File → Azure: Sign out** does the same — clearing the cached sign-in.
* Azure Data Explorer (Kusto) **queued ingestion** connector (`--target kusto-queued`) — batched server-side ingestion that needs no streaming policy, so it works against any cluster/table without setup. Automatically targets the `ingest-` data-management endpoint.
* Soft-coded variable definitions: the built-in variables (`UserId`, `ProductId`, `Device`, `DateTime.Now`, `FuelType(MessageIndex)`, `SettlementPeriod`) are now defined in a `variables.toml` file instead of being hardcoded. Each variable is a TOML table named by its token, with a `type` of `randomInt`, `randomItem`, `indexedItem`, `dateTimeNow` or `literal`.
* GUI: **File → Load Variable Definitions...** to load a custom definitions file at runtime.
* CLI: `--variables <file>` option to use a custom definitions file.

### Changed
* Renamed the streaming Kusto connector to `--target kusto-streaming` (was `--target kusto`). **Breaking:** `--target kusto` no longer resolves.
* Variable definitions are resolved from `--variables` / the GUI menu, then a `variables.toml` shipped next to the executable, then the built-in embedded defaults.

## [0.5.2] - 2024-10-28
* Fixed: Reset `_TotalSizeInBytes` counter before each run to avoid overpriced Mbps after the first run

## [0.5.1] - 2024-10-24
* Transfer now shows in Mbps (Megabits per second)

## [0.5.0] - 2024-10-23
* Added example payloads from https://docs.bond.tech/docs/transaction-payloads #16
* Added transfer data speed to bottom status pane

## [0.4.0] - 2024-06-24
### Features
* Microsoft Entra ID (Interactive Browser) Authentication #14
* Added ABOUT APP window, icons & "Test connection" button

## [0.3.0] - 2024-06-24
### Features
* Fix loading of Form Designer
* Add cancel button
* Add exception display 
* Visual tweaks to progress bar

## [0.2.0] - 2024-06-21
### Features
* Added reuse of compiled C# expressions from message payload, repeated use of an expression executes faster
* Limit use to single EventSender and Single EventHubProducerClient across all threads
* Updated completion logic that requires all threads to complete before updating UI
* Some tweaks to reduce locking of UI during execution

## [0.1.0] - 2024-06-07
* First release with basic features.
