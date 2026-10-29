<?xml version="1.0" encoding="utf-8"?>
<!-- Customer order EDIFACT ORDERS D.96A (EANCOM 97 subset) to the canonical order. The buyer is identified by the
     GLN in NAD+BY, the delivery point by NAD+DP; items by GTIN (LIN) or buyer's article number (PIA+1 ...:IN). -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS1 ScriptNS2 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:EFACT_D96A_ORDERS" />
  </xsl:template>

  <xsl:template match="/s0:EFACT_D96A_ORDERS">
    <xsl:variable name="buyerGln" select="string(s0:NADLoop1[s0:NAD/NAD01='BY']/s0:NAD/C082/C08201)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromGln($buyerGln)" />
    <ns0:Order>
      <ns0:Header>
        <ns0:OrderNumber><xsl:value-of select="s0:BGM/BGM02" /></ns0:OrderNumber>
        <ns0:Kind>CustomerOrder</ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:OrderDate><xsl:value-of select="ScriptNS0:EdifactToXmlDate(string(s0:DTM[C507/C50701='137']/C507/C50702), string(s0:DTM[C507/C50701='137']/C507/C50703))" /></ns0:OrderDate>
        <xsl:if test="s0:DTM[C507/C50701='2']">
          <ns0:RequestedDeliveryDate><xsl:value-of select="ScriptNS0:EdifactToXmlDate(string(s0:DTM[C507/C50701='2']/C507/C50702), string(s0:DTM[C507/C50701='2']/C507/C50703))" /></ns0:RequestedDeliveryDate>
        </xsl:if>
        <ns0:OrderType>
          <xsl:choose>
            <xsl:when test="s0:BGM/C002/C00201='226'">ZREL</xsl:when>
            <xsl:when test="s0:BGM/C002/C00201='221'">ZRSH</xsl:when>
            <xsl:otherwise>ZEDI</xsl:otherwise>
          </xsl:choose>
        </ns0:OrderType>
        <ns0:Currency>
          <xsl:choose>
            <xsl:when test="s0:CUXLoop1/s0:CUX/C504/C50402"><xsl:value-of select="s0:CUXLoop1[1]/s0:CUX/C504/C50402" /></xsl:when>
            <xsl:otherwise><xsl:value-of select="ScriptNS2:Currency($partner)" /></xsl:otherwise>
          </xsl:choose>
        </ns0:Currency>
        <xsl:if test="s0:TDTLoop1/s0:TDT/C040/C04001">
          <ns0:ShipVia><xsl:value-of select="s0:TDTLoop1[1]/s0:TDT/C040/C04001" /></ns0:ShipVia>
        </xsl:if>
        <ns0:ErpPartnerNumber><xsl:value-of select="ScriptNS2:ErpNumber($partner)" /></ns0:ErpPartnerNumber>
        <ns0:CustomerReference><xsl:value-of select="ScriptNS4:Coalesce(string(s0:RFFLoop1/s0:RFF[C506/C50601='CR']/C506/C50602), string(s0:BGM/BGM02))" /></ns0:CustomerReference>
        <xsl:if test="s0:FTX[FTX01='DEL' or FTX01='AAI']">
          <ns0:Notes><xsl:value-of select="ScriptNS4:Truncate(string(s0:FTX[FTX01='DEL' or FTX01='AAI'][1]/C108/C10801), 250)" /></ns0:Notes>
        </xsl:if>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="s0:NADLoop1">
          <xsl:variable name="nad" select="s0:NAD" />
          <xsl:variable name="role">
            <xsl:choose>
              <xsl:when test="$nad/NAD01='DP' or $nad/NAD01='ST'">ST</xsl:when>
              <xsl:when test="$nad/NAD01='IV'">BT</xsl:when>
              <xsl:otherwise><xsl:value-of select="$nad/NAD01" /></xsl:otherwise>
            </xsl:choose>
          </xsl:variable>
          <ns0:Party>
            <ns0:Role><xsl:value-of select="$role" /></ns0:Role>
            <xsl:if test="$nad/C082/C08201"><ns0:PartnerCode><xsl:value-of select="$nad/C082/C08201" /></ns0:PartnerCode></xsl:if>
            <xsl:if test="$nad/C082/C08203='9'"><ns0:Gln><xsl:value-of select="$nad/C082/C08201" /></ns0:Gln></xsl:if>
            <ns0:InternalId>
              <xsl:choose>
                <xsl:when test="$role='ST'"><xsl:value-of select="ScriptNS2:ShipToInternal($partner, string($nad/C082/C08201))" /></xsl:when>
                <xsl:when test="$role='BY'"><xsl:value-of select="ScriptNS2:ErpNumber($partner)" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="$nad/C082/C08201" /></xsl:otherwise>
              </xsl:choose>
            </ns0:InternalId>
            <xsl:if test="$nad/C080/C08001"><ns0:Name><xsl:value-of select="$nad/C080/C08001" /></ns0:Name></xsl:if>
            <xsl:if test="$nad/C059/C05901"><ns0:Street1><xsl:value-of select="$nad/C059/C05901" /></ns0:Street1></xsl:if>
            <xsl:if test="$nad/C059/C05902"><ns0:Street2><xsl:value-of select="$nad/C059/C05902" /></ns0:Street2></xsl:if>
            <xsl:if test="$nad/NAD06"><ns0:City><xsl:value-of select="$nad/NAD06" /></ns0:City></xsl:if>
            <xsl:if test="$nad/NAD07"><ns0:Region><xsl:value-of select="$nad/NAD07" /></ns0:Region></xsl:if>
            <xsl:if test="$nad/NAD08"><ns0:PostalCode><xsl:value-of select="$nad/NAD08" /></ns0:PostalCode></xsl:if>
            <xsl:if test="$nad/NAD09"><ns0:Country><xsl:value-of select="$nad/NAD09" /></ns0:Country></xsl:if>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="s0:LINLoop1">
          <xsl:variable name="gtin" select="ScriptNS5:Gtin14(string(s0:LIN[C212/C21202='EN']/C212/C21201))" />
          <xsl:variable name="buyerItem" select="string(s0:PIA[C212/C21202='IN' or C212/C21202='BP']/C212/C21201)" />
          <xsl:variable name="qty" select="s0:QTY[C186/C18601='21']/C186" />
          <ns0:Line>
            <ns0:LineNumber>
              <xsl:choose>
                <xsl:when test="number(s0:LIN/LIN01) = number(s0:LIN/LIN01)"><xsl:value-of select="number(s0:LIN/LIN01)" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="position()" /></xsl:otherwise>
              </xsl:choose>
            </ns0:LineNumber>
            <xsl:if test="$buyerItem != ''"><ns0:PartnerItem><xsl:value-of select="$buyerItem" /></ns0:PartnerItem></xsl:if>
            <xsl:if test="$gtin != ''"><ns0:Gtin><xsl:value-of select="$gtin" /></ns0:Gtin></xsl:if>
            <ns0:Item><xsl:value-of select="ScriptNS2:ItemToInternal($partner, $buyerItem, $gtin)" /></ns0:Item>
            <xsl:if test="s0:IMD/C273/C27304">
              <ns0:Description><xsl:value-of select="ScriptNS4:Truncate(string(s0:IMD[1]/C273/C27304), 80)" /></ns0:Description>
            </xsl:if>
            <ns0:Quantity><xsl:value-of select="$qty/C18602" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="ScriptNS1:UnEceToErp($partner, ScriptNS4:Coalesce(string($qty/C18603), 'PCE'))" /></ns0:Uom>
            <xsl:if test="s0:PRILoop1/s0:PRI[C509/C50901='AAA']/C509/C50902">
              <ns0:UnitPrice><xsl:value-of select="s0:PRILoop1/s0:PRI[C509/C50901='AAA']/C509/C50902" /></ns0:UnitPrice>
              <ns0:PriceBasis><xsl:value-of select="ScriptNS4:Coalesce(string(s0:PRILoop1/s0:PRI[C509/C50901='AAA']/C509/C50905), '1')" /></ns0:PriceBasis>
            </xsl:if>
            <xsl:if test="s0:DTM[C507/C50701='2']">
              <ns0:RequestedDate><xsl:value-of select="ScriptNS0:EdifactToXmlDate(string(s0:DTM[C507/C50701='2']/C507/C50702), string(s0:DTM[C507/C50701='2']/C507/C50703))" /></ns0:RequestedDate>
            </xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(s0:LINLoop1)" /></ns0:LineCount>
      </ns0:Summary>
    </ns0:Order>
  </xsl:template>
</xsl:stylesheet>
