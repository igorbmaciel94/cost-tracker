using System.Threading.Channels;
using CostTracker.Application.Investments.MarketData;

namespace CostTracker.Api.Services;

public sealed class InvestmentMarketDataRefreshSignal : IMarketDataRefreshScheduler
{
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void RequestRefresh() => _requests.Writer.TryWrite(true);

    public async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        _ = await _requests.Reader.ReadAsync(cancellationToken);
        while (_requests.Reader.TryRead(out _))
        {
            // Coalesce bursts of portfolio mutations into one provider refresh.
        }
    }
}
