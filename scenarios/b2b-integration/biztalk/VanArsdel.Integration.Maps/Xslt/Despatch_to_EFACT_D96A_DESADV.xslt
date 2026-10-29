<?xml version="1.0" encoding="utf-8"?>
<!-- Canonical despatch to EDIFACT DESADV D.96A (EANCOM): consignment CPS 1, one CPS per pallet with PAC/PCI/GIN
     (SSCC) and the lines on that pallet. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ns0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    xmlns:ScriptNS1="http://schemas.microsoft.com/BizTalk/2003/ScriptNS1"
    xmlns:ScriptNS2="http://schemas.microsoft.com/BizTalk/2003/ScriptNS2"
    xmlns:ScriptNS3="http://schemas.microsoft.com/BizTalk/2003/ScriptNS3"
    xmlns:ScriptNS4="http://schemas.microsoft.com/BizTalk/2003/ScriptNS4"
    xmlns:ScriptNS5="http://schemas.microsoft.com/BizTalk/2003/ScriptNS5"
    exclude-result-prefixes="s0 ScriptNS0 ScriptNS1 ScriptNS2 ScriptNS3 ScriptNS4 ScriptNS5">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:Despatch" />
  </xsl:template>

  <xsl:template match="/s0:Despatch">
    <xsl:variable name="h" select="s0:Header" />
    <xsl:variable name="partner" select="string($h/s0:PartnerId)" />
    <ns0:EFACT_D96A_DESADV>
      <ns0:UNH>
        <UNH1>1</UNH1>
        <UNH2><UNH2.1>DESADV</UNH2.1><UNH2.2>D</UNH2.2><UNH2.3>96A</UNH2.3><UNH2.4>UN</UNH2.4><UNH2.5>EAN005</UNH2.5></UNH2>
      </ns0:UNH>
      <ns0:BGM><C002><C00201>351</C00201></C002><BGM02><xsl:value-of select="$h/s0:DespatchNumber" /></BGM02><BGM03>9</BGM03></ns0:BGM>
      <ns0:DTM><C507><C50701>137</C50701><C50702><xsl:value-of select="ScriptNS0:XmlDateToEdifact(ScriptNS0:Today())" /></C50702><C50703>102</C50703></C507></ns0:DTM>
      <ns0:DTM><C507><C50701>11</C50701><C50702><xsl:value-of select="ScriptNS0:XmlDateToEdifact(string($h/s0:ShipDate))" /></C50702><C50703>102</C50703></C507></ns0:DTM>
      <xsl:if test="$h/s0:EstimatedDeliveryDate">
        <ns0:DTM><C507><C50701>17</C50701><C50702><xsl:value-of select="ScriptNS0:XmlDateToEdifact(string($h/s0:EstimatedDeliveryDate))" /></C50702><C50703>102</C50703></C507></ns0:DTM>
      </xsl:if>
      <ns0:RFFLoop1><ns0:RFF><C506><C50601>ON</C50601><C50602><xsl:value-of select="$h/s0:OrderNumber" /></C50602></C506></ns0:RFF></ns0:RFFLoop1>
      <ns0:NADLoop1><ns0:NAD><NAD01>BY</NAD01><C082><C08201><xsl:value-of select="ScriptNS2:Gln($partner)" /></C08201><C08203>9</C08203></C082></ns0:NAD></ns0:NADLoop1>
      <xsl:for-each select="s0:Parties/s0:Party[s0:Role='ST']">
        <ns0:NADLoop1><ns0:NAD><NAD01>DP</NAD01><C082><C08201><xsl:value-of select="s0:PartnerCode" /></C08201><C08203>9</C08203></C082></ns0:NAD></ns0:NADLoop1>
      </xsl:for-each>
      <ns0:NADLoop1><ns0:NAD><NAD01>SU</NAD01><C082><C08201>0614141000012</C08201><C08203>9</C08203></C082></ns0:NAD></ns0:NADLoop1>
      <xsl:if test="$h/s0:Carrier">
        <ns0:TDTLoop1><ns0:TDT><TDT01>20</TDT01><C220><C22001>30</C22001></C220><C040><C04001><xsl:value-of select="$h/s0:Carrier" /></C04001></C040></ns0:TDT></ns0:TDTLoop1>
      </xsl:if>
      <ns0:CPSLoop1>
        <ns0:CPS><CPS01>1</CPS01></ns0:CPS>
        <ns0:PACLoop1><ns0:PAC><PAC01><xsl:value-of select="$h/s0:PackageCount" /></PAC01><C202><C20201>201</C20201></C202></ns0:PAC></ns0:PACLoop1>
      </ns0:CPSLoop1>
      <xsl:for-each select="s0:Packages/s0:Package">
        <ns0:CPSLoop1>
          <ns0:CPS><CPS01><xsl:value-of select="position() + 1" /></CPS01><CPS02>1</CPS02></ns0:CPS>
          <ns0:PACLoop1>
            <ns0:PAC><PAC01>1</PAC01><C202><C20201>201</C20201></C202></ns0:PAC>
            <ns0:PCILoop1>
              <ns0:PCI><PCI01>33E</PCI01></ns0:PCI>
              <ns0:GIN><GIN01>BJ</GIN01><C208><C20801><xsl:value-of select="s0:Sscc" /></C20801></C208></ns0:GIN>
            </ns0:PCILoop1>
          </ns0:PACLoop1>
          <xsl:for-each select="s0:Lines/s0:Line">
            <ns0:LINLoop1>
              <ns0:LIN>
                <LIN01><xsl:value-of select="ScriptNS4:Coalesce(string(s0:OrderLineNumber), string(s0:LineNumber))" /></LIN01>
                <xsl:if test="s0:Gtin"><C212><C21201><xsl:value-of select="ScriptNS5:GtinForStandard(string(s0:Gtin), 'EDIFACT')" /></C21201><C21202>EN</C21202></C212></xsl:if>
              </ns0:LIN>
              <xsl:if test="s0:PartnerItem">
                <ns0:PIA><PIA01>1</PIA01><C212><C21201><xsl:value-of select="s0:PartnerItem" /></C21201><C21202>IN</C21202></C212></ns0:PIA>
              </xsl:if>
              <ns0:PIA><PIA01>1</PIA01><C212><C21201><xsl:value-of select="s0:Item" /></C21201><C21202>SA</C21202></C212></ns0:PIA>
              <ns0:QTY><C186><C18601>12</C18601><C18602><xsl:value-of select="ScriptNS3:Real(string(s0:Quantity))" /></C18602><C18603><xsl:value-of select="ScriptNS1:ErpToUnEce($partner, string(s0:Uom))" /></C18603></C186></ns0:QTY>
              <ns0:RFFLoop2><ns0:RFF><C506><C50601>ON</C50601><C50602><xsl:value-of select="$h/s0:OrderNumber" /></C50602><C50603><xsl:value-of select="ScriptNS4:Coalesce(string(s0:OrderLineNumber), string(s0:LineNumber))" /></C50603></C506></ns0:RFF></ns0:RFFLoop2>
            </ns0:LINLoop1>
          </xsl:for-each>
        </ns0:CPSLoop1>
      </xsl:for-each>
      <ns0:CNT><C270><C27001>2</C27001><C27002><xsl:value-of select="count(s0:Packages/s0:Package/s0:Lines/s0:Line)" /></C27002></C270></ns0:CNT>
      <ns0:UNT><UNT1>0</UNT1><UNT2>1</UNT2></ns0:UNT>
    </ns0:EFACT_D96A_DESADV>
  </xsl:template>
</xsl:stylesheet>
