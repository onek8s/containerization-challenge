<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical despatch to the X12 856 ship notice, hierarchy Shipment / Order / Tare (pallet, SSCC) / Item
     (BSN05 0001). HL numbers come from the CodeFunctoids counter, one instance per map execution. -->
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
    <xsl:apply-templates select="/s0:Despatch" />
  </xsl:template>

  <xsl:template match="/s0:Despatch">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="partner" select="string($h/s0:PartnerId)" />
    <xsl:variable name="hlShipment" select="ScriptNS5:Next('HL')" />
    <ns0:X12_00401_856>
      <ns0:ST><ST01>856</ST01><ST02>0001</ST02></ns0:ST>
      <ns0:BSN>
        <BSN01>00</BSN01>
        <BSN02><xsl:value-of select="$h/s0:DespatchNumber" /></BSN02>
        <BSN03><xsl:value-of select="ScriptNS0:TodayX12()" /></BSN03>
        <BSN04><xsl:value-of select="ScriptNS0:NowTimeX12()" /></BSN04>
        <BSN05>0001</BSN05>
      </ns0:BSN>
      <ns0:HLLoop1>
        <ns0:HL><HL01><xsl:value-of select="$hlShipment" /></HL01><HL03>S</HL03><HL04>1</HL04></ns0:HL>
        <ns0:TD1>
          <TD101>PLT94</TD101>
          <TD102><xsl:value-of select="$h/s0:PackageCount" /></TD102>
          <xsl:if test="$h/s0:GrossWeightKg"><TD106>G</TD106><TD107><xsl:value-of select="ScriptNS3:Real(string($h/s0:GrossWeightKg))" /></TD107><TD108>KG</TD108></xsl:if>
        </ns0:TD1>
        <xsl:if test="$h/s0:Carrier">
          <ns0:TD5><TD501>B</TD501><TD502>2</TD502><TD503><xsl:value-of select="$h/s0:Carrier" /></TD503><TD504>M</TD504></ns0:TD5>
        </xsl:if>
        <xsl:if test="$h/s0:TrackingNumber">
          <ns0:REF><REF01>CN</REF01><REF02><xsl:value-of select="$h/s0:TrackingNumber" /></REF02></ns0:REF>
        </xsl:if>
        <ns0:REF><REF01>BM</REF01><REF02><xsl:value-of select="$h/s0:DespatchNumber" /></REF02></ns0:REF>
        <ns0:DTM><DTM01>011</DTM01><DTM02><xsl:value-of select="ScriptNS0:XmlDateToX12(string($h/s0:ShipDate))" /></DTM02></ns0:DTM>
        <xsl:for-each select="s0:Parties/s0:Party[s0:Role='ST']">
          <ns0:N1Loop1>
            <ns0:N1><N101>ST</N101><N102><xsl:value-of select="ScriptNS4:CleanForEdi(string(s0:Name), 60)" /></N102><N103>92</N103><N104><xsl:value-of select="s0:PartnerCode" /></N104></ns0:N1>
          </ns0:N1Loop1>
        </xsl:for-each>
        <ns0:N1Loop1>
          <ns0:N1><N101>SF</N101><N102>VanArsdel Distribution</N102><N103>UL</N103><N104>0614141000029</N104></ns0:N1>
        </ns0:N1Loop1>
      </ns0:HLLoop1>
      <xsl:variable name="hlOrder" select="ScriptNS5:Next('HL')" />
      <ns0:HLLoop1>
        <ns0:HL><HL01><xsl:value-of select="$hlOrder" /></HL01><HL02><xsl:value-of select="$hlShipment" /></HL02><HL03>O</HL03><HL04>1</HL04></ns0:HL>
        <ns0:PRF><PRF01><xsl:value-of select="$h/s0:OrderNumber" /></PRF01></ns0:PRF>
      </ns0:HLLoop1>
      <xsl:for-each select="s0:Packages/s0:Package">
        <xsl:variable name="hlTare" select="ScriptNS5:Next('HL')" />
        <ns0:HLLoop1>
          <ns0:HL><HL01><xsl:value-of select="$hlTare" /></HL01><HL02><xsl:value-of select="$hlOrder" /></HL02><HL03>T</HL03><HL04>1</HL04></ns0:HL>
          <ns0:MAN><MAN01>GM</MAN01><MAN02><xsl:value-of select="s0:Sscc" /></MAN02></ns0:MAN>
        </ns0:HLLoop1>
        <xsl:for-each select="s0:Lines/s0:Line">
          <ns0:HLLoop1>
            <ns0:HL><HL01><xsl:value-of select="ScriptNS5:Next('HL')" /></HL01><HL02><xsl:value-of select="$hlTare" /></HL02><HL03>I</HL03><HL04>0</HL04></ns0:HL>
            <ns0:LIN>
              <LIN01><xsl:value-of select="ScriptNS4:Coalesce(string(s0:OrderLineNumber), string(s0:LineNumber))" /></LIN01>
              <xsl:choose>
                <xsl:when test="s0:PartnerItem"><LIN02>BP</LIN02><LIN03><xsl:value-of select="s0:PartnerItem" /></LIN03></xsl:when>
                <xsl:otherwise><LIN02>VP</LIN02><LIN03><xsl:value-of select="s0:Item" /></LIN03></xsl:otherwise>
              </xsl:choose>
              <xsl:if test="s0:PartnerItem"><LIN04>VP</LIN04><LIN05><xsl:value-of select="s0:Item" /></LIN05></xsl:if>
              <xsl:if test="s0:Gtin"><LIN06>UP</LIN06><LIN07><xsl:value-of select="ScriptNS5:GtinForStandard(string(s0:Gtin), 'X12')" /></LIN07></xsl:if>
            </ns0:LIN>
            <ns0:SN1>
              <SN101><xsl:value-of select="ScriptNS4:Coalesce(string(s0:OrderLineNumber), string(s0:LineNumber))" /></SN101>
              <SN102><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></SN102>
              <SN103><xsl:value-of select="ScriptNS1:ErpToX12($partner, string(s0:Uom))" /></SN103>
            </ns0:SN1>
          </ns0:HLLoop1>
        </xsl:for-each>
      </xsl:for-each>
      <ns0:CTT><CTT01><xsl:value-of select="ScriptNS5:Current('HL')" /></CTT01></ns0:CTT>
      <ns0:SE><SE01>0</SE01><SE02>0001</SE02></ns0:SE>
    </ns0:X12_00401_856>
  </xsl:template>
</xsl:stylesheet>
