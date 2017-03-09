namespace KK.AspNetCore.StaticFileCache
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using System;
    using System.IO;
    using System.Threading;

    public class StaticFileCacheFileProvider : IFileProvider
    {
        private readonly int fileSizeLimit = 256 * 1024;

        private readonly ILogger<StaticFileCacheFileProvider> logger;
        private readonly IFileProvider fileProvider;
        private readonly IMemoryCache cache;

        public StaticFileCacheFileProvider(
            ILogger<StaticFileCacheFileProvider> logger,
            IHostingEnvironment hostingEnv,
            IMemoryCache memoryCache,
            StaticFileCacheSettings settings
        )
        {
            this.logger = logger;
            this.fileProvider = hostingEnv.WebRootFileProvider;
            this.cache = memoryCache;

            if (settings != null)
            {
                this.fileSizeLimit = settings.FileSizeLimit * 1024;
            }
        }

        public void PrimeCache()
        {
            var started = this.logger.IsEnabled(LogLevel.Information) ? Timing.GetTimestamp() : 0;

            this.logger.LogInformation("Priming the cache");
            var cacheSize = this.PrimeCacheImpl("/");

            if (started != 0)
            {
                this.logger.LogInformation("Cache primed with {cacheEntriesCount} entries totalling {cacheEntriesSizeBytes} bytes in {elapsed}", cacheSize.Item1, cacheSize.Item2, Timing.GetDuration(started));
            }
        }

        private Tuple<int, long> PrimeCacheImpl(string currentPath)
        {
            this.logger.LogTrace("Priming cache for {currentPath}", currentPath);
            var cacheEntriesAdded = 0;
            var bytesCached = 0L;

            // TODO: Normalize the currentPath here, e.g. strip/always-add leading slashes, ensure slash consistency, etc.
            var prefix = string.Equals(currentPath, "/", StringComparison.OrdinalIgnoreCase) ? "/" : currentPath + "/";

            foreach (var fileInfo in this.GetDirectoryContents(currentPath))
            {
                if (fileInfo.IsDirectory)
                {
                    var cacheSize = this.PrimeCacheImpl(prefix + fileInfo.Name);
                    cacheEntriesAdded += cacheSize.Item1;
                    bytesCached += cacheSize.Item2;
                }
                else
                {
                    var stream = this.GetFileInfo(prefix + fileInfo.Name).CreateReadStream();
                    bytesCached += stream.Length;
                    stream.Dispose();
                    cacheEntriesAdded++;
                }
            }

            return Tuple.Create(cacheEntriesAdded, bytesCached);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            // TODO: Normalize the subpath here, e.g. strip/always-add leading slashes, ensure slash consistency, etc.
            var key = nameof(this.GetDirectoryContents) + "_" + subpath;
            IDirectoryContents cachedResult;
            if (this.cache.TryGetValue(key, out cachedResult))
            {
                // Item already exists in cache, just return it
                return cachedResult;
            }

            var directoryContents = this.fileProvider.GetDirectoryContents(subpath);
            if (!directoryContents.Exists)
            {
                // Requested subpath doesn't exist, just return
                return directoryContents;
            }

            // Create the cache entry and return
            var cacheEntry = this.cache.CreateEntry(key);
            cacheEntry.Value = directoryContents;
            cacheEntry.RegisterPostEvictionCallback((k, value, reason, s) =>
                this.logger.LogTrace("Cache entry {key} was evicted due to {reason}", k, reason));
            return directoryContents;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            // TODO: Normalize the subpath here, e.g. strip/always-add leading slashes, ensure slash consistency, etc.
            var key = nameof(this.GetFileInfo) + "_" + subpath;
            IFileInfo cachedResult;
            if (this.cache.TryGetValue(key, out cachedResult))
            {
                // Item already exists in cache, just return it
                return cachedResult;
            }

            var fileInfo = this.fileProvider.GetFileInfo(subpath);
            if (!fileInfo.Exists)
            {
                // Requested subpath doesn't exist, just return it
                return fileInfo;
            }

            if (fileInfo.Length > this.fileSizeLimit)
            {
                // File is too large to cache, just return it
                this.logger.LogTrace("File contents for {subpath} will not be cached as it's over the file size limit of {fileSizeLimit}", subpath, this.fileSizeLimit);
                return fileInfo;
            }

            // Create the cache entry and return
            var cachedFileInfo = new CachedFileInfo(this.logger, fileInfo, subpath);
            var fileChangedToken = this.Watch(subpath);
            fileChangedToken.RegisterChangeCallback(_ => this.logger.LogDebug("Change detected for {subpath} located at {filepath}", subpath, fileInfo.PhysicalPath), null);
            var cacheEntry = this.cache.CreateEntry(key)
                .RegisterPostEvictionCallback((k, value, reason, s) =>
                    this.logger.LogTrace("Cache entry {key} was evicted due to {reason}", k, reason))
                .AddExpirationToken(fileChangedToken)
                .SetValue(cachedFileInfo);

            // You have to call Dispose() to actually add the item to the underlying cache. Yeah, I know.
            cacheEntry.Dispose();
            return cachedFileInfo;
        }

        public IChangeToken Watch(string filter)
        {
            return this.fileProvider.Watch(filter);
        }

        private class CachedFileInfo : IFileInfo
        {
            private readonly ILogger logger;
            private readonly IFileInfo fileInfo;
            private readonly string subpath;
            private byte[] contents;

            public CachedFileInfo(ILogger logger, IFileInfo fileInfo, string subpath)
            {
                this.logger = logger;
                this.fileInfo = fileInfo;
                this.subpath = subpath;
            }

            public bool Exists => this.fileInfo.Exists;

            public bool IsDirectory => this.fileInfo.IsDirectory;

            public DateTimeOffset LastModified => this.fileInfo.LastModified;

            public long Length => this.fileInfo.Length;

            public string Name => this.fileInfo.Name;

            public string PhysicalPath => this.fileInfo.PhysicalPath;

            public Stream CreateReadStream()
            {
                var contents = this.contents;
                if (contents != null)
                {
                    this.logger.LogTrace("Returning cached file contents for {subpath} located at {filepath}", this.subpath, this.fileInfo.PhysicalPath);
                    return new MemoryStream(contents);
                }
                else
                {
                    this.logger.LogTrace("Loading file contents for {subpath} located at {filepath}", this.subpath, this.fileInfo.PhysicalPath);
                    MemoryStream ms;
                    using (var fs = this.fileInfo.CreateReadStream())
                    {
                        ms = new MemoryStream((int)fs.Length);
                        fs.CopyTo(ms);
                        contents = ms.ToArray();
                        ms.Position = 0;
                    }

                    if (Interlocked.CompareExchange(ref this.contents, contents, null) == null)
                    {
                        this.logger.LogTrace("Cached file contents for {subpath} located at {filepath}", this.subpath, this.fileInfo.PhysicalPath);
                    }

                    return ms;
                }
            }
        }
    }
}
