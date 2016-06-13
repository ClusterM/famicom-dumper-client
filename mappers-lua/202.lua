MapperName = "Mapper 202"
MapperNumber = 202
DefaultPrgSize = 128*1024
DefaultChrSize = 128*1024

function DumpPrg(size)
	Reset();
	local banks = size / 0x4000
	for b = 0, banks-1 do		
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteCpu(0x8000 + b * 2, {0})
		ReadAddPrg(0xC000, 0x4000)
	end
end

function DumpChr(size)
	Reset();
	local banks = size / 0x2000
	for b = 0, banks-1 do		
		print("Reading CHR bank #" .. tostring(b) .. "... ")
		WriteCpu(0x8000 + b * 2, {0})
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
