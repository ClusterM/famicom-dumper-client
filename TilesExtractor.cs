using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace com.clusterrr.Famicom
{
    public class TilesExtractor
    {
        private readonly byte[] data;
        public Color[] Colors = new Color[4];
        public int TilesCount
        {
            get { return data.Length / 16; }
        }

        public TilesExtractor(byte[] data)
        {
            this.data = data;
            Colors[0] = Color.Black;
            Colors[1] = Color.Blue;
            Colors[2] = Color.Red;
            Colors[3] = Color.White;
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
                    var color = Colors[colorNum];
                    result.SetPixel(x, y, color);
                }
            return result;
        }

        public Image GetAllTiles()
        {
            /*
            const int tilesPerRow = 16;
            var rows = TilesCount / tilesPerRow;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < tilesPerRow; x++)
                {
                    var tile = GetTile(x + y * tilesPerRow);
                    gr.DrawImageUnscaled(tile, x * 8, y * 8);
                }
            gr.Flush();
             */
            if (TilesCount == 0) throw new Exception("There are no CHR data in this ROM");
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
