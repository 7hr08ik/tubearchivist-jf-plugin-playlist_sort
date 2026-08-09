# Release notes — 1.5.0.0 — Sort seasons by TubeArchivist playlist

Upgrades in place over 1.4.4.0. The plugin GUID is unchanged, so replacing the DLL and restarting
Jellyfin is an upgrade, not a second install — there is nothing to uninstall first. Existing
settings are preserved; the new options default to off.

## What's new

Seasons can now be grouped and named after **TubeArchivist playlists** instead of upload years.

Previously a channel's videos were split into seasons by the year they were published, which mixed
unrelated playlists together — a single year could contain episodes from three different Let's Play
series. With this release, each TubeArchivist playlist becomes its own Jellyfin season, named exactly
as it is in TubeArchivist:

```
Before                          After
──────                          ─────
Season 2012                     Lets Play Skyrim (Chapter 1) : Orc Warlock
Season 2013                     Lets Play Skyrim (Chapter 2) : Orc Warlock
Season 2014                     Let's Play Falskaar (Skyrim)
...                             Let's Play Beyond Skyrim - Bruma
                                S.T.A.L.K.E.R. 2 : Heart of Chornobyl
```

Seasons are ordered chronologically by the publish date of each playlist's first video, so a channel
reads in the order it was made. Once assigned, a playlist keeps its season number permanently — new
playlists are appended at the end and never renumber existing seasons.

### Enabling it

1. Replace `plugins/TubeArchivistMetadata/Jellyfin.Plugin.TubeArchivistMetadata.dll` with the new build.
2. **Restart Jellyfin.** The Season metadata fetcher repair below runs at startup only.
3. Check the log for `Enabled the TubeArchivist Season metadata fetcher on library <name>`.
4. **Dashboard → Plugins → TubeArchivist Metadata → "Group seasons by TubeArchivist playlist"** → Save.
5. On the library: **Refresh metadata** → **"Replace all metadata"**.

The setting must be enabled *before* the refresh. Note that step 5 is a metadata refresh, not a
library scan — a scan only looks for new files and will not re-season episodes already imported.

Also new: an **Episode numbering scheme** option of *Playlist index*, which numbers episodes by their
position within the playlist rather than by date.

Videos that belong to no playlist are collected into a single season named **Unsorted**.

---

## Read this before upgrading

### 1. Existing libraries: the Season metadata fetcher is enabled automatically

This release adds a new Season metadata provider. Jellyfin stores the list of enabled metadata
fetchers **per library, per item type**, and a library created before this release has an *empty*
Season list.

That is not harmless. Jellyfin's `BaseItemManager.IsMetadataFetcherEnabled` does:

```csharp
return libraryTypeOptions.MetadataFetchers.Contains(name, StringComparison.OrdinalIgnoreCase);
```

There is no empty check, so an empty list disables **every** Season provider rather than falling back
to the defaults. The provider would simply never run: seasons keep their `Season 1`, `Season 2` names,
nothing appears in the log, and no error is raised.

**The plugin now fixes this for you.** On first startup after upgrading, it checks every library
that already uses TubeArchivist as its *Series* fetcher and adds the Season fetcher where it is
missing, saving the change to the library's `options.xml`:

```
[INF] Enabled the TubeArchivist Season metadata fetcher on library YouTube.
      Run a metadata refresh with "Replace all metadata" to apply playlist season names.
```

Libraries that do not use this plugin are left untouched.

**This runs once, not on every startup.** If you remove the Season fetcher yourself afterwards, it
stays removed — the plugin will not put it back. To verify or change it by hand:
**Libraries → *your library* → Manage → Metadata downloaders (Season) → TubeArchivist**.

If the repair cannot run for any reason it logs a warning and the plugin continues normally; only
season naming is affected.

### 2. Turning the setting off does not restore `Season N` names

Jellyfin's `MetadataService.MergeData` only overwrites a field when the target is empty or the
refresh is a full replace:

```csharp
if (replaceData || string.IsNullOrEmpty(target.Name))
```

Once a season is named after a playlist, that name is stored on the item. Disabling the setting stops
the plugin contributing any name — verified: the provider returns `HasMetadata = false` for every
season — but Jellyfin keeps the name it already has. A refresh alone will not revert it.

The same applies to season *membership*: an episode's `ParentIndexNumber` is sticky, so a refresh
cannot move episodes back into year-based seasons.

**To fully revert**, disable the setting and then remove the affected seasons so Jellyfin recreates
them on the next scan.

> ⚠️ **Do not delete the Series to reset it.** In Jellyfin, `DELETE /Items/{id}` on a Series, Season
> or Episode **deletes the media files from disk**. There is no trash or recycle bin. Delete only the
> Season entities, or point a throwaway library at a copy of the media.

---

## Also in this release

**Fixed: TubeArchivist playlists were fetched twice.** `GetPlaylists()` continued paging from
`Paginate.CurrentPage + 1`, but TubeArchivist treats `?page=0` and `?page=1` as the same first page,
so the first page was retrieved twice and every playlist was duplicated (50 results for 29 real
playlists). This also affected the existing playlist-sync tasks.

**Fixed: redirects during playlist paging.** The base URL was prefixed onto an already-absolute
`Location` header, producing an invalid URI. Redirects are now followed as-is, matching the channel
and video endpoints.

---

## Notes and limitations

- **Season names are read from TubeArchivist**, with whitespace normalised and a 120-character cap.
  Punctuation is preserved so seasons read as they do in TubeArchivist.
- **A video in several playlists** is placed in the lowest-numbered matching season, chosen
  deterministically so it does not move between refreshes.
- **If TubeArchivist is unreachable**, episodes fall back to upload-year grouping and a warning is
  logged. They are not swept into *Unsorted*, because that would be sticky and unrepairable.
- **Playlist data is cached for 30 minutes.** A newly created TubeArchivist playlist may take that
  long to appear, or restart Jellyfin to pick it up immediately.
- **Season 0 is never used.** Jellyfin reserves it for Specials and force-renames it.
- The playlist-to-season map is stored in the plugin configuration and survives restarts. Season
  numbers outside the valid range are rejected and reallocated automatically.
