--
--
-- http://wiki.nesdev.com/w/index.php/INES_Mapper_057
--

MapperName = "Mapper 57"
MapperNumber = 57
DefaultPrgSize = 8*0x4000
DefaultChrSize = 8*0x2000

function DumpPrg(size)
	Reset();
	local banks = size / 0x4000
	for b = 0, banks-1 do		
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteCpu(0x8800, { b * 32 })
		ReadAddPrg(0x8000, 0x4000)
	end
end

function DumpChr(size)
	Reset();
    local banks = size / 0x2000
    for b = 0, banks-1 do		
        print("Reading CHR bank #" .. tostring(b) .. "... ")
        -- TODO: Add H-byte
        WriteCpu(0x8000, {b})
        WriteCpu(0x8800, {b})
        ReadAddChr(0x0000, 0x2000)
    end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
