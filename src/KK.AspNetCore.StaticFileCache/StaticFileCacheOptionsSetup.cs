namespace KK.AspNetCore.StaticFileCache
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Options;

    public class StaticFileCacheOptionsSetup : IConfigureOptions<StaticFileOptions>
    {
        private readonly StaticFileCacheFileProvider staticFileCache;

        public StaticFileCacheOptionsSetup(StaticFileCacheFileProvider staticFileCache)
        {
            this.staticFileCache = staticFileCache;
        }

        public void Configure(StaticFileOptions options)
        {
            options.FileProvider = this.staticFileCache;
        }
    }
}
