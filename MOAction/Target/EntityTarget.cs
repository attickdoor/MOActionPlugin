using Dalamud.Game.ClientState.Objects.Types;

namespace MOAction.Target;

public class EntityTarget : TargetType
{
    public EntityTarget(PtrFunc func, string name) : base(func, name) { }
    public EntityTarget(PtrFunc func, string name, bool objneed) : base(func, name, objneed) { }

    public override IGameObject GetTarget()
    {
        var obj = GetPtr();
        if (IsTargetValid())
            return obj;

        return null;
    }

    public override bool IsTargetValid()
    {
        var obj = GetPtr();
        return obj != null;
    }
}