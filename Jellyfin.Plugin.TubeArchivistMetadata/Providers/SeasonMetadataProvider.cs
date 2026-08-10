using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TubeArchivistMetadata.TubeArchivist;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TubeArchivistMetadata.Providers
{
    /// <summary>
    /// Names Jellyfin seasons after the TubeArchivist playlist they represent.
    /// </summary>
    /// <remarks>
    /// Seasons are virtual: the media is a flat folder, so Jellyfin creates them itself and names
    /// them "Season {N}". It also overwrites any <c>SeasonName</c> set on an episode with the parent
    /// season's name, which is why the playlist name has to be applied here rather than when
    /// building the episode.
    /// </remarks>
    public class SeasonMetadataProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeasonMetadataProvider"/> class.
        /// </summary>
        public SeasonMetadataProvider()
        {
            if (Plugin.Instance == null)
            {
                throw new DataException("Uninitialized plugin!");
            }
            else
            {
                _logger = Plugin.Instance.Logger;
            }
        }

        /// <summary>
        /// Gets the provider name.
        /// </summary>
        public string Name => Constants.ProviderName;

        /// <inheritdoc />
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();

            if (Plugin.Instance?.Configuration.SortSeasonsByPlaylist != true)
            {
                return result;
            }

            // A season is identified only by its number, so without this check season 1 of an
            // unrelated show in another library would also be renamed. Requiring the parent series
            // to carry this plugin's provider id scopes the rename to TubeArchivist content, and is
            // more precise than the CollectionTitle name match used by the sync tasks.
            if (!info.SeriesProviderIds.ContainsKey(Constants.ProviderName))
            {
                return result;
            }

            var seasonNumber = info.IndexNumber;
            if (!seasonNumber.HasValue)
            {
                return result;
            }

            var seasonName = await PlaylistCache.GetInstance()
                .GetSeasonNameAsync(seasonNumber.Value, cancellationToken)
                .ConfigureAwait(true);

            if (string.IsNullOrEmpty(seasonName))
            {
                // Either the season predates the feature being enabled or the playlist is no longer
                // in TubeArchivist. Leaving HasMetadata false keeps Jellyfin's own "Season {N}".
                _logger.LogDebug("{Message}", string.Format(CultureInfo.CurrentCulture, "No TubeArchivist playlist name for season {0}.", seasonNumber.Value));
                return result;
            }

            _logger.LogDebug("{Message}", string.Format(CultureInfo.CurrentCulture, "Naming season {0} after playlist '{1}'.", seasonNumber.Value, seasonName));

            result.HasMetadata = true;
            result.Provider = Name;
            result.Item = new Season
            {
                Name = seasonName,
                IndexNumber = seasonNumber
            };

            return result;
        }

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        /// <inheritdoc />
        /// <remarks>
        /// This provider supplies season names only. Fetching an arbitrary URL through
        /// <see cref="Plugin.HttpClient"/> would send the TubeArchivist API key to whatever host the
        /// URL points at, so the capability is refused rather than left available unused.
        /// </remarks>
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotSupportedException(Name + " does not provide season images.");
        }
    }
}
