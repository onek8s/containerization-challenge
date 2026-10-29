Attribute VB_Name = "modRating"
Option Explicit

' Business Combined rating. Technical premium net of commission, before minimum premiums,
' tax and fees (those are applied by the quoting platform).
'
' History (see RELEASE.txt): 2004 first version for the Leeds branch; 2011 4.0 rewrite with
' binary compatibility; 2016 zones from ZONES.mdb; 2019 EL clerical/manual split; 2023 claims
' experience by section; 2024 4.12 work away loading on PL.

Public Type ClaimRow
    ClaimDate As Date
    SectionCode As String
    Amount As Currency
End Type

Public Type SectionResult
    Code As String
    Premium As Currency
    Rate As Double
    Basis As String
End Type

Public Type MessageRow
    MsgType As String   ' "R" refer, "D" decline
    Code As String
    Text As String
End Type

Public Type RatingResult
    Sections() As SectionResult
    SectionCount As Long
    Messages() As MessageRow
    ReferCount As Long
    DeclineCount As Long
    Clauses As Variant
End Type

Private m_colClauses As Collection

Public Function RateRisk(ByVal sProduct As String, ByVal sVersion As String, ByVal dtEffective As Date, _
                         ByVal dicRisk As Scripting.Dictionary, ByVal colSections As Collection, _
                         ByRef aClaims() As ClaimRow, ByVal lClaimCount As Long) As RatingResult
    Dim res As RatingResult
    Dim t As TradeRow
    Dim iFlood As Integer, iSubs As Integer, iCrime As Integer
    Dim sTrade As String, sPostcode As String
    Dim dic As Scripting.Dictionary

    Set m_colClauses = New Collection
    ReDim res.Sections(1 To 4)
    ReDim res.Messages(1 To 20)

    sTrade = CStr(Value(dicRisk, "TRADECODE", ""))
    sPostcode = CStr(Value(dicRisk, "POSTCODE", ""))

    If Not FindTrade(sVersion, sTrade, t) Then
        RaiseError ERR_TRADE_NOT_FOUND, "RateRisk", "Trade " & sTrade & " is not in the " & sVersion & " trade table"
    End If
    If Not GetZones(sPostcode, iFlood, iSubs, iCrime) Then
        RaiseError ERR_DISTRICT_NOT_FOUND, "RateRisk", "Postcode district " & District(sPostcode) & " is not in the zone table"
    End If

    If t.DeclineFlag Then AddMessage res, "D", "D10", "Trade declined by the rating tables"
    If t.ReferFlag Then AddMessage res, "R", "R12", "Trade referred by the rating tables"

    AddClause "GEN01"
    If Value(dicRisk, "PURPOSE", "NB") = "NB" Then AddClause "GEN02"

    If HasSection(colSections, "MD") Then
        Set dic = colSections("MD")
        RateMaterialDamage res, sVersion, t, dicRisk, dic, iFlood, iSubs, iCrime, aClaims, lClaimCount, dtEffective
    End If
    If HasSection(colSections, "BI") Then
        Set dic = colSections("BI")
        RateBusinessInterruption res, sVersion, t, dic, iFlood, aClaims, lClaimCount, dtEffective
    End If
    If HasSection(colSections, "EL") Then
        Set dic = colSections("EL")
        RateEmployersLiability res, sVersion, t, dic, aClaims, lClaimCount, dtEffective
    End If
    If HasSection(colSections, "PL") Then
        Set dic = colSections("PL")
        RatePublicLiability res, sVersion, t, dicRisk, dic, aClaims, lClaimCount, dtEffective
    End If

    CheckReferrals res, sVersion, t, dicRisk, colSections, iFlood, iSubs, iCrime

    res.Clauses = ClauseArray()
    RateRisk = res
End Function

