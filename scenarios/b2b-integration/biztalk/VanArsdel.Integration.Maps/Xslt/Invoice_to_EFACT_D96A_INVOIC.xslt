<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical customer invoice to EDIFACT INVOIC D.96A (EANCOM 97 subset): MOA 203 per line, TAX/MOA 124 per rate,
     MOA 77 / 79 / 125 / 124 in the summary section. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ns0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS1 ScriptNS2 ScriptNS3 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Invoice" />
  </xsl:template>

  <xsl:template match="/s0:Invoice">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="partner" select="string($h/s0:PartnerId)" />
    <ns0:EFACT_D96A_INVOIC>
      <ns0:UNH>
        <UNH1>1</UNH1>
        <UNH2><UNH2.1>INVOIC</UNH2.1><UNH2.2>D</UNH2.2><UNH2.3>96A</UNH2.3><UNH2.4>UN</UNH2.4><UNH2.5>EAN008</UNH2.5></UNH2>
      </ns0:UNH>
      <ns0:BGM><C002><C00201>380</C00201></C002><BGM02><xsl:value-of select="$h/s0:InvoiceNumber" /></BGM02><BGM03>9</BGM03></ns0:BGM>
      <ns0:DTM><C507><C50701>137</C50701><C50702><xsl:value-of select="ScriptNS0:XmlDateToEdifact(string($h/s0:InvoiceDate))" /></C50702><C50703>102</C50703></C507></ns0:DTM>
      <xsl:if test="$h/s0:OrderNumber">
        <ns0:RFFLoop1><ns0:RFF><C506><C50601>ON</C50601><C50602><xsl:value-of select="$h/s0:OrderNumber" /></C50602></C506></ns0:RFF></ns0:RFFLoop1>
      </xsl:if>
      <xsl:if test="$h/s0:DespatchNumber">
        <ns0:RFFLoop1><ns0:RFF><C506><C50601>AAK</C50601><C50602><xsl:value-of select="$h/s0:DespatchNumber" /></C50602></C506></ns0:RFF></ns0:RFFLoop1>
      </xsl:if>
      <ns0:NADLoop1><ns0:NAD><NAD01>BY</NAD01><C082><C08201><xsl:value-of select="ScriptNS2:Gln($partner)" /></C08201><C08203>9</C08203></C082></ns0:NAD></ns0:NADLoop1>
      <ns0:NADLoop1>
        <ns0:NAD>
          <NAD01>SU</NAD01>
          <C082><C08201>0614141000012</C08201><C08203>9</C08203></C082>
          <C080><C08001>VanArsdel Distribution B.V.</C08001></C080>
          <C059><C05901>Celsiusweg 41</C05901></C059>
          <NAD06>Venlo</NAD06><NAD08>5928 PR</NAD08><NAD09>NL</NAD09>
        </ns0:NAD>
        <ns0:RFFLoop2><ns0:RFF><C506><C50601>VA</C50601><C50602>NL855632014B01</C50602></C506></ns0:RFF></ns0:RFFLoop2>
      </ns0:NADLoop1>
      <xsl:for-each select="s0:Parties/s0:Party[s0:Role='BT' or s0:Role='ST']">
        <ns0:NADLoop1>
          <ns0:NAD>
            <NAD01><xsl:choose><xsl:when test="s0:Role='BT'">IV</xsl:when><xsl:otherwise>DP</xsl:otherwise></xsl:choose></NAD01>
            <C082><C08201><xsl:value-of select="ScriptNS4:Coalesce(string(s0:Gln), ScriptNS4:Coalesce(string(s0:PartnerCode), string(s0:InternalId)))" /></C08201><C08203>9</C08203></C082>
            <xsl:if test="s0:Name"><C080><C08001><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Name), 35)" /></C08001></C080></xsl:if>
            <xsl:if test="s0:Street1"><C059><C05901><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Street1), 35)" /></C05901></C059></xsl:if>
            <xsl:if test="s0:City"><NAD06><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:City), 35)" /></NAD06></xsl:if>
            <xsl:if test="s0:PostalCode"><NAD08><xsl:value-of select="s0:PostalCode" /></NAD08></xsl:if>
            <xsl:if test="s0:Country"><NAD09><xsl:value-of select="s0:Country" /></NAD09></xsl:if>
          </ns0:NAD>
          <xsl:if test="s0:VatNumber"><ns0:RFFLoop2><ns0:RFF><C506><C50601>VA</C50601><C50602><xsl:value-of select="s0:VatNumber" /></C50602></C506></ns0:RFF></ns0:RFFLoop2></xsl:if>
        </ns0:NADLoop1>
      </xsl:for-each>
      <ns0:CUXLoop1><ns0:CUX><C504><C50401>2</C50401><C50402><xsl:value-of select="$h/s0:Currency" /></C50402><C50403>4</C50403></C504></ns0:CUX></ns0:CUXLoop1>
      <xsl:if test="$h/s0:PaymentTermsDays">
        <ns0:PATLoop1>
          <ns0:PAT><PAT01>1</PAT01><C112><C11201>5</C11201><C11202>3</C11202><C11203>D</C11203><C11204><xsl:value-of select="$h/s0:PaymentTermsDays" /></C11204></C112></ns0:PAT>
          <xsl:if test="$h/s0:DueDate"><ns0:DTM><C507><C50701>13</C50701><C50702><xsl:value-of select="ScriptNS0:XmlDateToEdifact(string($h/s0:DueDate))" /></C50702><C50703>102</C50703></C507></ns0:DTM></xsl:if>
        </ns0:PATLoop1>
      </xsl:if>
      <xsl:for-each select="s0:Lines/s0:Line">
        <ns0:LINLoop1>
          <ns0:LIN>
            <LIN01><xsl:value-of select="s0:LineNumber" /></LIN01>
            <xsl:if test="s0:Gtin"><C212><C21201><xsl:value-of select="ScriptNS5:GtinForStandard(string(s0:Gtin), 'EDIFACT')" /></C21201><C21202>EN</C21202></C212></xsl:if>
          </ns0:LIN>
          <xsl:if test="s0:PartnerItem"><ns0:PIA><PIA01>1</PIA01><C212><C21201><xsl:value-of select="s0:PartnerItem" /></C21201><C21202>IN</C21202></C212></ns0:PIA></xsl:if>
          <ns0:PIA><PIA01>1</PIA01><C212><C21201><xsl:value-of select="s0:Item" /></C21201><C21202>SA</C21202></C212></ns0:PIA>
          <xsl:if test="s0:Description"><ns0:IMD><IMD01>F</IMD01><C273><C27304><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Description), 256)" /></C27304></C273></ns0:IMD></xsl:if>
          <ns0:QTY><C186><C18601>47</C18601><C18602><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></C18602><C18603><xsl:value-of select="ScriptNS1:ErpToUnEce($partner, string(s0:Uom))" /></C18603></C186></ns0:QTY>
          <ns0:MOALoop1><ns0:MOA><C516><C51601>203</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:LineAmount), 2)" /></C51602></C516></ns0:MOA></ns0:MOALoop1>
          <ns0:PRILoop1><ns0:PRI><C509><C50901>AAA</C50901><C50902><xsl:value-of select="ScriptNS3:Real(string(s0:UnitPrice))" /></C50902><xsl:if test="s0:PriceBasis and number(s0:PriceBasis) != 1"><C50905><xsl:value-of select="ScriptNS3:Real(string(s0:PriceBasis))" /></C50905></xsl:if></C509></ns0:PRI></ns0:PRILoop1>
          <xsl:if test="s0:OrderLineNumber">
            <ns0:RFFLoop3><ns0:RFF><C506><C50601>ON</C50601><C50602><xsl:value-of select="$h/s0:OrderNumber" /></C50602><C50603><xsl:value-of select="s0:OrderLineNumber" /></C50603></C506></ns0:RFF></ns0:RFFLoop3>
          </xsl:if>
          <xsl:if test="s0:TaxRate">
            <ns0:TAXLoop3><ns0:TAX><TAX01>7</TAX01><C241><C24101>VAT</C24101></C241><C243><C24304><xsl:value-of select="ScriptNS3:Real(string(s0:TaxRate))" /></C24304></C243><TAX06><xsl:choose><xsl:when test="number(s0:TaxRate) = 0">AE</xsl:when><xsl:otherwise>S</xsl:otherwise></xsl:choose></TAX06></ns0:TAX></ns0:TAXLoop3>
          </xsl:if>
        </ns0:LINLoop1>
      </xsl:for-each>
      <ns0:UNS><UNS01>S</UNS01></ns0:UNS>
      <ns0:CNT><C270><C27001>2</C27001><C27002><xsl:value-of select="count(s0:Lines/s0:Line)" /></C27002></C270></ns0:CNT>
      <ns0:MOALoop4><ns0:MOA><C516><C51601>77</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Summary/s0:GrandTotal), 2)" /></C51602></C516></ns0:MOA></ns0:MOALoop4>
      <ns0:MOALoop4><ns0:MOA><C516><C51601>79</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Summary/s0:LineTotal), 2)" /></C51602></C516></ns0:MOA></ns0:MOALoop4>
      <ns0:MOALoop4><ns0:MOA><C516><C51601>125</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Summary/s0:LineTotal), 2)" /></C51602></C516></ns0:MOA></ns0:MOALoop4>
      <ns0:MOALoop4><ns0:MOA><C516><C51601>124</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Summary/s0:TaxTotal), 2)" /></C51602></C516></ns0:MOA></ns0:MOALoop4>
      <xsl:for-each select="s0:Summary/s0:Taxes/s0:Tax">
        <ns0:TAXLoop5>
          <ns0:TAX><TAX01>7</TAX01><C241><C24101>VAT</C24101></C241><C243><C24304><xsl:value-of select="ScriptNS3:Real(string(s0:Rate))" /></C24304></C243><TAX06><xsl:choose><xsl:when test="number(s0:Rate) = 0">AE</xsl:when><xsl:otherwise>S</xsl:otherwise></xsl:choose></TAX06></ns0:TAX>
          <ns0:MOA><C516><C51601>125</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Base), 2)" /></C51602></C516></ns0:MOA>
          <ns0:MOA><C516><C51601>124</C51601><C51602><xsl:value-of select="ScriptNS3:Format(string(s0:Amount), 2)" /></C51602></C516></ns0:MOA>
        </ns0:TAXLoop5>
      </xsl:for-each>
      <ns0:UNT><UNT1>0</UNT1><UNT2>1</UNT2></ns0:UNT>
    </ns0:EFACT_D96A_INVOIC>
  </xsl:template>
</xsl:stylesheet>
