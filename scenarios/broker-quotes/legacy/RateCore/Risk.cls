VERSION 1.0 CLASS
BEGIN
  MultiUse = 0   'False
  Persistable = 0  'NotPersistable
  DataBindingBehavior = 0  'vbNone
  DataSourceBehavior  = 0  'vbNone
  MTSTransactionMode  = 0  'NotAnMTSObject
END
Attribute VB_Name = "Risk"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = True
Attribute VB_Description = "A risk to rate. Created by Engine.NewRisk."
Option Explicit

' Public members in vtable order (binary compatibility). Friend members are not on the interface.

Private m_sProduct As String
Private m_sVersion As String
Private m_dtEffective As Date
Private m_dicValues As Scripting.Dictionary
Private m_colSections As Collection           ' section code -> Scripting.Dictionary of values
Private m_aClaims() As ClaimRow
Private m_lClaimCount As Long
Private m_res As RatingResult
Private m_sErrorText As String

Public Sub SetValue(ByVal Name As String, ByVal Value As Variant)
    Select Case UCase$(Name)
        Case "PURPOSE", "POSTCODE", "TRADECODE", "YEARSTRADING", "TURNOVER", "CONSTRUCTION", "ROOF", _
             "YEARBUILT", "OCCUPIED", "LISTED", "FLOOD10", "ALARM"
            m_dicValues(UCase$(Name)) = Value
        Case Else
            RaiseError ERR_UNKNOWN_FIELD, "Risk.SetValue", "Unknown risk field " & Name
    End Select
End Sub

Public Function AddSection(ByVal SectionCode As String) As Boolean
    Dim dic As Scripting.Dictionary
    Select Case UCase$(SectionCode)
        Case "MD", "BI", "EL", "PL"
            Set dic = New Scripting.Dictionary
            dic.CompareMode = TextCompare
            On Error Resume Next
            m_colSections.Add dic, UCase$(SectionCode)
            AddSection = (Err.Number = 0)
            If Not AddSection Then m_sErrorText = "Section " & SectionCode & " added twice"
            On Error GoTo 0
        Case Else
            m_sErrorText = "Unknown section " & SectionCode
            AddSection = False
    End Select
End Function

Public Sub SetSectionValue(ByVal SectionCode As String, ByVal Name As String, ByVal Value As Variant)
    Dim dic As Scripting.Dictionary
    On Error Resume Next
    Set dic = m_colSections(UCase$(SectionCode))
    If Err.Number <> 0 Then
        On Error GoTo 0
        RaiseError ERR_UNKNOWN_SECTION, "Risk.SetSectionValue", "Section " & SectionCode & " not added"
    End If
    On Error GoTo 0
    dic(UCase$(Name)) = Value
End Sub

Public Sub AddClaim(ByVal ClaimDate As Date, ByVal SectionCode As String, ByVal Amount As Currency)
    m_lClaimCount = m_lClaimCount + 1
    ReDim Preserve m_aClaims(1 To m_lClaimCount)
    m_aClaims(m_lClaimCount).ClaimDate = ClaimDate
    m_aClaims(m_lClaimCount).SectionCode = UCase$(SectionCode)
    m_aClaims(m_lClaimCount).Amount = Amount
End Sub

Public Function Calculate() As Long
    On Error GoTo Failed
    m_sErrorText = ""
    m_res = RateRisk(m_sProduct, m_sVersion, m_dtEffective, m_dicValues, m_colSections, m_aClaims, m_lClaimCount)
    If m_res.DeclineCount > 0 Then
        Calculate = CALC_DECLINE
    ElseIf m_res.ReferCount > 0 Then
        Calculate = CALC_REFER
    Else
        Calculate = CALC_OK
    End If
    Exit Function
Failed:
    m_sErrorText = Err.Description
    LogLine "Calculate failed: " & Err.Description
    Calculate = CALC_ERROR
End Function

Public Property Get SectionCount() As Long
    SectionCount = m_res.SectionCount
End Property

Public Property Get SectionCode(ByVal Index As Long) As String
    SectionCode = m_res.Sections(Index).Code
End Property

Public Property Get SectionPremium(ByVal Index As Long) As Currency
    SectionPremium = m_res.Sections(Index).Premium
End Property

Public Property Get SectionRate(ByVal Index As Long) As Double
    SectionRate = m_res.Sections(Index).Rate
End Property

Public Property Get SectionBasis(ByVal Index As Long) As String
    SectionBasis = m_res.Sections(Index).Basis
End Property

Public Property Get MessageCount() As Long
    MessageCount = m_res.DeclineCount + m_res.ReferCount
End Property

Public Property Get MessageType(ByVal Index As Long) As String
    MessageType = m_res.Messages(Index).MsgType
End Property

Public Property Get MessageCode(ByVal Index As Long) As String
    MessageCode = m_res.Messages(Index).Code
End Property

Public Property Get MessageText(ByVal Index As Long) As String
    MessageText = m_res.Messages(Index).Text
End Property

Public Property Get Clauses() As Variant
    Clauses = m_res.Clauses
End Property

Public Property Get TableVersion() As String
    TableVersion = m_sProduct & "-" & m_sVersion
End Property

Public Property Get ErrorText() As String
    ErrorText = m_sErrorText
End Property

Friend Sub Init(ByVal sProduct As String, ByVal sVersion As String, ByVal dtEffective As Date)
    m_sProduct = sProduct
    m_sVersion = sVersion
    m_dtEffective = dtEffective
End Sub

Private Sub Class_Initialize()
    Set m_dicValues = New Scripting.Dictionary
    m_dicValues.CompareMode = TextCompare
    Set m_colSections = New Collection
End Sub

Private Sub Class_Terminate()
    Set m_dicValues = Nothing
    Set m_colSections = Nothing
End Sub
