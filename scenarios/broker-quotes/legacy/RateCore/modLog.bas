Attribute VB_Name = "modLog"
Option Explicit

' Optional trace file. Set HKLM\SOFTWARE\Humongous\RateCore\TraceFile (REG_SZ) to switch on.
' 32-bit registry view: HKLM\SOFTWARE\WOW6432Node\Humongous\RateCore on 64-bit Windows.

Private Declare Function RegOpenKeyEx Lib "advapi32.dll" Alias "RegOpenKeyExA" (ByVal hKey As Long, ByVal lpSubKey As String, ByVal ulOptions As Long, ByVal samDesired As Long, phkResult As Long) As Long
Private Declare Function RegQueryValueEx Lib "advapi32.dll" Alias "RegQueryValueExA" (ByVal hKey As Long, ByVal lpValueName As String, ByVal lpReserved As Long, lpType As Long, ByVal lpData As String, lpcbData As Long) As Long
Private Declare Function RegCloseKey Lib "advapi32.dll" (ByVal hKey As Long) As Long
Private Declare Function GetCurrentThreadId Lib "kernel32" () As Long

Private Const HKEY_LOCAL_MACHINE As Long = &H80000002
Private Const KEY_READ As Long = &H20019

Private m_sTraceFile As String
Private m_bChecked As Boolean

Public Sub LogLine(ByVal sText As String)
    Dim iFile As Integer
    If Not m_bChecked Then
        m_sTraceFile = ReadSetting("TraceFile")
        m_bChecked = True
    End If
    If Len(m_sTraceFile) = 0 Then Exit Sub
    On Error Resume Next
    iFile = FreeFile
    Open m_sTraceFile For Append Shared As #iFile
    Print #iFile, Format$(Now, "yyyy-mm-dd hh:nn:ss") & " [" & CStr(GetCurrentThreadId()) & "] " & sText
    Close #iFile
End Sub

Public Function ReadSetting(ByVal sName As String) As String
    Dim hKey As Long
    Dim sBuffer As String
    Dim lSize As Long
    Dim lType As Long
    If RegOpenKeyEx(HKEY_LOCAL_MACHINE, "SOFTWARE\Humongous\RateCore", 0, KEY_READ, hKey) <> 0 Then Exit Function
    sBuffer = String$(260, vbNullChar)
    lSize = 260
    If RegQueryValueEx(hKey, sName, 0, lType, sBuffer, lSize) = 0 And lSize > 1 Then
        ReadSetting = Left$(sBuffer, lSize - 1)
    End If
    RegCloseKey hKey
End Function
