function MapperName()
	return "CNROM"
end

function MapperNumber()
	return 3;
end

function DefaultPrgSize()
	return 32 * 1024
end

function DefaultChrSize()
	return 32 * 1024
end

function DumpPrg(size)
	print("Reading PRG...")
	prg = ReadPrg(0x8000, size)
	AddPrg(prg)
end

function DumpChr(size)	
	local banks = size / 0x2000
	for b = 0, banks-1 do
		print("Reading CHR bank #" .. tostring(b) .. "...")
		for i,v in pairs(prg) do
			if v == b then
				WriteCpu(0x8000+i, {b})
				break
			end
		end
		ReadAddChr(0xC000, 0x2000)
	end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
