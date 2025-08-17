using HarmonyLib;
using OpenTK.Mathematics;
using ProtoBuf;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Rivers;

public class RiversMod : ModSystem
{
    private static Harmony? Harmony { get; set; }

    public static float RiverSpeed { get; set; } = 1f;

    public IClientNetworkChannel clientChannel = null!;
    public IServerNetworkChannel serverChannel = null!;

    public ICoreAPI api = null!;

    public override double ExecuteOrder()
    {
        return 0;
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        clientChannel = api.Network.RegisterChannel("rivers")
            .RegisterMessageType(typeof(SpeedMessage))
            .SetMessageHandler<SpeedMessage>(OnSpeedMessage);

#pragma warning disable CS0618 // Type or member is obsolete
        api.RegisterCommand(new RiverZoomCommand());
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverChannel = api.Network.RegisterChannel("rivers")
            .RegisterMessageType(typeof(SpeedMessage));

#pragma warning disable CS0618 // Type or member is obsolete
        api.RegisterCommand(new RiverDebugCommand(api));
#pragma warning restore CS0618 // Type or member is obsolete

        RiverSpeed = RiverConfig.Loaded.riverSpeed;
        api.Event.PlayerJoin += Event_PlayerJoin;
    }

    private void Event_PlayerJoin(IServerPlayer byPlayer)
    {
        // Inform new players what the speed set by the server is.
        serverChannel.SendPacket(new SpeedMessage() { riverSpeed = RiverConfig.Loaded.riverSpeed }, byPlayer);
    }

    public static void OnSpeedMessage(SpeedMessage message)
    {
        RiverSpeed = message.riverSpeed;
    }

    public override void StartPre(ICoreAPI api)
    {
        string cfgFileName = "rivers.json";
        this.api = api;

#if DEBUG
        api.StoreModConfig(RiverConfig.Loaded, cfgFileName);
#else
        try
        {
            RiverConfig fromDisk;
            if ((fromDisk = api.LoadModConfig<RiverConfig>(cfgFileName)) == null)
            {
                api.StoreModConfig(RiverConfig.Loaded, cfgFileName);
            }
            else
            {
                RiverConfig.Loaded = fromDisk;
            }
        }
        catch
        {
            api.StoreModConfig(RiverConfig.Loaded, cfgFileName);
        }
#endif

        Patch();
    }

    public override void Dispose()
    {
        // Re-initialize values.
        ChunkTesselatorManagerPatch.BottomChunk = null!;
        BlockLayersPatches.Distances = null!;
        ZoomPatch.Multiplier = 0f;

        Unpatch();

        if (api.Side == EnumAppSide.Client)
        {
            ModDataCache.OnClientExit();
        }
        else
        {
            ModDataCache.OnServerExit();
        }
    }

    public static void Patch()
    {
        if (Harmony != null) return;

        Harmony = new Harmony("rivers");
        Harmony.PatchCategory("core");

        if (!RiverConfig.Loaded.disableFlow)
        {
            Harmony.PatchCategory("flow");
        }
    }

