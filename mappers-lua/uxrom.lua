function MapperName()
	return "UxROM"
end

function MapperNumber()
	return 2;
end

function DefaultPrgSize()
	return 256 * 1024
end

function DefaultChrSize()
	return 0
end

function DumpPrg(size)
	local banks = size / 0x4000
	print("Reading last PRG bank...")
	local last = ReadCpu(0xC000, 0x4000)
	for b = 0, banks-2 do
		print("Reading PRG bank #" .. tostring(b) .. "...")
		for i,v in pairs(last) do
			if v == b then
				WriteCpu(0xC000+i, {b})
				break
			end
		end
		ReadAddPrg(0x8000, 0x4000)
	end
	AddPrg(last)
end

function DumpChr(size)	
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
