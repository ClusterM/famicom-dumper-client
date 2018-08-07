MapperName = "MMC1"
MapperNumber = 1
DefaultPrgSize = 256 * 1024
DefaultChrSize = 128 * 1024

function WriteMMC1(address, data)
	WriteCpu(address, {data})
	WriteCpu(address, {math.floor(data/2)})
	WriteCpu(address, {math.floor(data/4)})
	WriteCpu(address, {math.floor(data/8)})
	WriteCpu(address, {math.floor(data/16)})
end

function DumpPrg(size)
	WriteCpu(0x8000, {0x80})
	WriteMMC1(0x8000, 0x0C)
	local banks = math.floor(size / 0x4000)
	for b = 0, banks-2 do
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteMMC1(0xE000, b)
		ReadAddPrg(0x8000, 0x4000)
	end

	print("Reading last PRG bank #" .. tostring(banks-1) .. "...")
	ReadAddPrg(0xC000, 0x4000)
end

function DumpChr(size)
	WritePrg(0x8000, {0x80})
	WriteMMC1(0x8000, 0x0C)
	local banks = math.floor(size / 0x1000)
	for b = 0, banks-1, 2 do
		print("Reading CHR banks #" .. tostring(b) .. " and #" .. tostring(b+1) .. "...")
		WriteMMC1(0xA000, b)
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	WriteCpu(0x8000, {0x80})
	WriteMMC1(0xE000, 0x00)
end
