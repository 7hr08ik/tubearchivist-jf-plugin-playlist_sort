using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TubeArchivistMetadata.Tasks
{
    /// <summary>
    /// Task which re-evaluates the season every episode belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An episode's <c>ParentIndexNumber</c> is sticky: <c>MetadataService</c> copies the stored
    /// value onto the working item before any provider runs, so the value is never empty and
    /// providers are never asked to supply a new one. Even a "Replace all metadata" refresh
    /// therefore leaves existing episodes in whatever season they were first imported into, which
    /// means toggling <c>SortSeasonsByPlaylist</c> has no effect on an existing library.
    /// </para>
    /// <para>
    /// Clearing the value first is what breaks the cycle. Once it is null the refresh has nothing to
    /// preserve and the episode provider assigns a season according to the current configuration.
    /// Deleting the seasons instead does not work: Jellyfin derives a season's id from its series
    /// and index number, and deleting one does not clear its episodes, so a rescan recreates
    /// exactly the same seasons.
    /// </para>
    /// <para>
    /// Nothing is deleted. Only <c>ParentIndexNumber</c> is modified, so media files, watch state
    /// and resume positions are unaffected, and Jellyfin removes the emptied seasons itself.
    /// </para>
    /// <para>
    /// There is no default trigger. Regrouping a library is disruptive and must be an explicit
    /// choice rather than a side effect of saving the settings page.
    /// </para>
    /// <para>
    /// Episodes are processed one at a time. Each refresh reaches TubeArchivist, so running them
    /// concurrently would multiply the load on it and interleave badly with a scheduled library
    /// scan. The cost is duration: expect roughly a second per episode.
    /// </para>
    /// <para>
    /// On a library large enough to run longer than <c>PlaylistCache</c>'s lifetime, the cache can
    /// refresh part way through. If playlists changed in TubeArchivist during the run, episodes
    /// handled before and after that point can be grouped against different playlist data. Running
    /// the task again once TubeArchivist has settled resolves it.
    /// </para>
    /// </remarks>
    public class RebuildPlaylistSeasonsTask : IScheduledTask
    {
        private readonly ILogger<Plugin> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="RebuildPlaylistSeasonsTask"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="providerManager">Provider manager.</param>
        /// <param name="fileSystem">File system.</param>
        public RebuildPlaylistSeasonsTask(
            ILogger<Plugin> logger,
            ILibraryManager libraryManager,
            IProviderManager providerManager,
            IFileSystem fileSystem)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
        }

        /// <inheritdoc/>
        public string Name => "Rebuild playlist seasons";

        /// <inheritdoc/>
        public string Description => "Re-evaluates which season each episode belongs to, applying the current \"Group seasons by TubeArchivist playlist\" setting to episodes which were already imported. Run this after changing that setting. Nothing is deleted.";

        /// <inheritdoc/>
        public string Category => "TubeArchivistMetadata";

        /// <inheritdoc/>
        public string Key => "RebuildPlaylistSeasonsTask";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            progress.Report(0);

            var collectionTitle = Plugin.Instance?.Configuration.CollectionTitle;
            if (string.IsNullOrEmpty(collectionTitle))
            {
                _logger.LogWarning("No collection title is configured, so there is no library to rebuild.");
                progress.Report(100);
                return;
            }

            var start = DateTime.Now;
            var episodes = GetEpisodes(collectionTitle);
            if (episodes.Count == 0)
            {
                _logger.LogWarning(
                    "Found no episodes in the collection {CollectionTitle}. Check that the collection title matches the library name.",
                    collectionTitle);
                progress.Report(100);
                return;
            }

            _logger.LogInformation(
                "Rebuilding seasons for {EpisodeCount} episode(s) in {CollectionTitle}. Grouping by playlist is {State}.",
                episodes.Count,
                collectionTitle,
                Plugin.Instance!.Configuration.SortSeasonsByPlaylist ? "enabled" : "disabled");

            var processed = 0;
            var cleared = 0;
            var failed = 0;

            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Clearing the stored season is the whole point of the task: the refresh below
                    // only reassigns a season when there is no existing value to preserve.
                    if (episode.ParentIndexNumber.HasValue)
                    {
                        episode.ParentIndexNumber = null;

                        // Calling the injected manager rather than BaseItem.UpdateToRepositoryAsync,
                        // which resolves the parent through the static BaseItem.LibraryManager.
                        //
                        // MetadataDownload, never MetadataEdit. ItemUpdateType is a [Flags] enum
                        // whose highest value is MetadataEdit, and ProviderManager treats
                        // "updateType >= MetadataEdit" as a manual edit: it then rewrites an .nfo
                        // beside the media whenever one already exists, even with local metadata
                        // saving switched off. That write would happen while ParentIndexNumber is
                        // still null, stripping <season> from the user's file. Anything below
                        // MetadataEdit skips the savers, and persistence is unaffected either way
                        // because UpdateItemsAsync saves to the database regardless of the reason.
                        await _libraryManager.UpdateItemAsync(
                            episode,
                            episode.GetParent(),
                            ItemUpdateType.MetadataDownload,
                            cancellationToken).ConfigureAwait(false);
                        cleared++;
                    }

                    var refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                        ImageRefreshMode = MetadataRefreshMode.None,
                        ReplaceAllMetadata = true,
                        ReplaceAllImages = false
                    };

                    await _providerManager.RefreshSingleItem(episode, refreshOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One unreadable episode must not abandon the rest of the library part way
                    // through, which would leave it split across two grouping schemes.
                    failed++;
                    _logger.LogError(ex, "Could not rebuild the season for {EpisodeName}.", episode.Name);
                }

                processed++;
                progress.Report(processed * 100.0 / episodes.Count);
            }

            _logger.LogInformation(
                "Rebuilt {Cleared} episode season(s) with {Failed} failure(s) in {Elapsed}. Season names refresh on the next library scan.",
                cleared,
                failed,
                DateTime.Now - start);

            progress.Report(100);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Intentionally empty. This task only ever runs when started by hand.
        /// </remarks>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        /// <summary>
        /// Finds every episode stored under the configured collection's folders.
        /// </summary>
        /// <remarks>
        /// Scoping deliberately uses the library's physical paths rather than
        /// <c>InternalItemsQuery.AncestorIds</c>. A <c>CollectionFolder</c> lives under
        /// <c>/config/root</c> and is not part of an episode's ancestor chain, which runs
        /// Series to media Folder to AggregateFolder. Filtering by the collection's id therefore
        /// matches nothing, and Jellyfin treats an empty ancestor filter as "no filter" and returns
        /// every episode on the server - including items left behind by removed libraries.
        /// </remarks>
        /// <param name="collectionTitle">The configured collection title.</param>
        /// <returns>The episodes to rebuild.</returns>
        private IReadOnlyList<Episode> GetEpisodes(string collectionTitle)
        {
            var locations = _libraryManager.GetVirtualFolders()
                .Where(f => string.Equals(f.Name, collectionTitle, StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => f.Locations ?? Array.Empty<string>())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();

            if (locations.Length == 0)
            {
                _logger.LogWarning(
                    "Library {CollectionTitle} was not found, or has no folders configured.",
                    collectionTitle);
                return Array.Empty<Episode>();
            }

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                Recursive = true
            });

            var episodes = items
                .OfType<Episode>()
                .Where(e => !string.IsNullOrEmpty(e.Path)
                    && locations.Any(l => e.Path.StartsWith(
                        l.EndsWith(Path.DirectorySeparatorChar) ? l : l + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogDebug(
                "Matched {EpisodeCount} episode(s) under {LocationCount} folder(s) of {CollectionTitle}.",
                episodes.Count,
                locations.Length,
                collectionTitle);

            return episodes;
        }
    }
}
