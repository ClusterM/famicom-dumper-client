using com.clusterrr.Famicom.Containers;
using com.clusterrr.Famicom.DumperConnection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace com.clusterrr.Famicom.Dumper
{
    static class Scripting
    {
        public static string[] MappersSearchDirectories = {
            Path.Combine(Directory.GetCurrentDirectory(), "mappers"),
            "/usr/share/famicom-dumper/mappers"
        };
        public static readonly string[] ScriptsSearchDirectories = {
            Path.Combine(Directory.GetCurrentDirectory(), "scripts"),
            "/usr/share/famicom-dumper/scripts"
        };

        public const string SCRIPTS_CACHE_DIRECTORY = ".dumpercache";
        public const string SCRIPT_START_METHOD = "Run";

        static Assembly Compile(string path)
        {
            int linesOffset = 0;
            var source = File.ReadAllText(path);
            var cacheDirectory = Path.Combine(Path.GetDirectoryName(path), SCRIPTS_CACHE_DIRECTORY);
            var cacheFile = Path.Combine(cacheDirectory, Path.GetFileNameWithoutExtension(path)) + ".dll";

            // Try to load cached assembly
            ;
            if (File.Exists(cacheFile))
            {
                var cacheCompileTime = new FileInfo(cacheFile).LastWriteTime;
                if ((cacheCompileTime >= new FileInfo(path).LastWriteTime) // recompile if script was changed
                    && (cacheCompileTime >= Program.BUILD_TIME.ToLocalTime())) // recompile if our app is newer
                {
                    try
                    {
                        var rawAssembly = File.ReadAllBytes(cacheFile);
                        return Assembly.Load(rawAssembly);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Can't load cached compiled script file: {ex.Message}");
                    }
                }
            }

            // And usings
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            var usings = root.Usings.Select(e => e.Name.ToString());
            var usingsToAdd = new string[]
            {
                "System",
                "System.IO",
                "System.Collections.Generic",
                "System.Linq",
                "com.clusterrr.Famicom",
                "com.clusterrr.Famicom.Dumper",
                "com.clusterrr.Famicom.DumperConnection",
                "com.clusterrr.Famicom.Containers"
            };
            foreach (var @using in usingsToAdd)
            {
                if (!usings.Contains(@using))
                {
                    source = $"using {@using};\r\n" + source;
                    linesOffset++; // for correct line numbers in errors
                }
            }
            tree = CSharpSyntaxTree.ParseText(source);

            // Loading assemblies
            var domainAssemblys = AppDomain.CurrentDomain.GetAssemblies();
            var metadataReferenceList = new List<MetadataReference>();
            foreach (var assembl in domainAssemblys)
            {
                unsafe
                {
                    assembl.TryGetRawMetadata(out byte* blob, out int length);
                    var moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                    var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                    var metadataReference = assemblyMetadata.GetReference();
                    metadataReferenceList.Add(metadataReference);
                }
            }
            unsafe
            {
                // Add extra refs
                // FamicomDumperConnection.dll
                typeof(IFamicomDumperConnectionExt).Assembly.TryGetRawMetadata(out byte* blob, out int length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // NesContainers.dll
                typeof(NesFile).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // for image processing
                typeof(System.Drawing.Bitmap).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                typeof(System.Drawing.Imaging.ImageFormat).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // for JSON
                typeof(System.Text.Json.JsonSerializer).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // for Regex
                typeof(System.Text.RegularExpressions.Regex).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                // wtf is it?
                typeof(System.Linq.Expressions.Expression).Assembly.TryGetRawMetadata(out blob, out length);
                metadataReferenceList.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
            }

            // Compile
            var cs = CSharpCompilation.Create("Script", new[] { tree }, metadataReferenceList,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            using var memoryStream = new MemoryStream();
            EmitResult result = cs.Emit(memoryStream);
            foreach (Diagnostic d in result.Diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden
#if !DEBUG
                && d.Severity != DiagnosticSeverity.Warning
#endif
                ))
            {
                Console.WriteLine($"{Path.GetFileName(path)} ({d.Location.GetLineSpan().StartLinePosition.Line - linesOffset + 1}, {d.Location.GetLineSpan().StartLinePosition.Character + 1}): {d.Severity.ToString().ToLower()} {d.Descriptor.Id}: {d.GetMessage()}");
            }
            if (result.Success)
            {
                var rawAssembly = memoryStream.ToArray();
                Assembly assembly = Assembly.Load(rawAssembly);
                // Save compiled assembly to cache (at least try)
                try
                {
                    if (!Directory.Exists(cacheDirectory))
                        Directory.CreateDirectory(cacheDirectory);
                    File.WriteAllBytes(cacheFile, rawAssembly);
                }
                catch { }
                return assembly;
            }
            else throw new InvalidProgramException();
        }

        static IMapper CompileMapper(string path)
        {
            Assembly assembly = Compile(path);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();
            var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
            if (constructor == null)
                throw new InvalidProgramException("There is no valid default constructor");
            var mapper = constructor.Invoke(Array.Empty<object>());
            if (!(mapper is IMapper))
                throw new InvalidProgramException("Class doesn't implement IMapper interface");
            return mapper as IMapper;
        }

        static Dictionary<string, IMapper> CompileAllMappers()
        {
            var result = new Dictionary<string, IMapper>();
            var mappersSearchDirectories = MappersSearchDirectories.Distinct().Where(d => Directory.Exists(d));
            if (!mappersSearchDirectories.Any())
            {
                Console.WriteLine("None of the listed mappers directories were found:");
                foreach (var d in MappersSearchDirectories)
                    Console.WriteLine($" {d}");
            }
            foreach (var mappersDirectory in mappersSearchDirectories)
            {
                Console.WriteLine($"Compiling mappers in {mappersDirectory}...");
                foreach (var f in Directory.GetFiles(mappersDirectory, "*.cs", SearchOption.AllDirectories))
                {
                    result[f] = CompileMapper(f);
                }
            }
            return result;
        }

        public static void ListMappers()
        {
            var mappers = CompileAllMappers();
            Console.WriteLine("Supported mappers:");
            Console.WriteLine(" {0,-30}{1,-24}{2,-9}{3,-24}", "File", "Name", "Number", "UNIF name");
            Console.WriteLine("----------------------------- ----------------------- -------- -----------------------");
            foreach (var mapperFile in mappers
                .Where(m => m.Value.Number >= 0)
                .OrderBy(m => m.Value.Number)
                .Union(mappers.Where(m => m.Value.Number < 0)
                .OrderBy(m => m.Value.Name)))
            {
                Console.WriteLine(" {0,-30}{1,-24}{2,-9}{3,-24}",
                    Path.GetFileName(mapperFile.Key),
                    mapperFile.Value.Name,
                    mapperFile.Value.Number >= 0 ? mapperFile.Value.Number.ToString() : "None",
                    mapperFile.Value.UnifName ?? "None");
            }
        }

        public static IMapper GetMapper(string mapperName)
        {
            if (File.Exists(mapperName)) // CS script?
            {
                Console.WriteLine($"Compiling {mapperName}...");
                return CompileMapper(mapperName);
            }

            if (string.IsNullOrEmpty(mapperName))
                mapperName = "0";
            var mapperList = CompileAllMappers()
                .Where(m => m.Value.Name.ToLower() == mapperName.ToLower()
                || (m.Value.Number >= 0 && m.Value.Number.ToString() == mapperName));
            if (!mapperList.Any()) throw new KeyNotFoundException("Can't find mapper");
            var mapper = mapperList.First();
            Console.WriteLine($"Using {Path.GetFileName(mapper.Key)} as mapper file");
            return mapper.Value;
        }

        public static void CompileAndExecute(string scriptPath, IFamicomDumperConnectionExt dumper, string filename, string mapperName, int prgSize, int chrSize, string unifName, string unifAuthor, bool battery, string[] args)
        {
            if (!File.Exists(scriptPath))
            {
                var scriptsPathes = ScriptsSearchDirectories.Select(d => Path.Combine(d, scriptPath)).Where(f => File.Exists(f));
                if (!scriptsPathes.Any())
                {
                    Console.WriteLine($"{Path.Combine(Directory.GetCurrentDirectory(), scriptPath)} not found");
                    foreach (var d in ScriptsSearchDirectories)
                        Console.WriteLine($"{Path.Combine(d, scriptPath)} not found");
                    throw new FileNotFoundException($"{scriptPath} not found");
                }
                scriptPath = scriptsPathes.First();
            }
            Console.WriteLine($"Compiling {scriptPath}...");
            Assembly assembly = Compile(scriptPath);
            var programs = assembly.GetTypes();
            if (!programs.Any())
                throw new InvalidProgramException("There is no assemblies");
            Type program = programs.First();

            try
            {
                object obj;
                MethodInfo method;

                // Let's check if static method exists
                var staticMethods = program.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == SCRIPT_START_METHOD);
                if (staticMethods.Any())
                {
                    obj = program;
                    method = staticMethods.First();
                }
                else
                {
                    // Let's try instance method, need to call constructor first
                    var constructor = program.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
                    if (constructor == null)
                        throw new InvalidProgramException($"There is no static {SCRIPT_START_METHOD} method and no valid default constructor");
                    obj = constructor.Invoke(Array.Empty<object>());
                    // Is it instance method with string[] parameter?
                    var instanceMethods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == SCRIPT_START_METHOD);
                    if (!instanceMethods.Any())
                    {
                        // Seems like there are no valid methods at all
                        throw new InvalidProgramException($"There is no {SCRIPT_START_METHOD} method");
                    }
                    method = instanceMethods.First();
                }

                ParameterInfo[] parameterInfos = method.GetParameters();
                List<object> parameters = new();
                bool filenameParamExists = false;
                bool mapperParamExists = false;
                bool prgSizeParamExists = false;
                bool chrSizeParamExists = false;
                bool unifNameParamExists = false;
                bool unifAuthorParamExists = false;
                bool batteryParamExists = false;
                bool argsParamExists = false;
                foreach (var parameterInfo in parameterInfos)
                {
                    var signature = $"{parameterInfo.ParameterType.Name} {parameterInfo.Name}";
                    switch (parameterInfo.Name.ToLower())
                    {
                        case "dumper":
                            parameters.Add(dumper);
                            break;
                        case "filename":
                            filenameParamExists = true;
                            if (string.IsNullOrEmpty(filename) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --file is not specified");
                            if (string.IsNullOrEmpty(filename) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(filename);
                            break;
                        case "mapper":
                            mapperParamExists = true;
                            parameters.Add(GetMapper(mapperName));
                            break;
                        case "prgsize":
                            prgSizeParamExists = true;
                            if ((prgSize < 0) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --prg-size is not specified");
                            if ((prgSize < 0) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(prgSize);
                            break;
                        case "chrsize":
                            chrSizeParamExists = true;
                            if ((chrSize < 0) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --chr-size is not specified");
                            if ((chrSize < 0) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(chrSize);
                            break;
                        case "unifname":
                            unifNameParamExists = true;
                            if (string.IsNullOrEmpty(unifName) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --unif-name is not specified");
                            if (string.IsNullOrEmpty(unifName) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(unifName);
                            break;
                        case "unifauthor":
                            unifAuthorParamExists = true;
                            if (string.IsNullOrEmpty(unifAuthor) && !parameterInfo.HasDefaultValue)
                                throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --unif-author is not specified");
                            if (string.IsNullOrEmpty(unifAuthor) && parameterInfo.HasDefaultValue)
                                parameters.Add(parameterInfo.DefaultValue);
                            else
                                parameters.Add(unifAuthor);
                            break;
                        case "battery":
                            batteryParamExists = true;
                            parameters.Add(battery);
                            break;
                        case "args":
                            argsParamExists = true;
                            parameters.Add(args);
                            break;
                        default:
                            switch (parameterInfo.ParameterType.Name)
                            {
                                // For backward compatibility
                                case nameof(IFamicomDumperConnection):
                                    parameters.Add(dumper);
                                    break;
                                case "String[]":
                                    argsParamExists = true;
                                    parameters.Add(args);
                                    break;
                                case nameof(IMapper):
                                    mapperParamExists = true;
                                    if (string.IsNullOrEmpty(mapperName) && !parameterInfo.HasDefaultValue)
                                        throw new ArgumentNullException(parameterInfo.Name, $"{program.Name}.{SCRIPT_START_METHOD} declared with \"{signature}\" parameter but --mapper is not specified");
                                    if (string.IsNullOrEmpty(mapperName) && parameterInfo.HasDefaultValue)
                                        parameters.Add(parameterInfo.DefaultValue);
                                    else
                                        parameters.Add(GetMapper(mapperName));
                                    break;
                                default:
                                    throw new ArgumentException($"Unknown parameter: {signature}");
                            }
                            break;
                    }
                }
                if (!filenameParamExists && !string.IsNullOrEmpty(filename))
                    Console.WriteLine($"WARNING: --file argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string filename\" parameter");
                if (!mapperParamExists && !string.IsNullOrEmpty(mapperName))
                    Console.WriteLine($"WARNING: --mapper argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"IMapper mapper\" parameter");
                if (!prgSizeParamExists && prgSize >= 0)
                    Console.WriteLine($"WARNING: --prg-size argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"int prgSize\" parameter");
                if (!chrSizeParamExists && chrSize >= 0)
                    Console.WriteLine($"WARNING: --chr-size argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"int chrSize\" parameter");
                if (!unifNameParamExists && !string.IsNullOrEmpty(unifName))
                    Console.WriteLine($"WARNING: --unif-name argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string unifName\" parameter");
                if (!unifAuthorParamExists && !string.IsNullOrEmpty(unifAuthor))
                    Console.WriteLine($"WARNING: --unif-author argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string unifAuthor\" parameter");
                if (!batteryParamExists && battery)
                    Console.WriteLine($"WARNING: --battery argument is specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"bool battery\" parameter");
                if (!argsParamExists && args.Any())
                    Console.WriteLine($"WARNING: command line arguments are specified but {program.Name}.{SCRIPT_START_METHOD} declared without \"string[] args\" parameter");

                // Start it!
                Console.WriteLine($"Running {program.Name}.{SCRIPT_START_METHOD}()...");
                method.Invoke(obj, parameters.ToArray());
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    throw ex.InnerException;
                else
                    throw;
            }
        }
    }
}
