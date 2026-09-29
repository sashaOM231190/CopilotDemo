using System.Collections.Concurrent;
using Cbs.Mcp.Contracts;

namespace Cbs.Mcp.Server.State;

public sealed class AnalysisRepository
{
    private readonly ConcurrentDictionary<string, AnalysisState> _states =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _expiry = TimeSpan.FromHours(2);

    public void Add(CbsInspectionResult inspection, IReadOnlyList<CbsArtifact> artifacts)
    {
        RemoveExpired();
        var state = new AnalysisState(inspection, artifacts);
        if (!_states.TryAdd(inspection.AnalysisId, state))
        {
            throw new InvalidOperationException(
                $"Analysis ID already exists: {inspection.AnalysisId}");
        }
    }

    public bool TryGet(string analysisId, out AnalysisState? state)
    {
        RemoveExpired();
        if (_states.TryGetValue(analysisId, out state))
        {
            state.Touch();
            return true;
        }

        return false;
    }

    private void RemoveExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - _expiry;
        foreach (var pair in _states.Where(pair => pair.Value.LastAccessedAt < cutoff))
        {
            _states.TryRemove(pair.Key, out _);
        }
    }
}
