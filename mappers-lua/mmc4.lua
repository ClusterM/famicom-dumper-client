MapperName = "MMC4"
MapperNumber = 10
DefaultPrgSize = 512 * 1024
DefaultChrSize = 256 * 1024

function WriteMMC4(address, data)
	WriteCpu(address, {data})
end

function DumpPrg(size)
	local banks = size / 0x4000
	for b = 0, banks-2 do
		print("Reading PRG bank #" .. tostring(b) .. "...")
		WriteMMC4(0xA000, b)
		ReadAddPrg(0x8000, 0x4000)
	end

	print("Reading last PRG bank #" .. tostring(banks-1) .. "...")
	ReadAddPrg(0xC000, 0x4000)
end

function DumpChr(size)
	local banks = size / 0x1000
	for b = 0, banks-1, 2 do
		print("Reading CHR banks #" .. tostring(b) .. ", #" .. tostring(b+1) .. "...")
		WriteMMC4(0xB000, b)
		WriteMMC4(0xC000, b)
		WriteMMC4(0xD000, b+1)
		WriteMMC4(0xE000, b+1)
		ReadAddChr(0x0000, 0x2000)
	end
end

function EnablePrgRam(size)

end
