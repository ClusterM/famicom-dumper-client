function MapperName()
	return "NROM"
end

function MapperNumber()
	return 0;
end

function DefaultPrgSize()
	return 32 * 1024
end

function DefaultChrSize()
	return 8 * 1024
end

function DumpPrg(size)
	print("Reading PRG...")
	ReadAddPrg(0x8000, size)
end

function DumpChr(size)	
	print("Reading CHR...")
	ReadAddChr(0x0000, size)
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
