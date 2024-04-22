using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace Rivers;

public class BEBehaviorWaterWheel : BEBehaviorMPRotor
{
    public ICoreServerAPI sapi;
    public float currentSpeed = 0;

    public AssetLocation sound;
    protected override AssetLocation Sound => sound;

    protected override float GetSoundVolume()
    {
        return currentSpeed * 0.05f;
    }

    // How much strain this being on the network applies.
    protected override float Resistance => 1f;

    // How quickly the speed ramps up.
    protected override double AccelerationFactor => 0.5;

    // Will accelerate up until it reaches this point.
    protected override float TargetSpeed => currentSpeed * Block.Attributes["speed"].AsFloat() * speedMultiplier;

    // How much torque will be applied, scaling linearly with speed.
    protected override float TorqueFactor => currentSpeed * Block.Attributes["torque"].AsFloat() * torqueMultiplier;

    public bool invert = false;

    public float diminishingFactor;

    public float riverSpeed;

    public float speedMultiplier = 1;
    public float torqueMultiplier = 1;

    public int seaLevel;

    public override float GetTorque(long tick, float speed, out float resistance)
    {
        float targetSpeed = TargetSpeed;
        capableSpeed += (targetSpeed - capableSpeed) * AccelerationFactor;
        float capableFloat = (float)capableSpeed;

        float dir = invert ? -1f : 1f;
        float absSpeed = Math.Abs(speed);
        float excessSpeed = absSpeed - capableFloat;
        bool wrongDirection = dir * speed < 0f;

        resistance = wrongDirection ? Resistance * TorqueFactor * Math.Min(0.8f, absSpeed * 400f) : excessSpeed > 0 ? Resistance * Math.Min(0.2f, excessSpeed * excessSpeed * 80f) : 0f;
        float power = wrongDirection ? capableFloat : capableFloat - absSpeed;
        return Math.Max(0f, power) * TorqueFactor * dir;
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        riverSpeed = RiverConfig.Loaded.riverSpeed;

        speedMultiplier = RiverConfig.Loaded.wheelSpeedMultiplier;
        torqueMultiplier = RiverConfig.Loaded.wheelTorqueMultiplier;

        sound = new AssetLocation("game:sounds/environment/waterfall");
        if (api.Side == EnumAppSide.Server)
        {
            sapi = api as ICoreServerAPI;
            Blockentity.RegisterGameTickListener(UpdateWaterWheel, 1000);
        }

        seaLevel = TerraGenConfig.seaLevel;
    }

    public void UpdateWaterWheel(float dt)
    {
        IWorldChunk chunk = sapi.World.BlockAccessor.GetChunk(Pos.X / 32, 0, Pos.Z / 32);
        int index2d = (Pos.Z % 32 * 32) + (Pos.X % 32);

        // Check if there's water below.
        bool water = false;
        BlockPos tempPos = Pos.Copy();
        for (int i = 0; i < Block.Attributes["radius"].AsInt(); i++)
        {
            tempPos.Sub(0, 1, 0);
            if (sapi.World.BlockAccessor.GetBlock(tempPos, BlockLayersAccess.Fluid).LiquidLevel == 7)
            {
                water = true;
            }
        }

        // Hard-coded sea-level + 2 so people can't put waterwheels in the sky.
        // If people want water wheels deep underground, yeah go ahead sport.
        if (water == false || Pos.Y > seaLevel + 2 || sapi.World.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Fluid).LiquidLevel == 7) // If there's no water below it's radius or water inside it don't move.
        {
            currentSpeed = 0;
            invert = false;
            Blockentity.MarkDirty();
            return;
        }

        BlockFacing facing = BlockFacing.FromCode(Block.Variant["side"]);

        float rot = 0;
        switch (facing.Index)
        {
            case 0:
                rot = 180;
                break;
            case 1:
                rot = 90;
                break;
            case 3:
                rot = 270;
                break;
        }

        float[] vectors = chunk.GetModdata<float[]>("flowVectors");
        if (vectors == null)
        {
            currentSpeed = 0;
            invert = false;
            Blockentity.MarkDirty();
            return;
        }

        if (rot == 0)
        {
            currentSpeed = vectors[index2d]; // Correct positive.
            invert = true;
        }
        else if (rot == 180)
        {
            currentSpeed = vectors[index2d];
            invert = false;
        }
        else if (rot == 90)
        {
            currentSpeed = vectors[index2d + 1024]; // Correct positive.
            invert = false;
        }
        else if (rot == 270)
        {
            currentSpeed = vectors[index2d + 1024];
            invert = true;
        }

        // Go in the other direction if it's flowing the other way.
        if (currentSpeed < 0)
        {
            invert = !invert;
            currentSpeed = -currentSpeed;
        }

        if (currentSpeed > 0.6f) currentSpeed = 1;

        Blockentity.MarkDirty();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        currentSpeed = tree.GetFloat("currentSpeed");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("currentSpeed", currentSpeed);
    }

    public BEBehaviorWaterWheel(BlockEntity blockentity) : base(blockentity)
    {
    }
}