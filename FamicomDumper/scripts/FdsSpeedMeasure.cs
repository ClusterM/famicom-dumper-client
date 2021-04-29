/* Famicom Disk System speed measure script
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

using System.Threading;
using System.Threading.Tasks;

class FdsSpeedMeasure
{
    void Run(IFamicomDumperConnection dumper)
    {
        try
        {
            // Just simple test that RAM adapter is connected
            bool ramAdapterPresent = true;
            dumper.WriteCpu(0x4023, 0x01); // enable disk registers
            dumper.WriteCpu(0x4026, 0x00);
            dumper.WriteCpu(0x4025, /*0b00100110*/ 0x26); // reset
            dumper.WriteCpu(0x0000, 0xFF); // to prevent open bus read
            var ext = dumper.ReadCpu(0x4033);
            if (ext != 0x00) ramAdapterPresent = false;
            dumper.WriteCpu(0x4026, 0xFF);
            dumper.WriteCpu(0x0000, 0x00); // to prevent open bus read
            ext = dumper.ReadCpu(0x4033);
            if ((ext & 0x7F) != 0x7F) ramAdapterPresent = false;
            if (!ramAdapterPresent) throw new IOException("Famicom Disk System RAM adapter IO error, is it connected?");

            dumper.WriteCpu(0x4025, 0b00100110); // reset
            dumper.WriteCpu(0x4025, 0b00100101); // enable motor without data transfer
            Thread.Sleep(100);
            // Check battery health
            ext = dumper.ReadCpu(0x4033);
            if ((ext & 0x80) == 0) throw new IOException("Battery voltage is low or power supply is not connected");

            Console.WriteLine("Measuring FDS drive speed, make sure that disk card is inserted and wait. Press ENTER to stop.");
            var cancellationTokenSource = new CancellationTokenSource();
            var task = SpeedMeasureLoop(dumper, cancellationTokenSource.Token);
            Console.ReadLine();
            cancellationTokenSource.Cancel();
            task.GetAwaiter().GetResult();
        }
        finally
        {
            // Stop
            dumper.WriteCpu(0x4025, 0b00100110);
        }
    }

    async Task SpeedMeasureLoop(IFamicomDumperConnection dumper, CancellationToken cancellationToken = default)
    {
        DateTime? lastCycleTime = null;
        while (true)
        {
            // Reset
            dumper.WriteCpu(0x4025, 0b00100110); // reset
            dumper.WriteCpu(0x4025, 0b00100101); // enable motor without data transfer

            // Wait for ready state
            while ((dumper.ReadCpu(0x4032) & 2) != 0)
            {
                if (cancellationToken.IsCancellationRequested) return;
                await Task.Delay(1);
            }

            // Calculate and print cycle duration
            if (lastCycleTime != null)
            {
                Console.WriteLine($"Full cycle time: {(int)(DateTime.Now - lastCycleTime.Value).TotalMilliseconds} ms");
            }
            // Remember cycle start time
            lastCycleTime = DateTime.Now;
        }
    }
}
