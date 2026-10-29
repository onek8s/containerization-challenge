<?xml version="1.0" encoding="utf-8"?>
<!-- Partner gateway order (one Order of an OrderBatch after debatching) to the canonical order. Gateway customers
     order with our own material numbers (Sku) and their account number. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/partners/orders/2016"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    exclude-result-prefixes="s0 ScriptNS1 ScriptNS2 ScriptNS4">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Order" />
  </xsl:template>

  <xsl:template match="/s0:Order">
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromErpCustomer(string(s0:AccountNumber))" />
    <ns0:Order>
      <ns0:Header>
        <ns0:OrderNumber><xsl:value-of select="s0:PoNumber" /></ns0:OrderNumber>
        <ns0:Kind>CustomerOrder</ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:OrderDate><xsl:value-of select="s0:OrderDate" /></ns0:OrderDate>
        <xsl:if test="s0:DeliverBy">
          <ns0:RequestedDeliveryDate><xsl:value-of select="s0:DeliverBy" /></ns0:RequestedDeliveryDate>
        </xsl:if>
        <ns0:OrderType>ZWEB</ns0:OrderType>
        <ns0:Currency><xsl:value-of select="ScriptNS2:Currency($partner)" /></ns0:Currency>
        <ns0:ErpPartnerNumber><xsl:value-of select="s0:AccountNumber" /></ns0:ErpPartnerNumber>
        <ns0:CustomerReference><xsl:value-of select="s0:PoNumber" /></ns0:CustomerReference>
        <xsl:if test="s0:Instructions">
          <ns0:Notes><xsl:value-of select="ScriptNS4:Truncate(string(s0:Instructions), 250)" /></ns0:Notes>
        </xsl:if>
      </ns0:Header>
      <ns0:Parties>
        <ns0:Party>
          <ns0:Role>BY</ns0:Role>
          <ns0:PartnerCode><xsl:value-of select="s0:AccountNumber" /></ns0:PartnerCode>
          <ns0:InternalId><xsl:value-of select="s0:AccountNumber" /></ns0:InternalId>
          <ns0:Name><xsl:value-of select="ScriptNS2:PartnerName($partner)" /></ns0:Name>
        </ns0:Party>
        <xsl:for-each select="s0:DeliveryAddress">
          <ns0:Party>
            <ns0:Role>ST</ns0:Role>
            <!-- one-time delivery address: the ERP creates a one-time ship-to from the address fields -->
            <ns0:InternalId>ONETIME</ns0:InternalId>
            <ns0:Name><xsl:value-of select="ScriptNS4:Truncate(string(s0:Name), 35)" /></ns0:Name>
            <ns0:Street1><xsl:value-of select="ScriptNS4:Truncate(string(s0:Line1), 35)" /></ns0:Street1>
            <xsl:if test="s0:Line2"><ns0:Street2><xsl:value-of select="ScriptNS4:Truncate(string(s0:Line2), 35)" /></ns0:Street2></xsl:if>
            <ns0:City><xsl:value-of select="s0:Town" /></ns0:City>
            <xsl:if test="s0:Postcode"><ns0:PostalCode><xsl:value-of select="s0:Postcode" /></ns0:PostalCode></xsl:if>
            <ns0:Country><xsl:value-of select="ScriptNS4:Upper(string(s0:Country))" /></ns0:Country>
          </ns0:Party>
        </xsl:for-each>
        <xsl:if test="not(s0:DeliveryAddress)">
          <!-- no delivery address: the goods go to the account's own ship-to (same number as the sold-to) -->
          <ns0:Party>
            <ns0:Role>ST</ns0:Role>
            <ns0:PartnerCode><xsl:value-of select="s0:AccountNumber" /></ns0:PartnerCode>
            <ns0:InternalId><xsl:value-of select="s0:AccountNumber" /></ns0:InternalId>
          </ns0:Party>
        </xsl:if>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="s0:Item">
          <ns0:Line>
            <ns0:LineNumber><xsl:value-of select="position() * 10" /></ns0:LineNumber>
            <ns0:Item><xsl:value-of select="ScriptNS4:Upper(string(s0:Sku))" /></ns0:Item>
            <xsl:if test="s0:Text"><ns0:Description><xsl:value-of select="ScriptNS4:Truncate(string(s0:Text), 80)" /></ns0:Description></xsl:if>
            <ns0:Quantity><xsl:value-of select="s0:Qty" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="ScriptNS1:TextToErp($partner, string(s0:Unit))" /></ns0:Uom>
            <xsl:if test="s0:Price">
              <ns0:UnitPrice><xsl:value-of select="s0:Price" /></ns0:UnitPrice>
              <ns0:PriceBasis>1</ns0:PriceBasis>
            </xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(s0:Item)" /></ns0:LineCount>
      </ns0:Summary>
    </ns0:Order>
  </xsl:template>
</xsl:stylesheet>
