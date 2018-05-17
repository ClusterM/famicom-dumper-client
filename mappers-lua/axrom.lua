MapperName = "AxROM"
MapperNumber = 7
DefaultPrgSize = 256 * 1024
DefaultChrSize = 0

function DumpPrg(size)
	local banks = size / 0x8000
	print("Reading random PRG bank...")
	local last = ReadCpu(0xC000, 0x4000)
	for b = 0, banks-1 do
		print("Reading PRG bank #" .. tostring(b) .. "...")
		for i,v in pairs(last) do
			if v == b then
				WriteCpu(0xC000+i, {b})
				break
			end
		end
		ReadAddPrg(0x8000, 0x4000)
		local last = ReadPrg(0xC000, 0x4000)
		AddPrg(last)
	end
end

function DumpChr(size)	
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
