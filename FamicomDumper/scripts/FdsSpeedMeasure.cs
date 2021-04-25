using System;

namespace com.clusterrr.FdsSpeedMeasure
{
    class FdsSpeedMeasure
    {
        void Run(IFamicomDumperConnection dumper)
        {
            try
            {
                DateTime? lastCycleTime = null;
                dumper.WriteCpu(0x4025, 6); // reset
                dumper.WriteCpu(0x4025, 5); // enable motor without data transfer
                // wait for non-ready state
                while ((dumper.ReadCpu(0x4032, 1)[0] & 2) == 0) ;
                while (true)
                {
                  // wait for ready state
                  while ((dumper.ReadCpu(0x4032, 1)[0] & 2) != 0) ;
                  // calculate and print cycle duration
                  if (lastCycleTime != null)
                  {
                    Console.WriteLine("Full cycle time: {0} ms", (int)(DateTime.Now - lastCycleTime.Value).TotalMilliseconds);
                  }
                  // remember cycle start time
                  lastCycleTime = DateTime.Now;
                  // wait for non-ready state
                  while ((dumper.ReadCpu(0x4032, 1)[0] & 2) == 0) ;
                }
            }
            finally
            {
                // stop
                dumper.WriteCpu(0x4025, 3);
            }
        }
    }
}
