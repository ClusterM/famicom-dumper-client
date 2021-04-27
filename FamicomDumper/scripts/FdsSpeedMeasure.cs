using System;
using System.Threading;

namespace com.clusterrr.FdsSpeedMeasure
{
    class FdsSpeedMeasure
    {
        void Run(IFamicomDumperConnection dumper)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine("  Famicom Disk System speed measure script");
                Console.WriteLine("  (c) Alexey 'Cluster' Avdyukhin / https://clusterrr.com / clusterrr@clusterrr.com");
                Console.WriteLine();

                // Just simple test that RAM adapter is connected
                bool ramAdapterPresent = true;
                dumper.WriteCpu(0x4023, 0x01); // enable disk registers
                dumper.WriteCpu(0x4026, 0x00);
                dumper.WriteCpu(0x4025, /*0b00100110*/ 0x26); // reset
                dumper.WriteCpu(0x0000, 0xFF); // to prevent open bus read
                var ext = dumper.ReadCpu(0x4033, 1)[0];
                if (ext != 0x00) ramAdapterPresent = false;
                dumper.WriteCpu(0x4026, 0xFF);
                dumper.WriteCpu(0x0000, 0x00); // to prevent open bus read
                ext = dumper.ReadCpu(0x4033, 1)[0];
                if ((ext & 0x7F) != 0x7F) ramAdapterPresent = false;
                if (!ramAdapterPresent) throw new IOException("Famicom Disk System RAM adapter IO error, is it connected?");

                dumper.WriteCpu(0x4025, /*0b00100110*/ 0x26); // reset
                dumper.WriteCpu(0x4025, /*0b00100101*/ 0x25); // enable motor without data transfer
                Thread.Sleep(100);
                // Check battery health
                ext = dumper.ReadCpu(0x4033, 1)[0];
                if ((ext & 0x80) == 0) throw new IOException("Battery voltage is low or power supply is not connected");

                Console.WriteLine("Measuring FDS drive speed, make sure that disk card is inserted and wait.");
                DateTime? lastCycleTime = null;
                while (true)
                {
                    // Reset
                    dumper.WriteCpu(0x4025, /*0b00100110*/ 0x26); // reset
                    dumper.WriteCpu(0x4025, /*0b00100101*/ 0x25); // enable motor without data transfer

                    // Wait for ready state
                    var startTime = DateTime.Now; // for timeout
                    while ((dumper.ReadCpu(0x4032, 1)[0] & 2) != 0)
                    {
                        if ((DateTime.Now - startTime).TotalSeconds >= 20) throw new TimeoutException("Timeout");
                    }

                    // Calculate and print cycle duration
                    if (lastCycleTime != null)
                    {
                        Console.WriteLine("Full cycle time: {0} ms", (int)(DateTime.Now - lastCycleTime.Value).TotalMilliseconds);
                    }
                    // Remember cycle start time
                    lastCycleTime = DateTime.Now;
                }
            }
            finally
            {
                // Stop
                dumper.WriteCpu(0x4025, /*0b00100110*/ 0x26);
            }
        }
    }
}
