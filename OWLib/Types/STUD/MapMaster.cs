﻿using System.IO;
using System.Runtime.InteropServices;


namespace OWLib.Types.STUD {
    [System.Diagnostics.DebuggerDisplay(OWLib.STUD.STUD_DEBUG_STR)]
    public class MapMaster : ISTUDInstance {
        public uint Id => 0xF42FE559;
        public string Name => "Map Master";

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct MapMasterHeader {
            public STUDInstanceInfo instance;
            public OWRecord mapData;
            public OWRecord zeroFile;
            public ulong unk1300_0;
            public ulong unk1300_1;
            public ulong offset1;
            public ulong unk1401_1;
            public OWRecord data;
            public OWRecord background;
            public OWRecord flag;
            public OWRecord f00D1;
            public OWRecord f00D2;
            public OWRecord audio;
            public ulong unk1;
            public ulong unk2;
            public ulong unk3;
            public ulong unk4;
            public ulong unk5;
            public ulong unk6;
            public ulong unk1401_3;
            public ulong unk1401_4;
            public OWRecord name;
            public OWRecord subline;
            public OWRecord subline2;
            public OWRecord stateA;
            public OWRecord stateB;
            public OWRecord description1;
            public OWRecord description2;
            public OWRecord texture1;
            public OWRecord unkRecord1;
            public OWRecord unkRecord2;
            public ulong offset2;
            public ulong unk1401_6;
            public uint unk1401_7;
            public uint unk7;
            public uint unk8;
            public uint unk9;
            public ulong unkA;
            public ulong unk1401_8;
            public ulong unk1401_9;
        }

        private MapMasterHeader header;
        public MapMasterHeader Header => header;

        public ulong DataKey(ushort type) {
            return (header.data.key & ~0xFFFF00000000ul) | (((ulong)type) << 32);
        }

        public void Read(Stream input, OWLib.STUD stud) {
            using (BinaryReader reader = new BinaryReader(input, System.Text.Encoding.Default, true)) {
                header = reader.Read<MapMasterHeader>();

                header.data.key = (header.data.key & ~0xFFFFFFFF00000000ul) | 0x0DD0000100000000ul;
            }
        }
    }
}
