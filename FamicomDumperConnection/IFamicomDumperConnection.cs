using com.clusterrr.Famicom.Containers;

namespace com.clusterrr.Famicom.DumperConnection
{
    public interface IFamicomDumperConnection
    {
        /// <summary>
        /// Simulate reset (M2 goes to Z-state for a second)
        /// </summary>
        void Reset();

        /// <summary>
        /// Read data from CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns></returns>
        byte[] ReadCpu(ushort address, int length);

        /// <summary>
        /// Read data from PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to read from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns></returns>
        byte[] ReadPpu(ushort address, int length);

        /// <summary>
        /// Write data to CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write (single byte)</param>
        void WriteCpu(ushort address, byte data);

        /// <summary>
        /// Write data to CPU (PRG) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        void WriteCpu(ushort address, byte[] data);

        /// <summary>
        /// Write data to PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write (single byte)</param>
        void WritePpu(ushort address, byte data);


        /// <summary>
        /// Write data to PPU (CHR) bus
        /// </summary>
        /// <param name="address">Address to write to</param>
        /// <param name="data">Data to write, address will be incremented after each byte</param>
        void WritePpu(ushort address, byte[] data);

        /// <summary>
        /// Get current mirroring
        /// </summary>
        /// <returns>bool[4] array with CIRAM A10 values for each region: $0000-$07FF, $0800-$0FFF, $1000-$17FF and $1800-$1FFF</returns>
        bool[] GetMirroringRaw();

        /// <summary>
        /// Get current mirroring
        /// </summary>
        /// <returns>Detected mirroring as NesFile.MirroringType</returns>
        NesFile.MirroringType GetMirroring();
    }
}