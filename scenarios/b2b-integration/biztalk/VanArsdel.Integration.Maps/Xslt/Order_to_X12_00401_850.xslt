<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical purchase order to X12 850 for US suppliers. -->
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
    <xsl:apply-templates select="/s0:Order" />
  </xsl:template>

  <xsl:template match="/s0:Order">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="partner" select="string($h/s0:PartnerId)" />
    <ns0:X12_00401_850>
      <ns0:ST><ST01>850</ST01><ST02>0001</ST02></ns0:ST>
      <ns0:BEG>
        <BEG01>00</BEG01>
        <BEG02>SA</BEG02>
        <BEG03><xsl:value-of select="$h/s0:OrderNumber" /></BEG03>
        <BEG05><xsl:value-of select="ScriptNS0:XmlDateToX12(string($h/s0:OrderDate))" /></BEG05>
      </ns0:BEG>
      <ns0:CUR><CUR01>BY</CUR01><CUR02><xsl:value-of select="$h/s0:Currency" /></CUR02></ns0:CUR>
      <ns0:REF><REF01>VN</REF01><REF02><xsl:value-of select="$h/s0:ErpPartnerNumber" /></REF02></ns0:REF>
      <xsl:if test="$h/s0:RequestedDeliveryDate">
        <ns0:DTM><DTM01>002</DTM01><DTM02><xsl:value-of select="ScriptNS0:XmlDateToX12(string($h/s0:RequestedDeliveryDate))" /></DTM02></ns0:DTM>
      </xsl:if>
      <xsl:for-each select="s0:Parties/s0:Party[s0:Role='BY' or s0:Role='ST' or s0:Role='SU']">
        <ns0:N1Loop1>
          <ns0:N1>
            <N101><xsl:value-of select="s0:Role" /></N101>
            <N102><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Name), 60)" /></N102>
            <xsl:if test="s0:Gln"><N103>UL</N103><N104><xsl:value-of select="s0:Gln" /></N104></xsl:if>
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
      <xsl:for-each select="s0:Lines/s0:Line">
        <ns0:PO1Loop1>
          <ns0:PO1>
            <PO101><xsl:value-of select="s0:LineNumber" /></PO101>
            <PO102><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></PO102>
            <PO103><xsl:value-of select="ScriptNS1:ErpToX12($partner, string(s0:Uom))" /></PO103>
            <xsl:if test="s0:UnitPrice"><PO104><xsl:value-of select="ScriptNS3:Real(string(s0:UnitPrice))" /></PO104></xsl:if>
            <xsl:if test="s0:UnitPrice"><PO105>PE</PO105></xsl:if>
            <PO106>BP</PO106>
            <PO107><xsl:value-of select="s0:Item" /></PO107>
            <xsl:if test="s0:PartnerItem"><PO108>VP</PO108><PO109><xsl:value-of select="s0:PartnerItem" /></PO109></xsl:if>
            <xsl:if test="s0:Gtin"><PO110>UP</PO110><PO111><xsl:value-of select="ScriptNS5:GtinForStandard(string(s0:Gtin), 'X12')" /></PO111></xsl:if>
          </ns0:PO1>
          <xsl:if test="s0:Description">
            <ns0:PIDLoop1><ns0:PID><PID01>F</PID01><PID05><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Description), 80)" /></PID05></ns0:PID></ns0:PIDLoop1>
          </xsl:if>
          <xsl:if test="s0:RequestedDate">
            <ns0:DTM><DTM01>002</DTM01><DTM02><xsl:value-of select="ScriptNS0:XmlDateToX12(string(s0:RequestedDate))" /></DTM02></ns0:DTM>
          </xsl:if>
        </ns0:PO1Loop1>
      </xsl:for-each>
      <ns0:CTTLoop1><ns0:CTT><CTT01><xsl:value-of select="count(s0:Lines/s0:Line)" /></CTT01></ns0:CTT></ns0:CTTLoop1>
      <ns0:SE><SE01>0</SE01><SE02>0001</SE02></ns0:SE>
    </ns0:X12_00401_850>
  </xsl:template>
</xsl:stylesheet>
