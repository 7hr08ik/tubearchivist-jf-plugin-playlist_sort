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

> **Existing libraries:** episodes already imported into year-based seasons will **not** move.
> `ParentIndexNumber` is sticky in Jellyfin — see "Turning the setting off" below for the measured
> result and the conversion procedure. A brand-new library groups by playlist immediately.

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

**Measured, not assumed.** On a 116-episode library the setting was disabled and a full
"Replace all metadata" refresh was run to completion. **Zero episodes changed season**, and all 30
seasons kept their playlist names. This holds even though `MergeData` does gate on
`replaceData || !target.ParentIndexNumber.HasValue`, because `MetadataService` seeds the working
item with the *existing* `ParentIndexNumber` before any provider runs, so the value is never empty
and the plugin is not asked to supply a new one.

### The same limit applies when first enabling the feature

This is the important consequence for existing installations. Because `ParentIndexNumber` is sticky
in *both* directions, enabling the setting and refreshing will **not** move episodes that Jellyfin
has already imported into year-based seasons. Those episodes keep their existing season.

New episodes imported after enabling the setting are grouped by playlist correctly, and a library
scanned for the first time with the setting already on is grouped entirely by playlist — verified on
a brand-new library.

### 3. Converting an existing library: run the "Rebuild playlist seasons" task

**Dashboard → Scheduled Tasks → Rebuild playlist seasons → run manually.**

The task clears each episode's stored season number in libraries matching the configured collection
title, then refreshes them so the current setting decides the season afresh. It works in both
directions: enabling the setting regroups a year-based library by playlist, and disabling it returns
episodes to upload-year seasons.

It has no automatic trigger. Regrouping a library is disruptive enough that it should happen when you
choose, not as a side effect of saving a settings page.

**It deletes nothing.** Only the season number field on each episode is modified. Verified on a
116-episode library: media files untouched, watch state and resume positions preserved, no orphaned
seasons left behind. Jellyfin removes the now-empty old seasons by itself.

Season *names* may briefly read "Season Unknown" immediately afterwards. That is a cached field on
the episode and the next library refresh restores the correct name.

**If you keep `.nfo` files next to your media, they win.** An `.nfo` containing `<season>` is a local
metadata source, and Jellyfin prefers it over anything this plugin supplies, so those episodes stay
where the file says regardless of the setting or the task. Measured: an episode with
`<season>1</season>` in its `.nfo` ignored the rebuild entirely, then moved correctly the moment the
file was removed. Either delete the `.nfo` files, remove their `<season>` element, or disable the
"Nfo" metadata reader for the library.

The task itself will **not** modify or create `.nfo` files. It saves below Jellyfin's "manual edit"
threshold precisely so the NFO savers stay out of the way — otherwise Jellyfin would rewrite any
existing `.nfo` while the season number was cleared, silently stripping `<season>` from it. Verified
against a hand-written `.nfo`: byte-identical after a full run.

> **Deleting the seasons by hand does not work** — worth stating, because it is the obvious approach.
> Jellyfin derives a season's item id deterministically from its series and index number, and
> deleting a season does not clear its episodes' stored season number, so a rescan recreates exactly
> the same seasons. Measured: 30 seasons deleted, 0 episodes moved.

> ⚠️ **Never delete a Series or Episode to reset state.** In Jellyfin, `DELETE /Items/{id}` on a
> file-backed item **deletes the media from disk**, and there is no trash or recycle bin. The task
> above avoids deletion entirely for exactly this reason.

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
