<?xml version="1.0" encoding="utf-8"?>
<!-- Customer purchase order X12 850 (004010) to the canonical order. Implementation guide VA-IG-850 v3.2:
     N1*BY carries our customer account in N104 with N103 = 91, ship-to by the customer's store/branch code (92). -->
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
    <xsl:apply-templates select="/s0:X12_00401_850" />
  </xsl:template>

  <xsl:template match="/s0:X12_00401_850">
    <xsl:variable name="account" select="string(s0:N1Loop1[s0:N1/N101='BY']/s0:N1/N104)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpCustomer($account)" />
    <ns0:Order>
      <ns0:Header>
        <ns0:OrderNumber><xsl:value-of select="s0:BEG/BEG03" /></ns0:OrderNumber>
        <ns0:Kind>CustomerOrder</ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:OrderDate><xsl:value-of select="ScriptNS0:X12ToXmlDate(string(s0:BEG/BEG05))" /></ns0:OrderDate>
        <xsl:if test="s0:DTM[DTM01='002']/DTM02">
          <ns0:RequestedDeliveryDate><xsl:value-of select="ScriptNS0:X12ToXmlDate(string(s0:DTM[DTM01='002']/DTM02))" /></ns0:RequestedDeliveryDate>
        </xsl:if>
        <ns0:OrderType>
          <xsl:choose>
            <xsl:when test="s0:BEG/BEG02='DS'">ZDRP</xsl:when>
            <xsl:when test="s0:BEG/BEG02='RL'">ZREL</xsl:when>
            <xsl:when test="s0:BEG/BEG02='KN'">ZRSH</xsl:when>
            <xsl:otherwise>ZEDI</xsl:otherwise>
          </xsl:choose>
        </ns0:OrderType>
        <ns0:Currency>
          <xsl:choose>
            <xsl:when test="s0:CUR/CUR02"><xsl:value-of select="s0:CUR/CUR02" /></xsl:when>
            <xsl:otherwise><xsl:value-of select="ScriptNS2:Currency($partner)" /></xsl:otherwise>
          </xsl:choose>
        </ns0:Currency>
        <xsl:if test="s0:TD5/TD503">
          <ns0:ShipVia><xsl:value-of select="s0:TD5/TD503" /></ns0:ShipVia>
        </xsl:if>
        <ns0:ErpPartnerNumber><xsl:value-of select="$account" /></ns0:ErpPartnerNumber>
        <ns0:CustomerReference>
          <xsl:value-of select="ScriptNS4:Coalesce(string(s0:REF[REF01='CO']/REF02), string(s0:BEG/BEG03))" />
        </ns0:CustomerReference>
        <xsl:if test="s0:N9Loop1/s0:MSG/MSG01">
          <ns0:Notes><xsl:value-of select="ScriptNS4:Truncate(string(s0:N9Loop1[1]/s0:MSG[1]/MSG01), 250)" /></ns0:Notes>
        </xsl:if>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="s0:N1Loop1">
          <ns0:Party>
            <ns0:Role><xsl:value-of select="s0:N1/N101" /></ns0:Role>
            <xsl:if test="s0:N1/N104"><ns0:PartnerCode><xsl:value-of select="s0:N1/N104" /></ns0:PartnerCode></xsl:if>
            <ns0:InternalId>
              <xsl:choose>
                <xsl:when test="s0:N1/N101='ST'"><xsl:value-of select="ScriptNS2:ShipToInternal($partner, string(s0:N1/N104))" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="s0:N1/N104" /></xsl:otherwise>
              </xsl:choose>
            </ns0:InternalId>
            <xsl:if test="s0:N1/N102"><ns0:Name><xsl:value-of select="s0:N1/N102" /></ns0:Name></xsl:if>
            <xsl:if test="s0:N3/N301"><ns0:Street1><xsl:value-of select="s0:N3/N301" /></ns0:Street1></xsl:if>
            <xsl:if test="s0:N3/N302"><ns0:Street2><xsl:value-of select="s0:N3/N302" /></ns0:Street2></xsl:if>
            <xsl:if test="s0:N4/N401"><ns0:City><xsl:value-of select="s0:N4/N401" /></ns0:City></xsl:if>
            <xsl:if test="s0:N4/N402"><ns0:Region><xsl:value-of select="s0:N4/N402" /></ns0:Region></xsl:if>
            <xsl:if test="s0:N4/N403"><ns0:PostalCode><xsl:value-of select="s0:N4/N403" /></ns0:PostalCode></xsl:if>
            <ns0:Country><xsl:value-of select="ScriptNS4:Coalesce(string(s0:N4/N404), 'US')" /></ns0:Country>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="s0:PO1Loop1">
          <xsl:variable name="po1" select="s0:PO1" />
          <xsl:variable name="buyerPart">
            <xsl:call-template name="productId"><xsl:with-param name="seg" select="$po1" /><xsl:with-param name="q" select="'BP'" /></xsl:call-template>
          </xsl:variable>
          <xsl:variable name="upc">
            <xsl:call-template name="productId"><xsl:with-param name="seg" select="$po1" /><xsl:with-param name="q" select="'UP'" /></xsl:call-template>
          </xsl:variable>
          <xsl:variable name="ean">
            <xsl:call-template name="productId"><xsl:with-param name="seg" select="$po1" /><xsl:with-param name="q" select="'EN'" /></xsl:call-template>
          </xsl:variable>
          <xsl:variable name="vendorPart">
            <xsl:call-template name="productId"><xsl:with-param name="seg" select="$po1" /><xsl:with-param name="q" select="'VP'" /></xsl:call-template>
          </xsl:variable>
          <xsl:variable name="gtin" select="ScriptNS5:Gtin14(ScriptNS4:Coalesce(string($upc), string($ean)))" />
          <ns0:Line>
            <ns0:LineNumber>
              <xsl:choose>
                <xsl:when test="number($po1/PO101) = number($po1/PO101)"><xsl:value-of select="number($po1/PO101)" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="position()" /></xsl:otherwise>
              </xsl:choose>
            </ns0:LineNumber>
            <xsl:if test="string($buyerPart) != ''"><ns0:PartnerItem><xsl:value-of select="$buyerPart" /></ns0:PartnerItem></xsl:if>
            <xsl:if test="$gtin != ''"><ns0:Gtin><xsl:value-of select="$gtin" /></ns0:Gtin></xsl:if>
            <ns0:Item>
              <xsl:choose>
                <!-- some customers send our material number as the vendor part number -->
                <xsl:when test="string($vendorPart) != '' and string($buyerPart) = '' and $gtin = ''"><xsl:value-of select="$vendorPart" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="ScriptNS2:ItemToInternal($partner, string($buyerPart), $gtin)" /></xsl:otherwise>
              </xsl:choose>
            </ns0:Item>
            <xsl:if test="s0:PIDLoop1/s0:PID/PID05">
              <ns0:Description><xsl:value-of select="ScriptNS4:Truncate(string(s0:PIDLoop1[1]/s0:PID/PID05), 80)" /></ns0:Description>
            </xsl:if>
            <ns0:Quantity><xsl:value-of select="$po1/PO102" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="ScriptNS1:X12ToErp($partner, string($po1/PO103))" /></ns0:Uom>
            <xsl:if test="$po1/PO104"><ns0:UnitPrice><xsl:value-of select="$po1/PO104" /></ns0:UnitPrice></xsl:if>
            <xsl:if test="$po1/PO104"><ns0:PriceBasis>1</ns0:PriceBasis></xsl:if>
            <xsl:if test="s0:DTM[DTM01='002']/DTM02">
              <ns0:RequestedDate><xsl:value-of select="ScriptNS0:X12ToXmlDate(string(s0:DTM[DTM01='002']/DTM02))" /></ns0:RequestedDate>
            </xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(s0:PO1Loop1)" /></ns0:LineCount>
        <xsl:if test="s0:CTTLoop1/s0:AMT/AMT02">
          <ns0:TotalAmount><xsl:value-of select="s0:CTTLoop1/s0:AMT/AMT02" /></ns0:TotalAmount>
        </xsl:if>
      </ns0:Summary>
    </ns0:Order>
  </xsl:template>

  <!-- Product/service id by qualifier from the qualifier/value pairs 06/07, 08/09, 10/11 of PO1 -->
  <xsl:template name="productId">
    <xsl:param name="seg" />
    <xsl:param name="q" />
    <xsl:choose>
      <xsl:when test="$seg/PO106 = $q"><xsl:value-of select="$seg/PO107" /></xsl:when>
      <xsl:when test="$seg/PO108 = $q"><xsl:value-of select="$seg/PO109" /></xsl:when>
      <xsl:when test="$seg/PO110 = $q"><xsl:value-of select="$seg/PO111" /></xsl:when>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
