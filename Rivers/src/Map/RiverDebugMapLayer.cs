using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Rivers;

[ProtoContract]
public class RiverDebugMapMessage
{
    [ProtoMember(1)]
    public List<RiverMapSegmentData> RiverSegments = [];

    [ProtoMember(2)]
    public List<RiverMapSegmentData> RegionSegments = [];
}

[ProtoContract]
public class RiverMapSegmentData
{
    [ProtoMember(1)]
    public double StartX;

    [ProtoMember(2)]
    public double StartZ;

    [ProtoMember(3)]
    public double EndX;

    [ProtoMember(4)]
    public double EndZ;

    [ProtoMember(5)]
    public float Width;
}

public class RiverDebugMapLayer : MapLayer
{
    private readonly List<RiverMapSegmentData> riverSegments = [];
    private readonly List<RiverMapSegmentData> regionSegments = [];
    private Vec2f startView = new();
    private Vec2f endView = new();
    private readonly Vec4f riverColor = new(0.12f, 0.43f, 0.92f, 0.95f);
    private readonly Vec4f regionColor = new(1f, 0.78f, 0.1f, 0.95f);
    private readonly Matrixf mvMat = new();

    private readonly ICoreClientAPI? capi;
    private readonly MeshRef? quadModel;

    public RiverDebugMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink)
    {
        Active = true;

        if (api.Side == EnumAppSide.Client)
        {
            capi = api as ICoreClientAPI;
            quadModel = capi?.Render.UploadMesh(QuadMeshUtil.GetQuad());
        }
    }

    private bool HasRenderableData => riverSegments.Count > 0 || regionSegments.Count > 0;

    public override string Title => "River Debug";
    public override string LayerGroupCode => "waypoints";
    public override EnumMapAppSide DataSide => EnumMapAppSide.Client;
    public override bool RequireChunkLoaded => false;

    public void SetData(List<RiverMapSegmentData> rivers, List<RiverMapSegmentData> regions)
    {
        riverSegments.Clear();
        regionSegments.Clear();

        if (rivers.Count > 0) riverSegments.AddRange(rivers);
        if (regions.Count > 0) regionSegments.AddRange(regions);

    }

    public override void Render(GuiElementMap mapElem, float dt)
    {
        if (capi == null || quadModel == null || !HasRenderableData) return;

        float pixelsPerBlock = (float)(mapElem.Bounds.InnerWidth / mapElem.CurrentBlockViewBounds.Width);

        RenderSegments(mapElem, quadModel, riverColor, riverSegments, pixelsPerBlock, true);
        RenderSegments(mapElem, quadModel, regionColor, regionSegments, pixelsPerBlock, false);
    }

    private void RenderSegments(GuiElementMap mapElem, MeshRef mesh, Vec4f color, List<RiverMapSegmentData> segments, float pixelsPerBlock, bool scaleWidthWithBlocks)
    {
        if (segments.Count == 0 || capi == null) return;

        IShaderProgram prog = capi.Render.GetEngineShader(EnumShaderProgram.Gui);
        prog.Uniform("rgbaIn", color);
        prog.Uniform("extraGlow", 0);
        prog.Uniform("applyColor", 0);
        prog.Uniform("noTexture", 1f);
        prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);

        foreach (RiverMapSegmentData segment in segments)
        {
            mapElem.TranslateWorldPosToViewPos(new Vec3d(segment.StartX, 0, segment.StartZ), ref startView);
            mapElem.TranslateWorldPosToViewPos(new Vec3d(segment.EndX, 0, segment.EndZ), ref endView);

            float deltaX = endView.X - startView.X;
            float deltaY = endView.Y - startView.Y;
            float length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

            if (length <= 0.05f) continue;

            float thickness = scaleWidthWithBlocks
                ? MathF.Max(1f, segment.Width * pixelsPerBlock)
                : MathF.Max(1f, segment.Width);
            float centerX = (startView.X + endView.X) * 0.5f;
            float centerY = (startView.Y + endView.Y) * 0.5f;
            float angle = MathF.Atan2(deltaY, deltaX);

            mvMat
                .Set(capi.Render.CurrentModelviewMatrix)
                .Translate((float)mapElem.Bounds.renderX + centerX, (float)mapElem.Bounds.renderY + centerY, 50)
                .RotateZ(angle)
                .Scale(length * 0.5f, thickness * 0.5f, 0);

            prog.UniformMatrix("modelViewMatrix", mvMat.Values);
            capi.Render.RenderMesh(mesh);
        }
    }

    public override void Dispose()
    {
        quadModel?.Dispose();
        base.Dispose();
    }
}
