using System.IO;

namespace AnimeSorterWin.Utilities;

public static class FileCollisionResolver
{
    /// <summary>
    /// 生成不会覆盖现有文件的目标路径；若发生重名会追加时间戳+短 GUID。
    /// </summary>
    public static string GetUniqueDestinationPath(string destinationPath)
    {
        if (!File.Exists(destinationPath))
            return destinationPath;

        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var ext = Path.GetExtension(destinationPath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);

        // 直接用时间戳+GUID 生成新文件名，避免循环碰撞成本。
        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var guid = Guid.NewGuid().ToString("N")[..8];
        var newFileName = $"{fileNameWithoutExt}_{ts}_{guid}{ext}";
        return Path.Combine(directory, newFileName);
    }
}

