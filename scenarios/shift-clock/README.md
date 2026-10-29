# Shift Clock

Time and attendance system for the plant gates. Each gate PC has an RFID badge reader connected to a
serial port. Employees swipe their badge when arriving and leaving; the **Shift Clock** Windows
service records the punch, beeps the reader to confirm and writes an entry to the Windows
**Application** event log (source `Shift Clock`).

Shift supervisors use the **Shift Clock Console** on the gate PC to review punches, add missed
punches, delete wrong ones and export the weekly hours file for payroll.

## Components

| Project | Description |
| --- | --- |
| `src/ShiftClock.Service` | Windows service: badge reader on the serial port, punch storage, supervisor pipe |
| `src/ShiftClock.Console` | Windows Forms supervisor console |
| `src/ShiftClock.Shared` | Badge frame parser, punch rules and the console/service protocol |
| `tests/ShiftClock.Tests` | Unit tests |
| `deploy` | Install script and sample employee list |

## Requirements

- Windows 10/11 or Windows Server 2019 or later, joined to the `NORTHWIND` domain
- .NET 8 SDK (to build) or .NET 8 Desktop Runtime (to run)
- RFID badge reader (125 kHz, ASCII output) on a serial port, 9600 baud 8N1, default `COM3`
- Domain groups `NORTHWIND\Shift Supervisors` (corrections and payroll export) and
  `NORTHWIND\Shift Leads` (read only access to the console)
- Write access for supervisors to the payroll share `\\fs01\payroll$\timeclock`

## Configuration

Settings are in the `ShiftClock` section of `appsettings.json` in the service folder; the install
script writes gate specific values to `appsettings.Production.json`.

| Setting | Default | Description |
| --- | --- | --- |
| `Terminal` | computer name | Gate name stored on each punch |
| `PortName` / `BaudRate` | `COM3` / `9600` | Badge reader port |
| `DataDirectory` | `%ProgramData%\Northwind\ShiftClock` | Punch files and `employees.csv` |
| `SupervisorGroup` | `NORTHWIND\Shift Supervisors` | May add/delete punches and export payroll |
| `ViewerGroup` | `NORTHWIND\Shift Leads` | May open the console |
| `PayrollExportPath` | `\\fs01\payroll$\timeclock` | Folder for payroll export files |
| `DuplicateWindowSeconds` | `90` | Repeated swipes within this time are ignored |
| `MaxShiftHours` | `16` | Open shifts older than this are treated as a missed clock-out |

Employees are read from `employees.csv` in the data directory (semicolon separated:
`BadgeId;EmployeeNumber;Name;CostCenter`). HR replaces the file nightly; changes are picked up
automatically. Punches are stored per month in `punches\yyyy-MM.jsonl`.

## Build

```powershell
dotnet build ShiftClock.sln -c Release
dotnet test ShiftClock.sln -c Release
```

## Install

From an elevated PowerShell on the gate PC:

```powershell
.\deploy\install-service.ps1 -Terminal GATE-NORTH -PortName COM3
```

This publishes both applications to `C:\Program Files\Northwind\ShiftClock`, registers the event log
source, creates the `ShiftClock` service (LocalSystem, automatic start) and puts a console shortcut on
the public desktop.

## Run for development

```powershell
# service in the foreground (elevated, so the event source and pipe ACL can be created)
dotnet run --project src/ShiftClock.Service

# console
dotnet run --project src/ShiftClock.Console
```
