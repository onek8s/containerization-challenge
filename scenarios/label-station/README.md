# Label Station

Desktop tool used at the packing desks to print shipping labels. The operator enters the order
number, carrier, recipient and parcel count, previews the label and prints one label per parcel
to the label printer attached to the packing station.

- Settings (printer, label size, default carrier, station ID) are stored per user in
  `HKEY_CURRENT_USER\Software\Northwind\LabelStation` and edited from **File > Settings**.
- Every printed label is written to the Windows **Application** event log (source `Label Station`)
  and appended to `%LOCALAPPDATA%\Northwind\LabelStation\print-history.csv`.

## Requirements

- Windows 10/11 or Windows Server with Desktop Experience
- .NET 8 SDK (to build) or .NET 8 Desktop Runtime (to run)
- A label printer installed in Windows (100 x 150 mm labels by default)

## Build

```powershell
dotnet build LabelStation.sln -c Release
```

## Run

```powershell
dotnet run --project src/LabelStation -c Release
```

To register the event log source once (requires an elevated PowerShell):

```powershell
New-EventLog -LogName Application -Source "Label Station"
```

Keyboard: **F9** prints the labels for the current order.
