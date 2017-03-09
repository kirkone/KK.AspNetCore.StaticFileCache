namespace KK.AspNetCore.StaticFileCache
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    public static class StaticFileCacheSettingsExtensions
    {
        /// <summary>
        /// Add the settings from "StaticFileCache" of the appsettings as a Singleton of KK.AspNetCore.StaticFileCache.StaticFileCacheSettings
        /// </summary>
        /// <param name="services">Specifies the contract for a collection of service descriptors.</param>
        /// <param name="configuration">Represents the root of an IConfiguration hierarchy.</param>
        /// <returns>IServiceCollection</returns>
        public static IServiceCollection AddStaticFileCacheSettings(
            this IServiceCollection services,
            IConfigurationRoot configuration
        )
        {
            var section = configuration.GetSection("StaticFileCache");
            var settings = new StaticFileCacheSettings();
            new ConfigureFromConfigurationOptions<StaticFileCacheSettings>(section)
                .Configure(settings);
            services.AddSingleton(settings);

            return services;
        }
    }
}
