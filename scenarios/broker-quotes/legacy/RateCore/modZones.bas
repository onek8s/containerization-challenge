Attribute VB_Name = "modZones"
Option Explicit

' Postcode district zones (flood 1-4, subsidence 1-4, crime 1-4) from ZONES.mdb.
' Pricing refreshes ZONES.mdb quarterly from the flood and crime data suppliers.

Private m_dicZones As Scripting.Dictionary   ' district -> "flood|subs|crime"

Public Sub LoadZones(ByVal sFile As String)
    Dim db As DAO.Database
    Dim rs As DAO.Recordset
    Set m_dicZones = New Scripting.Dictionary
    m_dicZones.CompareMode = TextCompare
    Set db = DBEngine.Workspaces(0).OpenDatabase(sFile, False, True)
    Set rs = db.OpenRecordset("SELECT District, FloodZone, SubsZone, CrimeZone FROM tblDistrict", dbOpenSnapshot)
    Do While Not rs.EOF
        m_dicZones(UCase$(rs!District)) = CStr(rs!FloodZone) & "|" & CStr(rs!SubsZone) & "|" & CStr(rs!CrimeZone)
        rs.MoveNext
    Loop
    rs.Close
    db.Close
End Sub

Public Function District(ByVal sPostcode As String) As String
    Dim i As Long
    sPostcode = UCase$(Trim$(sPostcode))
    i = InStr(sPostcode, " ")
    If i > 0 Then
        District = Left$(sPostcode, i - 1)
    ElseIf Len(sPostcode) > 3 Then
        District = Left$(sPostcode, Len(sPostcode) - 3)
    Else
        District = sPostcode
    End If
End Function

Public Function GetZones(ByVal sPostcode As String, ByRef iFlood As Integer, ByRef iSubs As Integer, ByRef iCrime As Integer) As Boolean
    Dim sDistrict As String
    Dim vParts As Variant
    sDistrict = District(sPostcode)
    If Not m_dicZones.Exists(sDistrict) Then Exit Function
    vParts = Split(m_dicZones(sDistrict), "|")
    iFlood = CInt(vParts(0))
    iSubs = CInt(vParts(1))
    iCrime = CInt(vParts(2))
    GetZones = True
End Function
