Attribute VB_Name = "TransmittalRegister"
' Transmittal register workbook macros (TransmittalRegister.xlsm).
' Requires the Litware.DocControl.RegisterClient component (Install-RegisterAddIn.ps1).
'
' Sheet "Register":     A1 = project code, table starts at A3
' Sheet "Transmittals": A1 = project code, table starts at A3
' Sheet "Issue":        B1 project, B2 purpose (IFR/IFA/IFC/IFI), B3 company, B4 contact, B5 e-mail,
'                       B6 notes, document numbers from A9 down
Option Explicit

Private Function Client() As Object
    Set Client = CreateObject("Litware.DocControl.RegisterClient")
End Function

Private Sub WriteTable(ws As Worksheet, data As Variant)
    ws.Range("A3", ws.Cells(ws.Rows.Count, 20)).ClearContents
    If IsEmpty(data) Then Exit Sub
    ws.Range("A3").Resize(UBound(data, 1) + 1, UBound(data, 2) + 1).Value = data
    ws.Rows(3).Font.Bold = True
    ws.Columns("A:J").AutoFit
End Sub

Public Sub RefreshRegister()
    Dim ws As Worksheet, c As Object, data As Variant
    Set ws = ThisWorkbook.Worksheets("Register")
    Set c = Client()
    data = c.GetRegister(CStr(ws.Range("A1").Value))
    If IsEmpty(data) Then
        MsgBox "Could not read the register:" & vbCrLf & c.LastError, vbExclamation
        Exit Sub
    End If
    WriteTable ws, data
    Application.StatusBar = "Register updated " & Format(Now, "yyyy-mm-dd hh:nn")
End Sub

Public Sub RefreshTransmittals()
    Dim ws As Worksheet, c As Object, data As Variant
    Set ws = ThisWorkbook.Worksheets("Transmittals")
    Set c = Client()
    data = c.GetTransmittals(CStr(ws.Range("A1").Value))
    If IsEmpty(data) Then
        MsgBox "Could not read the transmittals:" & vbCrLf & c.LastError, vbExclamation
        Exit Sub
    End If
    WriteTable ws, data
End Sub

Public Sub IssueTransmittal()
    Dim ws As Worksheet, c As Object, docs As String, r As Long, number As String
    Set ws = ThisWorkbook.Worksheets("Issue")
    r = 9
    Do While Len(Trim$(CStr(ws.Cells(r, 1).Value))) > 0
        docs = docs & Trim$(CStr(ws.Cells(r, 1).Value)) & ";"
        r = r + 1
    Loop
    If Len(docs) = 0 Then
        MsgBox "List the document numbers from A9 down.", vbInformation
        Exit Sub
    End If

    Set c = Client()
    number = c.CreateTransmittal(ws.Range("B1").Value, ws.Range("B2").Value, ws.Range("B3").Value, _
                                 ws.Range("B4").Value, ws.Range("B5").Value, ws.Range("B6").Value, docs)
    If Len(number) = 0 Then
        MsgBox "The transmittal was not created:" & vbCrLf & c.LastError, vbExclamation
    Else
        MsgBox number & " queued. The package is mailed when it has been built.", vbInformation
        ws.Range("D1").Value = number
    End If
End Sub

Public Sub OpenSelectedDocument()
    If Len(CStr(ActiveCell.Value)) > 0 Then Client().OpenInPortal CStr(ActiveCell.Value)
End Sub
