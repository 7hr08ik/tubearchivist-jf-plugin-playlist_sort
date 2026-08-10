namespace Jellyfin.Plugin.TubeArchivistMetadata.Configuration
{
    /// <summary>
    /// A persisted association between a TubeArchivist playlist and the Jellyfin season number
    /// used to represent it.
    /// </summary>
    /// <remarks>
    /// Jellyfin groups episodes into seasons using <c>ParentIndexNumber</c>, which is an integer,
    /// while TubeArchivist identifies playlists by string id. This entry persists the mapping so
    /// season numbers stay stable across restarts and metadata refreshes.
    /// A plain dictionary is not used because Jellyfin's XML configuration serializer does not
    /// reliably round-trip dictionary types.
    /// </remarks>
    public class PlaylistSeasonMapEntry
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistSeasonMapEntry"/> class.
        /// </summary>
        /// <remarks>
        /// A parameterless constructor is required by the XML serializer used to persist the
        /// plugin configuration.
        /// </remarks>
        public PlaylistSeasonMapEntry()
        {
            PlaylistId = string.Empty;
            PlaylistName = string.Empty;
        }

        /// <summary>
        /// Gets or sets the TubeArchivist playlist id.
        /// </summary>
        public string PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin season number representing the playlist.
        /// </summary>
        public int SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the last known TubeArchivist playlist name.
        /// </summary>
        /// <remarks>
        /// Stored so seasons keep a usable label when TubeArchivist is unreachable.
        /// </remarks>
        public string PlaylistName { get; set; }
    }
}
