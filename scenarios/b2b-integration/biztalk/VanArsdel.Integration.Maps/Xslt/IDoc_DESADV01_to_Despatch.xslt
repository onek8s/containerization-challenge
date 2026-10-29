<?xml version="1.0" encoding="utf-8"?>
<!-- Outbound delivery (goods issue) DESADV01 IDoc from the ERP to the canonical despatch. Handling units (E1EDL37)
     become packages; their contents (E1EDL44) are joined to the delivery items (E1EDL24) by POSNR. -->
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

  <xsl:key name="item" match="E1EDL24" use="POSNR" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:DESADV01/IDOC[1]/E1EDL20" />
  </xsl:template>

  <xsl:template match="E1EDL20">
    <xsl:variable name="soldTo" select="string(E1ADRM1[PARTNER_Q='AG']/PARTNER_ID)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpCustomer($soldTo)" />
    <ns0:Despatch>
      <ns0:Header>
        <ns0:DespatchNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(VBELN))" /></ns0:DespatchNumber>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:ShipDate><xsl:value-of select="ScriptNS0:IDocToXmlDate(substring(string(E1EDT13[QUALF='006']/NTANF), 1, 8))" /></ns0:ShipDate>
        <xsl:if test="E1EDT13[QUALF='007']">
          <ns0:EstimatedDeliveryDate><xsl:value-of select="ScriptNS0:IDocToXmlDate(substring(string(E1EDT13[QUALF='007']/NTANF), 1, 8))" /></ns0:EstimatedDeliveryDate>
        </xsl:if>
        <ns0:OrderNumber><xsl:value-of select="E1EDL24[E1EDL41/QUALI='001'][1]/E1EDL41/BSTNR" /></ns0:OrderNumber>
        <xsl:if test="E1EDL24/VGBEL"><ns0:ErpOrderNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDL24[VGBEL][1]/VGBEL))" /></ns0:ErpOrderNumber></xsl:if>
        <xsl:if test="ROUTE"><ns0:Carrier><xsl:value-of select="ScriptNS2:CarrierToPartner($partner, string(ROUTE))" /></ns0:Carrier></xsl:if>
        <xsl:if test="TRAID"><ns0:TrackingNumber><xsl:value-of select="TRAID" /></ns0:TrackingNumber></xsl:if>
        <xsl:if test="BTGEW"><ns0:GrossWeightKg><xsl:value-of select="ScriptNS3:Real(string(BTGEW))" /></ns0:GrossWeightKg></xsl:if>
        <ns0:PackageCount><xsl:value-of select="count(E1EDL37)" /></ns0:PackageCount>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="E1ADRM1[PARTNER_Q='AG' or PARTNER_Q='WE']">
          <ns0:Party>
            <ns0:Role>
              <xsl:choose>
                <xsl:when test="PARTNER_Q='WE'">ST</xsl:when>
                <xsl:otherwise>BY</xsl:otherwise>
              </xsl:choose>
            </ns0:Role>
            <ns0:PartnerCode>
              <xsl:choose>
                <xsl:when test="PARTNER_Q='WE'"><xsl:value-of select="ScriptNS2:ShipToPartner($partner, ScriptNS4:TrimLeadingZeros(string(PARTNER_ID)))" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(PARTNER_ID))" /></xsl:otherwise>
              </xsl:choose>
            </ns0:PartnerCode>
            <ns0:InternalId><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(PARTNER_ID))" /></ns0:InternalId>
            <xsl:if test="NAME1"><ns0:Name><xsl:value-of select="NAME1" /></ns0:Name></xsl:if>
            <xsl:if test="STREET1"><ns0:Street1><xsl:value-of select="STREET1" /></ns0:Street1></xsl:if>
            <xsl:if test="CITY1"><ns0:City><xsl:value-of select="CITY1" /></ns0:City></xsl:if>
            <xsl:if test="REGION"><ns0:Region><xsl:value-of select="REGION" /></ns0:Region></xsl:if>
            <xsl:if test="POSTL_COD1"><ns0:PostalCode><xsl:value-of select="POSTL_COD1" /></ns0:PostalCode></xsl:if>
            <xsl:if test="COUNTRY1"><ns0:Country><xsl:value-of select="COUNTRY1" /></ns0:Country></xsl:if>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Packages>
        <xsl:for-each select="E1EDL37">
          <ns0:Package>
            <ns0:Sscc><xsl:value-of select="EXIDV" /></ns0:Sscc>
            <ns0:Lines>
              <xsl:for-each select="E1EDL44">
                <xsl:variable name="it" select="key('item', POSNR)" />
                <ns0:Line>
                  <ns0:LineNumber><xsl:value-of select="number(POSNR)" /></ns0:LineNumber>
                  <xsl:if test="$it/E1EDL41/POSEX"><ns0:OrderLineNumber><xsl:value-of select="number($it/E1EDL41/POSEX)" /></ns0:OrderLineNumber></xsl:if>
                  <ns0:Item><xsl:value-of select="$it/MATNR" /></ns0:Item>
                  <xsl:if test="$it/KDMAT"><ns0:PartnerItem><xsl:value-of select="$it/KDMAT" /></ns0:PartnerItem></xsl:if>
                  <xsl:if test="$it/EAN11"><ns0:Gtin><xsl:value-of select="ScriptNS5:Gtin14(string($it/EAN11))" /></ns0:Gtin></xsl:if>
                  <xsl:if test="$it/ARKTX"><ns0:Description><xsl:value-of select="$it/ARKTX" /></ns0:Description></xsl:if>
                  <ns0:Quantity><xsl:value-of select="ScriptNS3:Real(string(VEMNG))" /></ns0:Quantity>
                  <ns0:Uom><xsl:value-of select="$it/VRKME" /></ns0:Uom>
                </ns0:Line>
              </xsl:for-each>
            </ns0:Lines>
          </ns0:Package>
        </xsl:for-each>
      </ns0:Packages>
    </ns0:Despatch>
  </xsl:template>
</xsl:stylesheet>
