<?xml version="1.0" encoding="utf-8"?>
<!-- Billing document INVOIC02 (output type ZEDI) from the ERP to the canonical customer invoice. Item amounts:
     E1EDP26 001 = net price, 003 = net line value; header totals E1EDS01 010 / 005 / 011. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/erp/idoc/2013"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS2 ScriptNS3 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:key name="rate" match="E1EDP01" use="E1EDP04/MSATZ" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:INVOIC02/IDOC[1]" />
  </xsl:template>

  <xsl:template match="IDOC">
    <xsl:variable name="soldTo" select="string(E1EDKA1[PARVW='AG']/PARTN)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpCustomer($soldTo)" />
    <xsl:variable name="invDate" select="ScriptNS0:IDocToXmlDate(string(E1EDK03[IDDAT='012']/DATUM))" />
    <xsl:variable name="terms" select="translate(string(E1EDK01/ZTERM), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', '')" />
    <ns0:Invoice>
      <ns0:Header>
        <ns0:InvoiceNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDK01/BELNR))" /></ns0:InvoiceNumber>
        <ns0:Kind>CustomerInvoice</ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:InvoiceDate><xsl:value-of select="$invDate" /></ns0:InvoiceDate>
        <xsl:if test="E1EDK02[QUALF='001']"><ns0:OrderNumber><xsl:value-of select="E1EDK02[QUALF='001']/BELNR" /></ns0:OrderNumber></xsl:if>
        <xsl:if test="E1EDK02[QUALF='002']"><ns0:ErpOrderNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDK02[QUALF='002']/BELNR))" /></ns0:ErpOrderNumber></xsl:if>
        <xsl:if test="E1EDK02[QUALF='012']"><ns0:DespatchNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDK02[QUALF='012']/BELNR))" /></ns0:DespatchNumber></xsl:if>
        <ns0:Currency><xsl:value-of select="E1EDK01/CURCY" /></ns0:Currency>
        <xsl:if test="$terms != ''">
          <ns0:PaymentTermsDays><xsl:value-of select="number($terms)" /></ns0:PaymentTermsDays>
          <ns0:DueDate><xsl:value-of select="ScriptNS0:DueDate($invDate, $terms)" /></ns0:DueDate>
        </xsl:if>
        <ns0:ErpPartnerNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros($soldTo)" /></ns0:ErpPartnerNumber>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="E1EDKA1">
          <ns0:Party>
            <ns0:Role>
              <xsl:choose>
                <xsl:when test="PARVW='AG'">BY</xsl:when>
                <xsl:when test="PARVW='RE'">BT</xsl:when>
                <xsl:when test="PARVW='WE'">ST</xsl:when>
                <xsl:when test="PARVW='RS'">SE</xsl:when>
                <xsl:otherwise><xsl:value-of select="PARVW" /></xsl:otherwise>
              </xsl:choose>
            </ns0:Role>
            <xsl:if test="PARVW='WE'"><ns0:PartnerCode><xsl:value-of select="ScriptNS2:ShipToPartner($partner, ScriptNS4:TrimLeadingZeros(string(PARTN)))" /></ns0:PartnerCode></xsl:if>
            <xsl:if test="ILNNR"><ns0:Gln><xsl:value-of select="ILNNR" /></ns0:Gln></xsl:if>
            <ns0:InternalId><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(PARTN))" /></ns0:InternalId>
            <xsl:if test="NAME1"><ns0:Name><xsl:value-of select="NAME1" /></ns0:Name></xsl:if>
            <xsl:if test="STRAS"><ns0:Street1><xsl:value-of select="STRAS" /></ns0:Street1></xsl:if>
            <xsl:if test="ORT01"><ns0:City><xsl:value-of select="ORT01" /></ns0:City></xsl:if>
            <xsl:if test="REGIO"><ns0:Region><xsl:value-of select="REGIO" /></ns0:Region></xsl:if>
            <xsl:if test="PSTLZ"><ns0:PostalCode><xsl:value-of select="PSTLZ" /></ns0:PostalCode></xsl:if>
            <xsl:if test="LAND1"><ns0:Country><xsl:value-of select="LAND1" /></ns0:Country></xsl:if>
            <xsl:if test="STCEG"><ns0:VatNumber><xsl:value-of select="STCEG" /></ns0:VatNumber></xsl:if>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="E1EDP01">
          <ns0:Line>
            <ns0:LineNumber><xsl:value-of select="number(POSEX)" /></ns0:LineNumber>
            <xsl:if test="E1EDP02[QUALF='001']/BELNR"><ns0:OrderLineNumber><xsl:value-of select="number(E1EDP02[QUALF='001']/BELNR)" /></ns0:OrderLineNumber></xsl:if>
            <ns0:Item><xsl:value-of select="E1EDP19[QUALF='002']/IDTNR" /></ns0:Item>
            <xsl:if test="E1EDP19[QUALF='001']"><ns0:PartnerItem><xsl:value-of select="E1EDP19[QUALF='001']/IDTNR" /></ns0:PartnerItem></xsl:if>
            <xsl:if test="E1EDP19[QUALF='003']"><ns0:Gtin><xsl:value-of select="ScriptNS5:Gtin14(string(E1EDP19[QUALF='003']/IDTNR))" /></ns0:Gtin></xsl:if>
            <xsl:if test="E1EDP19[QUALF='002']/KTEXT"><ns0:Description><xsl:value-of select="E1EDP19[QUALF='002']/KTEXT" /></ns0:Description></xsl:if>
            <ns0:Quantity><xsl:value-of select="ScriptNS3:Real(string(MENGE))" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="MENEE" /></ns0:Uom>
            <ns0:UnitPrice><xsl:value-of select="ScriptNS3:FromSapNumber(string(E1EDP26[QUALF='001']/BETRG))" /></ns0:UnitPrice>
            <ns0:PriceBasis><xsl:value-of select="ScriptNS4:Coalesce(string(PEINH), '1')" /></ns0:PriceBasis>
            <ns0:LineAmount><xsl:value-of select="ScriptNS3:Format(ScriptNS3:FromSapNumber(string(E1EDP26[QUALF='003']/BETRG)), 2)" /></ns0:LineAmount>
            <xsl:if test="E1EDP04/MSATZ"><ns0:TaxRate><xsl:value-of select="ScriptNS3:Percent(string(E1EDP04/MSATZ))" /></ns0:TaxRate></xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(E1EDP01)" /></ns0:LineCount>
        <ns0:LineTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS3:FromSapNumber(string(E1EDS01[SUMID='010']/SUMME)), 2)" /></ns0:LineTotal>
        <ns0:TaxTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(ScriptNS3:FromSapNumber(string(E1EDS01[SUMID='005']/SUMME)), '0'), 2)" /></ns0:TaxTotal>
        <ns0:GrandTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS3:FromSapNumber(string(E1EDS01[SUMID='011']/SUMME)), 2)" /></ns0:GrandTotal>
        <xsl:if test="E1EDP01/E1EDP04">
          <ns0:Taxes>
            <xsl:for-each select="E1EDP01[generate-id() = generate-id(key('rate', E1EDP04/MSATZ)[1])]">
              <xsl:variable name="r" select="string(E1EDP04/MSATZ)" />
              <ns0:Tax>
                <ns0:Rate><xsl:value-of select="ScriptNS3:Percent($r)" /></ns0:Rate>
                <ns0:Base><xsl:value-of select="ScriptNS3:Format(string(sum(key('rate', $r)/E1EDP26[QUALF='003']/BETRG)), 2)" /></ns0:Base>
                <ns0:Amount><xsl:value-of select="ScriptNS3:Format(string(sum(key('rate', $r)/E1EDP04/MWSBT)), 2)" /></ns0:Amount>
              </ns0:Tax>
            </xsl:for-each>
          </ns0:Taxes>
        </xsl:if>
      </ns0:Summary>
    </ns0:Invoice>
  </xsl:template>
</xsl:stylesheet>
