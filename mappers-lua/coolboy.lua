MapperName = "COOLBOY"
MapperUnifName = "COOLBOY"
DefaultPrgSize = 32 * 1024 * 1024
DefaultChrSize = 0

function DumpPrg(size)
	Reset()
	local banks = math.floor(size / 0x04000)
	for b = 0, banks-1 do
                r0 = (math.floor(b / 8) % 8) + (math.floor(b / 512) % 4) * 16 + 64
                r1 = (math.floor(b / 128) % 4) * 4 + (math.floor(b / 64) % 2) * 16 + 128
                r2 = 0
                r3 = 16 + (b % 8) * 2
          	WriteCpu(0x6000, {r0, r1, r2, r3})
		print("Reading PRG bank #" .. tostring(b+1) .. "/" .. tostring(banks) .. "...")
		ReadAddPrg(0x8000, 0x4000)
	end
end

function DumpChr(size)
end

function EnablePrgRam(size)
	Reset()
	WriteCpu(0xA001, {0x00})
	WriteCpu(0x6003, {0x80})
	WriteCpu(0xA001, {0x80})
end
