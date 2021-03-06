// File auto generated by STUHashTool
using static STULib.Types.Generic.Common;

namespace STULib.Types {
    [STU(0x07A0E32F, "STULoadout")]
    public class STULoadout : STUInstance {
        [STUField(0xB48F1D22, "m_name")]
        public STUGUID Name;

        [STUField(0xCA7E6EDC, "m_description")]
        public STUGUID Description;

        [STUField(0xFC33191B, "m_logicalButton")]
        public STUGUID LogicalButton;

        [STUField(0x9290B942)]
        public STUGUID m_9290B942;

        [STUField(0x3CD6DC1E, "m_texture")]
        public STUGUID Texture;

        [STUField(0xC8D38D7B, "m_infoMovie")]
        public STUGUID InfoMovie;

        [STUField(0x7E3ED979)]
        public STUGUID[] m_7E3ED979;  // STU_0A29DB0D

        [STUField(0xB1124918)]
        public STUGUID[] m_B1124918;

        [STUField(0x2C54AEAF, "m_category")]
        public Enums.LoadoutCategory Category;

        [STUField(0x0E679979)]
        public int WeaponIndex;
    }
}
