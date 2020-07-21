--
-- VRC6a mapper
-- http://wiki.nesdev.com/w/index.php/VRC6
--  Akumajou Densetsu
--

MapperName = "VRC6a"
MapperNumber = 24
DefaultPrgSize = 256 * 1024
DefaultChrSize = 256 * 1024

function DumpPrg(size)
	local banks = size / 0x4000
	for b = 0, banks-1 do
		print("Reading PRG banks #" .. tostring(b) .. "...")
		WriteCpu(0x8000, {b})
		ReadAddPrg(0x8000, 0x4000)
	end
end

function DumpChr(size)
    WriteCpu(0xB003,{0x00})
	local banks = size / 0x0400
	for b = 0, banks-1, 8 do
		print("Reading CHR banks #" .. tostring(b) .. ", #" .. tostring(b+1) .. ", #" .. tostring(b+2) .. ", #" .. tostring(b+3) .. ", #" .. tostring(b+4) .. ", #" .. tostring(b+5) .. ", #" .. tostring(b+6) .. " and #" .. tostring(b+7) .. "...")
		WriteCpu(0xD000, {b, b+1, b+2, b+3})
		WriteCpu(0xE000, {b+4, b+5, b+6, b+7})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	WriteCpu(0xB003, {0x80})
end
