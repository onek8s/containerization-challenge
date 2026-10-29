<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical customer invoice to X12 810. TDS01 is the invoice total in cents (N2). -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ns0="http://schemas.microsoft.com/BizTalk/EDI/X12/2006"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS1 ScriptNS3 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Invoice" />
  </xsl:template>

  <xsl:template match="/s0:Invoice">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="partner" select="string($h/s0:PartnerId)" />
    <ns0:X12_00401_810>
      <ns0:ST><ST01>810</ST01><ST02>0001</ST02></ns0:ST>
      <ns0:BIG>
        <BIG01><xsl:value-of select="ScriptNS0:XmlDateToX12(string($h/s0:InvoiceDate))" /></BIG01>
        <BIG02><xsl:value-of select="$h/s0:InvoiceNumber" /></BIG02>
        <xsl:if test="$h/s0:OrderNumber"><BIG04><xsl:value-of select="$h/s0:OrderNumber" /></BIG04></xsl:if>
        <BIG07>DI</BIG07>
      </ns0:BIG>
      <ns0:CUR><CUR01>SE</CUR01><CUR02><xsl:value-of select="$h/s0:Currency" /></CUR02></ns0:CUR>
      <xsl:if test="$h/s0:DespatchNumber"><ns0:REF><REF01>BM</REF01><REF02><xsl:value-of select="$h/s0:DespatchNumber" /></REF02></ns0:REF></xsl:if>
      <ns0:REF><REF01>IA</REF01><REF02><xsl:value-of select="$h/s0:ErpPartnerNumber" /></REF02></ns0:REF>
      <xsl:for-each select="s0:Parties/s0:Party[s0:Role='BT' or s0:Role='ST']">
        <ns0:N1Loop1>
          <ns0:N1>
            <N101><xsl:value-of select="s0:Role" /></N101>
            <N102><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Name), 60)" /></N102>
            <N103>92</N103>
            <N104><xsl:value-of select="ScriptNS4:Coalesce(string(s0:PartnerCode), string(s0:InternalId))" /></N104>
          </ns0:N1>
          <xsl:if test="s0:Street1"><ns0:N3><N301><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Street1), 55)" /></N301></ns0:N3></xsl:if>
          <xsl:if test="s0:City">
            <ns0:N4>
              <N401><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:City), 30)" /></N401>
              <xsl:if test="s0:Region"><N402><xsl:value-of select="s0:Region" /></N402></xsl:if>
              <xsl:if test="s0:PostalCode"><N403><xsl:value-of select="s0:PostalCode" /></N403></xsl:if>
              <xsl:if test="s0:Country"><N404><xsl:value-of select="s0:Country" /></N404></xsl:if>
            </ns0:N4>
          </xsl:if>
        </ns0:N1Loop1>
      </xsl:for-each>
      <ns0:N1Loop1>
        <ns0:N1><N101>RE</N101><N102>VanArsdel Distribution</N102><N103>UL</N103><N104>0614141000005</N104></ns0:N1>
        <ns0:N3><N301>PO Box 182200</N301></ns0:N3>
        <ns0:N4><N401>Columbus</N401><N402>OH</N402><N403>43218</N403><N404>US</N404></ns0:N4>
      </ns0:N1Loop1>
      <xsl:if test="$h/s0:PaymentTermsDays">
        <ns0:ITD>
          <ITD01>05</ITD01>
          <ITD02>3</ITD02>
          <xsl:if test="$h/s0:DueDate"><ITD06><xsl:value-of select="ScriptNS0:XmlDateToX12(string($h/s0:DueDate))" /></ITD06></xsl:if>
          <ITD07><xsl:value-of select="$h/s0:PaymentTermsDays" /></ITD07>
        </ns0:ITD>
      </xsl:if>
      <xsl:for-each select="s0:Lines/s0:Line">
        <ns0:IT1Loop1>
          <ns0:IT1>
            <IT101><xsl:value-of select="ScriptNS4:Coalesce(string(s0:OrderLineNumber), string(s0:LineNumber))" /></IT101>
            <IT102><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></IT102>
            <IT103><xsl:value-of select="ScriptNS1:ErpToX12($partner, string(s0:Uom))" /></IT103>
            <IT104><xsl:value-of select="ScriptNS3:Real(string(s0:UnitPrice))" /></IT104>
            <xsl:choose>
              <xsl:when test="s0:PartnerItem"><IT106>BP</IT106><IT107><xsl:value-of select="s0:PartnerItem" /></IT107><IT108>VP</IT108><IT109><xsl:value-of select="s0:Item" /></IT109></xsl:when>
              <xsl:otherwise><IT106>VP</IT106><IT107><xsl:value-of select="s0:Item" /></IT107></xsl:otherwise>
            </xsl:choose>
            <xsl:if test="s0:Gtin"><IT110>UP</IT110><IT111><xsl:value-of select="ScriptNS5:GtinForStandard(string(s0:Gtin), 'X12')" /></IT111></xsl:if>
          </ns0:IT1>
          <xsl:if test="s0:Description">
            <ns0:PIDLoop1><ns0:PID><PID01>F</PID01><PID05><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Description), 80)" /></PID05></ns0:PID></ns0:PIDLoop1>
          </xsl:if>
        </ns0:IT1Loop1>
      </xsl:for-each>
      <ns0:TDS><TDS01><xsl:value-of select="ScriptNS3:ImpliedDecimal(string(s0:Summary/s0:GrandTotal))" /></TDS01><TDS02><xsl:value-of select="ScriptNS3:ImpliedDecimal(string(s0:Summary/s0:LineTotal))" /></TDS02></ns0:TDS>
      <xsl:if test="number(s0:Summary/s0:TaxTotal) &gt; 0">
        <ns0:TXI><TXI01>ST</TXI01><TXI02><xsl:value-of select="ScriptNS3:Format(string(s0:Summary/s0:TaxTotal), 2)" /></TXI02></ns0:TXI>
      </xsl:if>
      <ns0:CTT><CTT01><xsl:value-of select="count(s0:Lines/s0:Line)" /></CTT01></ns0:CTT>
      <ns0:SE><SE01>0</SE01><SE02>0001</SE02></ns0:SE>
    </ns0:X12_00401_810>
  </xsl:template>
</xsl:stylesheet>
