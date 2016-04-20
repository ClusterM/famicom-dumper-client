# Client (PC-software) for Famicom Dumper/Programmer

This is client for Famicom Dumper/Programmer: [https://github.com/ClusterM/famicom-dumper](https://github.com/ClusterM/famicom-dumper)

You need Windows and .NET Framework 3.5 but it works fine on *nix using Mono.


## Usage

It's command-line application.

Usage: **famicom-dumper.exe \<command\> [options]**

Available commands:  
- **list-mappers** - list built in mappers  
- **dump** - dump cartridge  
- **reset** - simulate reset (M2 goes low for a second)
- **read-prg-ram** - read PRG RAM (battery backed save if exists)  
- **write-prg-ram** - write PRG RAM  
- **write-coolboy** - write COOLBOY cartridge  
- **write-coolgirl** - write COOLGIRL cartridge  
- **test-prg-ram** - run PRG RAM test  
- **test-chr-ram** - run CHR RAM test  
- **test-battery** - test battery-backed PRG RAM  
- **dump-tiles** - dump CHR data to PNG file  
  
Available options:  
- --**port** <*com*> - serial port of dumper or serial number of FTDI device, default - auto  
- --**mapper** <*mapper*> - number, name or patt to LUA script of mapper for dumping, default is 0 (NROM)  
- --**file** <*output.nes*> - output filename (.nes, .png or .sav)  
- --**psize** <*size*> - size of PRG memory to dump, you can use "K" or "M" suffixes  
- --**csize** <*size*> - size of CHR memory to dump, you can use "K" or "M" suffixes  
- --**lua** "<*lua_code*>" - execute lua code first
- --**unifname** <*name*> - internal ROM name for UNIF dumps  
- --**unifauthor** <*name*> - author of dump for UNIF dumps  
- --**reset** - do reset first
- --**sound** - play sounds


## Examples

Dump NROM-cartridge using dumper on port "COM14" to file "game.nes". PRG and CHR size are default.
~~~~
  > famicom-dumper.exe dump --port COM14 --mapper nrom --file game.nes
  PRG reader initialization... OK
  CHR reader initialization... OK
  Using mapper: #0 (NROM)
  Dumping...
  PRG memory size: 32K
  Dumping PRG... OK
  CHR memory size: 8K
  Dumping CHR... OK
  Mirroring: Horizontal (00 00 01 01)
  Saving to game.nes...
  Done in 3 seconds
~~~~

Dump MMC1-cartridge (iNES mapper #1) using dumper with serial number "A9Z1A0WD". PRG size is 128 kilobytes, CHR size is 128 kilobytes too.
~~~~
>famicom-dumper.exe dump --port A9Z1A0WD --mapper 1 --psize 128K --csize 128K --file game.nes
PRG reader initialization... OK
CHR reader initialization... OK
Using mapper: #1 (MMC1)
Dumping...
PRG memory size: 128K
Reading PRG bank #0... OK
Reading PRG bank #1... OK
Reading PRG bank #2... OK
Reading PRG bank #3... OK
...
~~~~

Autodetect FTDI device (port is not specified) by description, simulate reset and dump cartridge using Lua script:
~~~~
>famicom-dumper.exe dump --mapper mappers-lua/MMC3.lua --reset --psize 128K --csize 128K --file game.nes
Searhing for dumper (FTDI device with name "Famicom Dumper/Programmer")...
Number of FTDI devices: 1

Device Index: 0
Flags: 0
Type: FT_DEVICE_232R
ID: 4036001
Location ID: 62
Serial Number: A9Z1A0WD
Description: Famicom Dumper/Programmer

PRG reader initialization... OK
CHR reader initialization... OK
Reset... OK
Using mapper: #4 (MMC3)
Dumping...
PRG memory size: 128K
Reading PRG banks #0 and #1...
Reading PRG banks #2 and #3...
Reading PRG banks #4 and #5...
Reading PRG banks #6 and #7...
Reading PRG banks #8 and #9...
Reading PRG banks #10 and #11...
...
~~~~

Dump 32K of PRG and 8K of CHR as simple NROM cartridge but execute Lua script first:
~~~~
>famicom-dumper.exe dump --port COM14 --mapper 0 --psize 32K --csize 8K --file game.nes --lua "Reset() ; WriteCpu(0x8000, {0x00})"
PRG reader initialization... OK
CHR reader initialization... OK
Executing LUA script...
Reset... OK
CPU write $00 => $8000
Using mapper: #0 (NROM)
Dumping...
PRG memory size: 32K
Dumping PRG... OK
CHR memory size: 8K
Dumping CHR... OK
Mirroring: Horizontal (00 00 01 01)
Saving to game.nes...
Done in 5 seconds
~~~~
Available Lua functions:
- WriteCpu(address, table_of_data)
- WritePpu(address, table_of_data)
- Reset()

Dump 32MBytes of COOLBOY cartridge using Lua script and save it as UNIF file with some extra info:
~~~~
>famicom-dumper.exe dump --port COM14 --mapper mappers-lua\coolboy.lua --psize 32M --file coolboy.unf --unifname "COOLBOY 400-IN-1" --unifauthor "John Smith"
PRG reader initialization... OK
CHR reader initialization... OK
Using mapper: COOLBOY
Dumping...
PRG memory size: 32768K
Reading PRG banks #0/0 and #0/1...
Reading PRG banks #0/2 and #0/3...
Reading PRG banks #0/4 and #0/5...
~~~~

Read battery-backed save from MMC1 cartridge:
~~~~
D:\Projects\C#\FamicomDumper\famicom-dumper\bin\Release>famicom-dumper.exe read-prg-ram --port COM14 --mapper mmc1 --file "zelda.sav"
PRG reader initialization... OK
CHR reader initialization... OK
Using mapper: #1 (MMC1)
Reading PRG-RAM...
Done in 2 seconds
~~~~

Write battery-backed save back to MMC1 cartridge:
~~~~
>famicom-dumper.exe write-prg-ram --port COM14 --mapper mmc1 --file "zelda_hacked.sav"
PRG reader initialization... OK
CHR reader initialization... OK
Using mapper: #1 (MMC1)
Writing PRG-RAM... Done in 1 seconds
~~~~

Rewrite ultracheap chinese COOLBOY cartridge and play sound when it's done:
~~~~
>famicom-dumper.exe write-coolboy --port COM14 --file "CoolBoy 400-in-1 (Alt Version, 403 games)(Unl)[U][!].nes" --sound
PRG reader initialization... OK
CHR reader initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/2048 (0%, 00:00:02/00:40:53)...
~~~~
But you need to unsolder pins /OE and /WE and connect them to TCK and TDO pins on JTAG connector.

Also you can rewrite [COOLGIRL](https://github.com/ClusterM/coolgirl-famicom-multicard) cartridges:
~~~~
>famicom-dumper.exe  write-coolgirl --file multirom.unf --port COM14
PRG reader initialization... OK
CHR reader initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/114 (0%, 00:00:02/00:00:02)... OK
Writing 2/114 (0%, 00:00:02/00:00:02)... OK
Writing 3/114 (1%, 00:00:02/00:00:02)... OK
Writing 4/114 (2%, 00:00:02/00:00:02)... OK
Erasing sector... OK
Writing 5/114 (3%, 00:00:03/00:00:29)... OK
~~~~


## Lua scripting

You can write Lua scripts to add support for more mappers. You must declare those constants:
~~~~
MapperName = "NROM"
~~~~
to define mapper name.

~~~~
DefaultPrgSize = 32 * 1024
~~~~
and
~~~~
DefaultChrSize = 8 * 1024
~~~~
to define default PRG and CHR size.

You need to define mapper number for iNES file:
~~~~
MapperNumber = 7
~~~~
or mapper name for UNIF-mappers:
~~~~
MapperUnifName = "COOLBOY"
~~~~

Of course you need to declare dumping functions:
~~~~
function DumpPrg(size)
	print("Reading PRG...")
	ReadAddPrg(0x8000, size)
end

function DumpChr(size)	
	print("Reading CHR...")
	ReadAddChr(0x0000, size)
end
~~~~

And function to enable PRG-RAM, so we can write and read battery-backed saves:
~~~~
function EnablePrgRam(size)
	WriteCpu(0x8000, {0x80})
	WriteMMC1(0xE000, 0x00)
end
~~~~

Available functions:
- **ReadPrg**(address, length) - read PRG data, returns table of data
- **ReadCpu**(address, length) - alias for *ReadPrg*
- **WriteCpu**(address, table_of_data) - write data to CPU bus
- **WritePrg**(address, table_of_data) - alias for *WriteCpu*
- **AddPrg**(table_of_data) - add data to dumped PRG
- **AddPrgResult**(table_of_data) - alias for *AddPrg*
- **ReadAddPrg**(address, length) - it's like *AddPrg(ReadPrg(address, length))* but faster
- **ReadChr**(address, length) - read CHR data, returns table of data
- **ReadPpu**(address, length) - alias for *ReadChr*
- **WritePpu**(address, table_of_data) - write data to PPU bus
- **WriteChr**(address, table_of_data) - alias for *WriteChr*
- **AddChr**(table_of_data) - add data to dumped CHR
- **AddChrResult**(table_of_data) - alias for *AddChr*
- **ReadAddChr**(address, length) - it's like *AddChr(ReadChr(address, length))* but faster
- **Reset()** - simulate reset (M2 goes low for a second)
- **Error(message)** - generate exception (stop application with a message)

You can find examples in *mappers-lua* folder.

Also you can use **WriteCpu**, **WriteChr** and **Reset** functions on command line.
This simple command:
~~~~
>famicom-dumper.exe dump --port COM14 --mapper 0 --psize 32K --csize 8K --file game.nes --lua "Reset() ; WriteCpu(0x8000, {0x00})"
~~~~
writes $00 to $8000 before dumping.
