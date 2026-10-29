VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "Engine"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = True
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
Attribute VB_Description = "RateCore rating engine. Create one per thread and call Initialise once."
Option Explicit

' Public interface (binary compatible since 4.0, 2011). Keep the order of the public members:
' callers bind to the vtable. New members go at the end.

Private m_sLastError As String

Public Function Initialise(ByVal TablesPath As String) As Boolean
    If TablesLoaded Then
        Initialise = True
        Exit Function
    End If
    Initialise = LoadTables(TablesPath)
    If Not Initialise Then m_sLastError = LastTableError
End Function

Public Property Get Version() As String
    Version = CStr(App.Major) & "." & CStr(App.Minor) & "." & CStr(App.Revision)
End Property

Public Property Get LastError() As String
    LastError = m_sLastError
End Property

Public Function NewRisk(ByVal Product As String, ByVal EffectiveDate As Date) As Risk
    Dim v As VersionRow
    If Not TablesLoaded Then RaiseError ERR_NOT_INITIALISED, "Engine.NewRisk", "Initialise has not been called."
    If Not ProductExists(Product) Then RaiseError ERR_UNKNOWN_PRODUCT, "Engine.NewRisk", "Unknown product " & Product
    If Not FindVersion(Product, EffectiveDate, v) Then
        RaiseError ERR_NO_TABLE_VERSION, "Engine.NewRisk", "No " & Product & " tables for " & Format$(EffectiveDate, "dd/mm/yyyy")
    End If
    Set NewRisk = New Risk
    NewRisk.Init Product, v.Version, EffectiveDate
End Function

Public Function TableVersion(ByVal Product As String, ByVal EffectiveDate As Date) As String
    Dim v As VersionRow
    If FindVersion(Product, EffectiveDate, v) Then TableVersion = v.Product & "-" & v.Version
End Function

Public Sub Terminate()
    ' Tables stay cached for the thread; nothing to release per engine.
    LogLine "Engine terminated"
End Sub
