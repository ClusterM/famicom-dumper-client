MapperName = "MMC2"
MapperNumber = 9
DefaultPrgSize = 128 * 1024
DefaultChrSize = 128 * 1024

function DumpPrg(size)
	local banks = math.floor(size / 0x2000)
	for b = 0, banks-4 do
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteCpu(0xA000, {b})
		ReadAddPrg(0x8000, 0x2000)
	end

	print("Reading last three PRG banks...")
	ReadAddPrg(0xA000, 0x6000)
end

function DumpChr(size)
	local banks = math.floor(size / 0x1000)
	for b = 0, banks-1, 2 do
		print("Reading CHR banks #" .. tostring(b) .. ", #" .. tostring(b+1) .. "...")
		WriteCpu(0xB000, {b})
		WriteCpu(0xC000, {b})
		WriteCpu(0xD000, {b+1})
		WriteCpu(0xE000, {b+1})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	print("8 KB PRG RAM bank in PlayChoice version only")
end
