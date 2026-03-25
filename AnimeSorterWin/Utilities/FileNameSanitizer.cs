using System.IO;

namespace AnimeSorterWin.Utilities;

/// <summary>
/// 文件夹命名清洗：避免非法文件系统字符导致 Move/Copy 失败。
/// </summary>
public static class FileNameSanitizer
{
    public static string SanitizeFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown";

        // Windows 路径非法字符：< > : " / \ | ? * 以及控制字符。
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim();
        var result = new char[chars.Length];
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            result[i] = invalid.Contains(c) ? '_' : c;
        }

        var sanitized = new string(result).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}

