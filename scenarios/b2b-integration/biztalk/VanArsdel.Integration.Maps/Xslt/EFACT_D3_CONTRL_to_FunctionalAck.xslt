<?xml version="1.0" encoding="utf-8"?>
<!-- Received EDIFACT CONTRL (syntax 3) to the canonical functional acknowledgement. UCI04: 7 = accepted,
     4 = rejected, 8 = received; UCM03 per message. PartnerId is set from EDI.UNB2.1 by the orchestration. -->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:s0="http://schemas.microsoft.com/BizTalk/EDI/EDIFACT/2006"
    xmlns:ns0="http://schemas.vanarsdel.com/integration/canonical/2014"
    xmlns:ScriptNS0="http://schemas.microsoft.com/BizTalk/2003/ScriptNS0"
    exclude-result-prefixes="s0 ScriptNS0">
  <xsl:output omit-xml-declaration="yes" method="xml" version="1.0" indent="no" />

  <xsl:param name="PartnerId" select="''" />

  <xsl:template match="/">
    <xsl:apply-templates select="/s0:EFACT_D3_CONTRL" />
  </xsl:template>

  <xsl:template match="/s0:EFACT_D3_CONTRL">
    <xsl:variable name="ucms" select="s0:UCMLoop1/s0:UCM" />
    <xsl:variable name="rejected" select="$ucms[UCM03='4']" />
    <ns0:FunctionalAck>
      <ns0:PartnerId><xsl:value-of select="$PartnerId" /></ns0:PartnerId>
      <ns0:Standard>EDIFACT</ns0:Standard>
      <ns0:AcknowledgedControlNumber><xsl:value-of select="s0:UCI/UCI01" /></ns0:AcknowledgedControlNumber>
      <ns0:Status>
        <xsl:choose>
          <xsl:when test="s0:UCI/UCI04='4'">Rejected</xsl:when>
          <xsl:when test="count($rejected) = 0">Accepted</xsl:when>
          <xsl:when test="count($rejected) = count($ucms)">Rejected</xsl:when>
          <xsl:otherwise>AcceptedWithErrors</xsl:otherwise>
        </xsl:choose>
      </ns0:Status>
      <ns0:ReceivedUtc><xsl:value-of select="ScriptNS0:NowUtc()" /></ns0:ReceivedUtc>
      <xsl:if test="s0:UCI/UCI04='4' or $rejected">
        <ns0:Errors>
          <xsl:if test="s0:UCI/UCI05">
            <ns0:Error><ns0:Segment>UNB</ns0:Segment><ns0:Code><xsl:value-of select="concat('UCI05-', s0:UCI/UCI05)" /></ns0:Code></ns0:Error>
          </xsl:if>
          <xsl:for-each select="$rejected">
            <ns0:Error>
              <ns0:Segment>UNH</ns0:Segment>
              <ns0:Code><xsl:value-of select="concat('UCM04-', UCM04)" /></ns0:Code>
              <ns0:Text><xsl:value-of select="concat('Message ', UCM01, ' (', S009/S00901, ')')" /></ns0:Text>
            </ns0:Error>
          </xsl:for-each>
        </ns0:Errors>
      </xsl:if>
    </ns0:FunctionalAck>
  </xsl:template>
</xsl:stylesheet>
