<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical customer order to an ORDERS05 IDoc for the ERP (inbound sales order, message variant EDI,
     partner type KU = sold-to account). Written to the ERP's IDoc inbound folder as XML. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ns0="http://schemas.vanarsdel.com/erp/idoc/2013"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS2 ScriptNS3 ScriptNS4">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Order" />
  </xsl:template>

  <xsl:template match="/s0:Order">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="soldTo" select="ScriptNS4:PadLeft(string($h/s0:ErpPartnerNumber), 10, '0')" />
    <ns0:ORDERS05>
      <IDOC BEGIN="1">
        <EDI_DC40 SEGMENT="1">
          <TABNAM>EDI_DC40</TABNAM>
          <DIRECT>2</DIRECT>
          <IDOCTYP>ORDERS05</IDOCTYP>
          <MESTYP>ORDERS</MESTYP>
          <SNDPOR>BIZTALK</SNDPOR>
          <SNDPRT>KU</SNDPRT>
          <SNDPRN><xsl:value-of select="$soldTo" /></SNDPRN>
          <RCVPOR>SAPVAP</RCVPOR>
          <RCVPRT>LS</RCVPRT>
          <RCVPRN>VAPCLNT100</RCVPRN>
          <CREDAT><xsl:value-of select="ScriptNS0:TodayX12()" /></CREDAT>
          <CRETIM><xsl:value-of select="concat(ScriptNS0:NowTimeX12(), '00')" /></CRETIM>
          <REFINT><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:OrderNumber), 14)" /></REFINT>
        </EDI_DC40>
        <E1EDK01 SEGMENT="1">
          <CURCY><xsl:value-of select="$h/s0:Currency" /></CURCY>
          <BSART><xsl:value-of select="ScriptNS4:Coalesce(string($h/s0:OrderType), 'ZEDI')" /></BSART>
          <BELNR><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:OrderNumber), 35)" /></BELNR>
          <xsl:if test="$h/s0:ShipVia"><VSART><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:ShipVia), 2)" /></VSART></xsl:if>
        </E1EDK01>
        <E1EDK14 SEGMENT="1">
          <QUALF>012</QUALF>
          <ORGID><xsl:value-of select="ScriptNS4:Coalesce(string($h/s0:OrderType), 'ZEDI')" /></ORGID>
        </E1EDK14>
        <E1EDK14 SEGMENT="1">
          <QUALF>008</QUALF>
          <ORGID>
            <xsl:choose>
              <xsl:when test="$h/s0:Currency='EUR' or $h/s0:Currency='NOK'">2000</xsl:when>
              <xsl:otherwise>1000</xsl:otherwise>
            </xsl:choose>
          </ORGID>
        </E1EDK14>
        <E1EDK03 SEGMENT="1">
          <IDDAT>012</IDDAT>
          <DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:OrderDate))" /></DATUM>
        </E1EDK03>
        <xsl:if test="$h/s0:RequestedDeliveryDate">
          <E1EDK03 SEGMENT="1">
            <IDDAT>002</IDDAT>
            <DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:RequestedDeliveryDate))" /></DATUM>
          </E1EDK03>
        </xsl:if>
        <E1EDKA1 SEGMENT="1">
          <PARVW>AG</PARVW>
          <PARTN><xsl:value-of select="$soldTo" /></PARTN>
        </E1EDKA1>
        <xsl:for-each select="s0:Parties/s0:Party[s0:Role='ST']">
          <E1EDKA1 SEGMENT="1">
            <PARVW>WE</PARVW>
            <xsl:choose>
              <xsl:when test="s0:InternalId and s0:InternalId != 'ONETIME'">
                <PARTN><xsl:value-of select="ScriptNS4:PadLeft(string(s0:InternalId), 10, '0')" /></PARTN>
              </xsl:when>
              <xsl:otherwise>
                <PARTN>ONETIME</PARTN>
              </xsl:otherwise>
            </xsl:choose>
            <xsl:if test="s0:Gln"><ILNNR><xsl:value-of select="s0:Gln" /></ILNNR></xsl:if>
            <xsl:if test="s0:Name"><NAME1><xsl:value-of select="ScriptNS4:Truncate(string(s0:Name), 35)" /></NAME1></xsl:if>
            <xsl:if test="s0:Street1"><STRAS><xsl:value-of select="ScriptNS4:Truncate(string(s0:Street1), 35)" /></STRAS></xsl:if>
            <xsl:if test="s0:Street2"><STRS2><xsl:value-of select="ScriptNS4:Truncate(string(s0:Street2), 35)" /></STRS2></xsl:if>
            <xsl:if test="s0:City"><ORT01><xsl:value-of select="ScriptNS4:Truncate(string(s0:City), 35)" /></ORT01></xsl:if>
            <xsl:if test="s0:PostalCode"><PSTLZ><xsl:value-of select="s0:PostalCode" /></PSTLZ></xsl:if>
            <xsl:if test="s0:Region"><REGIO><xsl:value-of select="s0:Region" /></REGIO></xsl:if>
            <xsl:if test="s0:Country"><LAND1><xsl:value-of select="s0:Country" /></LAND1></xsl:if>
            <xsl:if test="s0:PartnerCode"><IHREZ><xsl:value-of select="s0:PartnerCode" /></IHREZ></xsl:if>
          </E1EDKA1>
        </xsl:for-each>
        <E1EDK02 SEGMENT="1">
          <QUALF>001</QUALF>
          <BELNR><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:OrderNumber), 35)" /></BELNR>
          <DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:OrderDate))" /></DATUM>
        </E1EDK02>
        <xsl:if test="$h/s0:Notes">
          <E1EDKT1 SEGMENT="1">
            <TDID>Z001</TDID>
            <E1EDKT2 SEGMENT="1">
              <TDLINE><xsl:value-of select="ScriptNS4:SplitLine(string($h/s0:Notes), 70, 1)" /></TDLINE>
            </E1EDKT2>
            <xsl:if test="ScriptNS4:SplitLine(string($h/s0:Notes), 70, 2) != ''">
              <E1EDKT2 SEGMENT="1">
                <TDLINE><xsl:value-of select="ScriptNS4:SplitLine(string($h/s0:Notes), 70, 2)" /></TDLINE>
              </E1EDKT2>
            </xsl:if>
          </E1EDKT1>
        </xsl:if>
        <xsl:for-each select="s0:Lines/s0:Line">
          <E1EDP01 SEGMENT="1">
            <POSEX><xsl:value-of select="ScriptNS4:PadLeft(string(s0:LineNumber), 6, '0')" /></POSEX>
            <MENGE><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></MENGE>
            <MENEE><xsl:value-of select="s0:Uom" /></MENEE>
            <xsl:if test="s0:UnitPrice">
              <VPREI><xsl:value-of select="ScriptNS3:Format(string(s0:UnitPrice), 2)" /></VPREI>
              <PEINH><xsl:value-of select="ScriptNS4:Coalesce(string(s0:PriceBasis), '1')" /></PEINH>
            </xsl:if>
            <xsl:if test="s0:RequestedDate or $h/s0:RequestedDeliveryDate">
              <E1EDP20 SEGMENT="1">
                <WMENG><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></WMENG>
                <EDATU><xsl:value-of select="ScriptNS0:XmlDateToIDoc(ScriptNS4:Coalesce(string(s0:RequestedDate), string($h/s0:RequestedDeliveryDate)))" /></EDATU>
              </E1EDP20>
            </xsl:if>
            <E1EDP19 SEGMENT="1">
              <QUALF>002</QUALF>
              <IDTNR><xsl:value-of select="s0:Item" /></IDTNR>
            </E1EDP19>
            <xsl:if test="s0:PartnerItem">
              <E1EDP19 SEGMENT="1">
                <QUALF>001</QUALF>
                <IDTNR><xsl:value-of select="ScriptNS4:Truncate(string(s0:PartnerItem), 35)" /></IDTNR>
                <xsl:if test="s0:Description"><KTEXT><xsl:value-of select="ScriptNS4:Truncate(string(s0:Description), 70)" /></KTEXT></xsl:if>
              </E1EDP19>
            </xsl:if>
            <xsl:if test="s0:Gtin">
              <E1EDP19 SEGMENT="1">
                <QUALF>003</QUALF>
                <IDTNR><xsl:value-of select="s0:Gtin" /></IDTNR>
              </E1EDP19>
            </xsl:if>
          </E1EDP01>
        </xsl:for-each>
        <E1EDS01 SEGMENT="1">
          <SUMID>001</SUMID>
          <SUMME><xsl:value-of select="count(s0:Lines/s0:Line)" /></SUMME>
        </E1EDS01>
      </IDOC>
    </ns0:ORDERS05>
  </xsl:template>
</xsl:stylesheet>
