<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical supplier invoice to an INVOIC02 IDoc (message variant MM, partner type LI = vendor) for invoice
     verification in the ERP. Amounts are written with a trailing minus for credit notes, as the ERP expects. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ns0="http://schemas.vanarsdel.com/erp/idoc/2013"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS3 ScriptNS4">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Invoice" />
  </xsl:template>

  <xsl:template match="/s0:Invoice">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="vendor" select="ScriptNS4:PadLeft(string($h/s0:ErpPartnerNumber), 10, '0')" />
    <xsl:variable name="sign"><xsl:if test="$h/s0:Kind='SupplierCreditNote'">-</xsl:if></xsl:variable>
    <ns0:INVOIC02>
      <IDOC BEGIN="1">
        <EDI_DC40 SEGMENT="1">
          <TABNAM>EDI_DC40</TABNAM>
          <DIRECT>2</DIRECT>
          <IDOCTYP>INVOIC02</IDOCTYP>
          <MESTYP>INVOIC</MESTYP>
          <SNDPOR>BIZTALK</SNDPOR>
          <SNDPRT>LI</SNDPRT>
          <SNDPRN><xsl:value-of select="$vendor" /></SNDPRN>
          <RCVPOR>SAPVAP</RCVPOR>
          <RCVPRT>LS</RCVPRT>
          <RCVPRN>VAPCLNT100</RCVPRN>
          <CREDAT><xsl:value-of select="ScriptNS0:TodayX12()" /></CREDAT>
          <CRETIM><xsl:value-of select="concat(ScriptNS0:NowTimeX12(), '00')" /></CRETIM>
          <REFINT><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:InvoiceNumber), 14)" /></REFINT>
        </EDI_DC40>
        <E1EDK01 SEGMENT="1">
          <CURCY><xsl:value-of select="$h/s0:Currency" /></CURCY>
          <xsl:if test="$h/s0:PaymentTermsDays"><ZTERM><xsl:value-of select="concat('N', ScriptNS4:PadLeft(string($h/s0:PaymentTermsDays), 3, '0'))" /></ZTERM></xsl:if>
          <BSART><xsl:choose><xsl:when test="$h/s0:Kind='SupplierCreditNote'">CRME</xsl:when><xsl:otherwise>INVO</xsl:otherwise></xsl:choose></BSART>
          <BELNR><xsl:value-of select="ScriptNS4:Truncate(string($h/s0:InvoiceNumber), 35)" /></BELNR>
        </E1EDK01>
        <xsl:for-each select="s0:Parties/s0:Party[s0:Role='SE' or s0:Role='BT' or s0:Role='ST']">
          <E1EDKA1 SEGMENT="1">
            <PARVW>
              <xsl:choose>
                <xsl:when test="s0:Role='SE'">LF</xsl:when>
                <xsl:when test="s0:Role='BT'">RE</xsl:when>
                <xsl:otherwise>WE</xsl:otherwise>
              </xsl:choose>
            </PARVW>
            <xsl:if test="s0:Role='SE'"><PARTN><xsl:value-of select="$vendor" /></PARTN></xsl:if>
            <xsl:if test="s0:Gln"><ILNNR><xsl:value-of select="s0:Gln" /></ILNNR></xsl:if>
            <xsl:if test="s0:Name"><NAME1><xsl:value-of select="ScriptNS4:Truncate(string(s0:Name), 35)" /></NAME1></xsl:if>
            <xsl:if test="s0:Street1"><STRAS><xsl:value-of select="ScriptNS4:Truncate(string(s0:Street1), 35)" /></STRAS></xsl:if>
            <xsl:if test="s0:City"><ORT01><xsl:value-of select="ScriptNS4:Truncate(string(s0:City), 35)" /></ORT01></xsl:if>
            <xsl:if test="s0:PostalCode"><PSTLZ><xsl:value-of select="s0:PostalCode" /></PSTLZ></xsl:if>
            <xsl:if test="s0:Country"><LAND1><xsl:value-of select="s0:Country" /></LAND1></xsl:if>
            <xsl:if test="s0:VatNumber"><STCEG><xsl:value-of select="s0:VatNumber" /></STCEG></xsl:if>
          </E1EDKA1>
        </xsl:for-each>
        <xsl:if test="$h/s0:OrderNumber"><E1EDK02 SEGMENT="1"><QUALF>001</QUALF><BELNR><xsl:value-of select="$h/s0:OrderNumber" /></BELNR></E1EDK02></xsl:if>
        <xsl:if test="$h/s0:DespatchNumber"><E1EDK02 SEGMENT="1"><QUALF>012</QUALF><BELNR><xsl:value-of select="$h/s0:DespatchNumber" /></BELNR></E1EDK02></xsl:if>
        <E1EDK02 SEGMENT="1"><QUALF>009</QUALF><BELNR><xsl:value-of select="$h/s0:InvoiceNumber" /></BELNR><DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:InvoiceDate))" /></DATUM></E1EDK02>
        <E1EDK03 SEGMENT="1"><IDDAT>012</IDDAT><DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:InvoiceDate))" /></DATUM></E1EDK03>
        <xsl:if test="$h/s0:DueDate"><E1EDK03 SEGMENT="1"><IDDAT>024</IDDAT><DATUM><xsl:value-of select="ScriptNS0:XmlDateToIDoc(string($h/s0:DueDate))" /></DATUM></E1EDK03></xsl:if>
        <xsl:for-each select="s0:Lines/s0:Line">
          <E1EDP01 SEGMENT="1">
            <POSEX><xsl:value-of select="ScriptNS4:PadLeft(string(s0:LineNumber), 6, '0')" /></POSEX>
            <MENGE><xsl:value-of select="ScriptNS3:Format(string(s0:Quantity), 3)" /></MENGE>
            <MENEE><xsl:value-of select="s0:Uom" /></MENEE>
            <PEINH><xsl:value-of select="ScriptNS4:Coalesce(string(s0:PriceBasis), '1')" /></PEINH>
            <xsl:if test="s0:OrderLineNumber">
              <E1EDP02 SEGMENT="1"><QUALF>001</QUALF><BELNR><xsl:value-of select="$h/s0:OrderNumber" /></BELNR><DATUM><xsl:value-of select="ScriptNS4:PadLeft(string(s0:OrderLineNumber), 5, '0')" /></DATUM></E1EDP02>
            </xsl:if>
            <xsl:if test="s0:PartnerItem"><E1EDP19 SEGMENT="1"><QUALF>001</QUALF><IDTNR><xsl:value-of select="s0:PartnerItem" /></IDTNR></E1EDP19></xsl:if>
            <E1EDP19 SEGMENT="1">
              <QUALF>002</QUALF>
              <IDTNR><xsl:value-of select="s0:Item" /></IDTNR>
              <xsl:if test="s0:Description"><KTEXT><xsl:value-of select="ScriptNS4:Truncate(string(s0:Description), 40)" /></KTEXT></xsl:if>
            </E1EDP19>
            <xsl:if test="s0:Gtin"><E1EDP19 SEGMENT="1"><QUALF>003</QUALF><IDTNR><xsl:value-of select="s0:Gtin" /></IDTNR></E1EDP19></xsl:if>
            <E1EDP26 SEGMENT="1"><QUALF>001</QUALF><BETRG><xsl:value-of select="concat(ScriptNS3:Format(string(s0:UnitPrice), 2), $sign)" /></BETRG></E1EDP26>
            <E1EDP26 SEGMENT="1"><QUALF>003</QUALF><BETRG><xsl:value-of select="concat(ScriptNS3:Format(string(s0:LineAmount), 2), $sign)" /></BETRG></E1EDP26>
            <xsl:if test="s0:TaxRate">
              <E1EDP04 SEGMENT="1">
                <MWSKZ><xsl:choose><xsl:when test="number(s0:TaxRate) = 0">V0</xsl:when><xsl:otherwise>V1</xsl:otherwise></xsl:choose></MWSKZ>
                <MSATZ><xsl:value-of select="ScriptNS3:Format(string(s0:TaxRate), 2)" /></MSATZ>
                <MWSBT><xsl:value-of select="ScriptNS3:Format(string(round(number(s0:LineAmount) * number(s0:TaxRate)) div 100), 2)" /></MWSBT>
              </E1EDP04>
            </xsl:if>
          </E1EDP01>
        </xsl:for-each>
        <E1EDS01 SEGMENT="1"><SUMID>010</SUMID><SUMME><xsl:value-of select="concat(ScriptNS3:Format(string(s0:Summary/s0:LineTotal), 2), $sign)" /></SUMME><WAERQ><xsl:value-of select="$h/s0:Currency" /></WAERQ></E1EDS01>
        <E1EDS01 SEGMENT="1"><SUMID>005</SUMID><SUMME><xsl:value-of select="concat(ScriptNS3:Format(string(s0:Summary/s0:TaxTotal), 2), $sign)" /></SUMME><WAERQ><xsl:value-of select="$h/s0:Currency" /></WAERQ></E1EDS01>
        <E1EDS01 SEGMENT="1"><SUMID>011</SUMID><SUMME><xsl:value-of select="concat(ScriptNS3:Format(string(s0:Summary/s0:GrandTotal), 2), $sign)" /></SUMME><WAERQ><xsl:value-of select="$h/s0:Currency" /></WAERQ></E1EDS01>
        <E1EDS01 SEGMENT="1"><SUMID>001</SUMID><SUMME><xsl:value-of select="count(s0:Lines/s0:Line)" /></SUMME></E1EDS01>
      </IDOC>
    </ns0:INVOIC02>
  </xsl:template>
</xsl:stylesheet>
