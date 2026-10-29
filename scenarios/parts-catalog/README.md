# Parts Catalog

Spare parts register used by the maintenance stores. Web UI for browsing and
editing parts, plus a small JSON API used by the work order system to look up
and adjust stock.

## Requirements

- .NET 8 SDK

## Build and test

```
dotnet build
dotnet test
```

## Run

```
cd src/PartsCatalog
dotnet run
```

The site listens on http://localhost:5080. On first start the database
`App_Data/catalog.db` is created and filled with sample parts. Application logs
are written to the `logs` folder.

## Configuration

`src/PartsCatalog/appsettings.json`:

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:Catalog` | SQLite connection string |
| `Catalog:DataFolder` | Folder created at startup for the database file |
| `Catalog:LogFolder` | Folder for daily log files |
| `Catalog:CompanyName` | Shown in the page footer |

## API

| Method | Path | Description |
| --- | --- | --- |
| GET | `/api/parts?q=&category=&reorder=` | Search parts |
| GET | `/api/parts/{partNumber}` | Get one part |
| POST | `/api/parts/{partNumber}/adjust` | Adjust stock, body `{ "delta": -2, "reason": "WO-1234" }` |
| GET | `/ping` | Returns `OK` |
