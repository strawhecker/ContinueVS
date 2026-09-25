namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Lightweight snapshot of a loaded debugger module (gap92_2 inspection surface).
    /// </summary>
    public sealed class ModuleInfo
    {
        /// <summary>
        /// Module name (e.g. "MyApp.dll").
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Full path of the module on disk.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Base memory address of the loaded module (best effort).
        /// </summary>
        public string? BaseAddress { get; set; }

        /// <summary>
        /// Symbol (PDB) path when resolvable, otherwise null.
        /// </summary>
        public string? SymbolPath { get; set; }
    }
}
