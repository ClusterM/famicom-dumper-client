MapperName = "MMC3"
MapperNumber = 4
DefaultPrgSize = 512 * 1024
DefaultChrSize = 256 * 1024

function DumpPrg(size)
	local banks = size / 0x2000
	for b = 0, banks-3, 2 do
		print("Reading PRG banks #" .. tostring(b) .. " and #" .. tostring(b+1) .. "...")
		WriteCpu(0x8000, {6, b})
		WriteCpu(0x8000, {7, b+1})
		ReadAddPrg(0x8000, 0x4000)
	end

	print("Reading last PRG banks #" .. tostring(banks-2) .. " and #" .. tostring(banks-1) .. "...")
	ReadAddPrg(0xC000, 0x4000)
end

function DumpChr(size)
	local banks = size / 0x0400
	for b = 0, banks-1, 8 do
		print("Reading CHR banks #" .. tostring(b) .. ", #" .. tostring(b+1) .. ", #" .. tostring(b+2) .. ", #" .. tostring(b+3) .. ", #" .. tostring(b+4) .. ", #" .. tostring(b+5) .. ", #" .. tostring(b+6) .. " and #" .. tostring(b+7) .. "...")
		WriteCpu(0x8000, {0, b})
		WriteCpu(0x8000, {1, b+2})
		WriteCpu(0x8000, {2, b+4})
		WriteCpu(0x8000, {3, b+5})
		WriteCpu(0x8000, {4, b+6})
		WriteCpu(0x8000, {5, b+7})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	WriteCpu(0xA001, {0x80})
end
