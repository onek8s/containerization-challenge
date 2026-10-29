Attribute VB_Name = "modTables"
Option Explicit

' Rate tables. One Access database per product year (BC2023.mdb, BC2024.mdb ...) maintained
' by Pricing, plus ZONES.mdb for postcode districts. Opened read-only with DAO/Jet and cached
' in module variables (per thread: VB6 module data lives in thread local storage).

Public Type TradeRow
    TradeCode As String
    TradeGroup As String
    MDRate As Double          ' per mille on buildings and contents
    StockRate As Double       ' per mille on stock
    BIRate As Double          ' per mille on gross profit
    ELClericalRate As Double  ' per cent of clerical wageroll
    ELManualRate As Double    ' per cent of manual wageroll
    PLRate As Double          ' per mille on turnover
    ReferFlag As Boolean
    DeclineFlag As Boolean
End Type

Public Type VersionRow
    Product As String
    Version As String
    DatabaseFile As String
    EffectiveFrom As Date
    EffectiveTo As Date
End Type

Private m_sPath As String
Private m_dbWorkspace As DAO.Workspace
Private m_colTrades As Collection          ' key: version|trade -> index into m_aTrades
Private m_aTrades() As TradeRow
Private m_lTradeCount As Long
Private m_aVersions() As VersionRow
Private m_lVersionCount As Long
Private m_dicFactors As Scripting.Dictionary ' key: version|factor|key -> value
Private m_bLoaded As Boolean
Private m_sLastError As String

Public Property Get TablesLoaded() As Boolean
    TablesLoaded = m_bLoaded
End Property

Public Function LoadTables(ByVal sPath As String) As Boolean
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Dim sFile As String

    On Error GoTo Failed
    If Right$(sPath, 1) <> "\" Then sPath = sPath & "\"
    m_sPath = sPath
    Set m_dbWorkspace = DBEngine.Workspaces(0)
    Set m_colTrades = New Collection
    Set m_dicFactors = New Scripting.Dictionary
    m_dicFactors.CompareMode = TextCompare
    m_lTradeCount = 0
    m_lVersionCount = 0

    ' VERSIONS.mdb lists which product database covers which dates.
    Set db = m_dbWorkspace.OpenDatabase(sPath & "VERSIONS.mdb", False, True)
    Set rs = db.OpenRecordset("SELECT Product, Version, DatabaseFile, EffectiveFrom, EffectiveTo FROM tblVersion ORDER BY Product, EffectiveFrom", dbOpenSnapshot)
    Do While Not rs.EOF
        m_lVersionCount = m_lVersionCount + 1
        ReDim Preserve m_aVersions(1 To m_lVersionCount)
        With m_aVersions(m_lVersionCount)
            .Product = rs!Product
            .Version = rs!Version
            .DatabaseFile = rs!DatabaseFile
            .EffectiveFrom = rs!EffectiveFrom
            .EffectiveTo = Nz(rs!EffectiveTo, #12/31/2099#)
        End With
        rs.MoveNext
    Loop
    rs.Close
    db.Close

    Dim i As Long
    For i = 1 To m_lVersionCount
        LoadProductDatabase m_aVersions(i)
    Next i

    LoadZones sPath & "ZONES.mdb"
    m_bLoaded = True
    LoadTables = True
    LogLine "Tables loaded from " & sPath & ": " & CStr(m_lVersionCount) & " versions, " & CStr(m_lTradeCount) & " trade rows"
    Exit Function

Failed:
    If Err.Number = 3045 Or Err.Number = 3051 Then
        LastTableError = "Rate tables are locked or not accessible: " & Err.Description
    Else
        LastTableError = "Loading rate tables failed: " & Err.Description & " (" & CStr(Err.Number) & ")"
    End If
    LogLine LastTableError
    LoadTables = False
End Function

Public Property Get LastTableError() As String
    LastTableError = m_sLastError
End Property

Public Property Let LastTableError(ByVal sValue As String)
    m_sLastError = sValue
End Property

Private Sub LoadProductDatabase(ByRef v As VersionRow)
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Dim sKey As String

    Set db = m_dbWorkspace.OpenDatabase(m_sPath & v.DatabaseFile, False, True)

    Set rs = db.OpenRecordset("SELECT * FROM tblTrade", dbOpenSnapshot)
    Do While Not rs.EOF
        m_lTradeCount = m_lTradeCount + 1
        ReDim Preserve m_aTrades(1 To m_lTradeCount)
        With m_aTrades(m_lTradeCount)
            .TradeCode = rs!TradeCode
            .TradeGroup = rs!TradeGroup
            .MDRate = rs!MDRate
            .StockRate = rs!StockRate
            .BIRate = rs!BIRate
            .ELClericalRate = rs!ELClericalRate
            .ELManualRate = rs!ELManualRate
            .PLRate = rs!PLRate
            .ReferFlag = rs!ReferFlag
            .DeclineFlag = rs!DeclineFlag
        End With
        m_colTrades.Add m_lTradeCount, v.Version & "|" & rs!TradeCode
        rs.MoveNext
    Loop
    rs.Close

    ' tblFactor: Factor (e.g. CONSTRUCTION, ROOF, EXCESS, INDEMNITY, PLLIMIT, FLOOD, CRIME, ALARM, CLAIMS), FactorKey, FactorValue
    Set rs = db.OpenRecordset("SELECT Factor, FactorKey, FactorValue FROM tblFactor", dbOpenSnapshot)
    Do While Not rs.EOF
        sKey = v.Version & "|" & rs!Factor & "|" & rs!FactorKey
        If Not m_dicFactors.Exists(sKey) Then m_dicFactors.Add sKey, CDbl(rs!FactorValue)
        rs.MoveNext
    Loop
    rs.Close
    db.Close
End Sub

Public Function FindVersion(ByVal sProduct As String, ByVal dtEffective As Date, ByRef v As VersionRow) As Boolean
    Dim i As Long
    For i = m_lVersionCount To 1 Step -1
        If m_aVersions(i).Product = sProduct And dtEffective >= m_aVersions(i).EffectiveFrom And dtEffective <= m_aVersions(i).EffectiveTo Then
            v = m_aVersions(i)
            FindVersion = True
            Exit Function
        End If
    Next i
End Function

Public Function ProductExists(ByVal sProduct As String) As Boolean
    Dim i As Long
    For i = 1 To m_lVersionCount
        If m_aVersions(i).Product = sProduct Then
            ProductExists = True
            Exit Function
        End If
    Next i
End Function

Public Function FindTrade(ByVal sVersion As String, ByVal sTradeCode As String, ByRef t As TradeRow) As Boolean
    Dim lIndex As Long
    On Error Resume Next
    lIndex = m_colTrades(sVersion & "|" & sTradeCode)
    If Err.Number <> 0 Then
        Err.Clear
        Exit Function
    End If
    On Error GoTo 0
    t = m_aTrades(lIndex)
    FindTrade = True
End Function

Public Function Factor(ByVal sVersion As String, ByVal sFactor As String, ByVal sKey As String, Optional ByVal dDefault As Double = 1#) As Double
    Dim sFull As String
    sFull = sVersion & "|" & sFactor & "|" & sKey
    If m_dicFactors.Exists(sFull) Then
        Factor = m_dicFactors(sFull)
    Else
        Factor = dDefault
    End If
End Function

Private Function Nz(ByVal v As Variant, ByVal vDefault As Variant) As Variant
    If IsNull(v) Then Nz = vDefault Else Nz = v
End Function
