namespace ShiftClock.Service;

public sealed class ShiftClockOptions
{
    public const string Section = "ShiftClock";

    /// <summary>Name printed on punches, normally the gate name.</summary>
    public string Terminal { get; set; } = Environment.MachineName;

    public string PortName { get; set; } = "COM3";
    public int BaudRate { get; set; } = 9600;

    /// <summary>Byte sent back to the reader to sound the accept beep.</summary>
    public byte AcceptBeep { get; set; } = 0x06;

    /// <summary>Byte sent back to the reader to sound the reject beep.</summary>
    public byte RejectBeep { get; set; } = 0x15;

    public string DataDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Northwind", "ShiftClock");

    /// <summary>Windows group whose members may correct punches and run payroll exports.</summary>
    public string SupervisorGroup { get; set; } = @"NORTHWIND\Shift Supervisors";

    /// <summary>Windows group whose members may connect to the console at all (read only).</summary>
    public string ViewerGroup { get; set; } = @"NORTHWIND\Shift Leads";

    /// <summary>UNC folder the payroll system picks export files up from.</summary>
    public string PayrollExportPath { get; set; } = @"\\fs01\payroll$\timeclock";

    public int DuplicateWindowSeconds { get; set; } = 90;
    public int MaxShiftHours { get; set; } = 16;
}
