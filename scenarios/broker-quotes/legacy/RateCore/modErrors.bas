Attribute VB_Name = "modErrors"
Option Explicit

' Error numbers raised to callers: vbObjectError + 512 + n
Public Const ERR_BASE As Long = vbObjectError + 512
Public Const ERR_NOT_INITIALISED As Long = ERR_BASE + 1
Public Const ERR_UNKNOWN_PRODUCT As Long = ERR_BASE + 2
Public Const ERR_NO_TABLE_VERSION As Long = ERR_BASE + 3
Public Const ERR_UNKNOWN_FIELD As Long = ERR_BASE + 4
Public Const ERR_UNKNOWN_SECTION As Long = ERR_BASE + 5
Public Const ERR_TABLES_LOCKED As Long = ERR_BASE + 6
Public Const ERR_DISTRICT_NOT_FOUND As Long = ERR_BASE + 7
Public Const ERR_TRADE_NOT_FOUND As Long = ERR_BASE + 8

' Calculate() results
Public Const CALC_OK As Long = 0
Public Const CALC_REFER As Long = 1
Public Const CALC_DECLINE As Long = 2
Public Const CALC_ERROR As Long = -1

Public Sub RaiseError(ByVal lNumber As Long, ByVal sSource As String, ByVal sText As String)
    LogLine "ERROR " & CStr(lNumber - ERR_BASE) & " " & sSource & ": " & sText
    Err.Raise lNumber, "RateCore." & sSource, sText
End Sub
