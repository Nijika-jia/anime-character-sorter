using System.Security.Cryptography;
using System.IO;

namespace AnimeSorterWin.Utilities.Hashing;

/// <summary>
/// 计算文件 MD5：用于缓存命中与避免重复识别请求。
/// </summary>
public static class Md5Hasher
{
    public static async Task<string> ComputeMd5HexAsync(string filePath, CancellationToken ct)
    {
        // 用流式读取避免一次性占用大量内存。
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        var buffer = new byte[1024 * 256];

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
            if (read <= 0)
                break;

            incremental.AppendData(buffer, 0, read);
        }

        var hash = incremental.GetHashAndReset();
        return Convert.ToHexString(hash);
    }
}

