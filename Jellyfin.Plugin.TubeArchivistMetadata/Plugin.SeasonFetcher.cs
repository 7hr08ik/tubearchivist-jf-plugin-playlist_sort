using System;
using System.Linq;
using Jellyfin.Plugin.TubeArchivistMetadata.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TubeArchivistMetadata
{
    /// <summary>
    /// One time repair of the Season metadata fetcher on libraries created before this plugin
    /// provided one.
    /// </summary>
    /// <remarks>
    /// Kept out of <c>Plugin.cs</c> so that file stays close to upstream and rebases cheaply:
    /// only the <c>partial</c> keyword and the constructor call differ there.
    /// </remarks>
    public partial class Plugin
    {
        /// <summary>
        /// Enables this plugin's Season metadata fetcher on libraries where it is missing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A library stores the set of metadata fetchers it has enabled per item type. Libraries
        /// created before <see cref="Providers.SeasonMetadataProvider"/> existed have an empty list
        /// for Season, and <c>BaseItemManager.IsMetadataFetcherEnabled</c> returns
        /// <c>MetadataFetchers.Contains(name)</c> with no empty check, so an empty list disables
        /// every Season provider rather than allowing the defaults.
        /// </para>
        /// <para>
        /// The result is silent: seasons keep Jellyfin's "Season {N}" names, no provider runs and
        /// nothing is logged. Rather than making every upgrading user find the setting by hand, the
        /// fetcher is added on startup for libraries that already use this plugin for series.
        /// </para>
        /// </remarks>
        public void EnableSeasonMetadataFetcher()
        {
            if (Configuration.SeasonFetcherAutoEnabled)
            {
                return;
            }

            try
            {
                foreach (var virtualFolder in LibraryManager.GetVirtualFolders())
                {
                    TryEnableSeasonFetcher(virtualFolder);
                }

                // Run once, so removing the fetcher by hand later is respected.
                Configuration.SeasonFetcherAutoEnabled = true;
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                // Deliberately broad: this runs from the constructor, so an escaping exception
                // would abort plugin construction and disable metadata, sync and tasks entirely.
                // Season naming is a convenience and must never be able to do that.
                Logger.LogWarning(ex, "Could not enable the Season metadata fetcher. Season names will fall back to \"Season N\".");
            }
        }

        private void TryEnableSeasonFetcher(VirtualFolderInfo virtualFolder)
        {
            var options = virtualFolder.LibraryOptions;
            var seriesOptions = options?.GetTypeOptions(nameof(Series));

            // Only touch libraries already using this plugin, so unrelated libraries keep
            // whatever the user configured.
            if (options == null
                || seriesOptions?.MetadataFetchers == null
                || !seriesOptions.MetadataFetchers.Contains(Constants.ProviderName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var seasonOptions = options.GetTypeOptions(nameof(Season));
            if (seasonOptions == null)
            {
                // No stored Season entry means Jellyfin falls back to the global defaults, which
                // already include this provider.
                return;
            }

            var fetchers = seasonOptions.MetadataFetchers ?? Array.Empty<string>();
            if (fetchers.Contains(Constants.ProviderName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            seasonOptions.MetadataFetchers = fetchers.Append(Constants.ProviderName).ToArray();
            seasonOptions.MetadataFetcherOrder = (seasonOptions.MetadataFetcherOrder ?? Array.Empty<string>())
                .Append(Constants.ProviderName)
                .ToArray();

            if (!SaveLibraryOptions(virtualFolder, options))
            {
                Logger.LogWarning(
                    "Enabled the {ProviderName} Season metadata fetcher on library {LibraryName} in memory only; the library could not be resolved to save it.",
                    Constants.ProviderName,
                    virtualFolder.Name);
                return;
            }

            Logger.LogInformation(
                "Enabled the {ProviderName} Season metadata fetcher on library {LibraryName}. Run a metadata refresh with \"Replace all metadata\" to apply playlist season names.",
                Constants.ProviderName,
                virtualFolder.Name);
        }

        /// <summary>
        /// Writes a library's options back to disk.
        /// </summary>
        /// <remarks>
        /// Mutating the <see cref="LibraryOptions"/> object alone only updates
        /// <c>CollectionFolder</c>'s static in-memory cache, which any library rename clears - the
        /// change would silently disappear mid-session. <c>UpdateLibraryOptions</c> writes
        /// <c>options.xml</c> and raises the event the rest of Jellyfin expects.
        /// Virtual so tests can observe the save without a real <c>CollectionFolder</c>.
        /// </remarks>
        /// <param name="virtualFolder">The library whose options changed.</param>
        /// <param name="options">The mutated options to persist.</param>
        /// <returns><c>true</c> when the options were persisted.</returns>
        protected virtual bool SaveLibraryOptions(VirtualFolderInfo virtualFolder, LibraryOptions options)
        {
            if (Guid.TryParse(virtualFolder.ItemId, out var itemId)
                && LibraryManager.GetItemById(itemId) is CollectionFolder collectionFolder)
            {
                collectionFolder.UpdateLibraryOptions(options);
                return true;
            }

            return false;
        }
    }
}
