using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Entities.Events;

namespace Diginsight.Analyzer.Business;

public interface IEventService
{
    Task EmitAsync(IEnumerable<EventRecipient> eventRecipients, Func<EventRecipientInput, DateTime, Event> makeEvent);
}
