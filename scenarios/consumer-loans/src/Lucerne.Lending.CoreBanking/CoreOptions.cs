namespace Lucerne.Lending.CoreBanking;

public sealed class CoreOptions
{
    public const string Section = "Core";

    /// <summary>IbmI (production) or Simulator (development, training).</summary>
    public string Provider { get; set; } = "IbmI";

    /// <summary>IBM i system name as configured in IBM i Access Client Solutions.</summary>
    public string SystemName { get; set; } = "core.corp.local";

    /// <summary>Library holding the lending programs and SQL procedures.</summary>
    public string LendingLibrary { get; set; } = "LNLIB";

    /// <summary>Customer master library.</summary>
    public string CustomerLibrary { get; set; } = "CIFLIB";

    public string EventQueueLibrary { get; set; } = "LNLIB";
    public string EventQueue { get; set; } = "LNEVTQ";

    /// <summary>Kerberos sign-on of the process identity; when false UserId/Password are used.</summary>
    public bool UseKerberos { get; set; } = true;
    public string? UserId { get; set; }
    public string? Password { get; set; }

    public string BranchCode { get; set; } = "0901";
    public int ProgramCallTimeoutSeconds { get; set; } = 30;
    public int MaxConnections { get; set; } = 4;

    /// <summary>ODBC connection string for SQL access (customer exposure, reference data).</summary>
    public string OdbcConnectionString { get; set; } =
        "Driver={IBM i Access ODBC Driver};System=core.corp.local;Signon=4;Naming=1;DefaultLibraries=LNLIB,CIFLIB,RATLIB;CCSID=1208;TranslateBinary=1;CommitMode=0;";

    /// <summary>OLE DB connection string (payment history, used by collections).</summary>
    public string OleDbConnectionString { get; set; } =
        "Provider=IBMDA400;Data Source=core.corp.local;Default Collection=LNLIB;Convert Date Time To Char=FALSE;Catalog Library List=LNLIB,CIFLIB;";

    /// <summary>Simulator database file (Provider=Simulator only).</summary>
    public string SimulatorDatabase { get; set; } = "App_Data/core-simulator.db";
}
