namespace Jellyfin.Plugin.TubeArchivistMetadata.Configuration;

/// <summary>
/// The NumberingScheme.
/// </summary>
public enum NumberingScheme
{
    /// <summary>
    /// Default (no numbering).
    /// </summary>
    Default,

    /// <summary>
    /// YYYYMMDD (e.g. 20250804 for August 4th, 2025).
    /// </summary>
    YYYYMMDD,

    /// <summary>
    /// The video position within its TubeArchivist playlist.
    /// Falls back to <see cref="Default"/> when the video does not belong to a playlist.
    /// </summary>
    PlaylistIndex,
}
