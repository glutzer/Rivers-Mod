using Vintagestory.API.Common;

namespace Rivers;

public class RiversApi : ModSystem
{
    public static RiversApi? Instance { get; private set; }
    private ICoreAPI api = null!;

    public override double ExecuteOrder()
    {
        return 0;
    }

    public override void StartPre(ICoreAPI api)
    {
        this.api = api;

        if (api.Side == EnumAppSide.Client) return;

        Instance = this;
    }

    public override void Dispose()
    {
        if (api.Side == EnumAppSide.Client) return;

        Instance = null;
    }
}