using System.Runtime.InteropServices;

// Security (S4): resolve every [DllImport] in this assembly from System32 only,
// so a malicious DLL placed next to the exe (e.g. a fake atiadlxx.dll) can't be loaded.
// GPU driver DLLs (atiadlxx/atiadlxy), user32 and kernel32 all live in System32.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
