<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:output method="xml" indent="yes"/>
    
    <xsl:template match="/">
        <part_dictionary>
            <xsl:for-each select="ModelInfo/PartInfo">
                <part>
                    <xsl:for-each select="./Name">
                        <name><xsl:value-of select="."/></name>
                    </xsl:for-each>
                    <xsl:for-each select="./Copy">
                        <copy><xsl:value-of select="."/></copy>
                    </xsl:for-each>
                    <xsl:for-each select="./Animation">
                        <animation>
                            <xsl:for-each select="./Parameter">
                                    <parameter>
                                        <xsl:for-each select="./Sim">
                                            <sim>
                                                <xsl:for-each select="./Variable">
                                                    <variable><xsl:value-of select="."/></variable>
                                                </xsl:for-each>
                                                <xsl:for-each select="./Units">
                                                    <units><xsl:value-of select="."/></units>
                                                </xsl:for-each>
                                                <xsl:for-each select="./Scale">
                                                    <scale><xsl:value-of select="."/></scale>
                                                </xsl:for-each>
                                                <xsl:for-each select="./Bias">
                                                    <bias><xsl:value-of select="."/></bias>
                                                </xsl:for-each>
                                            </sim>
                                        </xsl:for-each>
                                        <xsl:for-each select="./Code">
                                            <code><xsl:value-of select="."/></code>
                                        </xsl:for-each>
                                        <xsl:for-each select="./Lag">
                                            <lag><xsl:value-of select="."/></lag>
                                        </xsl:for-each>
                                    </parameter>
                            </xsl:for-each>
                        </animation>
                    </xsl:for-each>
                    <xsl:for-each select="./MouseRect">
                        <mouserect>
                            <xsl:for-each select="./Cursor">
                                <cursor><xsl:value-of select="."/></cursor>
                            </xsl:for-each>
                            <xsl:for-each select="./HelpID">
                                <help_id><xsl:value-of select="."/></help_id>
                            </xsl:for-each>
                            <xsl:for-each select="./TooltipID">
                                <tooltip_id><xsl:value-of select="."/></tooltip_id>
                            </xsl:for-each>
                            <xsl:for-each select="./TooltipText">
                                <tooltip_text><xsl:value-of select="."/></tooltip_text>
                            </xsl:for-each>
                            <xsl:for-each select="./EventID">
                                <event_id><xsl:value-of select="."/></event_id>
                            </xsl:for-each>
                            <xsl:for-each select="./MouseFlags">
                                <mouse_flags><xsl:value-of select="."/></mouse_flags>
                            </xsl:for-each>
                            <xsl:for-each select="./CallbackCode">
                                <callback_code><xsl:value-of select="."/></callback_code>
                            </xsl:for-each>
                            <xsl:for-each select="./CallbackDragging">
                                <callback_dragging>
                                    <xsl:for-each select="./Variable">
                                        <variable><xsl:value-of select="."/></variable>
                                    </xsl:for-each>
                                    <xsl:for-each select="./Units">
                                        <units><xsl:value-of select="."/></units>
                                    </xsl:for-each>
                                    <xsl:for-each select="./Scale">
                                        <scale><xsl:value-of select="."/></scale>
                                    </xsl:for-each>
                                    <xsl:for-each select="./YScale">
                                        <yscale><xsl:value-of select="."/></yscale>
                                    </xsl:for-each>
                                    <xsl:for-each select="./MinValue">
                                        <minvalue><xsl:value-of select="."/></minvalue>
                                    </xsl:for-each>
                                    <xsl:for-each select="./MaxValue">
                                        <maxvalue><xsl:value-of select="."/></maxvalue>
                                    </xsl:for-each>
                                    <xsl:for-each select="./EventID">
                                        <event_id><xsl:value-of select="."/></event_id>
                                    </xsl:for-each>
                                </callback_dragging>
                            </xsl:for-each>
                            <xsl:for-each select="./CallbackJumpDragging">
                                <callback_jump_dragging>
                                    <xsl:for-each select="./XMovement">
                                        <xmovement>
                                            <xsl:for-each select="./Delta">
                                                <delta><xsl:value-of select="."/></delta>
                                            </xsl:for-each>
                                            <xsl:for-each select="./EventIdInc">
                                                <event_id_inc><xsl:value-of select="."/></event_id_inc>
                                            </xsl:for-each>
                                            <xsl:for-each select="./EventIdDec">
                                                <event_id_dec><xsl:value-of select="."/></event_id_dec>
                                            </xsl:for-each>        
                                        </xmovement>
                                    </xsl:for-each>
                                    <xsl:for-each select="./YMovement">
                                        <ymovement>
                                            <xsl:for-each select="./Delta">
                                                <delta><xsl:value-of select="."/></delta>
                                            </xsl:for-each>
                                            <xsl:for-each select="./EventIdInc">
                                                <event_id_inc><xsl:value-of select="."/></event_id_inc>
                                            </xsl:for-each>
                                            <xsl:for-each select="./EventIdDec">
                                                <event_id_dec><xsl:value-of select="."/></event_id_dec>
                                            </xsl:for-each>
                                        </ymovement>
                                    </xsl:for-each>
                                </callback_jump_dragging>
                            </xsl:for-each>
                        </mouserect>
                    </xsl:for-each>
                    <xsl:for-each select="./Visibility">
                        <visible_in_range>
                            <xsl:for-each select="./Parameter">
                                <parameter>
                                    <xsl:for-each select="./Code">
                                        <code><xsl:value-of select="."/></code>
                                    </xsl:for-each>
                                    <xsl:for-each select="./Sim">
                                        <sim>
                                            <xsl:for-each select="./Variable">
                                                <variable><xsl:value-of select="."/></variable>
                                            </xsl:for-each>
                                            <xsl:for-each select="./Units">
                                                <units><xsl:value-of select="."/></units>
                                            </xsl:for-each>
                                            <xsl:for-each select="./Scale">
                                                <scale><xsl:value-of select="."/></scale>
                                            </xsl:for-each>
                                            <xsl:for-each select="./Bias">
                                                <bias><xsl:value-of select="."/></bias>
                                            </xsl:for-each>
                                        </sim>
                                    </xsl:for-each>
                                </parameter>
                            </xsl:for-each>
                            <xsl:for-each select="./MinValue">
                                <minvalue><xsl:value-of select="."/></minvalue>
                            </xsl:for-each>
                            <xsl:for-each select="./MaxValue">
                                <maxvalue><xsl:value-of select="."/></maxvalue>
                            </xsl:for-each>
                        </visible_in_range>
                    </xsl:for-each>
                </part>
            </xsl:for-each>
        </part_dictionary>
    </xsl:template>
</xsl:stylesheet>