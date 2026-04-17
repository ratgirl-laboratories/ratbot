using System.Collections.Concurrent;
using RatBot.Application.Features.Meta.Models;

namespace RatBot.Interactions.Modules.Meta.State;

public sealed class MetaSuggestionPendingStore
{
    private readonly ConcurrentDictionary<string, MetaSuggestionDraft> _drafts =
        new ConcurrentDictionary<string, MetaSuggestionDraft>();

    public string Save(MetaSuggestionDraft draft)
    {
        string token = Guid.CreateVersion7().ToString("N");
        _drafts[token] = draft;
        return token;
    }

    public bool TryTake(string token, out MetaSuggestionDraft? draft)
    {
        bool removed = _drafts.TryRemove(token, out MetaSuggestionDraft? value);
        draft = value;
        return removed;
    }
}
