using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TubeArchivistMetadata.Configuration;
using Jellyfin.Plugin.TubeArchivistMetadata.Utilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.TubeArchivistMetadata.TubeArchivist
{
    /// <summary>
    /// Caches the TubeArchivist playlist membership of videos and maps each playlist to a stable
    /// Jellyfin season number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Follows the plugin's static singleton pattern (see <see cref="TubeArchivistApi.GetInstance"/>);
    /// the plugin does not use dependency injection for its internals.
    /// </para>
    /// <para>
    /// Caching is required rather than convenient. Retrieving playlists returns every playlist on
    /// the TubeArchivist server, while episode metadata is requested one video at a time, so an
    /// uncached lookup would issue that request once per episode during a library scan.
    /// </para>
    /// </remarks>
    public sealed class PlaylistCache : IDisposable
    {
        private static readonly object InstanceLock = new object();
        private static PlaylistCache? _instance;

        private readonly ILogger _logger;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private readonly object _mapLock = new object();

        // YouTube ids are case sensitive, so lookups must be ordinal.
        private Dictionary<string, PlaylistAssignment> _videoToPlaylist = new Dictionary<string, PlaylistAssignment>(StringComparer.Ordinal);
        private Dictionary<int, string> _seasonNames = new Dictionary<int, string>();
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private bool _lastRefreshFailed;

        private PlaylistCache()
        {
            if (Plugin.Instance == null)
            {
                throw new DataException("Uninitialized plugin!");
            }

            _logger = Plugin.Instance.Logger;
        }

        /// <summary>
        /// Gets the cache lifetime before a refresh from TubeArchivist is attempted.
        /// </summary>
        public static TimeSpan CacheLifetime { get; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets the instance of the <see cref="PlaylistCache"/> class.
        /// </summary>
        /// <returns>The PlaylistCache instance.</returns>
        public static PlaylistCache GetInstance()
        {
            lock (InstanceLock)
            {
                _instance ??= new PlaylistCache();
                return _instance;
            }
        }

        /// <summary>
        /// Resets the cached instance. Intended for tests.
        /// </summary>
        public static void ResetInstance()
        {
            lock (InstanceLock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Present to satisfy CA1001. The cache lives for the lifetime of the process, so
        /// <see cref="ResetInstance"/> is the only sanctioned caller: disposing the shared instance
        /// directly leaves every later lookup throwing <see cref="ObjectDisposedException"/>.
        /// </remarks>
        public void Dispose()
        {
            _refreshLock.Dispose();
        }

        /// <summary>
        /// Gets the playlist a video belongs to, refreshing the cache when it has expired.
        /// </summary>
        /// <param name="videoId">YouTube video id.</param>
        /// <param name="cancellationToken">Token used to abort a refresh while a library scan is cancelled.</param>
        /// <returns>
        /// The playlist assignment, or <c>null</c> when the video belongs to no playlist or the
        /// playlist data could not be retrieved.
        /// </returns>
        public async Task<PlaylistAssignment?> GetAssignmentAsync(string videoId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                return null;
            }

            await EnsureFreshAsync(cancellationToken).ConfigureAwait(false);

            lock (_mapLock)
            {
                return _videoToPlaylist.TryGetValue(videoId, out var assignment) ? assignment : null;
            }
        }

        /// <summary>
        /// Gets the season name for a season number produced by this cache.
        /// </summary>
        /// <param name="seasonNumber">The Jellyfin season number.</param>
        /// <param name="cancellationToken">Token used to abort a refresh while a library scan is cancelled.</param>
        /// <returns>The season name, or <c>null</c> when the number is not a known playlist season.</returns>
        public async Task<string?> GetSeasonNameAsync(int seasonNumber, CancellationToken cancellationToken = default)
        {
            if (seasonNumber == Constants.UnsortedSeasonNumber)
            {
                return Constants.UnsortedSeasonName;
            }

            await EnsureFreshAsync(cancellationToken).ConfigureAwait(false);

            lock (_mapLock)
            {
                return _seasonNames.TryGetValue(seasonNumber, out var name) ? name : null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether usable playlist data is available.
        /// </summary>
        /// <remarks>
        /// Lets callers tell "this video is in no playlist" apart from "playlists could not be
        /// retrieved". Without it a TubeArchivist outage would move an entire library into the
        /// Unsorted season, and because Jellyfin does not overwrite an existing
        /// <c>ParentIndexNumber</c> that cannot be undone by a later refresh.
        /// </remarks>
        /// <param name="cancellationToken">Token used to abort a refresh while a library scan is cancelled.</param>
        /// <returns><c>true</c> when playlist data was retrieved successfully.</returns>
        public async Task<bool> HasPlaylistDataAsync(CancellationToken cancellationToken = default)
        {
            await EnsureFreshAsync(cancellationToken).ConfigureAwait(false);

            lock (_mapLock)
            {
                return !_lastRefreshFailed && _seasonNames.Count > 0;
            }
        }

        private async Task EnsureFreshAsync(CancellationToken cancellationToken)
        {
            lock (_mapLock)
            {
                if (DateTime.UtcNow - _lastRefreshUtc < CacheLifetime)
                {
                    return;
                }
            }

            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Another caller may have refreshed while this one waited.
                lock (_mapLock)
                {
                    if (DateTime.UtcNow - _lastRefreshUtc < CacheLifetime)
                    {
                        return;
                    }
                }

                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var taApi = TubeArchivistApi.GetInstance();
            ISet<Playlist>? playlists;

            try
            {
                playlists = await taApi.GetPlaylists().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
                // Failure policy: keep whatever is cached and let callers fall back to upload year
                // grouping. A partial migration is worse than no migration.
                _logger.LogWarning(ex, "Could not retrieve TubeArchivist playlists. Falling back to upload year seasons.");
                MarkRefreshFailed();
                return;
            }

            if (playlists == null)
            {
                _logger.LogWarning("TubeArchivist returned no playlist data. Falling back to upload year seasons.");
                MarkRefreshFailed();
                return;
            }

            var distinctPlaylists = playlists
                .Where(p => !string.IsNullOrEmpty(p.Id))
                .GroupBy(p => p.Id, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            // Season names from the persisted map, so seasons keep a usable label for playlists
            // TubeArchivist no longer returns. Live data below takes precedence.
            var seasonNames = new Dictionary<int, string>();
            var seasonNumbers = await BuildSeasonMapAsync(distinctPlaylists, taApi, seasonNames).ConfigureAwait(false);
            var videoToPlaylist = new Dictionary<string, PlaylistAssignment>(StringComparer.Ordinal);

            foreach (var playlist in distinctPlaylists)
            {
                if (!seasonNumbers.TryGetValue(playlist.Id, out var seasonNumber))
                {
                    continue;
                }

                var seasonName = Utils.SanitizePlaylistName(playlist.Name, playlist.Id);
                seasonNames[seasonNumber] = seasonName;

                foreach (var entry in playlist.Entries)
                {
                    if (string.IsNullOrEmpty(entry.YoutubeId))
                    {
                        continue;
                    }

                    // A video can appear in several playlists. Pick the lowest season number so the
                    // choice is deterministic across refreshes.
                    if (videoToPlaylist.TryGetValue(entry.YoutubeId, out var existing))
                    {
                        if (existing.SeasonNumber <= seasonNumber)
                        {
                            continue;
                        }

                        _logger.LogDebug(
                            "Video {VideoId} is in multiple playlists. Using season {ChosenSeason} ({ChosenName}) instead of {OtherSeason}.",
                            entry.YoutubeId,
                            seasonNumber,
                            seasonName,
                            existing.SeasonNumber);
                    }

                    videoToPlaylist[entry.YoutubeId] = new PlaylistAssignment(playlist.Id, seasonName, seasonNumber, entry.Index);
                }
            }

            WarnOnDuplicateSeasonNames(seasonNames);

            lock (_mapLock)
            {
                _videoToPlaylist = videoToPlaylist;
                _seasonNames = seasonNames;
                _lastRefreshUtc = DateTime.UtcNow;
                _lastRefreshFailed = false;
            }

            _logger.LogInformation(
                "Cached {PlaylistCount} TubeArchivist playlists covering {VideoCount} videos.",
                seasonNames.Count,
                videoToPlaylist.Count);
        }

        /// <summary>
        /// Assigns a Jellyfin season number to every playlist, persisting the result so numbers stay
        /// stable across restarts.
        /// </summary>
        /// <remarks>
        /// Playlists already present in the configuration keep their number. New playlists are
        /// appended after the highest number in use, seeded in chronological order by the publish
        /// date of their first video, so an initial build reads chronologically without later
        /// additions renumbering existing seasons.
        /// </remarks>
        private async Task<Dictionary<string, int>> BuildSeasonMapAsync(
            IReadOnlyCollection<Playlist> playlists,
            TubeArchivistApi taApi,
            Dictionary<int, string> persistedNames)
        {
            // Resolve the configuration fresh: saving the plugin settings replaces the whole
            // PluginConfiguration instance, so a cached reference would be silently orphaned.
            var configuration = Plugin.Instance?.Configuration;
            if (configuration == null)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            var assigned = new Dictionary<string, int>(StringComparer.Ordinal);
            var unmapped = new List<Playlist>();
            var usedSeasons = new HashSet<int>();

            lock (_mapLock)
            {
                foreach (var entry in configuration.PlaylistSeasonMap)
                {
                    if (string.IsNullOrEmpty(entry.PlaylistId))
                    {
                        continue;
                    }

                    // The map is user-editable XML. An out-of-range number would either collide with
                    // the Specials season (0), be dropped by Jellyfin (negative), or merge a real
                    // playlist into "Unsorted" (>= 9000), so reject it and let the playlist be
                    // reallocated below.
                    if (entry.SeasonNumber <= 0 || entry.SeasonNumber >= Constants.UnsortedSeasonNumber)
                    {
                        _logger.LogWarning(
                            "Ignoring out of range season number {SeasonNumber} for playlist {PlaylistId}.",
                            entry.SeasonNumber,
                            entry.PlaylistId);
                        continue;
                    }

                    // Two playlists sharing a season would merge into one Jellyfin season, with the
                    // name decided by whichever was read last. Keep the first and reallocate the
                    // other below, same as an out-of-range entry.
                    if (!usedSeasons.Add(entry.SeasonNumber))
                    {
                        _logger.LogWarning(
                            "Ignoring duplicate season number {SeasonNumber} for playlist {PlaylistId}.",
                            entry.SeasonNumber,
                            entry.PlaylistId);
                        continue;
                    }

                    assigned[entry.PlaylistId] = entry.SeasonNumber;

                    // Keeps season names available when TubeArchivist cannot be reached.
                    if (!string.IsNullOrEmpty(entry.PlaylistName))
                    {
                        persistedNames[entry.SeasonNumber] = entry.PlaylistName;
                    }
                }
            }

            foreach (var playlist in playlists)
            {
                if (!assigned.ContainsKey(playlist.Id))
                {
                    unmapped.Add(playlist);
                }
            }

            if (unmapped.Count == 0)
            {
                return assigned;
            }

            var seeds = new List<(Playlist Playlist, DateTime Sort)>();
            foreach (var playlist in unmapped)
            {
                seeds.Add((playlist, await GetPlaylistSortDateAsync(playlist, taApi).ConfigureAwait(false)));
            }

            var ordered = seeds
                .OrderBy(s => s.Sort)
                .ThenBy(s => s.Playlist.Name, StringComparer.Ordinal)
                .Select(s => s.Playlist)
                .ToList();

            var nextSeason = assigned.Count == 0 ? 1 : assigned.Values.Max() + 1;
            var added = new List<PlaylistSeasonMapEntry>();

            for (var i = 0; i < ordered.Count; i++)
            {
                var playlist = ordered[i];
                if (nextSeason >= Constants.UnsortedSeasonNumber)
                {
                    var remaining = ordered.Count - i;
                    _logger.LogError(
                        "Reached the maximum of {Maximum} playlist seasons. {Remaining} playlist(s) will be grouped into the {SeasonName} season, starting with {PlaylistName} ({PlaylistId}).",
                        Constants.UnsortedSeasonNumber - 1,
                        remaining,
                        Constants.UnsortedSeasonName,
                        playlist.Name,
                        playlist.Id);
                    break;
                }

                assigned[playlist.Id] = nextSeason;
                added.Add(new PlaylistSeasonMapEntry
                {
                    PlaylistId = playlist.Id,
                    SeasonNumber = nextSeason,
                    PlaylistName = Utils.SanitizePlaylistName(playlist.Name, playlist.Id)
                });

                nextSeason++;
            }

            if (added.Count > 0)
            {
                PersistNewEntries(added);
            }

            return assigned;
        }

        /// <summary>
        /// Gets the date used to order a playlist against the others.
        /// </summary>
        /// <remarks>
        /// TubeArchivist exposes no stable playlist date: <c>playlist_last_refresh</c> changes on
        /// every sync. The publish date of the playlist's first video is used instead, which is an
        /// approximation because playlist order is not strictly chronological.
        /// </remarks>
        private async Task<DateTime> GetPlaylistSortDateAsync(Playlist playlist, TubeArchivistApi taApi)
        {
            var firstEntry = playlist.Entries
                .Where(e => !string.IsNullOrEmpty(e.YoutubeId))
                .OrderBy(e => e.Index)
                .FirstOrDefault();

            if (firstEntry == null)
            {
                return DateTime.MaxValue;
            }

            try
            {
                var video = await taApi.GetVideo(firstEntry.YoutubeId).ConfigureAwait(false);
                if (video != null)
                {
                    return video.Published;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
                _logger.LogDebug(ex, "Could not resolve a sort date for playlist {PlaylistId}.", playlist.Id);
            }

            // Undated playlists sort last so they never displace dated ones.
            return DateTime.MaxValue;
        }

        /// <summary>
        /// Adds newly allocated playlist seasons to the persisted map.
        /// </summary>
        /// <remarks>
        /// <see cref="MediaBrowser.Common.Plugins.BasePlugin{TConfigurationType}.SaveConfiguration()"/>
        /// is called outside the lock: it writes to disk, and holding a monitor across it would
        /// block every other scan thread. That leaves a millisecond window in which saving the
        /// settings page could replace the configuration instance and drop these entries; they are
        /// simply reallocated on the next refresh. The window cannot be closed from here because
        /// Jellyfin's own config swap does not take this lock.
        /// </remarks>
        /// <param name="added">The entries to persist.</param>
        private void PersistNewEntries(List<PlaylistSeasonMapEntry> added)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                return;
            }

            lock (_mapLock)
            {
                var map = plugin.Configuration.PlaylistSeasonMap;

                foreach (var entry in added)
                {
                    // An entry for this playlist may already exist holding a value the map builder
                    // rejected as out of range. Skipping it would leave the bad value on disk to be
                    // re-read and re-rejected on every refresh, so the map would never heal.
                    for (var i = map.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(map[i].PlaylistId, entry.PlaylistId, StringComparison.Ordinal))
                        {
                            map.RemoveAt(i);
                        }
                    }

                    map.Add(entry);
                }
            }

            // Mutating the collection alone does not reach disk.
            plugin.SaveConfiguration();

            _logger.LogInformation("Assigned season numbers to {Count} new TubeArchivist playlists.", added.Count);
        }

        private void WarnOnDuplicateSeasonNames(Dictionary<int, string> seasonNames)
        {
            var duplicates = seasonNames
                .GroupBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var duplicate in duplicates)
            {
                _logger.LogWarning(
                    "Playlists share the season name {SeasonName}. They remain separate seasons (numbers {SeasonNumbers}).",
                    duplicate.Key,
                    string.Join(", ", duplicate.Select(d => d.Key.ToString(CultureInfo.InvariantCulture))));
            }
        }

        private void MarkRefreshFailed()
        {
            lock (_mapLock)
            {
                _lastRefreshFailed = true;

                // Retry on the next lookup rather than serving stale data for the full lifetime.
                _lastRefreshUtc = DateTime.UtcNow - CacheLifetime + TimeSpan.FromMinutes(1);
            }
        }
    }
}
