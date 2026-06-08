using System.Security.Cryptography;
using System.Text;

namespace Starkive;

internal static class PasswordGenerator
{
    internal static string Generate(int length,
        bool useUpper, bool useLower, bool useDigits, bool useSpecial)
    {
        const string upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower   = "abcdefghjkmnpqrstuvwxyz";
        const string digits  = "23456789";
        const string special = "!@#$%^&*-_=+?";

        var pool     = new StringBuilder();
        var required = new List<string>();
        if (useUpper)   { pool.Append(upper);   required.Add(upper); }
        if (useLower)   { pool.Append(lower);   required.Add(lower); }
        if (useDigits)  { pool.Append(digits);  required.Add(digits); }
        if (useSpecial) { pool.Append(special); required.Add(special); }

        string charset = pool.Length > 0 ? pool.ToString() : lower;

        var bytes = new byte[length * 2];
        RandomNumberGenerator.Fill(bytes);

        var sb = new StringBuilder(length);
        int ri = 0;
        foreach (var cat in required)
            sb.Append(cat[bytes[ri++] % cat.Length]);
        for (int i = sb.Length; i < length; i++)
            sb.Append(charset[(bytes[i] + bytes[i + length]) % charset.Length]);

        // Fisher-Yates shuffle with CSPRNG
        var arr          = sb.ToString().ToCharArray();
        var shuffleBytes = new byte[arr.Length];
        RandomNumberGenerator.Fill(shuffleBytes);
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = shuffleBytes[i] % (i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return new string(arr);
    }
}
