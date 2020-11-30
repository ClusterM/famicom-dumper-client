using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System.Collections.Generic;

namespace com.clusterrr.Famicom
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
        int Number { get; }
        
        /// <summary>
        /// Number of submapper (0 if none)
        /// </summary>
        //byte Submapper { get; }
        
        /// <summary>
        /// Name of the mapper to store in UNIF container (null if none)
        /// </summary>
        string UnifName { get; }

        /// <summary>
        /// Default PRG size to dump (in bytes)
        /// </summary>
        int DefaultPrgSize { get; }

        /// <summary>
        /// Default CHR size to dump (in bytes)
        /// </summary>
        int DefaultChrSize { get; }

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
        void DumpChr(IFamicomDumperConnection dumper, List<byte> data, int size = 0);

        /// <summary>
        /// This method will be called to enable PRG RAM
        /// </summary>
        /// <param name="dumper"></param>
        void EnablePrgRam(IFamicomDumperConnection dumper);

        /// <summary>
        /// This method must return mirroring type, it can call dumper.GetMirroring() if it's not fixed
        /// </summary>
        /// <param name="dumper">FamicomDumperConnection object to access cartridge</param>
        /// <returns></returns>
        //NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper);
    }
}
