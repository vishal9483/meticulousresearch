using Microsoft.Extensions.DependencyInjection;
using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Data;

/// <summary>
/// DI registration for the persistence foundation. Registers a singleton <see cref="DataStore"/>
/// rooted at the supplied data directory (already initialized), and surfaces
/// <see cref="IProjectFileStore"/> for consumers. Downstream features resolve these to read/write
/// data without knowing where the database lives.
/// </summary>
public static class DataStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers and initializes the data store rooted at <paramref name="dataDirectory"/>.
    /// Requires an <see cref="IClock"/> to already be registered.
    /// </summary>
    public static IServiceCollection AddDataStore(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton(sp =>
        {
            var clock = sp.GetRequiredService<IClock>();
            var store = new DataStore(clock, dataDirectory);
            store.Initialize();
            return store;
        });

        services.AddSingleton<IProjectFileStore>(sp => sp.GetRequiredService<DataStore>().FileStore);

        return services;
    }
}
