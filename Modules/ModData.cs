using System;
using System.IO;
using System.Linq;

namespace TownOfHostForE.Modules;

public static class ModData
{
    public const string DataDirectoryName = "TOHFE_DATA";
    private const string LegacyDataDirectoryName = "TOH_DATA";

    public static void Initialize()
    {
        Directory.CreateDirectory(DataDirectoryName);
        TryCopyLegacyData();
    }

    private static void TryCopyLegacyData()
    {
        try
        {
            var legacyDirectory = new DirectoryInfo(LegacyDataDirectoryName);
            if (!legacyDirectory.Exists) return;

            var dataDirectory = new DirectoryInfo(DataDirectoryName);
            if (dataDirectory.EnumerateFileSystemInfos().Any()) return;

            CopyDirectory(legacyDirectory, dataDirectory);
            Logger.Info($"{LegacyDataDirectoryName} から {DataDirectoryName} に既存データをコピーしました。", nameof(ModData));
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, nameof(ModData));
        }
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        Directory.CreateDirectory(destination.FullName);

        foreach (var file in source.EnumerateFiles())
        {
            file.CopyTo(Path.Combine(destination.FullName, file.Name), overwrite: false);
        }

        foreach (var directory in source.EnumerateDirectories())
        {
            CopyDirectory(directory, new DirectoryInfo(Path.Combine(destination.FullName, directory.Name)));
        }
    }
}
