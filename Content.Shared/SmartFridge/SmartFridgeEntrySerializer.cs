// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.SmartFridge;

/// <summary>
/// Serializes <see cref="SmartFridgeEntry"/> as a plain string so that it can be used as a YAML mapping key.
/// </summary>
[TypeSerializer]
public sealed class SmartFridgeEntrySerializer :
    ITypeSerializer<SmartFridgeEntry, ValueDataNode>,
    ITypeReader<SmartFridgeEntry, MappingDataNode> // Backwards compatibility with the old DataRecord representation.
{
    public SmartFridgeEntry Read(ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<SmartFridgeEntry>? instanceProvider = null)
    {
        return new SmartFridgeEntry(node.Value);
    }

    public DataNode Write(ISerializationManager serializationManager,
        SmartFridgeEntry value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return new ValueDataNode(value.Name);
    }

    public ValidationNode Validate(ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        return new ValidatedValueNode(node);
    }

    public ValidationNode Validate(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (node.TryGetValue("name", out var nameNode) && nameNode is ValueDataNode)
            return new ValidatedValueNode(nameNode);

        return new ErrorNode(node, "Smart fridge entry must contain a name field");
    }

    public SmartFridgeEntry Read(ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<SmartFridgeEntry>? instanceProvider = null)
    {
        return new SmartFridgeEntry(((ValueDataNode) node["name"]).Value);
    }
}
