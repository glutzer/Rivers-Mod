namespace Rivers;

/// <summary>
/// Common noise methods for all noises.
/// </summary>
public class Noise
{
    public FastNoise noise;

    public Noise(int seed, float frequency, int octaves, float gain = 0.5f, float lacunarity = 2)
    {
        noise = new FastNoise(seed);
        noise.SetNoiseType(FastNoise.NoiseType.OpenSimplex2);
        noise.SetFractalType(FastNoise.FractalType.FBm);

        noise.SetFrequency(frequency);
        noise.SetFractalOctaves(octaves);
        noise.SetFractalGain(gain);
        noise.SetFractalLacunarity(lacunarity);

        noise.SetFractalWeightedStrength(-0.20f); // Achieves values closer to 0.

        noise.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.Euclidean); // Euclidean default.
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance); // F1 default.
    }

    public Noise Jitter(float value)
    {
        noise.SetCellularJitter(value);
        return this;
    }

    public Noise Ridged()
    {
        noise.SetFractalType(FastNoise.FractalType.Ridged);
        return this;
    }

    public Noise OpenSimplex2S()
    {
        noise.SetNoiseType(FastNoise.NoiseType.OpenSimplex2S);
        return this;
    }

    public Noise Cellular()
    {
        noise.SetNoiseType(FastNoise.NoiseType.Cellular);
        return this;
    }

    public Noise Perlin()
    {
        noise.SetNoiseType(FastNoise.NoiseType.Perlin);
        return this;
    }

    public Noise EuclideanSq()
    {
        noise.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.EuclideanSq);
        return this;
    }

    public Noise Dist2()
    {
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance2);
        return this;
    }

    public Noise Dist2Add()
    {
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance2Add);
        return this;
    }

    public Noise Dist2Sub()
    {
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance2Sub);
        return this;
    }

    public Noise Dist2Mul()
    {
        noise.SetCellularReturnType(FastNoise.CellularReturnType.Distance2Mul);
        return this;
    }

    public Noise CellValue()
    {
        noise.SetCellularReturnType(FastNoise.CellularReturnType.CellValue);
        return this;
    }

    /// <summary>
    /// Raw noise.
    /// </summary>
    public float GetNoise(int x, int y)
    {
        return noise.GetNoise(x, y);
    }

    public float GetNoise(float x, float y)
    {
        return noise.GetNoise(x, y);
    }

    public float GetNoise(double x, double y)
    {
        return noise.GetNoise(x, y);
    }

    /// <summary>
    /// Raw 3D noise.
    /// </summary>
    public float GetNoise(int x, int y, int z)
    {
        return noise.GetNoise(x, y, z);
    }

    public float GetNoise(float x, float y, float z)
    {
        return noise.GetNoise(x, y, z);
    }

    public float GetNoise(double x, double y, double z)
    {
        return noise.GetNoise(x, y, z);
    }

    /// <summary>
    /// 2D noise normalized from -1 to 1.
    /// </summary>
    public float GetNormalNoise(int x, int y)
    {
        return noise.GetNormalNoise(x, y);
    }

    public float GetNormalNoise(float x, float y)
    {
        return noise.GetNormalNoise(x, y);
    }

    public float GetNormalNoise(double x, double y)
    {
        return noise.GetNormalNoise(x, y);
    }

    /// <summary>
    /// 3D noise normalized from -1 to 1.
    /// </summary>
    public float GetNormalNoise(int x, int y, int z)
    {
        return noise.GetNormalNoise(x, y, z);
    }

    public float GetNormalNoise(float x, float y, float z)
    {
        return noise.GetNormalNoise(x, y, z);
    }

    public float GetNormalNoise(double x, double y, double z)
    {
        return noise.GetNormalNoise(x, y, z);
    }

    /// <summary>
    /// 2D noise normalized from 0 to 1.
    /// </summary>
    public float GetPosNoise(int x, int y)
    {
        return noise.GetPosNormalNoise(x, y);
    }

    public float GetPosNoise(float x, float y)
    {
        return noise.GetPosNormalNoise(x, y);
    }

    public float GetPosNoise(double x, double y)
    {
        return noise.GetPosNormalNoise(x, y);
    }

    /// <summary>
    /// 3D noise normalized from 0 to 1.
    /// </summary>
    public float GetPosNoise(int x, int y, int z)
    {
        return noise.GetPosNormalNoise(x, y, z);
    }

    public float GetPosNoise(float x, float y, float z)
    {
        return noise.GetPosNormalNoise(x, y, z);
    }

    public float GetPosNoise(double x, double y, double z)
    {
        return noise.GetPosNormalNoise(x, y, z);
    }
}