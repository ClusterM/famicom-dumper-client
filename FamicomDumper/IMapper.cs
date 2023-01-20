using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;

#nullable disable

namespace com.clusterrr.Famicom.Dumper
{
    public interface IMapper
    {
        /// <summary>
        /// Name of the mapper
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Number of the mapper to spore in the iNES header (-1 if none)
        /// </summary>
        int Number { get => -1; }

        /// <summary>
        /// Number of submapper (0 if none)
        /// </summary>
        byte Submapper { get => 0; }

        /// <summary>
        /// Name of the mapper to store in UNIF container (null if none)
        /// </summary>
        string UnifName { get => null; }

        /// <summary>
        /// Default PRG size to dump (in bytes)
        /// </summary>
        int DefaultPrgSize { get; }

        /// <summary>
        /// Default CHR size to dump (in bytes)
        /// </summary>
        int DefaultChrSize { get => 0; }

        /// <summary>
        /// This method will be called to dump PRG
        /// </summary>
        /// <param name="dumper">FamicomDumperConnection object to access cartridge</param>
        /// <param name="data">This list must be filled with dumped PRG data</param>
        /// <param name="size">Size of PRG to dump requested by user (in bytes)</param>
        void DumpPrg(IFamicomDumperConnection dumper, List<byte> data, int size = 0);

        /// <summary>
        /// This method will be called to dump CHR
        /// </summary>
        /// <param name="dumper">FamicomDumperConnection object to access cartridge</param>
        /// <param name="data">This list must be filled with dumped CHR data</param>
        /// <param name="size">Size of CHR to dump requested by user (in bytes)</param>
        void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size = 0)
            => throw new NotSupportedException("This mapper doesn't have a CHR ROM");

        /// <summary>
        /// This method will be called to enable PRG RAM
        /// </summary>
        /// <param name="dumper"></param>
        void EnablePrgRam(IFamicomDumperConnection dumper) 
            => throw new NotImplementedException("PRG RAM is not supported by this mapper");

        /// <summary>
        /// This method must return mirroring type, it can call dumper.GetMirroring() if it's fixed
        /// </summary>
        /// <param name="dumper">FamicomDumperConnection object to access cartridge</param>
        /// <returns>Mirroring type</returns>
        MirroringType GetMirroring(IFamicomDumperConnection dumper) => dumper.GetMirroring();

        /* Optional properties */
        /// <summary>
        /// Default PRG RAM size, can be used with NES 2.0
        /// </summary>
        public int DefaultPrgRamSize { get => -1; }

        /// <summary>
        /// Default CHR RAM size, can be used with NES 2.0
        /// </summary>
        public int DefaultChrRamSize { get => -1; }

        /// <summary>
        /// Default PRG NVRAM size, can be used with NES 2.0
        /// </summary>
        public int DefaultPrgNvramSize { get => -1; }

        /// <summary>
        /// Default CHR NVRAM size, can be used with NES 2.0
        /// </summary>
        public int DefaultChrNvramSize { get => -1; }
    }
}

#nullable restore
