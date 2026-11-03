using Content.Shared.Construction.Prototypes;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

/// <summary>
/// The client sends this to update their construction favorites.
/// </summary>
public sealed class MsgUpdateConstructionFavorites : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public List<ProtoId<ConstructionPrototype>> Favorites = [];

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Favorites.Clear();

        var length = buffer.ReadVariableInt32();

        // A modified client can claim a huge count and stall the main loop for minutes while
        // the loop allocates empty entries. Every favorite takes at least one byte on the wire.
        if (length <= 0 || length > (buffer.LengthBits - buffer.Position) / 8)
            return;

        for (var i = 0; i < length; i++)
        {
            Favorites.Add(new ProtoId<ConstructionPrototype>(buffer.ReadString()));
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Favorites.Count);
        foreach (var favorite in Favorites)
        {
            buffer.Write(favorite);
        }
    }
}
