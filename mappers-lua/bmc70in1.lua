MapperName = "BMC-70in1"
MapperUnifName = MapperName
MapperNumber = -1
DefaultPrgSize = 128 * 1024
DefaultChrSize = 64 * 1024

function DumpPrg(size)
	local bank_size = 0x4000
	local banks = math.floor(size / bank_size)
	for i = 0, banks - 1 do
		print("Reading PRG bank #" .. tostring(i))
		WriteCpu(0xF000 + i, { 0 })
		ReadAddPrg(0x8000, bank_size)
	end
end

function DumpChr(size)
	local bank_size = 0x2000
	local banks = math.floor(size / bank_size)
	for i = 0, banks - 1 do
		print("Reading CHR bank #" .. tostring(i))
		WriteCpu(0xA000 + i, { 0 })
		ReadAddChr(0x0000, bank_size)
	end
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper") 
end
