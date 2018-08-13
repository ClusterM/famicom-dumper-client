--
-- data latch 74161
--
MapperName = "Mapper 203"
MapperNumber = 203
DefaultPrgSize = 4*0x4000
DefaultChrSize = 4*0x2000

function DumpPrg(size)
	local banks = size / 0x4000
	for b = 0, banks-1 do		
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteCpu(0x8000, {b * 4})
		ReadAddPrg(0xC000, 0x4000)
	end
end

function DumpChr(size)
	local banks = size / 0x2000
	for b = 0, banks-1 do		
		print("Reading CHR bank #" .. tostring(b) .. "... ")
		WriteCpu(0x8000, {b})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
