using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cluster.Famicom
{
    public class NesFile
    {
        public byte[] PRG = null;
        public byte[] CHR = null;
        public byte[] Trainer = null;
        public byte Mapper = 0;
        public MirroringType Mirroring = MirroringType.Horizontal;
        public TvSystemType TvSystem = TvSystemType.Ntsc;
        public bool SRAM = false;
        public bool VSunisystem = false;
        public bool PlayChoice10 = false;

        public enum MirroringType { Horizontal = 0, Vertical = 1, FourScreenVram = 2, Unknown_both = 0xfe, Unknown_none = 0xff };
        public enum TvSystemType { Ntsc = 0, Pal = 1 };

        public NesFile()
        {
        }

        public NesFile(byte[] data)
        {

            if (data[0] != 0x4E ||
            data[1] != 0x45 ||
            data[2] != 0x53 ||
            data[3] != 0x1A) throw new Exception("Invalid NES file");

            var prgSize = data[4] * 16384;
            var chrSize = data[5] * 8192;
            Mirroring = (MirroringType)(data[6] & 1);
            SRAM = (data[6] & (1 << 1)) != 0;
            if ((data[6] & (1 << 2)) != 0)
            {
                Trainer = new byte[512];
            }
            else
            {
                Trainer = null;
            }
            if ((data[6] & (1 << 3)) != 0)
                Mirroring = MirroringType.FourScreenVram;

            Mapper = (byte)((data[6] >> 4) | (data[7] & 0xF0));

            data[7] = 0;

            VSunisystem = (data[7] & 1) != 0;
            PlayChoice10 = (data[7] & (1 << 1)) != 0;

            TvSystem = (TvSystemType) (data[9] & 1);

            int offset = 16;
            if (Trainer != null)
            {
                Array.Copy(data, offset, Trainer, 0, 512);
                offset += 512;
            }

            PRG = new byte[prgSize];
            Array.Copy(data, offset, PRG, 0, prgSize);
            offset += prgSize;

            CHR = new byte[chrSize];
            Array.Copy(data, offset, CHR, 0, chrSize);
        }

        public NesFile(string fileName)
            : this(File.ReadAllBytes(fileName))
        {
        }

        public void Save(string fileName)
        {
            var data = new List<byte>();
            var header = new byte[16];
            header[0] = 0x4E;
            header[1] = 0x45;
            header[2] = 0x53;
            header[3] = 0x1A;
            header[4] = (byte)(PRG.Length / 16384);
            header[5] = (byte)(CHR.Length / 8192);
            header[6] = 0;
            if (Mirroring == MirroringType.Vertical) header[6] |= 1;
            if (SRAM) header[6] |= (1 << 1);
            if (Trainer != null) header[6] |= (1 << 2);
            if (Mirroring == MirroringType.FourScreenVram)
                header[6] |= (1 << 3);
            header[6] |= (byte)(Mapper << 4);

            header[7] = 0;
            if (VSunisystem) header[7] |= 1;
            if (PlayChoice10) header[7] |= 1 << 1;
            header[7] |= (byte)(Mapper & 0xf0);

            header[8] = 0; // PRG RAM size in 8 KB

            header[9] = 0;
            if (TvSystem == TvSystemType.Pal) header[9] |= 1;

            data.AddRange(header);
            data.AddRange(PRG);
            data.AddRange(CHR);
            if (Trainer != null)
                data.AddRange(Trainer);

            File.WriteAllBytes(fileName, data.ToArray());
        }
    }
}
