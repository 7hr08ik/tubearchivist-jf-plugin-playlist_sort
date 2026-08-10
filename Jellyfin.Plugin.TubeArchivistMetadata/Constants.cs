namespace Jellyfin.Plugin.TubeArchivistMetadata
{
    /// <summary>
    /// Class containing plugin constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Plugin name, as shown in the Jellyfin dashboard.
        /// </summary>
        /// <remarks>
        /// The <c>_PlaylistSort</c> suffix marks this fork so it is distinguishable from the
        /// upstream plugin at a glance. This is a display name only. It must not be confused with
        /// <see cref="ProviderName"/>, which is persisted against every item, nor with the assembly
        /// name, from which Jellyfin derives the configuration file name.
        /// </remarks>
        public const string PluginName = "TubeArchivistMetadata_PlaylistSort";

        /// <summary>
        /// Plugin GUID.
        /// </summary>
        public const string PluginGuid = "dc97d0c6-28b0-4242-afb4-5833ae1b3715";

        /// <summary>
        /// Providers name.
        /// </summary>
        public const string ProviderName = "TubeArchivist";

        /// <summary>
        /// Season number used to group videos which do not belong to any TubeArchivist playlist.
        /// </summary>
        /// <remarks>
        /// Season 0 cannot be used: Jellyfin's <c>SeasonMetadataService.BeforeSaveInternal</c>
        /// force-renames season 0 to the library's "Specials" display name.
        /// </remarks>
        public const int UnsortedSeasonNumber = 9000;

        /// <summary>
        /// Display name of the season grouping videos which do not belong to any TubeArchivist playlist.
        /// </summary>
        public const string UnsortedSeasonName = "Unsorted";
    }
}
