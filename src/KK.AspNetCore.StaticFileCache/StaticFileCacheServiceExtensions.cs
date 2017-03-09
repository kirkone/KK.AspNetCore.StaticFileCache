namespace KK.AspNetCore.StaticFileCache
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    public static class StaticFileCacheServiceExtensions
    {
        public static IServiceCollection AddStaticFileCache(this IServiceCollection services)
        {
            // Turn off compaction on memory pressure as it results in things being evicted during the priming of the
            // cache on application start.
            services.AddMemoryCache(options => options.CompactOnMemoryPressure = false);
            services.AddSingleton<StaticFileCacheFileProvider>();
            services.AddSingleton<IConfigureOptions<StaticFileOptions>, StaticFileCacheOptionsSetup>();
            return services;
        }
    }
}
