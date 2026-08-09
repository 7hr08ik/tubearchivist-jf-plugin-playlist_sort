namespace Jellyfin.Plugin.TubeArchivistMetadata.TubeArchivist
{
    /// <summary>
    /// The TubeArchivist playlist a video belongs to, resolved to the Jellyfin season
    /// representing that playlist.
    /// </summary>
    /// <param name="PlaylistId">TubeArchivist playlist id.</param>
    /// <param name="SeasonName">Sanitized playlist name, used as the Jellyfin season name.</param>
    /// <param name="SeasonNumber">Jellyfin season number representing the playlist.</param>
    /// <param name="Index">Position of the video within the playlist.</param>
    public sealed record PlaylistAssignment(
        string PlaylistId,
        string SeasonName,
        int SeasonNumber,
        int Index);
}
