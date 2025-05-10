namespace Rivers;

public class RiverConfig
{
    public static RiverConfig Loaded { get; set; } = new RiverConfig();

    public bool disableFlow = false;

    public int minForkAngle = 10; // When a river forks, each segment will go at this angle minimum.
    public int forkVariation = 35; // How much to add to the minimum fork angle.
    public int normalAngle = 20; // When adding single node change from 0 to this angle in direction left or right.

    // Maximum the current landform with lerp to the river at the river border.
    // 1 min, 1 max = always a full valley.
    // 0 min, 0 max = always a cave.
    public float valleyStrengthMax = 1;
    public float valleyStrengthMin = 0.4f;
    public float noiseExpansion = 1.5f; // How much the noise will be mulitplied before being clamped. Will make it transition from min -> max faster.

    // Each river node (single segment) creates a bounding box around it's start and end points.
    // It's inflated by this many blocks.
    // If 2 bounds of different rivers overlap that node will not be generated.
    // Prevents overlapping rivers.
    public int riverPaddingBlocks = 128;

    // How much to multiply the land scale by, to make the continents larger.
    public float landScaleMultiplier = 1;

    // Minimum and maximum size of rivers, in width.
    public float minSize = 14;
    public float maxSize = 50;

    // Minimum amount of segments a river must be to not be culled after map generated. Maximum amount before generation stops.
    public int minNodes = 8;
    public int maxNodes = 20;

    // How much to grow in size each node.
    public float riverGrowth = 3f;

    // How many times a river fork can go downhill.
    public int downhillError = 1;

    // Minimum length of a river node and how much to add to it randomly.
    public int minLength = 150;
    public int lengthVariation = 200;

    public int zoneSize = 256; // Zone size in blocks. Must be a multiple of 32.
    public int zonesInRegion = 128; // How many zones wide/tall the region is.
    public int RegionSize => zoneSize * zonesInRegion; // Region size in blocks.
    public int ChunksInRegion => RegionSize / 32;
    public int ChunksInZone => zoneSize / 32;

    // Chance for a river to be seeded at a coastal zone.
    public float riverSpawnChance = 0.2f;

    // Chance for node to split when generating a new node.
    public float riverSplitChance = 0.35f;

    // Chance for a lake when nodes stop.
    public float lakeChance = 0.15f;

    public int segmentsInRiver = 3; // Sub-segments 1 node is comprised of.
    public double segmentOffset = 40; // How much to offset each inner segment in blocks.

    // Base and depth based on the square root of the river size.
    public double baseDepth = 0.1; // Minimum depth.
    public double riverDepth = 0.022; // Depth to grow.

    // How much the ellipsoid carving the river should start above sea level and how big the top is in relation.
    public int heightBoost = 8;
    public float topFactor = 1;

    // Values relating to distortion of rivers.
    public int riverOctaves = 2;
    public float riverFrequency = 0.0075f;
    public float riverLacunarity = 3;
    public float riverGain = 0.3f;
    public int riverDistortionStrength = 10;

    // How fast rivers and water wheels should flow, can be changed after worldgen.
    public float riverSpeed = 4;

    // How wide a valley can be at world height.
    public double maxValleyWidth = 75;

    // How many blocks of submerged land, relative to default height, a spot is considered an ocean at.
    public float oceanThreshold = 30;

    // If stone should be generated under blocks with gravity.
    public bool fixGravityBlocks = true;

    // If boulders should generate near rivers.
    public bool boulders = true;

    // Gravel on sides of river.
    public bool gravelBeaches = true;

    // Better valleys.
    public bool valleysV2 = false;
}