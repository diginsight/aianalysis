using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public sealed class EventRecipient
{
    private EventRecipientInput? input;

    public required string Name { get; init; }

    [AllowNull]
    public EventRecipientInput Input
    {
        get => input ??= new EventRecipientInput();
        init => input = value;
    }
}
