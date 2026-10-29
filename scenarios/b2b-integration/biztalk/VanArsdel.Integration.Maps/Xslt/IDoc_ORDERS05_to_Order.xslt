<?xml version="1.0" encoding="utf-8"?>
<!-- Purchase order from the ERP (ORDERS05 outbound, partner type LI = vendor) to the canonical order (Kind
     PurchaseOrder). Our ship-to is the receiving DC (partner function WE with its GLN). -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/erp/idoc/2013"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS2 ScriptNS3 ScriptNS4">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:ORDERS05/IDOC[1]" />
  </xsl:template>

  <xsl:template match="IDOC">
    <xsl:variable name="vendor" select="string(E1EDKA1[PARVW='LF']/PARTN)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpVendor($vendor)" />
    <ns0:Order>
      <ns0:Header>
        <ns0:OrderNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDK01/BELNR))" /></ns0:OrderNumber>
        <ns0:Kind>PurchaseOrder</ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:OrderDate><xsl:value-of select="ScriptNS0:IDocToXmlDate(string(E1EDK03[IDDAT='012']/DATUM))" /></ns0:OrderDate>
        <xsl:if test="E1EDK03[IDDAT='002']">
          <ns0:RequestedDeliveryDate><xsl:value-of select="ScriptNS0:IDocToXmlDate(string(E1EDK03[IDDAT='002']/DATUM))" /></ns0:RequestedDeliveryDate>
        </xsl:if>
        <ns0:OrderType><xsl:value-of select="ScriptNS4:Coalesce(string(E1EDK01/BSART), 'NB')" /></ns0:OrderType>
        <ns0:Currency><xsl:value-of select="E1EDK01/CURCY" /></ns0:Currency>
        <ns0:ErpPartnerNumber><xsl:value-of select="ScriptNS4:TrimLeadingZeros($vendor)" /></ns0:ErpPartnerNumber>
        <ns0:ErpOrderNumber><xsl:value-of select="E1EDK01/BELNR" /></ns0:ErpOrderNumber>
        <ns0:CustomerReference><xsl:value-of select="ScriptNS4:TrimLeadingZeros(string(E1EDK01/BELNR))" /></ns0:CustomerReference>
      </ns0:Header>
      <ns0:Parties>
        <ns0:Party>
          <ns0:Role>BY</ns0:Role>
          <ns0:Gln>0614141000005</ns0:Gln>
          <ns0:InternalId>VANARSDEL</ns0:InternalId>
          <ns0:Name>VanArsdel Distribution</ns0:Name>
        </ns0:Party>
        <xsl:for-each select="E1EDKA1[PARVW='WE']">
          <ns0:Party>
            <ns0:Role>ST</ns0:Role>
            <xsl:if test="ILNNR"><ns0:PartnerCode><xsl:value-of select="ILNNR" /></ns0:PartnerCode></xsl:if>
            <xsl:if test="ILNNR"><ns0:Gln><xsl:value-of select="ILNNR" /></ns0:Gln></xsl:if>
            <ns0:InternalId><xsl:value-of select="PARTN" /></ns0:InternalId>
            <xsl:if test="NAME1"><ns0:Name><xsl:value-of select="NAME1" /></ns0:Name></xsl:if>
            <xsl:if test="STRAS"><ns0:Street1><xsl:value-of select="STRAS" /></ns0:Street1></xsl:if>
            <xsl:if test="ORT01"><ns0:City><xsl:value-of select="ORT01" /></ns0:City></xsl:if>
            <xsl:if test="REGIO"><ns0:Region><xsl:value-of select="REGIO" /></ns0:Region></xsl:if>
            <xsl:if test="PSTLZ"><ns0:PostalCode><xsl:value-of select="PSTLZ" /></ns0:PostalCode></xsl:if>
            <xsl:if test="LAND1"><ns0:Country><xsl:value-of select="LAND1" /></ns0:Country></xsl:if>
          </ns0:Party>
        </xsl:for-each>
        <ns0:Party>
          <ns0:Role>SU</ns0:Role>
          <ns0:Gln><xsl:value-of select="ScriptNS2:Gln($partner)" /></ns0:Gln>
          <ns0:InternalId><xsl:value-of select="ScriptNS4:TrimLeadingZeros($vendor)" /></ns0:InternalId>
          <ns0:Name><xsl:value-of select="ScriptNS2:PartnerName($partner)" /></ns0:Name>
        </ns0:Party>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="E1EDP01">
          <xsl:variable name="item" select="string(E1EDP19[QUALF='002']/IDTNR)" />
          <ns0:Line>
            <ns0:LineNumber><xsl:value-of select="number(POSEX)" /></ns0:LineNumber>
            <ns0:PartnerItem><xsl:value-of select="ScriptNS4:Coalesce(string(E1EDP19[QUALF='001']/IDTNR), ScriptNS2:ItemToPartner($partner, $item))" /></ns0:PartnerItem>
            <xsl:if test="ScriptNS2:GtinOfItem($item) != ''"><ns0:Gtin><xsl:value-of select="ScriptNS2:GtinOfItem($item)" /></ns0:Gtin></xsl:if>
            <ns0:Item><xsl:value-of select="$item" /></ns0:Item>
            <xsl:if test="E1EDP19[QUALF='002']/KTEXT"><ns0:Description><xsl:value-of select="E1EDP19[QUALF='002']/KTEXT" /></ns0:Description></xsl:if>
            <ns0:Quantity><xsl:value-of select="ScriptNS3:Real(string(MENGE))" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="MENEE" /></ns0:Uom>
            <xsl:if test="VPREI">
              <ns0:UnitPrice><xsl:value-of select="ScriptNS3:Real(string(VPREI))" /></ns0:UnitPrice>
              <ns0:PriceBasis><xsl:value-of select="ScriptNS4:Coalesce(string(PEINH), '1')" /></ns0:PriceBasis>
            </xsl:if>
            <xsl:if test="E1EDP20/EDATU">
              <ns0:RequestedDate><xsl:value-of select="ScriptNS0:IDocToXmlDate(string(E1EDP20[1]/EDATU))" /></ns0:RequestedDate>
            </xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(E1EDP01)" /></ns0:LineCount>
      </ns0:Summary>
    </ns0:Order>
  </xsl:template>
</xsl:stylesheet>
