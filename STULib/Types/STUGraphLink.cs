// File auto generated by STUHashTool

using STULib.Types.Generic;

namespace STULib.Types {
    [STU(0x6E63F8E1, "STUGraphLink")]
    public class STUGraphLink : Common.STUInstance {
        [STUField(0xE3B4FA5C, "m_uniqueID")]
        public Common.STUUUID UniqueID;

        [STUField(0x498B0009, EmbeddedInstance = true)]
        public STUGraphPlug m_498B0009;

        [STUField(0xEA1269DF, EmbeddedInstance = true)]
        public STUGraphPlug m_EA1269DF;
    }
}
