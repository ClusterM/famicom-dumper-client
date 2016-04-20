MapperName = "NROM"
MapperNumber = 0
DefaultPrgSize = 32 * 1024
DefaultChrSize = 8 * 1024

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
