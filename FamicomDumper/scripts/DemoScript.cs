class DemoScript // Class name doesn't matter
{
    /* 
     * Main method name must be "Run". You can specify those parameters in any order:
     * 
     *  IFamicomDumperConnection dumper - dumper object used to access dumper
     *  string filename                 - filename specified by --file argument
     *  IMapper mapper                  - mapper object compiled from mapper script specified by --mapper argument
     *  int prgSize                     - PRG size specified by --prg-size argument (parsed)
     *  int chrSize                     - CHR size specified by --chr-size argument (parsed)
     *  string unifName                 - string specified by --unif-name argument
     *  string unifAuthor               - string specified by --unif-author argument
     *  bool battery                    - true if --battery argument is specified
     *  string[] args                   - additional command line arguments
     * 
     * You can specify additional arguments this way: >famicom-dumper script --csfile DemoScript.cs - argument1 argument2 argument3
     * 
     * Always define default value if parameter is optional.
     * The only exception is the "mapper" parameter, the "NROM" mapper will be used by default.
     * Also "args" will be a zero-length array if additional arguments are not specified
     * 
     */
    void Run(IFamicomDumperConnection dumper, string[] args, IMapper mapper, int prgSize = 128 * 1024, int chrSize = -1)
    {
        if (mapper.Number >= 0)
            Console.WriteLine($"Using mapper: #{mapper.Number} ({mapper.Name})");
        else
            Console.WriteLine($"Using mapper: {mapper.Name}");

        if (chrSize < 0)
        {
            // Oh no, CHR size is not specified! Lets use mapper's default
            chrSize = mapper.DefaultChrSize;
        }

        Console.WriteLine($"PRG size: {prgSize}");
        Console.WriteLine($"CHR size: {chrSize}");

        if (args.Any())
            Console.WriteLine("Additional command line arguments: " + string.Join(", ", args));

        // You can use other methods
        Reset(dumper);
    }

    void Reset(IFamicomDumperConnection dumper)
    {
        Console.Write("Reset... ");
        dumper.Reset();
        Console.WriteLine("OK");
    }
}
