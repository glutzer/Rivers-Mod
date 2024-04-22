namespace Rivers;

public class RiverConfig
{
    public static RiverConfig Loaded { get; set; } = new RiverConfig();

    // When forking move at these angles.
    public int minForkAngle = 10;
    public int forkVaration = 35;

    // When adding another node change by 0 to this angle left or right.
    public int normalAngle = 20;

    // Minimum and maximum size of rivers.
    public float minSize = 8;
    public float maxSize = 50;

    // Minimum amount of segments a river must be to not be culled after map generated. Maximum amount before generation stops.
    public int minNodes = 8;
    public int maxNodes = 20;

    // How much to grow in size each node.
    public float riverGrowth = 2.5f;

    // How many times a river fork can go downhill.
    public int error = 1;

    // Minimum length of a river node and how much to add to it randomly.
    public int minLength = 150;
    public int lengthVariation = 200;

    // Grid for generating rivers. Don't make this bigger, it's already laggy.
    public int zoneSize = 256;
    public int zonesInPlate = 128;

    // Chance for a river to be seeded at a coastal zone.
    public int riverSpawnChance = 5;

    // Chance for node to split.
    public int riverSplitChance = 60;

    // Chance for a lake when nodes stop.
    public int lakeChance = 15;

    // Segments 1 node is composed of.
    public int segmentsInRiver = 3;

    // How much to offset each inner segment.
    public double segmentOffset = 40;

    // Base and depth based on the square root of the river size.
    public double baseDepth = 0.1;
    public double riverDepth = 0.022;

    // How much the ellipsoid carving the river should start above sea level and how big the top is in relation.
    public int heightBoost = 8;
    public float topFactor = 1;

    // Values relating to distortion of rivers.
    public int riverOctaves = 2;
    public float riverFrequency = 0.0075f;
    public float riverLacunarity = 3;
    public float riverGain = 0.3f;
    public int riverStrength = 12;

    // How fast rivers and water wheels should flow, can be changed after worldgen.
    public float riverSpeed = 8;

    // How wide a valley can be at world height.
    public double maxValleyWidth = 50;

    // How many blocks of submerged land, relative to default height, a spot is considered an ocean at.
    public float oceanThreshold = 30;

    // Water wheel speed and torque.
    public float wheelSpeedMultiplier = 0.4f;
    public float wheelTorqueMultiplier = 0.4f;

    // If stone should be generated under blocks with gravity.
    public bool fixGravityBlocks = true;

    // If rivers should emit particles on the client.
    public bool clientParticles = true;

    // If boulders and logs should generate near rivers.
    public bool boulders = true;

    // If deposits should generate.
    public bool riverDeposits = true;

    // How much of the river bed should be clay.
    public float clayDepositFrequency = 0.2f;

    // If brown and red clay should be integrated.
    public bool clayExpansion = true;

    // Gravel on sides of river.
    public bool gravel = true;
}