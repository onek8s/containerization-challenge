<?xml version="1.0" encoding="utf-8"?>
<!-- Supplier invoice EDIFACT INVOIC D.96A to the canonical supplier invoice. The supplier is identified by the GLN in
     NAD+SU; our article number is PIA+1 ...:IN (buyer's), the supplier's own number PIA+1 ...:SA. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
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
    <xsl:apply-templates select="/s0:EFACT_D96A_INVOIC" />
  </xsl:template>

  <xsl:template match="/s0:EFACT_D96A_INVOIC">
    <xsl:variable name="supplierGln" select="string(s0:NADLoop1[s0:NAD/NAD01='SU']/s0:NAD/C082/C08201)" />
    <xsl:variable name="partner" select="ScriptNS2:PartnerIdFromGln($supplierGln)" />
    <xsl:variable name="invDtm" select="s0:DTM[C507/C50701='137']/C507" />
    <xsl:variable name="invDate" select="ScriptNS0:EdifactToXmlDate(string($invDtm/C50702), string($invDtm/C50703))" />
    <xsl:variable name="pat" select="s0:PATLoop1[1]" />
    <ns0:Invoice>
      <ns0:Header>
        <ns0:InvoiceNumber><xsl:value-of select="s0:BGM/BGM02" /></ns0:InvoiceNumber>
        <ns0:Kind><xsl:choose><xsl:when test="s0:BGM/C002/C00201='381'">SupplierCreditNote</xsl:when><xsl:otherwise>SupplierInvoice</xsl:otherwise></xsl:choose></ns0:Kind>
        <ns0:PartnerId><xsl:value-of select="$partner" /></ns0:PartnerId>
        <ns0:InvoiceDate><xsl:value-of select="$invDate" /></ns0:InvoiceDate>
        <xsl:if test="s0:RFFLoop1/s0:RFF[C506/C50601='ON']"><ns0:OrderNumber><xsl:value-of select="s0:RFFLoop1/s0:RFF[C506/C50601='ON']/C506/C50602" /></ns0:OrderNumber></xsl:if>
        <xsl:if test="s0:RFFLoop1/s0:RFF[C506/C50601='AAK']"><ns0:DespatchNumber><xsl:value-of select="s0:RFFLoop1/s0:RFF[C506/C50601='AAK']/C506/C50602" /></ns0:DespatchNumber></xsl:if>
        <ns0:Currency><xsl:value-of select="ScriptNS4:Coalesce(string(s0:CUXLoop1[1]/s0:CUX/C504/C50402), ScriptNS2:Currency($partner))" /></ns0:Currency>
        <xsl:if test="$pat/s0:PAT/C112/C11204">
          <ns0:PaymentTermsDays><xsl:value-of select="number($pat/s0:PAT/C112/C11204)" /></ns0:PaymentTermsDays>
        </xsl:if>
        <xsl:choose>
          <xsl:when test="$pat/s0:DTM[C507/C50701='13']">
            <ns0:DueDate><xsl:value-of select="ScriptNS0:EdifactToXmlDate(string($pat/s0:DTM[C507/C50701='13']/C507/C50702), string($pat/s0:DTM[C507/C50701='13']/C507/C50703))" /></ns0:DueDate>
          </xsl:when>
          <xsl:when test="$pat/s0:PAT/C112/C11204">
            <ns0:DueDate><xsl:value-of select="ScriptNS0:DueDate($invDate, string($pat/s0:PAT/C112/C11204))" /></ns0:DueDate>
          </xsl:when>
        </xsl:choose>
        <ns0:ErpPartnerNumber><xsl:value-of select="ScriptNS2:ErpNumber($partner)" /></ns0:ErpPartnerNumber>
      </ns0:Header>
      <ns0:Parties>
        <xsl:for-each select="s0:NADLoop1">
          <xsl:variable name="nad" select="s0:NAD" />
          <ns0:Party>
            <ns0:Role>
              <xsl:choose>
                <xsl:when test="$nad/NAD01='SU'">SE</xsl:when>
                <xsl:when test="$nad/NAD01='IV'">BT</xsl:when>
                <xsl:when test="$nad/NAD01='DP'">ST</xsl:when>
                <xsl:otherwise><xsl:value-of select="$nad/NAD01" /></xsl:otherwise>
              </xsl:choose>
            </ns0:Role>
            <xsl:if test="$nad/C082/C08201"><ns0:PartnerCode><xsl:value-of select="$nad/C082/C08201" /></ns0:PartnerCode></xsl:if>
            <xsl:if test="$nad/C082/C08203='9'"><ns0:Gln><xsl:value-of select="$nad/C082/C08201" /></ns0:Gln></xsl:if>
            <xsl:if test="$nad/NAD01='SU'"><ns0:InternalId><xsl:value-of select="ScriptNS2:ErpNumber($partner)" /></ns0:InternalId></xsl:if>
            <xsl:if test="$nad/C080/C08001"><ns0:Name><xsl:value-of select="$nad/C080/C08001" /></ns0:Name></xsl:if>
            <xsl:if test="$nad/C059/C05901"><ns0:Street1><xsl:value-of select="$nad/C059/C05901" /></ns0:Street1></xsl:if>
            <xsl:if test="$nad/NAD06"><ns0:City><xsl:value-of select="$nad/NAD06" /></ns0:City></xsl:if>
            <xsl:if test="$nad/NAD08"><ns0:PostalCode><xsl:value-of select="$nad/NAD08" /></ns0:PostalCode></xsl:if>
            <xsl:if test="$nad/NAD09"><ns0:Country><xsl:value-of select="$nad/NAD09" /></ns0:Country></xsl:if>
            <xsl:if test="s0:RFFLoop2/s0:RFF[C506/C50601='VA']"><ns0:VatNumber><xsl:value-of select="s0:RFFLoop2/s0:RFF[C506/C50601='VA']/C506/C50602" /></ns0:VatNumber></xsl:if>
          </ns0:Party>
        </xsl:for-each>
      </ns0:Parties>
      <ns0:Lines>
        <xsl:for-each select="s0:LINLoop1">
          <xsl:variable name="gtin" select="ScriptNS5:Gtin14(string(s0:LIN[C212/C21202='EN']/C212/C21201))" />
          <xsl:variable name="ourItem" select="string(s0:PIA[C212/C21202='IN']/C212/C21201)" />
          <xsl:variable name="supplierItem" select="string(s0:PIA[C212/C21202='SA']/C212/C21201)" />
          <xsl:variable name="qty" select="s0:QTY[C186/C18601='47']/C186" />
          <xsl:variable name="pri" select="s0:PRILoop1/s0:PRI[C509/C50901='AAA']/C509" />
          <ns0:Line>
            <ns0:LineNumber><xsl:value-of select="number(s0:LIN/LIN01)" /></ns0:LineNumber>
            <xsl:if test="s0:RFFLoop3/s0:RFF[C506/C50601='ON']/C506/C50603">
              <ns0:OrderLineNumber><xsl:value-of select="number(s0:RFFLoop3/s0:RFF[C506/C50601='ON']/C506/C50603)" /></ns0:OrderLineNumber>
            </xsl:if>
            <ns0:Item>
              <xsl:choose>
                <xsl:when test="$ourItem != ''"><xsl:value-of select="$ourItem" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="ScriptNS2:ItemToInternal($partner, $supplierItem, $gtin)" /></xsl:otherwise>
              </xsl:choose>
            </ns0:Item>
            <xsl:if test="$supplierItem != ''"><ns0:PartnerItem><xsl:value-of select="$supplierItem" /></ns0:PartnerItem></xsl:if>
            <xsl:if test="$gtin != ''"><ns0:Gtin><xsl:value-of select="$gtin" /></ns0:Gtin></xsl:if>
            <xsl:if test="s0:IMD/C273/C27304"><ns0:Description><xsl:value-of select="ScriptNS4:Truncate(string(s0:IMD[1]/C273/C27304), 80)" /></ns0:Description></xsl:if>
            <ns0:Quantity><xsl:value-of select="$qty/C18602" /></ns0:Quantity>
            <ns0:Uom><xsl:value-of select="ScriptNS1:UnEceToErp($partner, ScriptNS4:Coalesce(string($qty/C18603), 'PCE'))" /></ns0:Uom>
            <ns0:UnitPrice><xsl:value-of select="ScriptNS4:Coalesce(string($pri/C50902), '0')" /></ns0:UnitPrice>
            <ns0:PriceBasis><xsl:value-of select="ScriptNS4:Coalesce(string($pri/C50905), '1')" /></ns0:PriceBasis>
            <ns0:LineAmount>
              <xsl:choose>
                <xsl:when test="s0:MOALoop1/s0:MOA[C516/C51601='203']"><xsl:value-of select="ScriptNS3:Format(string(s0:MOALoop1/s0:MOA[C516/C51601='203']/C516/C51602), 2)" /></xsl:when>
                <xsl:otherwise><xsl:value-of select="ScriptNS3:LineAmount(string($qty/C18602), string($pri/C50902), ScriptNS4:Coalesce(string($pri/C50905), '1'))" /></xsl:otherwise>
              </xsl:choose>
            </ns0:LineAmount>
            <xsl:if test="s0:TAXLoop3/s0:TAX/C243/C24304"><ns0:TaxRate><xsl:value-of select="s0:TAXLoop3[1]/s0:TAX/C243/C24304" /></ns0:TaxRate></xsl:if>
          </ns0:Line>
        </xsl:for-each>
      </ns0:Lines>
      <ns0:Summary>
        <ns0:LineCount><xsl:value-of select="count(s0:LINLoop1)" /></ns0:LineCount>
        <ns0:LineTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(string(s0:MOALoop4/s0:MOA[C516/C51601='79']/C516/C51602), string(s0:MOALoop4/s0:MOA[C516/C51601='125']/C516/C51602)), 2)" /></ns0:LineTotal>
        <ns0:TaxTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(string(s0:MOALoop4/s0:MOA[C516/C51601='124']/C516/C51602), '0'), 2)" /></ns0:TaxTotal>
        <ns0:GrandTotal><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(string(s0:MOALoop4/s0:MOA[C516/C51601='77']/C516/C51602), string(s0:MOALoop4/s0:MOA[C516/C51601='9']/C516/C51602)), 2)" /></ns0:GrandTotal>
        <xsl:if test="s0:TAXLoop5">
          <ns0:Taxes>
            <xsl:for-each select="s0:TAXLoop5">
              <ns0:Tax>
                <ns0:Rate><xsl:value-of select="ScriptNS4:Coalesce(string(s0:TAX/C243/C24304), '0')" /></ns0:Rate>
                <ns0:Base><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(string(s0:MOA[C516/C51601='125']/C516/C51602), '0'), 2)" /></ns0:Base>
                <ns0:Amount><xsl:value-of select="ScriptNS3:Format(ScriptNS4:Coalesce(string(s0:MOA[C516/C51601='124']/C516/C51602), '0'), 2)" /></ns0:Amount>
              </ns0:Tax>
            </xsl:for-each>
          </ns0:Taxes>
        </xsl:if>
      </ns0:Summary>
    </ns0:Invoice>
  </xsl:template>
</xsl:stylesheet>
