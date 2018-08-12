--
-- http://wiki.nesdev.com/w/index.php/INES_Mapper_058
--

MapperName = "Mapper 58"
MapperNumber = 58
DefaultPrgSize = 8*0x4000
DefaultChrSize = 8*0x2000

function DumpPrg(size)
	local banks = size / 0x4000
	for b = 0, banks-1 do		
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteCpu(0x8040 + b, { 0 })
		ReadAddPrg(0x8000, 0x4000)
	end
end

function DumpChr(size)
	local banks = size / 0x2000
	for b = 0, banks-1 do		
		print("Reading CHR bank #" .. tostring(b) .. "... ")
		WriteCpu(0x8000 + b * 8, {0})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
