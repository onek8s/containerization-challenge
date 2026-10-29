Attribute VB_Name = "modReferral"
Option Explicit

' Rating table referrals. The binding authority rules (sums insured, claims, construction)
' are applied by the quoting platform; these are the ones that depend on the tables.

Public Sub CheckReferrals(ByRef res As RatingResult, ByVal sVersion As String, ByRef t As TradeRow, _
                          ByVal dicRisk As Scripting.Dictionary, ByVal colSections As Collection, _
                          ByVal iFlood As Integer, ByVal iSubs As Integer, ByVal iCrime As Integer)
    Dim dic As Scripting.Dictionary
    Dim cStock As Currency, cBuildings As Currency

    If HasSection(colSections, "MD") Then
        Set dic = colSections("MD")
        cStock = CCur(Value(dic, "STOCK", 0))
        cBuildings = CCur(Value(dic, "BUILDINGS", 0))

        If iFlood >= 4 Then AddMessage res, "R", "R10", "Postcode district in flood zone 4"
        If iSubs >= 4 And cBuildings > 0 Then AddMessage res, "R", "R13", "Postcode district in subsidence zone 4"
        If iCrime >= 4 And cStock > Factor(sVersion, "LIMIT", "CRIME4STOCK", 50000#) Then
            AddMessage res, "R", "R11", "Stock over limit in crime zone 4"
        End If
        If cBuildings + CCur(Value(dic, "CONTENTS", 0)) + cStock > Factor(sVersion, "LIMIT", "MDTOTAL", 5000000#) Then
            AddMessage res, "R", "R20", "Material damage sums insured above the table limit"
        End If
    End If

    If HasSection(colSections, "EL") Then
        Set dic = colSections("EL")
        If CCur(Value(dic, "MANUALWAGEROLL", 0)) > Factor(sVersion, "LIMIT", "ELMANUAL", 1000000#) Then
            AddMessage res, "R", "R21", "Manual wageroll above the table limit"
        End If
    End If

    If HasSection(colSections, "PL") Then
        If CCur(Value(dicRisk, "TURNOVER", 0)) > Factor(sVersion, "LIMIT", "PLTURNOVER", 10000000#) Then
            AddMessage res, "R", "R22", "Turnover above the table limit"
        End If
    End If

    If CInt(Value(dicRisk, "YEARSTRADING", 0)) = 0 And t.TradeGroup = "Hospitality" Then
        AddMessage res, "R", "R30", "New hospitality business"
    End If
End Sub
