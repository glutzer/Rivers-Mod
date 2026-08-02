using HarmonyLib;
using OpenTK.Mathematics;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Rivers;

public class RiversMod : ModSystem
{
    private static Harmony? Harmony { get; set; }

    public static float RiverSpeed { get; set; } = 1f;
    public static bool ClientFlowDisabled { get; private set; }

    public IClientNetworkChannel clientChannel = null!;
    public IServerNetworkChannel serverChannel = null!;
    private ICoreClientAPI? capi;

    public ICoreAPI api = null!;

    public override double ExecuteOrder()
    {
        return 5;
    }

    public override void Start(ICoreAPI api)
    {
        api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<RiverDebugMapLayer>("riverdebug", 0.9);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;

        clientChannel = api.Network.RegisterChannel("rivers")
            .RegisterMessageType<SpeedMessage>()
            .RegisterMessageType<RiverDebugMapMessage>()
            .SetMessageHandler<SpeedMessage>(OnSpeedMessage)
            .SetMessageHandler<RiverDebugMapMessage>(OnRiverDebugMapMessage);

#pragma warning disable CS0618 // Type or member is obsolete
        api.RegisterCommand(new RiverZoomCommand());
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverChannel = api.Network.RegisterChannel("rivers")
            .RegisterMessageType<SpeedMessage>()
            .RegisterMessageType<RiverDebugMapMessage>();

#pragma warning disable CS0618 // Type or member is obsolete
        api.RegisterCommand(new RiverDebugCommand(api, serverChannel));
#pragma warning restore CS0618 // Type or member is obsolete

        RiverSpeed = RiverConfig.Loaded.riverSpeed;
        api.Event.PlayerJoin += Event_PlayerJoin;
    }

    private void Event_PlayerJoin(IServerPlayer byPlayer)
    {
        // Inform new players what the speed set by the server is.
        serverChannel.SendPacket(new SpeedMessage() { riverSpeed = RiverConfig.Loaded.riverSpeed, flowDisabled = RiverConfig.Loaded.disableFlow }, byPlayer);
    }

    public static void OnSpeedMessage(SpeedMessage message)
    {
        RiverSpeed = message.riverSpeed;
        ClientFlowDisabled = message.flowDisabled;

        // Check if singleplayer.
        RePatchFlow();
    }

    private void OnRiverDebugMapMessage(RiverDebugMapMessage message)
    {
        if (capi == null) return;

        if (capi.ModLoader.GetModSystem<WorldMapManager>().MapLayers.FirstOrDefault(ml => ml is RiverDebugMapLayer) is RiverDebugMapLayer mapLayer)
        {
            mapLayer.SetData(message.RiverSegments, message.RegionSegments);
            capi.ShowChatMessage($"River debug map updated ({message.RiverSegments.Count} river lines, {message.RegionSegments.Count} region lines).");
        }
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
        RiverRegionCache.Clear();

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

    public static void RePatchFlow()
    {
        if (Harmony == null) return;

        if (ClientFlowDisabled)
        {
            Harmony.UnpatchCategory("flow");
        }
    }

    public static void Patch()
    {
        if (Harmony != null) return;

        Harmony = new Harmony("rivers");
        Harmony.PatchCategory("core");
        Harmony.PatchCategory("flow");
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
    public float riverSpeed = 1f;
    public bool flowDisabled;
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
    private readonly IServerNetworkChannel serverChannel;

    public RiverDebugCommand(ICoreServerAPI sapi, IServerNetworkChannel serverChannel)
    {
        this.sapi = sapi;
        this.serverChannel = serverChannel;

        Command = "riverdebug";
        Description = "River debug map commands: rivers, region, full, clear";
        Syntax = "/riverdebug <rivers|region|full|clear>";

        RequiredPrivilege = Privilege.ban;
    }

    public override void CallHandler(IPlayer player, int groupId, CmdArgs args)
    {
        try
        {
            if (player is not IServerPlayer serverPlayer || player.Entity == null)
            {
                return;
            }

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

            if (mode == "clear")
            {
                RiverDebugMapMessage clearMessage = new();
                serverChannel.SendPacket(clearMessage, serverPlayer);
                sapi.SendMessage(player, 0, "Cleared river debug map data.", EnumChatType.Notification);
                return;
            }

            if (mode is not ("rivers" or "region" or "full"))
            {
                sapi.SendMessage(player, 0, "Usage: /riverdebug <rivers|region|full|clear>", EnumChatType.Notification);
                return;
            }

            int worldX = (int)player.Entity.Pos.X;
            int worldZ = (int)player.Entity.Pos.Z;
            int chunkX = worldX / 32;
            int chunkZ = worldZ / 32;

            int chunksInRegion = RiverConfig.Loaded.zonesInRegion * RiverConfig.Loaded.zoneSize / 32;
            int regionX = chunkX / chunksInRegion;
            int regionZ = chunkZ / chunksInRegion;

            RiverRegion region = RiverRegionCache.GetOrCreate(sapi, regionX, regionZ);
            Vector2d regionStart = region.GlobalRegionStart;

            RiverDebugMapMessage message = new();

            if (mode is "rivers" or "full")
            {
                AddRiverSegments(message.RiverSegments, region, regionStart);
            }

            if (mode is "region" or "full")
            {
                AddRegionSegments(message.RegionSegments, regionStart);
            }

            serverChannel.SendPacket(message, serverPlayer);
            sapi.SendMessage(player, 0, $"Sent {message.RiverSegments.Count} river lines and {message.RegionSegments.Count} region lines.", EnumChatType.Notification);
        }
        catch (Exception e)
        {
            sapi.SendMessage(player, 0, $"Error, {e.Message}", EnumChatType.Notification);
        }
    }

    private static void AddRiverSegments(List<RiverMapSegmentData> target, RiverRegion region, Vector2d regionStart)
    {
        foreach (River river in region.rivers)
        {
            foreach (RiverNode node in river.nodes)
            {
                int count = node.segments.Length;
                for (int i = 0; i < count; i++)
                {
                    RiverSegment segment = node.segments[i];
                    float width = node.startSize;

                    if (count > 1)
                    {
                        float t = (float)i / (count - 1);
                        width = GameMath.Lerp(node.startSize, node.endSize, t);
                    }

                    width = Math.Max(1f, width);

                    target.Add(new RiverMapSegmentData
                    {
                        StartX = segment.startPos.X + regionStart.X,
                        StartZ = segment.startPos.Y + regionStart.Y,
                        EndX = segment.endPos.X + regionStart.X,
                        EndZ = segment.endPos.Y + regionStart.Y,
                        Width = width
                    });
                }
            }
        }
    }

    private static void AddRegionSegments(List<RiverMapSegmentData> target, Vector2d regionStart)
    {
        double size = RiverConfig.Loaded.zonesInRegion * RiverConfig.Loaded.zoneSize;
        double x1 = regionStart.X;
        double z1 = regionStart.Y;
        double x2 = x1 + size;
        double z2 = z1 + size;

        target.Add(new RiverMapSegmentData { StartX = x1, StartZ = z1, EndX = x2, EndZ = z1, Width = 2f });
        target.Add(new RiverMapSegmentData { StartX = x2, StartZ = z1, EndX = x2, EndZ = z2, Width = 2f });
        target.Add(new RiverMapSegmentData { StartX = x2, StartZ = z2, EndX = x1, EndZ = z2, Width = 2f });
        target.Add(new RiverMapSegmentData { StartX = x1, StartZ = z2, EndX = x1, EndZ = z1, Width = 2f });
    }
}
