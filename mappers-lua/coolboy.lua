MapperName = "COOLBOY"
MapperUnifName = "COOLBOY"
DefaultPrgSize = 32 * 1024 * 1024
DefaultChrSize = 0

function DumpPrg(size)
	Reset()
	local outbanks = size / 0x80000
	for o = 0, outbanks-1 do
		local outbank = o * 4
		local banks = 0x80000 / 0x2000
		local r0 = 0
		local r1 = 0
		r0 = r0 + (outbank % 8) -- bits 2, 1, 0 => 2, 1, 0
		outbank = outbank - (outbank % 8)
		r1 = r1 + (outbank % 16) * 2 -- bit 3 => 5
		outbank = outbank - (outbank % 16)
		r1 = r1 + (outbank % 64) / 4 -- bits 5, 4 => 3, 2
		outbank = outbank - (outbank % 64) -- bits 7, 6 => 5, 4
		r0 = r0 + outbank / 4
		WriteCpu(0x6000, {r0, r1, 0, 0})

		for b = 0, banks-1, 2 do
			print("Reading PRG banks #" .. tostring(o) .. "/" .. tostring(b) .. " and #"  .. tostring(o) .. "/" ..  tostring(b+1) .. "...")
			WriteCpu(0x8000, {6, b})
			WriteCpu(0x8000, {7, b+1})
			ReadAddPrg(0x8000, 0x4000)
		end
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
