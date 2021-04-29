using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace com.clusterrr.Famicom
{
    public class FDS
    {
        public static void WriteFDS(FamicomDumperConnection dumper, string fileName, bool needCheck = false)
        {
            if (dumper.ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old, update it to read/write FDS cards");
            var oldTimeout = dumper.Timeout;
            try
            {
                dumper.Timeout = 30000;
                CheckRAMAdapter(dumper);

                var rom = new FdsFile(fileName);

                for (int sideNumber = 0; sideNumber < rom.Sides.Count; sideNumber++)
                {
                    var driveStatus = dumper.ReadCpu(0x4032);
                    if ((driveStatus & 1) != 0)
                    {
                        Console.Write($"Please set disk card, side #{sideNumber + 1}... ");
                        while ((driveStatus & 1) != 0)
                        {
                            Thread.Sleep(100);
                            driveStatus = dumper.ReadCpu(0x4032);
                        }
                        Console.WriteLine("OK");
                    }

                    PrintDiskHeaderInfo(rom.Sides[sideNumber].DiskInfoBlock);
                    Console.WriteLine($"Number of non-hidden files: {rom.Sides[sideNumber].FileAmount}");
                    Console.WriteLine($"Number of hidden files: {rom.Sides[sideNumber].Files.Count - rom.Sides[sideNumber].FileAmount}");
                    var blocks = rom.Sides[sideNumber].GetBlocks().ToArray();
                    Console.WriteLine($"Total blocks to write: {blocks.Length}");
                    byte blocksWrited = 0;
                    while (blocksWrited < blocks.Length)
                    {
                        uint totalSize = 1;
                        var blockIDs = new List<byte>();
                        var blocksToWrite = new List<IFdsBlock>();

                        for (byte i = blocksWrited; i < blocks.Count(); i++)
                        {
                            if (totalSize + blocks[i].Length + 3 <= dumper.MaxWritePacketSize)
                            {
                                blocksToWrite.Add(blocks[i]);
                                blockIDs.Add(i);
                                totalSize += blocks[i].Length + 3;
                            }
                            else break;
                        }
                        if (!blocksToWrite.Any())
                            throw new OutOfMemoryException("Dumper has not enoght memory to write such big block");
                        Console.Write($"Writing block(s): {string.Join(", ", blockIDs)}... ");
                        dumper.WriteFdsBlocks(blockIDs.ToArray(), blocksToWrite.ToArray());
                        Console.WriteLine("OK");
                        blocksWrited += (byte)blocksToWrite.Count;
                    }

                    if (needCheck)
                    {
                        Console.WriteLine("Starting verification process");
                        var hiddenFiles = rom.Sides[sideNumber].Files.Count > rom.Sides[sideNumber].FileAmount;
                        var sideImage = DumpFDSSide(dumper, dumpHiddenFiles: hiddenFiles, printDiskInfo: false);
                        if (!sideImage.DiskInfoBlock.Equals(rom.Sides[sideNumber].DiskInfoBlock))
                            throw new IOException("Disk info block verification failed");
                        if (!sideImage.FileAmount.Equals(rom.Sides[sideNumber].FileAmount))
                            throw new IOException("File amount block verification failed");
                        if (sideImage.Files.Count < rom.Sides[sideNumber].Files.Count)
                            throw new IOException($"Invalid file count: {sideImage.Files.Count} < {rom.Sides[sideNumber].Files.Count}");
                        for (int f = 0; f < rom.Sides[sideNumber].Files.Count; f++)
                        {
                            if (!sideImage.Files[f].HeaderBlock.Equals(rom.Sides[sideNumber].Files[f].HeaderBlock))
                                throw new IOException($"File #{f} header block verification failed");
                            if (!sideImage.Files[f].DataBlock.Equals(rom.Sides[sideNumber].Files[f].DataBlock))
                                throw new IOException($"File #{f} data block verification failed");
                        }
                        Console.WriteLine("Verification successful.");
                    }

                    if (sideNumber + 1 < rom.Sides.Count)
                    {
                        driveStatus = dumper.ReadCpu(0x4032);
                        if ((driveStatus & 1) == 0)
                        {
                            Console.Write($"Please remove disk card... ");
                            while ((driveStatus & 1) == 0)
                            {
                                Thread.Sleep(100);
                                driveStatus = dumper.ReadCpu(0x4032);
                            }
                            Console.WriteLine("OK");
                        }
                    }
                }
            }
            finally
            {
                dumper.Timeout = oldTimeout;
            }
        }

        public static void DumpFDS(FamicomDumperConnection dumper, string fileName, byte sides = 1, bool dumpHiddenFiles = true, bool useHeader = true)
        {
            if (dumper.ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old, update it to read/write FDS cards");
            CheckRAMAdapter(dumper);

            var sideImages = new List<FdsDiskSide>();
            for (int side = 1; side <= sides; side++)
            {
                var driveStatus = dumper.ReadCpu(0x4032);
                if ((driveStatus & 1) != 0)
                {
                    Console.Write($"Please set disk card, side #{side}... ");
                    while ((driveStatus & 1) != 0)
                    {
                        Thread.Sleep(100);
                        driveStatus = dumper.ReadCpu(0x4032);
                    }
                    Console.WriteLine("OK");
                }
                var sideImage = DumpFDSSide(dumper, dumpHiddenFiles, printDiskInfo: true);
                sideImages.Add(sideImage);

                if (side < sides)
                {
                    driveStatus = dumper.ReadCpu(0x4032);
                    if ((driveStatus & 1) == 0)
                    {
                        Console.Write($"Please remove disk card... ");
                        while ((driveStatus & 1) == 0)
                        {
                            Thread.Sleep(100);
                            driveStatus = dumper.ReadCpu(0x4032);
                        }
                        Console.WriteLine("OK");
                    }
                }
            }
            Console.Write($"Saving to {fileName}... ");
            var fdsImage = new FdsFile(sideImages);
            fdsImage.Save(fileName, useHeader);
            Console.WriteLine("OK");
        }

        private static FdsDiskSide DumpFDSSide(FamicomDumperConnection dumper, bool dumpHiddenFiles = true, bool printDiskInfo = false)
        {
            if (dumper.ProtocolVersion < 3)
                throw new NotSupportedException("Dumper firmware version is too old, update it to read/write FDS cards");
            var oldTimeout = dumper.Timeout;
            try
            {
                dumper.Timeout = 30000;
                CheckRAMAdapter(dumper);

                IEnumerable<IFdsBlock> blocks;
                if (dumper.MaxReadPacketSize != ushort.MaxValue)
                    // Reading block by block
                    blocks = DumpSlow(dumper, dumpHiddenFiles, printDiskInfo);
                else
                    // Reading the whole disk at once
                    blocks = DumpFast(dumper, dumpHiddenFiles, printDiskInfo);
                var sideImage = new FdsDiskSide(blocks);
                if (sideImage.Files.Count < sideImage.FileAmount)
                    throw new IOException($"Invalid file count: {sideImage.Files.Count} < {sideImage.FileAmount}");
                return sideImage;
            }
            finally
            {
                dumper.Timeout = oldTimeout;
            }
        }

        private static void CheckRAMAdapter(FamicomDumperConnection dumper)
        {
            // Just simple test that RAM adapter is connected
            bool ramAdapterPresent = true;
            dumper.WriteCpu(0x4023, 0x01); // enable disk registers
            dumper.WriteCpu(0x4026, 0x00);
            dumper.WriteCpu(0x4025, 0b00100110); // reset
            dumper.WriteCpu(0x0000, 0xFF); // to prevent open bus read
            var ext = dumper.ReadCpu(0x4033);
            if (ext != 0x00) ramAdapterPresent = false;
            dumper.WriteCpu(0x4026, 0xFF);
            dumper.WriteCpu(0x0000, 0x00); // to prevent open bus read
            ext = dumper.ReadCpu(0x4033);
            if ((ext & 0x7F) != 0x7F) ramAdapterPresent = false;
            if (!ramAdapterPresent) throw new IOException("RAM adapter IO error, is it connected?");
        }

        private static IEnumerable<IFdsBlock> DumpSlow(FamicomDumperConnection dumper, bool dumpHiddenFiles = false, bool printDiskInfo = false)
        {
            var blocks = new List<IFdsBlock>();
            byte blockNumber = 0;
            while (true)
            {
                switch (blockNumber)
                {
                    case 0:
                        Console.Write("Reading disk info block... ");
                        break;
                    case 1:
                        Console.Write("Reading file amount block... ");
                        break;
                    default:
                        if ((blockNumber % 2) == 0)
                            Console.Write($"Reading file #{(blockNumber - 2) / 2}/{(blocks[1] as FdsBlockFileAmount).FileAmount} header block... ");
                        else
                            Console.Write($"Reading file #{(blockNumber - 2) / 2}/{(blocks[1] as FdsBlockFileAmount).FileAmount} data block... ");
                        break;
                }
                var fdsData = dumper.ReadFdsBlocks(blockNumber, 1);
                if (fdsData.Length == 0)
                {
                    if (blocks.Count > 2 && blocks.Count >= 2 + (blocks[1] as FdsBlockFileAmount).FileAmount * 2)
                    {
                        Console.WriteLine("Invalid block, it's not hidden file, aboritng");
                        break;
                    }
                    throw new IOException($"Invalid block #{blockNumber} (file #{(blockNumber - 2) / 2})");
                }
                var block = fdsData[0];
                if (!block.IsValid)
                {
                    switch (blockNumber)
                    {
                        case 0:
                            throw new IOException($"Invalid disk info block");
                        case 1:
                            throw new IOException($"Invalid file amount block");
                    }
                    if (blocks.Count >= 2 + (blocks[1] as FdsBlockFileAmount).FileAmount * 2)
                    {
                        Console.WriteLine("Invalid block, it's not hidden file, abortitng");
                        break;
                    }
                    else
                    {
                        // Fatal error if bad block ID on non-hidden file
                        throw new IOException($"Invalid block #{blockNumber} (file #{(blockNumber - 2) / 2}) type");
                    }
                }
                if (!block.CrcOk)
                {
                    switch (blockNumber)
                    {
                        case 0:
                            throw new IOException($"Invalid CRC on disk info block");
                        case 1:
                            throw new IOException($"Invalid CRC on file amount block");
                    }
                    if (blocks.Count < 2 + (blocks[1] as FdsBlockFileAmount).FileAmount * 2)
                    {
                        // Fatal error if bad CRC on non-hidden file
                        throw new IOException($"Invalid CRC on block #{blockNumber} (file #{(blockNumber - 2) / 2})");
                    }
                    else
                    {
                        Console.WriteLine("Invalid CRC, it's not hidden file, abortitng");
                        break;
                    }
                }
                blocks.AddRange(fdsData);
                Console.WriteLine($"OK");
                // Some info
                if (printDiskInfo)
                {
                    switch (blockNumber)
                    {
                        case 0:
                            PrintDiskHeaderInfo(block as FdsBlockDiskInfo);
                            break;
                        case 1:
                            Console.WriteLine($"Number of non-hidden files: {(block as FdsBlockFileAmount).FileAmount}");
                            break;
                        default:
                            if ((blockNumber % 2) == 0)
                            {
                                Console.WriteLine($"File #{(blockNumber - 2) / 2}:");
                                PrintFileHeaderInfo(block as FdsBlockFileHeader);
                            }
                            break;
                    }
                }
                // Abort if end of head meet
                if (block.EndOfHeadMeet)
                {
                    Console.WriteLine("End of head meet, aborting");
                    break;
                }
                // Abort if last file dumped
                if (!dumpHiddenFiles && (blocks.Count >= 2) && (blocks.Count >= 2 + (blocks[1] as FdsBlockFileAmount).FileAmount * 2))
                    break;
                blockNumber++;
            }
            if (dumpHiddenFiles)
            {
                Console.WriteLine($"Number of hidden files: {(blocks.Count - 2) / 2 - (blocks[1] as FdsBlockFileAmount).FileAmount}");
            }
            return blocks;
        }

        private static IEnumerable<IFdsBlock> DumpFast(FamicomDumperConnection dumper, bool dumpHiddenFiles = false, bool printDiskInfo = false)
        {
            Console.Write($"Reading disk... ");
            var blocks = dumper.ReadFdsBlocks().ToArray();
            if (blocks.Length == 0)
                throw new IOException("Invalid disk info block");
            if (!blocks[0].IsValid)
                throw new IOException($"Invalid disk info block type");
            if (!blocks[0].CrcOk)
                throw new IOException($"Invalid CRC on disk info block");
            if (printDiskInfo)
                PrintDiskHeaderInfo(blocks[0] as FdsBlockDiskInfo);
            if (blocks.Length == 1)
                throw new IOException("Invalid file amount block");
            if (!blocks[1].IsValid)
                throw new IOException($"Invalid file amount block type");
            if (!blocks[1].CrcOk)
                throw new IOException($"Invalid CRC on file amount block");
            Console.WriteLine($"Done");
            var fileAmount = (blocks[1] as FdsBlockFileAmount).FileAmount;
            if (printDiskInfo)
                Console.WriteLine($"Number of non-hidden files: {fileAmount}");

            // Check files and print info
            int validBlocks = 2 + fileAmount * 2;
            for (int blockNumber = 2; blockNumber < (dumpHiddenFiles ? blocks.Length : (2 + fileAmount * 2)); blockNumber++)
            {
                var block = blocks[blockNumber];
                if (!block.IsValid)
                {
                    if (blocks.Length < 2 + fileAmount * 2)
                        throw new IOException($"Invalid block #{blockNumber} (file #{(blockNumber - 2) / 2}) type");
                    else
                    {
                        if (printDiskInfo)
                            Console.WriteLine($"Invalid block #{blockNumber}, it's not hidden file, aboritng");
                        validBlocks = blockNumber;
                        break;
                    }
                }
                if (!block.CrcOk)
                {
                    if (blocks.Length < 2 + fileAmount * 2)
                        throw new IOException($"Invalid CRC on block #{blockNumber} (file #{(blockNumber - 2) / 2})");
                    else
                    {
                        if (printDiskInfo)
                            Console.WriteLine($"Invalid CRC on block #{blockNumber}, it's not hidden file, aboritng");
                        validBlocks = blockNumber;
                        break;
                    }
                }
                if ((blockNumber % 2) == 0)
                {
                    if (printDiskInfo)
                    {
                        Console.WriteLine($"File #{(blockNumber - 2) / 2}/{fileAmount}:");
                        PrintFileHeaderInfo(block as FdsBlockFileHeader);
                    }
                }
            }
            if (blocks.Length < 2 + fileAmount * 2)
                throw new IOException($"Only {(blocks.Length - 2) / 2} of {fileAmount} valid files received");
            if (dumpHiddenFiles && printDiskInfo)
            {
                Console.WriteLine($"Number of hidden files: {(blocks.Length - 2) / 2 - fileAmount}");
            }
            return blocks.Take(validBlocks);
        }

        public static void PrintDiskHeaderInfo(FdsBlockDiskInfo header)
        {
            Console.WriteLine("Disk info block:");
            Console.WriteLine($" Game name: {header.GameName}");
            Console.WriteLine($" Manufacturer code: {header.ManufacturerCode}");
            Console.Write($" Game type: ");
            switch (header.GameType)
            {
                case ' ':
                    Console.WriteLine("normal disk");
                    break;
                case 'E':
                    Console.WriteLine("event");
                    break;
                case 'R':
                    Console.WriteLine("reduction in price");
                    break;
                default:
                    Console.WriteLine($"{header.GameType}");
                    break;
            }
            Console.WriteLine($" Game version: {header.GameVersion}");
            Console.WriteLine($" Disk number: {header.DiskNumber}");
            Console.WriteLine($" Disk side: {header.DiskSide}");
            if ((byte)header.ActualDiskSide <= 1)
                Console.WriteLine($" Actual disk side: {header.ActualDiskSide}");
            else
                Console.WriteLine($" Actual disk side: ${(byte)header.ActualDiskSide:X2}");
            Console.WriteLine($" Disk type: {header.DiskType}");
            Console.WriteLine($" Manufacturing date: {header.ManufacturingDate:yyyy.MM.dd}");
            Console.WriteLine($" Country code: {header.CountryCode.ToString().Replace("_", " ")}");
            if (header.RewrittenDate.Year > 1925 && header.RewrittenDate != header.ManufacturingDate)
                Console.WriteLine($" Rewritten date: {header.RewrittenDate:yyyy.MM.dd}");
            Console.WriteLine($" Disk writer serial number: ${header.DiskWriterSerialNumber:X4}");
            Console.WriteLine($" Disk rewrite count: {header.DiskRewriteCount}");
            Console.WriteLine($" Price code: ${header.Price:X2}");
            //Console.WriteLine($"Boot file id: ${header.BootFile}");
        }

        public static void PrintFileHeaderInfo(FdsBlockFileHeader header)
        {
            Console.WriteLine($" File name: {header.FileName}");
            Console.WriteLine($" File indicate code: ${header.FileIndicateCode:X2}");
            Console.WriteLine($" File kind: {header.FileKind}");
            Console.WriteLine($" File destination address: ${header.FileAddress:X4}");
            Console.WriteLine($" File size: {header.FileSize} bytes (${header.FileSize:X4})");
        }
    }
}
