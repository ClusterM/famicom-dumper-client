# Client (PC-software) for Famicom Dumper/Programmer

This is the client for the Famicom Dumper/Programmer hardware:
- [https://github.com/ClusterM/famicom-dumper-writer](https://github.com/ClusterM/famicom-dumper-writer) - my own dumper project
- [https://github.com/postal2201/8-bit-DumpShield](https://github.com/postal2201/8-bit-DumpShield) - Arduino MEGA2560 Shield

This application developed to run on Windows with .NET Framework 4.8 and Linux with Mono 6.6.0 or greater.


## Features

It can be used to:
- Dump Famicom/NES cartridges using C# scripts to describe any mapper, also it's bundled with scripts for some popular mappers
- Reverse engineer unknown mappers using C# scripts
- Read/write battery backed PRG RAM to transfer game saves
- Read/write Famicom Disk System cards
- (Re)write ultra cheap COOLBOY cartridges using both soldering (for old revisions) and soldering-free (new ones) versions, also it supports both COOLBOY (with $600x registers) and COOLBOY2 aka MINDKIDS (with $500x registers)
- (Re)write [COOLGIRL](https://github.com/ClusterM/coolgirl-famicom-multicard) cartridges
- Test hardware in cartridges
- Do everything described above over the network


## Usage

It's a command-line application.

Usage on Windows: **FamicomDumper.exe \<command\> [options]**

On *nix you can run it using [Mono](https://www.mono-project.com/): **mono FamicomDumper.exe \<command\> [options]**

Available commands:
- **list-mappers** - list available mappers to dump
- **dump** - dump cartridge
- **server** - start server for remote dumping
- **script** - execute C# script specified by --csfile option
- **reset** - simulate reset (M2 goes to Z-state for a second)
- **dump-fds** - dump FDS card using RAM adapter and FDS drive
- **write-fds** - write FDS card using RAM adapter and FDS drive
- **dump-tiles** - dump CHR data to PNG file
- **read-prg-ram** - read PRG RAM (battery backed save if exists)
- **write-prg-ram** - write PRG RAM
- **write-coolboy** - write COOLBOY cartridge
- **write-coolgirl** - write COOLGIRL cartridge
- **write-eeprom** - write EEPROM-based cartridge
- **test-prg-ram** - run PRG RAM test
- **test-chr-ram** - run CHR RAM test
- **test-battery** - test battery-backed PRG RAM
- **test-prg-ram-coolgirl** - run PRG RAM test for COOLGIRL cartridge
- **test-chr-ram-coolgirl** - run CHR RAM test for COOLGIRL cartridge
- **test-coolgirl** - run all RAM tests for COOLGIRL cartridge
- **test-bads-coolgirl** - find bad sectors on COOLGIRL cartridge
- **read-crc-coolgirl** - show CRC checksum for COOLGIRL
- **info-coolboy** - show information about COOLBOY's flash memory
- **info-coolgirl** - show information about COOLGIRL's flash memory

Available options:
- **--port** <*com*> - serial port of dumper or serial number of FTDI device, default - auto
- **--tcpport** <*port*> - TCP port for client/server communication, default - 26672
- **--host** <*host*> - enable network client and connect to specified host
- **--mapper** <*mapper*> - number, name or path to C# script of mapper for dumping, default is 0 (NROM)
- **--file** <*output.nes*> - output filename (.nes, .png or .sav)
- **--psize** <*size*> - size of PRG memory to dump, you can use "K" or "M" suffixes
- **--csize** <*size*> - size of CHR memory to dump, you can use "K" or "M" suffixes
- **--reset** - simulate reset first
- **--csfile** <*C#_file*> - execute C# script from file
- **--unifname** <*name*> - internal ROM name for UNIF dumps
- **--unifauthor** <*name*> - author of dump for UNIF dumps
- **--fds-sides** <*number*> - number of FDS sides to dump (default 1)
- **--fds-no-header** - do not add header to output file during FDS dumping
- **--fds-dump-hidden** - try to dump hidden files during FDS dumping
- **--badsectors** - comma separated list of bad sectors for COOLBOY/COOLGIRL writing
- **--sound** - play sound when done or error occured
- **--check** - verify COOLBOY/COOLGIRL checksum after writing
- **--lock** - write-protect COOLBOY/COOLGIRL sectors after writing


## Mapper script files
Mapper files are stored in "mappers" subdirectory. When you specify a mapper number or name, the application compiles the scripts in that directory to find a matching one.

Mapper scripts are written in C# language. Each script must contain namespace (any name allowed) with class (also any name) that impliments [IMapper](https://github.com/ClusterM/famicom-dumper-client/blob/master/FamicomDumper/IMapper.cs) interface.
```C#
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
        byte Submapper { get; }

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
        NesFile.MirroringType GetMirroring(IFamicomDumperConnection dumper);
    }
```

FamicomDumperConnection implements [IFamicomDumperConnection](https://github.com/ClusterM/famicom-dumper-client/blob/master/FamicomDumperConnection/IFamicomDumperConnection.cs) interface:
```C#
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
```

Check "mappers" directory for examples.


## Other scripts

You can run custom C# scripts to interact with dumper and cartridge. It's usefull for reverse engineering. Each script must contain namespace (any name allowed) with class (also any name) that contains **void Run(IFamicomDumperConnection dumper)** method. This method will be executed if --csfile option is specified. Also you can use [NesFile](https://github.com/ClusterM/famicom-dumper-client/blob/master/NesContainers/NesFile.cs) and [UnifFile](https://github.com/ClusterM/famicom-dumper-client/blob/master/NesContainers/UnifFile.cs) containers.

You can run script alone like this:
```
FamicomDumper.exe script --csfile DemoScript.cs
```
Or execute it before main action like this:
```
FamicomDumper.exe dump --mapper MMC3 --file game.nes --csfile DemoScript.cs
```

So you can write your own code to interact with dumper object and read/write data from/to cartridge. This dumper object can be even on another PC (read below)! Check DemoScript.cs file for example script.


## Remoting

You can start this application as server on one PC:
```
FamicomDumper.exe server --port COM14
```

And dump cartridge over network using another PC:
```
FamicomDumper.exe dump --mapper CNROM --file game.nes --host example.com
```

It's useful if you want to reverse engineer cartridge of your remote friend. You can use all commands and scripts to interact with remote dumper just like it's connected locally.


## Examples

Dump NROM-cartridge using dumper on port "COM14" to file "game.nes". PRG and CHR sizes are default.
~~~~
  > FamicomDumper.exe dump --port COM14 --mapper nrom --file game.nes
  Dumper initialization... OK
  Using mapper: #0 (NROM)
  Dumping...
  PRG memory size: 32K
  Dumping PRG... OK
  CHR memory size: 8K
  Dumping CHR... OK
  Mirroring: Horizontal (0 0 1 1)
  Saving to game.nes...
  Done in 3 seconds
~~~~

Dump MMC1-cartridge (iNES mapper #1) using dumper with serial number (Windows only) "A9Z1A0WD". PRG size is 128 kilobytes, CHR size is 128 kilobytes too.
~~~~
>FamicomDumper.exe dump --port A9Z1A0WD --mapper 1 --psize 128K --csize 128K --file game.nes
Dumper initialization... OK
Using mapper: #1 (MMC1)
Dumping...
PRG memory size: 128K
Reading PRG bank #0... OK
Reading PRG bank #1... OK
Reading PRG bank #2... OK
Reading PRG bank #3... OK
...
~~~~

Dump 32K of PRG and 8K of CHR as simple NROM cartridge but execute C# script first:
~~~~
>FamicomDumper.exe dump --port COM14 --mapper 0 --psize 32K --csize 8K --file game.nes --csfile init.cs"
Dumper initialization... OK
Compiling init.cs...
Running init.Run()...
Dumping...
PRG memory size: 32K
Dumping PRG... OK
CHR memory size: 8K
Dumping CHR... OK
Mirroring: Horizontal (0 0 1 1)
Saving to game.nes...
Done in 5 seconds
~~~~

Dump 32MBytes of COOLBOY cartridge using C# script and save it as UNIF file with some extra info:
~~~~
>FamicomDumper.exe dump --port COM14 --mapper mappers\coolboy.cs --psize 32M --file coolboy.unf --unifname "COOLBOY 400-IN-1" --unifauthor "John Smith"
Dumper initialization... OK
Using mapper: COOLBOY
Dumping...
PRG memory size: 32768K
Reading PRG banks #0/0 and #0/1...
Reading PRG banks #0/2 and #0/3...
Reading PRG banks #0/4 and #0/5...
~~~~

Read battery-backed save from MMC1 cartridge:
~~~~
>FamicomDumper.exe read-prg-ram --port COM14 --mapper mmc1 --file "zelda.sav"
Dumper initialization... OK
Using mapper: #1 (MMC1)
Reading PRG-RAM...
Done in 2 seconds
~~~~

Write battery-backed save back to MMC1 cartridge:
~~~~
>FamicomDumper.exe write-prg-ram --port COM14 --mapper mmc1 --file "zelda_hacked.sav"
Dumper initialization... OK
Using mapper: #1 (MMC1)
Writing PRG-RAM... Done in 1 seconds
~~~~

Rewrite ultracheap chinese COOLBOY cartridge and play sound when it's done:
~~~~
>FamicomDumper.exe write-coolboy --port COM14 --file "CoolBoy 400-in-1 (Alt Version, 403 games)(Unl)[U][!].nes" --sound
Dumper initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/2048 (0%, 00:00:02/00:40:53)...
~~~~

Also you can rewrite [COOLGIRL](https://github.com/ClusterM/coolgirl-famicom-multicard) cartridges:
~~~~
>FamicomDumper.exe write-coolgirl --file multirom.unf --port COM14
Dumper initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/114 (0%, 00:00:02/00:00:02)... OK
Writing 2/114 (0%, 00:00:02/00:00:02)... OK
Writing 3/114 (1%, 00:00:02/00:00:02)... OK
Writing 4/114 (2%, 00:00:02/00:00:02)... OK
Erasing sector... OK
Writing 5/114 (3%, 00:00:03/00:00:29)... OK
~~~~

Dump two-sided Famicom Disk System card:
~~~~
>FamicomDumper.exe dump-fds --fds-sides 2
Autodetected virtual serial port: COM13
Dumper initialization... OK
Reading disk... Done.
Disk info block:
 Game name: MET
 Manufacturer code: Nintendo
 Game type: normal disk
 Game version: 2
 Disk number: 0
 Disk side: A
 Actual disk side: $FF
 Disk type: FMS
 Manufacturing date: 1986.09.09
 Country code: Japan
 Disk writer serial number: $0961
 Disk rewrite count: 0
 Price code: $FF
Number of non-hidden files: 15
...
Please remove disk card... OK
Please set disk card, side #2...
...
Done in 0:00:25
~~~~

Start server on port 9999 and let other person to dump cartridge over network:
~~~~
>FamicomDumper.exe server --tcpport 9999
Autodetected virtual serial port: COM13
Dumper initialization... OK
Listening port 9999, press any key to stop
~~~~

Connect to remote dumper and execute C# script:
~~~~
FamicomDumper.exe script --csfile DemoScript.cs --host clusterrr.com --tcpport 9999
Dumper initialization... OK
Compiling DemoScript.cs...
Running DemoScript.Run()...
~~~~


## Donation and contact

E-mail and PayPal: clusterrr@clusterrr.com
