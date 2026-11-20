using System.Buffers.Binary;

namespace AIGuard.Application.Biometrics;

/// <summary>Yüz vektörünün şifrelenmeden önceki bayt biçimi.</summary>
public static class FaceVector
{
    /// <summary>ArcFace çıktısının boyutu (D2).</summary>
    public const int Dimension = 512;

    /// <summary>
    /// Sözleşmeye göre vektör L2 ile normalize (birim uzunlukta). Normalize olmayan vektör saklanırsa
    /// sonraki kosinüs benzerlikleri sessizce bozulur; bu yüzden kayıt anında reddediliyor.
    /// </summary>
    private const double NormTolerance = 0.01;

    public static bool IsValid(float[]? vector)
    {
        if (vector is not { Length: Dimension } || !vector.All(float.IsFinite))
        {
            return false;
        }

        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        return Math.Abs(norm - 1) <= NormTolerance;
    }

    // Sıra makineden bağımsız olsun diye açıkça little-endian.
    public static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), vector[i]);
        }

        return bytes;
    }

    public static float[] FromBytes(byte[] bytes)
    {
        if (bytes.Length != Dimension * sizeof(float))
        {
            throw new ArgumentException("Yüz vektörü beklenen boyutta değil.", nameof(bytes));
        }

        var vector = new float[Dimension];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }

        return vector;
    }
}