Private Sub RateMaterialDamage(ByRef res As RatingResult, ByVal sVersion As String, ByRef t As TradeRow, _
                               ByVal dicRisk As Scripting.Dictionary, ByVal dic As Scripting.Dictionary, _
                               ByVal iFlood As Integer, ByVal iSubs As Integer, ByVal iCrime As Integer, _
                               ByRef aClaims() As ClaimRow, ByVal lClaimCount As Long, ByVal dtEffective As Date)
    Dim cBuildings As Currency, cContents As Currency, cStock As Currency, cComputer As Currency
    Dim dRate As Double, dLoad As Double, dTheft As Double
    Dim cPremium As Currency
    Dim iYearBuilt As Integer

    cBuildings = CCur(Value(dic, "BUILDINGS", 0))
    cContents = CCur(Value(dic, "CONTENTS", 0))
    cStock = CCur(Value(dic, "STOCK", 0))
    cComputer = CCur(Value(dic, "COMPUTER", 0))

    dLoad = Factor(sVersion, "CONSTRUCTION", CStr(Value(dicRisk, "CONSTRUCTION", "STD")))
    dLoad = dLoad * Factor(sVersion, "ROOF", CStr(Value(dicRisk, "ROOF", "STD")))
    dLoad = dLoad * Factor(sVersion, "FLOOD", CStr(iFlood))
    dLoad = dLoad * Factor(sVersion, "SUBS", CStr(iSubs))
    iYearBuilt = CInt(Value(dicRisk, "YEARBUILT", 1980))
    If iYearBuilt < 1900 Then dLoad = dLoad * Factor(sVersion, "AGE", "PRE1900", 1.15)
    If CBool(Value(dicRisk, "LISTED", False)) Then dLoad = dLoad * Factor(sVersion, "LISTED", "Y", 1.2)
    If Not CBool(Value(dicRisk, "OCCUPIED", True)) Then dLoad = dLoad * Factor(sVersion, "UNOCC", "Y", 1.5)
    dLoad = dLoad * Factor(sVersion, "EXCESS", CStr(CLng(Value(dic, "EXCESS", 250))))
    dLoad = dLoad * ClaimsLoad(sVersion, "MD", aClaims, lClaimCount, dtEffective)

    ' Theft load on contents and stock by crime zone, reduced for alarms
    dTheft = Factor(sVersion, "CRIME", CStr(iCrime)) * Factor(sVersion, "ALARM", CStr(CInt(Value(dicRisk, "ALARM", 0))))

    dRate = t.MDRate * dLoad
    cPremium = CCur((cBuildings * dRate + cContents * dRate * dTheft + cStock * t.StockRate * dLoad * dTheft) / 1000#)
    cPremium = cPremium + CCur(cComputer * Factor(sVersion, "COMPUTER", "RATE", 4#) / 1000#)

    AddSectionResult res, "MD", cPremium, dRate, "Per mille on sums insured"

    AddClause "MD01"
    If cContents + cStock > 0 Then AddClause "MD05"
    If CInt(Value(dicRisk, "ALARM", 0)) = 2 Then AddClause "MD07"
    If iFlood >= 3 Then AddClause "MD12"
    If iSubs >= 3 Then AddClause "MD14"
    If Not CBool(Value(dicRisk, "OCCUPIED", True)) Then AddClause "MD20"
End Sub

Private Sub RateBusinessInterruption(ByRef res As RatingResult, ByVal sVersion As String, ByRef t As TradeRow, _
                                     ByVal dic As Scripting.Dictionary, ByVal iFlood As Integer, _
                                     ByRef aClaims() As ClaimRow, ByVal lClaimCount As Long, ByVal dtEffective As Date)
    Dim cGP As Currency
    Dim dRate As Double
    cGP = CCur(Value(dic, "GROSSPROFIT", 0))
    dRate = t.BIRate * Factor(sVersion, "INDEMNITY", CStr(CInt(Value(dic, "INDEMNITY", 12))))
    dRate = dRate * Factor(sVersion, "FLOOD", CStr(iFlood))
    dRate = dRate * ClaimsLoad(sVersion, "BI", aClaims, lClaimCount, dtEffective)
    AddSectionResult res, "BI", CCur(cGP * dRate / 1000#), dRate, "Per mille on gross profit"
    AddClause "BI01"
End Sub

Private Sub RateEmployersLiability(ByRef res As RatingResult, ByVal sVersion As String, ByRef t As TradeRow, _
                                   ByVal dic As Scripting.Dictionary, _
                                   ByRef aClaims() As ClaimRow, ByVal lClaimCount As Long, ByVal dtEffective As Date)
    Dim cWage As Currency, cManual As Currency, cClerical As Currency
    Dim cPremium As Currency
    Dim lEmployees As Long
    Dim dLoad As Double

    cWage = CCur(Value(dic, "WAGEROLL", 0))
    cManual = CCur(Value(dic, "MANUALWAGEROLL", 0))
    lEmployees = CLng(Value(dic, "EMPLOYEES", 0))
    If cManual > cWage Then cManual = cWage
    cClerical = cWage - cManual
    ' 2019: wagerolls before the split were all rated at the manual rate
    If cManual = 0 And t.TradeGroup = "Trades" Then cManual = cWage: cClerical = 0

    dLoad = ClaimsLoad(sVersion, "EL", aClaims, lClaimCount, dtEffective)
    cPremium = CCur((cClerical * t.ELClericalRate + cManual * t.ELManualRate) / 100# * dLoad)
    If cPremium < lEmployees * Factor(sVersion, "EL", "PEREMPLOYEE", 35#) Then
        cPremium = CCur(lEmployees * Factor(sVersion, "EL", "PEREMPLOYEE", 35#))
    End If
    AddSectionResult res, "EL", cPremium, t.ELManualRate, "Per cent on wageroll"
    AddClause "EL01"
    If t.TradeGroup = "Trades" Then AddClause "EL04"
End Sub

Private Sub RatePublicLiability(ByRef res As RatingResult, ByVal sVersion As String, ByRef t As TradeRow, _
                                ByVal dicRisk As Scripting.Dictionary, ByVal dic As Scripting.Dictionary, _
                                ByRef aClaims() As ClaimRow, ByVal lClaimCount As Long, ByVal dtEffective As Date)
    Dim cTurnover As Currency
    Dim dRate As Double
    cTurnover = CCur(Value(dicRisk, "TURNOVER", 0))
    dRate = t.PLRate * Factor(sVersion, "PLLIMIT", CStr(CLng(CCur(Value(dic, "LIMIT", 2000000)) / 1000000)))
    If CBool(Value(dic, "PRODUCTS", False)) Then dRate = dRate * Factor(sVersion, "PRODUCTS", "Y", 1.15)
    If CBool(Value(dic, "WORKAWAY", False)) Then dRate = dRate * Factor(sVersion, "WORKAWAY", t.TradeGroup, 1.25)
    dRate = dRate * ClaimsLoad(sVersion, "PL", aClaims, lClaimCount, dtEffective)
    AddSectionResult res, "PL", CCur(cTurnover * dRate / 1000#), dRate, "Per mille on turnover"
    AddClause "PL01"
    If t.TradeGroup = "Trades" And CBool(Value(dic, "WORKAWAY", False)) Then AddClause "PL03"
    If t.TradeGroup = "Hospitality" Or t.TradeGroup = "Food production" Then AddClause "PL08"
End Sub

' 10% per claim on the section in the five years before the effective date, capped (CLAIMS|MAX).
Private Function ClaimsLoad(ByVal sVersion As String, ByVal sSection As String, ByRef aClaims() As ClaimRow, _
                            ByVal lClaimCount As Long, ByVal dtEffective As Date) As Double
    Dim i As Long, lCount As Long
    Dim dLoad As Double
    For i = 1 To lClaimCount
        If aClaims(i).SectionCode = sSection And aClaims(i).ClaimDate >= DateAdd("yyyy", -5, dtEffective) Then lCount = lCount + 1
    Next i
    dLoad = 1# + lCount * Factor(sVersion, "CLAIMS", "PERCLAIM", 0.1)
    If dLoad > Factor(sVersion, "CLAIMS", "MAX", 1.5) Then dLoad = Factor(sVersion, "CLAIMS", "MAX", 1.5)
    ClaimsLoad = dLoad
End Function

Public Sub AddMessage(ByRef res As RatingResult, ByVal sType As String, ByVal sCode As String, ByVal sText As String)
    Dim lIndex As Long
    lIndex = res.ReferCount + res.DeclineCount + 1
    If lIndex > UBound(res.Messages) Then ReDim Preserve res.Messages(1 To lIndex + 10)
    res.Messages(lIndex).MsgType = sType
    res.Messages(lIndex).Code = sCode
    res.Messages(lIndex).Text = sText
    If sType = "D" Then res.DeclineCount = res.DeclineCount + 1 Else res.ReferCount = res.ReferCount + 1
End Sub

Private Sub AddSectionResult(ByRef res As RatingResult, ByVal sCode As String, ByVal cPremium As Currency, ByVal dRate As Double, ByVal sBasis As String)
    res.SectionCount = res.SectionCount + 1
    res.Sections(res.SectionCount).Code = sCode
    res.Sections(res.SectionCount).Premium = Round(cPremium, 2)
    res.Sections(res.SectionCount).Rate = dRate
    res.Sections(res.SectionCount).Basis = sBasis
End Sub

Private Sub AddClause(ByVal sCode As String)
    On Error Resume Next
    m_colClauses.Add sCode, sCode
End Sub

Private Function ClauseArray() As Variant
    Dim a() As String
    Dim i As Long
    If m_colClauses.Count = 0 Then
        ClauseArray = Array()
        Exit Function
    End If
    ReDim a(0 To m_colClauses.Count - 1)
    For i = 1 To m_colClauses.Count
        a(i - 1) = m_colClauses(i)
    Next i
    ClauseArray = a
End Function

Public Function Value(ByVal dic As Scripting.Dictionary, ByVal sKey As String, ByVal vDefault As Variant) As Variant
    If dic.Exists(sKey) Then
        If IsNull(dic(sKey)) Or IsEmpty(dic(sKey)) Then Value = vDefault Else Value = dic(sKey)
    Else
        Value = vDefault
    End If
End Function

Public Function HasSection(ByVal col As Collection, ByVal sCode As String) As Boolean
    Dim o As Object
    On Error Resume Next
    Set o = col(sCode)
    HasSection = (Err.Number = 0)
End Function
