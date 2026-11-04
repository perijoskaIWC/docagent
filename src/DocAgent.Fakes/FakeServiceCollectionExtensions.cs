using DocAgent.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DocAgent.Fakes;

public static class FakeServiceCollectionExtensions
{
    /// <summary>
    /// Registers one fake <see cref="StrategyOption"/> per key, so GET /api/strategies and the UI show every planned
    /// strategy from day one. A real strategy registers its own option when its owner turns off the fake.
    /// </summary>
    public static IServiceCollection AddFakeStrategyOptions(this IServiceCollection services, string slot, params string[] keys)
    {
        foreach (var key in keys)
            services.AddSingleton(new StrategyOption(slot, key, Description: "Fake", IsFake: true));
        return services;
    }
}
