<?xml version="1.0" encoding="utf-8"?>
<!-- Supplier invoice X12 810 to the canonical supplier invoice. The supplier quotes the vendor number we gave it in
     REF*IA; BIG04 is our purchase order, IT101 our PO line, VP / BP the supplier's and our item numbers. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.microsoft.com/BizTalk/EDI/X12/2006"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS1 ScriptNS2 ScriptNS3 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:X12_00401_810" />
  </xsl:template>

  <xsl:template name="item">
    <xsl:param name="it1" />
    <xsl:param name="qualifier" />
    <xsl:choose>
      <xsl:when test="$it1/IT106=$qualifier"><xsl:value-of select="$it1/IT107" /></xsl:when>
      <xsl:when test="$it1/IT108=$qualifier"><xsl:value-of select="$it1/IT109" /></xsl:when>
      <xsl:when test="$it1/IT110=$qualifier"><xsl:value-of select="$it1/IT111" /></xsl:when>
    </xsl:choose>
  </xsl:template>

  <xsl:template match="/s0:X12_00401_810">
    <xsl:variable name="vendor" select="string(s0:REF[REF01='IA']/REF02)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpVendor($vendor)" />
    <xsl:variable name="invDate" select="ScriptNS0:X12ToXmlDate(string(s0:BIG/BIG01))" />
    <ns0:Invoice>
      <ns0:Header>
        <ns0:InvoiceNumber><xsl:value-of select="s0:BIG/BIG02" /></ns0:InvoiceNumber>
        <ns0:Kind><xsl:choose><xsl:when test="s0:BIG/BIG07='CR'">SupplierCreditNote</xsl:when><xsl:otherwise>SupplierInvoice</xsl:otherwise></xsl:choose></ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:InvoiceDate><xsl:value-of select="$invDate" /></ns0:InvoiceDate>
        <xsl:if test="s0:BIG/BIG04"><ns0:OrderNumber><xsl:value-of select="s0:BIG/BIG04" /></ns0:OrderNumber></xsl:if>
        <xsl:if test="s0:REF[REF01='BM']"><ns0:DespatchNumber><xsl:value-of select="s0:REF[REF01='BM']/REF02" /></ns0:DespatchNumber></xsl:if>
        <ns0:Currency><xsl:value-of select="ScriptNS4:Coalesce(string(s0:CUR/CUR02), ScriptNS2:Currency($partner))" /></ns0:Currency>
        <xsl:if test="s0:ITD/ITD07">
          <ns0:PaymentTermsDays><xsl:value-of select="number(s0:ITD/ITD07)" /></ns0:PaymentTermsDays>
          <ns0:DueDate>
            <xsl:choose>
              <xsl:when test="s0:ITD/ITD06"><xsl:value-of select="ScriptNS0:X12ToXmlDate(string(s0:ITD/ITD06))" /></xsl:when>
              <xsl:otherwise><xsl:value-of select="ScriptNS0:DueDate($invDate, string(s0:ITD/ITD07))" /></xsl:otherwise>
            </xsl:choose>
          </ns0:DueDate>
        </xsl:if>
        <ns0:ErpPartnerNumber><xsl:value-of select="$vendor" /></ns0:ErpPartnerNumber>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="s0:N1Loop1">
          <ns0:Party>
            <ns0:Role><xsl:value-of select="s0:N1/N101" /></ns0:Role>
            <xsl:if test="s0:N1/N104"><ns0:PartnerCode><xsl:value-of select="s0:N1/N104" /></ns0:PartnerCode></xsl:if>
            <xsl:if test="s0:N1/N103='UL'"><ns0:Gln><xsl:value-of select="s0:N1/N104" /></ns0:Gln></xsl:if>
            <xsl:if test="s0:N1/N101='SE'"><ns0:InternalId><xsl:value-of select="$vendor" /></ns0:InternalId></xsl:if>
            <xsl:if test="s0:N1/N102"><ns0:Name><xsl:value-of select="s0:N1/N102" /></ns0:Name></xsl:if>
            <xsl:if test="s0:N3/N301"><ns0:Street1><xsl:value-of select="s0:N3[1]/N301" /></ns0:Street1></xsl:if>
            <xsl:if test="s0:N4/N401"><ns0:City><xsl:value-of select="s0:N4/N401" /></ns0:City></xsl:if>
            <xsl:if test="s0:N4/N402"><ns0:Region><xsl:value-of select="s0:N4/N402" /></ns0:Region></xsl:if>
            <xsl:if test="s0:N4/N403"><ns0:PostalCode><xsl:value-of select="s0:N4/N403" /></ns0:PostalCode></xsl:if>
            <ns0:Country><xsl:value-of select="ScriptNS4:Coalesce(string(s0:N4/N404), 'US')" /></ns0:Country>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="s0:IT1Loop1">
          <xsl:variable name="it1" select="s0:IT1" />
          <xsl:variable name="ourItem"><xsl:call-template name="item"><xsl:with-param name="it1" select="$it1" /><xsl:with-param name="qualifier" select="'BP'" /></xsl:call-template></xsl:variable>
          <xsl:variable name="supplierItem"><xsl:call-template name="item"><xsl:with-param name="it1" select="$it1" /><xsl:with-param name="qualifier" select="'VP'" /></xsl:call-template></xsl:variable>
          <xsl:variable name="upc"><xsl:call-template name="item"><xsl:with-param name="it1" select="$it1" /><xsl:with-param name="qualifier" select="'UP'" /></xsl:call-template></xsl:variable>
          <xsl:variable name="gtin" select="ScriptNS5:Gtin14(string($upc))" />
          <ns0:Line>
            <ns0:LineNumber><xsl:value-of select="position()" /></ns0:LineNumber>
            <xsl:if test="number($it1/IT101) = number($it1/IT101)"><ns0:OrderLineNumber><xsl:value-of select="number($it1/IT101)" /></ns0:OrderLineNumber></xsl:if>
            <ns0:Item>
              <xsl:choose>
                <xsl:when test="$ourItem != ''"><xsl:value-of select="$ourItem" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="ScriptNS2:ItemToInternal($partner, string($supplierItem), $gtin)" /></xsl:otherwise>
              </xsl:choose>
            </ns0:Item>
            <xsl:if test="$supplierItem != ''"><ns0:PartnerItem><xsl:value-of select="$supplierItem" /></ns0:PartnerItem></xsl:if>
            <xsl:if test="$gtin != ''"><ns0:Gtin><xsl:value-of select="$gtin" /></ns0:Gtin></xsl:if>
            <xsl:if test="s0:PIDLoop1/s0:PID/PID05"><ns0:Description><xsl:value-of select="ScriptNS4:Truncate(string(s0:PIDLoop1[1]/s0:PID/PID05), 80)" /></ns0:Description></xsl:if>
            <ns0:Quantity><xsl:value-of select="$it1/IT102" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="ScriptNS1:X12ToErp($partner, string($it1/IT103))" /></ns0:Uom>
            <ns0:UnitPrice><xsl:value-of select="$it1/IT104" /></ns0:UnitPrice>
            <ns0:PriceBasis>1</ns0:PriceBasis>
            <ns0:LineAmount><xsl:value-of select="ScriptNS3:LineAmount(string($it1/IT102), string($it1/IT104), '1')" /></ns0:LineAmount>
            <xsl:if test="s0:TXI/TXI03"><ns0:TaxRate><xsl:value-of select="s0:TXI[1]/TXI03" /></ns0:TaxRate></xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(s0:IT1Loop1)" /></ns0:LineCount>
        <xsl:variable name="tax" select="ScriptNS4:Coalesce(string(s0:TXI[TXI01='ST' or TXI01='VA']/TXI02), '0')" />
        <xsl:variable name="total" select="ScriptNS3:FromImpliedDecimal(string(s0:TDS/TDS01), 2)" />
        <ns0:LineTotal>
          <xsl:choose>
            <xsl:when test="s0:TDS/TDS02"><xsl:value-of select="ScriptNS3:FromImpliedDecimal(string(s0:TDS/TDS02), 2)" /></xsl:when>
            <xsl:otherwise><xsl:value-of select="ScriptNS3:Format(string(number($total) - number($tax)), 2)" /></xsl:otherwise>
          </xsl:choose>
        </ns0:LineTotal>
        <ns0:TaxTotal><xsl:value-of select="ScriptNS3:Format($tax, 2)" /></ns0:TaxTotal>
        <ns0:GrandTotal><xsl:value-of select="$total" /></ns0:GrandTotal>
      </ns0:Summary>
    </ns0:Invoice>
  </xsl:template>
</xsl:stylesheet>
