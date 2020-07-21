--
-- http://wiki.nesdev.com/w/index.php/AxROM
--

MapperName = "AxROM"
MapperNumber = 7
DefaultPrgSize = 256 * 1024
DefaultChrSize = 0

function DumpPrg(size)
	local banks = size / 0x8000
	print("Reading random PRG bank...")
	local chunk
	for b = 0, banks-1 do
        for h = 0x8000, 0xffff, 0x100 do
            chunk = ReadPrg(h, 0x100)
            for i,v in pairs(chunk) do
                if (v % 8) == b then -- only first 3 bits
                    WriteCpu(h + i - 1, {v})
                    chunk = nil
                    break
                end
            end
            if chunk == nil then break end
        end
        print("Reading PRG bank #" .. tostring(b) .. "...")
		ReadAddPrg(0x8000, 0x8000)
	end
end

function DumpChr(size)	
end

function EnablePrgRam(size)
	print("Warning: SRAM is not supported by this mapper")
end
