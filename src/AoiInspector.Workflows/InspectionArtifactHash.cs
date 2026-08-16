using System.Security.Cryptography;

namespace AoiInspector.Workflows;

public static class InspectionArtifactHash
{
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string TryComputeSha256(string path)
    {
        try
        {
            return ComputeSha256(path);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
