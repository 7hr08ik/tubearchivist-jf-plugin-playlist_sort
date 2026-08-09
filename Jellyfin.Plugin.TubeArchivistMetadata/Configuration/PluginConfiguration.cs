using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.TubeArchivistMetadata.Utilities;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TubeArchivistMetadata.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        private ILogger _logger;
        private string _tubeArchivistUrl;
        private string _tubeArchivistApiKey;
        private HashSet<string> _jfUsernamesTo;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            if (Plugin.Instance == null)
            {
                throw new DataException("Uninitialized plugin!");
            }
            else
            {
                _logger = Plugin.Instance.Logger;
            }

            CollectionTitle = string.Empty;
            _tubeArchivistUrl = string.Empty;
            _tubeArchivistApiKey = string.Empty;
            MaxDescriptionLength = 500;
            JFTAProgressSync = false;
            JFUsernameFrom = string.Empty;
            TAJFProgressSync = false;
            JFTAPlaylistsSync = false;
            JFTAPlaylistsDelete = false;
            TAJFPlaylistsSync = false;
            TAJFPlaylistsDelete = false;
            _jfUsernamesTo = new HashSet<string>();
            TAJFProgressTaskInterval = 60;
            JFTAPlaylistsSyncTaskInterval = 60;
            TAJFPlaylistsSyncTaskInterval = 60;
            SortSeasonsByPlaylist = false;
            SeasonFetcherAutoEnabled = false;
            PlaylistSeasonMap = new Collection<PlaylistSeasonMapEntry>();
        }

        /// <summary>
        /// Gets or sets TubeArchivist collection display name.
        /// </summary>
        public string CollectionTitle { get; set; }

        /// <summary>
        /// Gets or sets TubeArchivist URL.
        /// </summary>
        public string TubeArchivistUrl
        {
            get
            {
                return _tubeArchivistUrl;
            }

            set
            {
                if (value.StartsWith("http://", StringComparison.CurrentCulture) || value.StartsWith("https://", StringComparison.CurrentCulture))
                {
                    _tubeArchivistUrl = Utils.SanitizeUrl(value);
                }
                else
                {
                    _logger.LogInformation("{Message}", "Given TubeArchivist URL contains no schema. Adding http://...");
                    _tubeArchivistUrl = Utils.SanitizeUrl("http://" + value);
                }

                Plugin.Instance?.LogTAApiConnectionStatus();
            }
        }

        /// <summary>
        /// Gets or sets TubeArchivist API key.
        /// </summary>
        public string TubeArchivistApiKey
        {
            get
            {
                return _tubeArchivistApiKey;
            }

            set
            {
                _tubeArchivistApiKey = value;
                Plugin.Instance?.LogTAApiConnectionStatus();
                Plugin.Instance?.UpdateAuthorizationHeader(value);
            }
        }

        /// <summary>
        /// Gets or sets maximum series and episodes overviews length.
        /// </summary>
        public int MaxDescriptionLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable TA->JF playback progress synchronization.
        /// </summary>
        public bool TAJFProgressSync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable JF->TA playback progress synchronization.
        /// </summary>
        public bool JFTAProgressSync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable JF->TA playlists synchronization.
        /// </summary>
        public bool JFTAPlaylistsSync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to delete playlists from TA when not found on JF.
        /// </summary>
        public bool JFTAPlaylistsDelete { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable TA->JF playlists synchronization.
        /// </summary>
        public bool TAJFPlaylistsSync { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to delete playlists from JF when not found on TA.
        /// </summary>
        public bool TAJFPlaylistsDelete { get; set; }

        /// <summary>
        /// Gets or sets the playback progress owner Jellyfin username to synchronize data to TubeArchivist.
        /// </summary>
        public string JFUsernameFrom { get; set; }

        /// <summary>
        /// Gets or sets the playback progress owners Jellyfin usernames to synchronize data from TubeArchivist.
        /// </summary>
        public string JFUsernamesTo
        {
            get
            {
                _logger.LogInformation("JFUsernamesTo configured: {Message}", string.Join(", ", _jfUsernamesTo));
                return string.Join(", ", _jfUsernamesTo);
            }

            set
            {
                // Clear existing usernames
                _jfUsernamesTo.Clear();

                // Split by comma, then trim each part to remove leading/trailing spaces
                foreach (var username in value.Split(','))
                {
                    var trimmedUsername = username.Trim();
                    if (!string.IsNullOrEmpty(trimmedUsername))
                    {
                        _jfUsernamesTo.Add(trimmedUsername);
                    }
                }

                _logger.LogInformation("Set JFUsernamesTo to: {Message}", string.Join(", ", _jfUsernamesTo));
            }
        }

        /// <summary>
        /// Gets or sets the interval in seconds at which the TubeArchivist to Jellyfin playback progress synchronization task should run.
        /// It requires Jellyfin server restart to take effect.
        /// </summary>
        public int TAJFProgressTaskInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval in seconds at which the Jellyfin to TubeArchivist playlists synchronization task should run.
        /// It requires Jellyfin server restart to take effect.
        /// </summary>
        public int JFTAPlaylistsSyncTaskInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval in seconds at which the TubeArchivist to Jellyfin playlists synchronization task should run.
        /// It requires Jellyfin server restart to take effect.
        /// </summary>
        public int TAJFPlaylistsSyncTaskInterval { get; set; }

        /// <summary>
        /// Gets or sets the preferred numbering scheme for episodes (index number) in Jellyfin.
        /// </summary>
        public NumberingScheme EpisodeNumberingScheme { get; set; } = NumberingScheme.Default;

        /// <summary>
        /// Gets or sets a value indicating whether to group episodes into seasons by TubeArchivist
        /// playlist instead of by upload year.
        /// </summary>
        /// <remarks>
        /// Changing this setting requires a "Refresh metadata" with "Replace all metadata" enabled:
        /// Jellyfin only overwrites an existing season name when replacing metadata.
        /// </remarks>
        public bool SortSeasonsByPlaylist { get; set; }

        /// <summary>
        /// Gets the persisted TubeArchivist playlist to Jellyfin season number associations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Populated automatically; not user editable. Entries are append-only so season numbers
        /// remain stable once assigned.
        /// </para>
        /// <para>
        /// <see cref="JsonIncludeAttribute"/> is required and must not be removed. Jellyfin persists
        /// plugin configuration as XML, but the plugin configuration API endpoint deserializes the
        /// posted body with System.Text.Json, which ignores non-public setters. Without this
        /// attribute the collection is dropped whenever the configuration page is saved, and
        /// <c>BasePlugin.UpdateConfiguration</c> then writes the emptied collection back to disk.
        /// A public setter is not an option because it violates CA2227.
        /// This is unrelated to the Newtonsoft usage of the TubeArchivist API models.
        /// </para>
        /// </remarks>
        [JsonInclude]
        public Collection<PlaylistSeasonMapEntry> PlaylistSeasonMap { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the one time migration which enables this
        /// plugin's Season metadata fetcher on existing libraries has already run.
        /// </summary>
        /// <remarks>
        /// Libraries created before the Season provider existed store an empty Season fetcher list,
        /// which Jellyfin treats as "all disabled" rather than "use the defaults". The migration
        /// repairs that once; keeping this flag means a fetcher removed by hand afterwards stays
        /// removed instead of being re-added on every restart.
        /// </remarks>
        public bool SeasonFetcherAutoEnabled { get; set; }

        /// <summary>
        /// Gets the playback progress owners Jellyfin usernames to synchronize data from TubeArchivist.
        /// </summary>
        /// <returns>An array of usernames.</returns>
        public HashSet<string> GetJFUsernamesToArray()
        {
            return _jfUsernamesTo;
        }
    }
}
