using System;
using System.Collections.Generic;

using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;

using CKAN.IO;
using CKAN.Avc;

namespace CKAN.NetKAN.Services
{
    internal interface IModuleService
    {
        Tuple<ZipEntry, bool>? FindInternalAvc(ReleaseDto module, ZipFile zipfile, string internalFilePath);
        AvcVersion? GetInternalAvc(ReleaseDto module, string filePath, string? internalFilePath = null);
        JObject? GetInternalCkan(ReleaseDto module, string zipPath);
        bool HasInstallableFiles(ReleaseDto module, string filePath);

        IEnumerable<InstallableFile> GetConfigFiles(ReleaseDto module, ZipFile zip);
        IEnumerable<InstallableFile> GetPlugins(ReleaseDto module, ZipFile zip);
        IEnumerable<InstallableFile> GetCrafts(ReleaseDto module, ZipFile zip);
        IEnumerable<InstallableFile> GetSourceCode(ReleaseDto module, ZipFile zip);

        IEnumerable<string> GetInternalSpaceWarpInfos(ReleaseDto module,
                                                      ZipFile    zip,
                                                      string?    internalFilePath = null);

        IEnumerable<ZipEntry> FileSources(ReleaseDto module, ZipFile zip);
        IEnumerable<string> FileDestinations(ReleaseDto module, string filePath);
    }
}
