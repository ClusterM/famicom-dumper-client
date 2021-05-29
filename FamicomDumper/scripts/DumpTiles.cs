/* Tiles dumper script
 *
 * Copyright notice for this file:
 *  Copyright (C) 2021 Cluster
 *  http://clusterrr.com
 *  clusterrr@clusterrr.com
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
 *
 */

/*
 * Usage: famicom-dumper script --cs-script DumpTiles.cs --mapper <mapper> --file <output_file.png> --chr-size <size>
 */

using System.Drawing;
using System.Drawing.Imaging;

class DumpTiles
{
    void Run(IFamicomDumperConnection dumper, string fileName, IMapper mapper, int chrSize)
    {
        if (mapper.Number >= 0)
            Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
        else
            Console.WriteLine($"Using mapper: {mapper.Name}");
        Console.WriteLine("Dumping...");
        List<byte> chr = new();
        chrSize = chrSize >= 0 ? chrSize : mapper.DefaultChrSize;
        Console.WriteLine($"CHR memory size: {chrSize / 1024}K");
        mapper.DumpChr(dumper, chr, chrSize);
        var tiles = new TilesExtractor(chr.ToArray());
        var allTiles = tiles.GetAllTiles();
        Console.WriteLine($"Saving to {fileName}...");
        allTiles.Save(fileName, ImageFormat.Png);
    }

    public class TilesExtractor
    {
        private readonly byte[] data;
        private Color[] colors = new Color[4];
        public int TilesCount
        {
            get { return data.Length / 16; }
        }

        public TilesExtractor(byte[] data)
        {
            this.data = data;
            colors[0] = Color.Black;
            colors[1] = Color.Blue;
            colors[2] = Color.Red;
            colors[3] = Color.White;
        }

        public Image GetTile(int id)
        {
            var tileData = new byte[16];
            Array.Copy(data, id * 16, tileData, 0, 16);
            var result = new Bitmap(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int colorNum = (tileData[y] >> (7 - x)) & 1;
                    colorNum += ((tileData[y + 8] >> (7 - x)) & 1) * 2;
                    var color = colors[colorNum];
                    result.SetPixel(x, y, color);
                }
            return result;
        }

        public Image GetAllTiles()
        {
            if (TilesCount == 0) throw new ArgumentOutOfRangeException("TilesCount", "There are no CHR data in this ROM");
            int pages = TilesCount / 256;
            var result = new Bitmap(256, pages * 128 / 2);
            var gr = Graphics.FromImage(result);
            for (int p = 0; p < pages; p++)
            {
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        var tile = GetTile(p * 256 + x + y * 16);
                        gr.DrawImageUnscaled(tile, x * 8 + (p % 2) * 128, y * 8 + p / 2 * 128);
                    }
            }
            gr.Flush();
            return result;
        }
    }
}
