# Client (PC-software) for Famicom Dumper/Programmer

This is the client for the Famicom Dumper/Programmer hardware:
- [https://github.com/ClusterM/famicom-dumper](https://github.com/ClusterM/famicom-dumper) (my own project, based on AVR)
- [https://github.com/HardWrMan/SuperDumperFW](https://github.com/HardWrMan/SuperDumperFW) (alternative project by HardWrMan, based on STM32)

You need Windows and .NET Framework 3.5 but it works fine on *nix using Mono.

## Usage

It's a command-line application.

Usage: **famicom-dumper.exe \<command\> [options]**

Available commands:  
- **list-mappers** - list built in mappers  
- **dump** - dump cartridge  
- **dump-tiles** - dump CHR data to PNG file  
- **reset** - simulate reset (M2 goes low for a second)
- **read-prg-ram** - read PRG RAM (battery backed save if exists)  
- **write-prg-ram** - write PRG RAM  
- **write-coolboy-gpio** - write COOLBOY cartridge using GPIO
- **write-coolboy-direct** - write COOLBOY cartridge directly
- **write-coolgirl** - write COOLGIRL cartridge  
- **write-eeprom** - write EEPROM-based cartridge  
- **console** - start interactive Lua console
- **test-prg-ram** - run PRG RAM test
- **test-chr-ram** - run CHR RAM test
- **test-battery** - test battery-backed PRG RAM
- **test-prg-ram-coolgirl** - run PRG RAM test for COOLGIRL cartridge
- **test-chr-ram-coolgirl** - run CHR RAM test for COOLGIRL cartridge
- **test-coolgirl** - run all RAM tests for COOLGIRL cartridge
- **test-bads-coolgirl** - find bad sectors on COOLGIRL cartridge
- **read-crc-coolgirl** - shows CRC checksum for COOLGIRL
- **info-coolboy** - show information about COOLBOY's flash memory
- **info-coolgirl** - show information about COOLGIRL's flash memory

Available options:  
- --**port** <*com*> - serial port of dumper or serial number of FTDI device, default - auto  
- --**mapper** <*mapper*> - number, name or path to LUA script of mapper for dumping, default is 0 (NROM)  
- --**file** <*output.nes*> - output filename (.nes, .png or .sav)  
- --**psize** <*size*> - size of PRG memory to dump, you can use "K" or "M" suffixes  
- --**csize** <*size*> - size of CHR memory to dump, you can use "K" or "M" suffixes  
- --**luafile** "<*lua_code*>" - execute Lua code from file first
- --**lua** "<*lua_code*>" - execute this Lua code first
- --**unifname** <*name*> - internal ROM name for UNIF dumps  
- --**unifauthor** <*name*> - author of dump for UNIF dumps  
- --**reset** - do reset first
- --**badsectors** - comma separated list of bad sectors for COOLBOY/COOLGIRL flashing
- --**sound** - play sounds
- --**check** - verify COOLBOY/COOLGIRL checksum after writing
- --**lock** - write-protect COOLBOY/COOLGIRL sectors after writing

## Examples

Dump NROM-cartridge using dumper on port "COM14" to file "game.nes". PRG and CHR sizes are default.
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
>famicom-dumper.exe dump --mapper mappers-lua\MMC3.lua --reset --psize 128K --csize 128K --file game.nes
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
>famicom-dumper.exe read-prg-ram --port COM14 --mapper mmc1 --file "zelda.sav"
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

Rewrite ultracheap chinese COOLBOY cartridge using GPIO pins on /OE-/WE and play sound when it's done:
~~~~
>famicom-dumper.exe write-coolboy-gpio --port COM14 --file "CoolBoy 400-in-1 (Alt Version, 403 games)(Unl)[U][!].nes" --sound
PRG reader initialization... OK
CHR reader initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/2048 (0%, 00:00:02/00:40:53)...
~~~~
You need to unsolder pins /OE and /WE and connect them to TCK and TDO pins on JTAG connector.

Same for new COOLBOY where /OE and /WE are connected to mapper, soldering not required:
~~~~
>famicom-dumper.exe write-coolboy --port COM14 --file "CoolBoy 400-in-1 (Alt Version, 403 games)(Unl)[U][!].nes" --sound
PRG reader initialization... OK
CHR reader initialization... OK
Reset... OK
Erasing sector... OK
Writing 1/2048 (0%, 00:00:02/00:40:53)...
~~~~

Also you can rewrite [COOLGIRL](https://github.com/ClusterM/coolgirl-famicom-multicard) cartridges:
~~~~
>famicom-dumper.exe write-coolgirl --file multirom.unf --port COM14
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
- **WriteFile**(filename, data) - write table of *data* to file
- **WriteNes**(filename, prg, chr, mapper, vertical) - create .nes file using this *prg* table *chr* table, *mapper* number and mirroring (*vertical* - boolean value)
- **Reset()** - simulate reset (M2 goes low for a second)
- **Error(message)** - generate exception (stop application with a message)

You can find examples in *mappers-lua* folder.

Also you can use those functions on command line.
This simple command:
~~~~
>famicom-dumper.exe dump --port COM14 --mapper 0 --psize 32K --csize 8K --file game.nes --lua "Reset() ; WriteCpu(0x8000, {0x00})"
~~~~
writes $00 to $8000 before dumping.


And finally you can use interactive Lua console:
~~~~
>famicom-dumper.exe console --port COM14
PRG reader initialization... OK
CHR reader initialization... OK
Executing Lua script myfunctions.lua...
Starting interactive Lua console, type "exit" to exit.
> Reset()
Reset... OK
> prg = ReadCpu(0x8000, 0x8000)
Reading 32768 bytes from CPU:$8000
> print(prg[1])
1
> print(prg[2])
2
> print(prg[3])
4
> WriteCpu(0x8000, {1})
CPU write $01 => $8000
> chr = ReadChr(0x0000, 0x2000)
Reading 8192 bytes from PPU:$0000
> WriteFile("prg.bin", prg)
Writing data to "prg.bin"... OK
> WriteNes("game1.nes", prg, chr, 0, false)
Writing data to NES file "game1.nes" (mapper=0, mirroring=horizontal)... OK
> WriteCpu(0x8001, {2})
CPU write $02 => $8001
> chr = ReadChr(0x0000, 0x2000)
Reading 8192 bytes from PPU:$0000
> WriteNes("game2.nes", prg, chr, 0, true)
Writing data to NES file "game2.nes" (mapper=0, mirroring=vertical)... OK
> p = ReadCpu
> prg = p(0x8000, 0x8000)
Reading 32768 bytes from CPU:$8000
> allp = function() return p(0x8000, 0x8000) end
> prg = allp()
Reading 32768 bytes from CPU:$8000
> w = function() WriteFile("prg.bin", prg) end
> w()
Writing data to "prg.bin"... OK
> dumpprg = function() prg = allp() ; w() end
> dumpprg()
Reading 32768 bytes from CPU:$8000
Writing data to "prg.bin"... OK
>
~~~~

You can create and use a Lua script file with your own functions:
~~~~
>famicom-dumper.exe console --port COM14 --luafile myfunctions.lua
PRG reader initialization... OK
CHR reader initialization... OK
Executing Lua script "myfunctions.lua"...
Starting interactive Lua console, type "exit" to exit.
> readprg()
Reading 32768 bytes from CPU:$8000
> readchr()
Reading 8192 bytes from PPU:$0000
> savenes()
Writing data to NES file "game.nes" (mapper=0, mirroring=horizontal)... OK
>
~~~~

## Building
 1. Install packages from NuGet
 2. Build the `famicom-dumper.csproj` project
