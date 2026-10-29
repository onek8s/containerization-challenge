<?xml version="1.0" encoding="utf-8"?>
<!-- Received X12 997 to the canonical functional acknowledgement. The 997 body carries no sender, so the
     ProcessFunctionalAck orchestration sets PartnerId from the EDI context (EDI.ISA06) after the transform; the
     parameter is only used by the test harness. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.microsoft.com/BizTalk/EDI/X12/2006"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    exclude-result-prefixes="s0 ScriptNS0">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:param name="PartnerId" select="''" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:X12_00401_997" />
  </xsl:template>

  <xsl:template match="/s0:X12_00401_997">
    <ns0:FunctionalAck>
      <ns0:PartnerId><xsl:value-of select="$PartnerId" /></ns0:PartnerId>
      <ns0:Standard>X12</ns0:Standard>
      <ns0:AcknowledgedControlNumber><xsl:value-of select="s0:AK1/AK102" /></ns0:AcknowledgedControlNumber>
      <ns0:AcknowledgedGroupNumber><xsl:value-of select="s0:AK1/AK102" /></ns0:AcknowledgedGroupNumber>
      <ns0:Status>
        <xsl:choose>
          <xsl:when test="s0:AK9/AK901='A'">Accepted</xsl:when>
          <xsl:when test="s0:AK9/AK901='E' or s0:AK9/AK901='P'">AcceptedWithErrors</xsl:when>
          <xsl:otherwise>Rejected</xsl:otherwise>
        </xsl:choose>
      </ns0:Status>
      <ns0:ReceivedUtc><xsl:value-of select="ScriptNS0:NowUtc()" /></ns0:ReceivedUtc>
      <xsl:if test="s0:AK2Loop1[s0:AK5/AK501 != 'A']">
        <ns0:Errors>
          <xsl:for-each select="s0:AK2Loop1[s0:AK5/AK501 != 'A']">
            <xsl:variable name="set" select="string(s0:AK2/AK202)" />
            <xsl:for-each select="s0:AK3Loop1">
              <ns0:Error>
                <ns0:Segment><xsl:value-of select="s0:AK3/AK301" /></ns0:Segment>
                <ns0:Position><xsl:value-of select="s0:AK3/AK302" /></ns0:Position>
                <ns0:Code><xsl:value-of select="concat('AK304-', s0:AK3/AK304)" /></ns0:Code>
                <ns0:Text>
                  <xsl:value-of select="concat('Set ', $set)" />
                  <xsl:for-each select="s0:AK4"><xsl:value-of select="concat('; element ', AK401, ' error ', AK403)" /></xsl:for-each>
                </ns0:Text>
              </ns0:Error>
            </xsl:for-each>
            <xsl:if test="not(s0:AK3Loop1)">
              <ns0:Error>
                <ns0:Code><xsl:value-of select="concat('AK502-', s0:AK5/AK502)" /></ns0:Code>
                <ns0:Text><xsl:value-of select="concat('Set ', $set, ' ', s0:AK5/AK501)" /></ns0:Text>
              </ns0:Error>
            </xsl:if>
          </xsl:for-each>
        </ns0:Errors>
      </xsl:if>
    </ns0:FunctionalAck>
  </xsl:template>
</xsl:stylesheet>
