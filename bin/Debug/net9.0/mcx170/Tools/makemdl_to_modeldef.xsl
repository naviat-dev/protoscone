<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0"
                xmlns:xls="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes"/>
    
    <xsl:template match="/">
        <ModelInfo>
            <xsl:for-each select="part_dictionary/part">
                <PartInfo>
                    <xsl:for-each select="./name">
                        <Name>fs9_<xsl:value-of select="."/></Name>
                    </xsl:for-each>
                    <xsl:for-each select="./copy">
                        <Copy><xsl:value-of select="."/></Copy>
                    </xsl:for-each>
                    <xsl:for-each select="./animation">
                        <Animation>
                            <xsl:for-each select="./parameter">
                                    <Parameter>
                                        <xsl:for-each select="./sim">
                                            <Sim>
                                                <xsl:for-each select="./variable">
                                                    <Variable><xsl:value-of select="."/></Variable>
                                                </xsl:for-each>
                                                <xsl:for-each select="./units">
                                                    <Units><xsl:value-of select="."/></Units>
                                                </xsl:for-each>
                                                <xsl:for-each select="./scale">
                                                    <Scale><xsl:value-of select="."/></Scale>
                                                </xsl:for-each>
                                                <xsl:for-each select="./bias">
                                                    <Bias><xsl:value-of select="."/></Bias>
                                                </xsl:for-each>
                                            </Sim>
                                        </xsl:for-each>
                                        <xsl:for-each select="./code">
                                            <Code><xsl:value-of select="."/></Code>
                                        </xsl:for-each>
                                        <xsl:for-each select="./lag">
                                            <Lag><xsl:value-of select="."/></Lag>
                                        </xsl:for-each>
                                    </Parameter>
                            </xsl:for-each>
                        </Animation>
                    </xsl:for-each>
                    <xsl:for-each select="./mouserect">
                        <MouseRect>
                            <xsl:for-each select="./cursor">
                                <Cursor><xsl:value-of select="."/></Cursor>
                            </xsl:for-each>
                            <xsl:for-each select="./help_id">
                                <HelpID><xsl:value-of select="."/></HelpID>
                            </xsl:for-each>
                            <xsl:for-each select="./tooltip_id">
                                <TooltipID><xsl:value-of select="."/></TooltipID>
                            </xsl:for-each>
                            <xsl:for-each select="./tooltip_text">
                                <TooltipText><xsl:value-of select="."/></TooltipText>
                            </xsl:for-each>
                            <xsl:for-each select="./event_id">
                                <EventID><xsl:value-of select="."/></EventID>
                            </xsl:for-each>
                            <xsl:for-each select="./mouse_flags">
                                <MouseFlags><xsl:value-of select="."/></MouseFlags>
                            </xsl:for-each>
                            <xsl:for-each select="./callback_code">
                                <CallbackCode><xsl:value-of select="."/></CallbackCode>
                            </xsl:for-each>
                            <xsl:for-each select="./callback_dragging">
                                <CallbackDragging>
                                    <xsl:for-each select="./variable">
                                        <Variable><xsl:value-of select="."/></Variable>
                                    </xsl:for-each>
                                    <xsl:for-each select="./units">
                                        <Units><xsl:value-of select="."/></Units>
                                    </xsl:for-each>
                                    <xsl:for-each select="./scale">
                                        <Scale><xsl:value-of select="."/></Scale>
                                    </xsl:for-each>
                                    <xsl:for-each select="./yscale">
                                        <YScale><xsl:value-of select="."/></YScale>
                                    </xsl:for-each>
                                    <xsl:for-each select="./minvalue">
                                        <MinValue><xsl:value-of select="."/></MinValue>
                                    </xsl:for-each>
                                    <xsl:for-each select="./maxvalue">
                                        <MaxValue><xsl:value-of select="."/></MaxValue>
                                    </xsl:for-each>
                                    <xsl:for-each select="./event_id">
                                        <EventID><xsl:value-of select="."/></EventID>
                                    </xsl:for-each>
                                </CallbackDragging>
                            </xsl:for-each>
                            <xsl:for-each select="./callback_jump_dragging">
                                <CallbackJumpDragging>
                                    <xsl:for-each select="./xmovement">
                                        <XMovement>
                                            <xsl:for-each select="./delta">
                                                <Delta><xsl:value-of select="."/></Delta>
                                            </xsl:for-each>
                                            <xsl:for-each select="./event_id_inc">
                                                <EventIdInc><xsl:value-of select="."/></EventIdInc>
                                            </xsl:for-each>
                                            <xsl:for-each select="./event_id_dec">
                                                <EventIdDec><xsl:value-of select="."/></EventIdDec>
                                            </xsl:for-each>        
                                        </XMovement>
                                    </xsl:for-each>
                                    <xsl:for-each select="./ymovement">
                                        <YMovement>
                                            <xsl:for-each select="./delta">
                                                <Delta><xsl:value-of select="."/></Delta>
                                            </xsl:for-each>
                                            <xsl:for-each select="./event_id_inc">
                                                <EventIdInc><xsl:value-of select="."/></EventIdInc>
                                            </xsl:for-each>
                                            <xsl:for-each select="./event_id_dec">
                                                <EventIdDec><xsl:value-of select="."/></EventIdDec>
                                            </xsl:for-each>
                                        </YMovement>
                                    </xsl:for-each>
                                </CallbackJumpDragging>
                            </xsl:for-each>
                        </MouseRect>
                    </xsl:for-each>
                    <xsl:for-each select="./visible_in_range">
                        <Visibility>
                            <xsl:for-each select="./parameter">
                                <Parameter>
                                    <xsl:for-each select="./code">
                                        <xsl:choose>
                                            <xsl:when test="../../minvalue and ../../minvalue!='1'">
                                                <Code><xsl:value-of select="."/> !</Code>
                                            </xsl:when>
                                            <xls:otherwise>
                                                <Code><xsl:value-of select="."/></Code>
                                            </xls:otherwise>
                                        </xsl:choose>
                                    </xsl:for-each>
                                    <xsl:for-each select="./sim">
                                        <Sim>
                                            <xsl:for-each select="./variable">
                                                <Variable><xsl:value-of select="."/></Variable>
                                            </xsl:for-each>
                                            <xsl:for-each select="./units">
                                                <Units><xsl:value-of select="."/></Units>
                                            </xsl:for-each>
                                            <xsl:for-each select="./scale">
                                                <Scale><xsl:value-of select="."/></Scale>
                                            </xsl:for-each>
                                            <xsl:for-each select="./bias">
                                                <Bias><xsl:value-of select="."/></Bias>
                                            </xsl:for-each>
                                        </Sim>
                                    </xsl:for-each>
                                </Parameter>
                            </xsl:for-each>
                            <xsl:for-each select="./minvalue">
                                <MinValue><xsl:value-of select="."/></MinValue>
                            </xsl:for-each>
                            <xsl:for-each select="./maxvalue">
                                <MaxValue><xsl:value-of select="."/></MaxValue>
                            </xsl:for-each>
                        </Visibility>
                    </xsl:for-each>
                </PartInfo>
            </xsl:for-each>
        </ModelInfo>
    </xsl:template>
</xsl:stylesheet>