    public static void Unpatch()
    {
        if (Harmony == null) return;

        Harmony.UnpatchAll("rivers");
        Harmony = null;
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SpeedMessage
{
    public float riverSpeed = 1;
}

public class RiverZoomCommand : ClientChatCommand
{
    public static bool Zoomed { get; set; }

    public RiverZoomCommand()
    {
        Command = "riverdebug";
        Description = "Zooms out";
        Syntax = ".riverdebug";
    }

    public override void CallHandler(IPlayer player, int groupId, CmdArgs args)
    {
        if (args[0] == "selectedblockid")
        {
            BlockSelection? currentSelection = player.CurrentBlockSelection;
            if (currentSelection == null) return;

            ((ICoreClientAPI)player.Entity.World.Api).ShowChatMessage(currentSelection.Block.Id.ToString() + " " + currentSelection.Block.Code);
        }

        if (args[0] == "zoom")
        {
            try
            {
                Zoomed = !Zoomed;
            }
            catch
            {

            }
        }
    }
}

public class RiverDebugCommand : ServerChatCommand
{
    public ICoreServerAPI sapi;

    public RiverDebugCommand(ICoreServerAPI sapi)
    {
        this.sapi = sapi;

        Command = "riverdebug";
        Description = "Debug command for rivers";
        Syntax = "/riverdebug";

        RequiredPrivilege = Privilege.ban;
    }

    public override void CallHandler(IPlayer player, int groupId, CmdArgs args)
    {
        try
        {
            if (sapi.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault((MapLayer ml) => ml is WaypointMapLayer) is not WaypointMapLayer wp) return;

            int worldX = (int)player.Entity.Pos.X;
            int worldZ = (int)player.Entity.Pos.Z;
            int chunkX = worldX / 32;
            int chunkZ = worldZ / 32;

            int chunksInPlate = RiverConfig.Loaded.zonesInRegion * RiverConfig.Loaded.zoneSize / 32;

            int plateX = chunkX / chunksInPlate;
            int plateZ = chunkZ / chunksInPlate;

            RiverRegion plate = ObjectCacheUtil.GetOrCreate(sapi, $"{plateX}-{plateZ}", () =>
            {
                return new RiverRegion(sapi, plateX, plateZ);
            });

            Vector2d plateStart = plate.GlobalRegionStart;

            if (args[0] == "starts")
            {
                foreach (RiverSegment segment in plate.riverStarts)
                {
                    int r = sapi.World.Rand.Next(255);
                    int g = sapi.World.Rand.Next(255);
                    int b = sapi.World.Rand.Next(255);
                    MapRiver(wp, segment, r, g, b, player, plateStart);
                }

                sapi.SendMessage(player, 0, $"{riversMapped} rivers, {biggestRiver} biggest. {biggestX - sapi.World.DefaultSpawnPosition.X}, {biggestZ - sapi.World.DefaultSpawnPosition.Z}.", EnumChatType.Notification);
                riversMapped = 0;
                biggestRiver = 0;
                biggestX = 0;
                biggestZ = 0;

                wp.CallMethod("ResendWaypoints", player);
            }

            if (args[0] == "full")
            {
                foreach (River river in plate.rivers)
                {
                    int r = sapi.World.Rand.Next(255);
                    int g = sapi.World.Rand.Next(255);
                    int b = sapi.World.Rand.Next(255);

                    foreach (RiverNode node in river.nodes)
                    {
                        AddWaypoint(wp, "x", new Vec3d(node.startPos.X + plateStart.X, 0, node.startPos.Y + plateStart.Y), player.PlayerUID, r, g, b, "River " + node.startSize.ToString(), false);
                    }
                }

                wp.CallMethod("ResendWaypoints", player);
            }

            if (args[0] == "land")
            {
                foreach (RiverZone zone in plate.zones)
                {
                    if (zone.oceanZone)
                    {
                        AddWaypoint(wp, "x", new Vec3d(plateStart.X + zone.localZoneCenterPosition.X, 0, plateStart.Y + zone.localZoneCenterPosition.Y), player.PlayerUID, 0, 100, 255, "River Ocean", false);
                    }
                    else
                    {
                        AddWaypoint(wp, "x", new Vec3d(plateStart.X + zone.localZoneCenterPosition.X, 0, plateStart.Y + zone.localZoneCenterPosition.Y), player.PlayerUID, 255, 150, 150, "River Land", false);
                    }
                }

                wp.CallMethod("ResendWaypoints", player);
            }

            if (args[0] == "ocean")
            {
                int oceanTiles = 0;

                foreach (RiverZone zone in plate.zones)
                {
                    if (zone.oceanZone)
                    {
                        oceanTiles++;
                        AddWaypoint(wp, "x", new Vec3d(plateStart.X + zone.localZoneCenterPosition.X, 0, plateStart.Y + zone.localZoneCenterPosition.Y), player.PlayerUID, 0, 100, 255, "River Ocean", false);
                    }
                }

                wp.CallMethod("ResendWaypoints", player);

                sapi.SendMessage(player, 0, $"{oceanTiles} ocean tiles.", EnumChatType.Notification);
            }

            if (args[0] == "coastal")
            {
                int coastalTiles = 0;

                foreach (RiverZone zone in plate.zones)
                {
                    if (zone.coastalZone)
                    {
                        coastalTiles++;
                        AddWaypoint(wp, "x", new Vec3d(plateStart.X + zone.localZoneCenterPosition.X, 0, plateStart.Y + zone.localZoneCenterPosition.Y), player.PlayerUID, 255, 100, 255, "River Ocean", false);
                    }
                }

                wp.CallMethod("ResendWaypoints", player);

                sapi.SendMessage(player, 0, $"{coastalTiles} ocean tiles.", EnumChatType.Notification);
            }

            if (args[0] == "clear")
            {
                wp.Waypoints.Clear();

                wp.Waypoints.RemoveAll(wp => wp.Title.StartsWith("River"));


                wp.CallMethod("ResendWaypoints", player);
            }
        }
        catch (Exception e)
        {
            sapi.SendMessage(player, 0, $"Error, {e.Message}", EnumChatType.Notification);
        }
    }

    public int riversMapped = 0;
    public int biggestRiver = 0;
    public int biggestX = 0;
    public int biggestZ = 0;

    public void MapRiver(WaypointMapLayer wp, RiverSegment segment, int r, int g, int b, IPlayer player, Vector2d plateStart)
    {
        AddWaypoint(wp, "x", new Vec3d(segment.startPos.X + plateStart.X, 0, segment.startPos.Y + plateStart.Y), player.PlayerUID, r, g, b, $"River {segment.riverNode?.startSize}");

        if (segment.riverNode?.startSize > biggestRiver)
        {
            biggestRiver = (int)segment.riverNode.startSize;
            biggestX = (int)(segment.startPos.X + plateStart.X);
            biggestZ = (int)(segment.startPos.Y + plateStart.Y);
        }

        riversMapped++;
    }

    public static void AddWaypoint(WaypointMapLayer wp, string type, Vec3d worldPos, string playerUid, int r, int g, int b, string name, bool pin = true)
    {
        wp.Waypoints.Add(new Waypoint
        {
            Color = ColorUtil.ColorFromRgba(r, g, b, 255),
            Icon = type,
            Pinned = pin,
            Position = worldPos,
            OwningPlayerUid = playerUid,
            Title = name
        });
    }
}