<h1 align="center">Jellyfin TubeArchivist Plugin</h1>

## Forked for Playlist Sorting

--

## The What and Why

This fork, was made as a personal AI Slop project. 

I love the idea behind TubeArchivist, and having it all working automagically through Jellyfin. But I want the videos sorted by Playlist, NOT by year. I watch a couple of YouTubers that make LetPlay's, and with this plugin things weren't sorted right for me. I want seasons Named as the playlist is named.

So I vibe slopped this with my own [Opencode Setup](https://github.com/7hr08ik/AMPG-Opencode-and-Friends), on locally hosted AI and some free APIs.

All this does, is add the ability to sort by Playlist instead of Year in Jellyfin. I updated the Readme to reflect my changes.

--

## About

<p>This plugin adds the metadata provider for <a href="https://www.tubearchivist.com/">TubeArchivist</a>, offering improved flexibility and native integration with Jellyfin compared to previous solutions.</p>

## How it works
The media organization is a `Shows` collection, where each channel is a show and its videos are the episodes, organized in seasons either by year (the default) or by TubeArchivist playlist.<br>
The plugin interacts with TubeArchivist APIs to fetch videos and channels metadata.

### Features
- Add metadata for videos (episodes)
- Add metadata for channels (shows)
- Add images for videos (episodes), ie. thumb images
- Add images for channels (shows), ie. thumb, tvart and banner images
- Organize videos (episodes) by year (seasons)
- **Organize videos (episodes) by TubeArchivist playlist (seasons)** - see [Playlist seasons](#playlist-seasons)
- Bidirectional playback progress synchronization
- Bidirectional playlists synchronization

> [!WARNING]
> Enabling synchronization in both directions you can run in race conditions and unexpected results.

## Installation

> [!IMPORTANT]
> This fork is **not** published to a plugin repository and has no GitHub releases. Build it from
> source and copy the DLL in. It appears in the dashboard as
> **TubeArchivist Metadata-playlist_sort** so you can tell it apart from the upstream plugin.

### Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/7hr08ik/tubearchivist-jf-plugin.git
cd tubearchivist-jf-plugin
dotnet publish Jellyfin.Plugin.TubeArchivistMetadata -c Release -o bin
```

### Install

1. Copy **only** `bin/Jellyfin.Plugin.TubeArchivistMetadata.dll` into the
   `plugins/TubeArchivistMetadata` folder of your Jellyfin installation, creating it if needed.

   `dotnet publish` also writes ~30 Jellyfin dependency DLLs into `bin/`. Do not copy those - they
   shadow the server's own assemblies and can stop the plugin loading.

2. Match the ownership and permissions Jellyfin uses for its other plugin files.
3. Restart Jellyfin.

### Upgrading from the upstream plugin

This fork keeps the upstream plugin GUID, so replacing the DLL is an **in-place upgrade**. There is
nothing to uninstall, and your existing settings are preserved. Keep the folder name
`TubeArchivistMetadata` - installing alongside the original rather than over it would leave Jellyfin
loading two plugins with the same GUID.

After restarting, confirm the dashboard shows **TubeArchivist Metadata-playlist_sort**.

See [Playlist seasons](#playlist-seasons) for how to enable playlist season grouping and how to
convert a library that is already grouped by year.

## Configuration
<p>This plugin requires that you have already an instance of TubeArchivist up and running.</p>
Once installed, you have to configure the following parameters in the plugin configuration:
<ul>
    <li>Collection display name</li>
    <li>TubeArchivist instance address</li>
    <li>TubeArchivist API key</li>
    <li>Overviews length (channels and videos descriptions)</li>
    <li>Group seasons by TubeArchivist playlist - see <a href="#playlist-seasons">Playlist seasons</a></li>
    <li>Playback synchronization settings discussed in the <a href="#playback-synchronization">Playback synchronization</a> paragraph</li>
</ul>

![Plugin configuration](https://github.com/tubearchivist/tubearchivist-jf-plugin/assets/31162436/d34464ea-ddfb-44b3-9d3e-5d5974956c58)


## Use the plugin
<p>Using the plugin is very simple. Let's start from the beginning:</p>

_NOTE: If you are using Docker containers, it is important to mount the TubeArchivist media path into Jellyfin container as **read-only**, in order to avoid possible operations on the media files that will break TubeArchivist._ <br>
1. Go to `Dashboard -> Libraries` and add a media library
2. In the form select `Shows` as Content type, set a display name for the library and set the TubeArchivist media folder in the `Folders` section
![Add library](https://github.com/tubearchivist/tubearchivist-jf-plugin/assets/31162436/1eca534e-0929-4134-8587-3cff0009f618)

4. Scrolling down, uncheck all metadata and image providers except `TubeArchivist`. This fork **does** provide a Season metadata provider, so leave `TubeArchivist` enabled under Seasons as well - it is what names seasons after playlists. If the option is missing there, the plugin enables it for you on the next restart
5. Save and come back to Home, you will see the newly added library. Jellyfin will have executed the metadata fetching for you after the collection creation and then you will see the metadata and the images of channels and videos


## Synchronization
This plugin has different bidirectional sycnhronization features, that can be configured in the specific section in the plugin configuration page:
![Synchronization settings](https://github.com/user-attachments/assets/b0bb556b-fce3-4a3e-bc6c-b0a2b482cedc)

### Playback synchronization
<p>Starting from v1.3.1 this plugin offers playback progress and watched status bidirectional synchronization, but you can choose to enable only a one way synchronization (Jellyfin -> TubeArchivist or TubeArchivist -> Jellyfin) too.</p>

#### Jellyfin -> TubeArchivist playback synchronization
<p>This kind of synchronization is done listening for progress and watched status changes while playing the videos for the specified users.<br>Furthermore, there is a task that runs at Jellyfin startup to synchronize the whole library.</p>
<p>In the text field you can specify one Jellyfin username to synchronize data of to TubeArchivist.</p>

#### TubeArchivist -> Jellyfin playback synchronization
<p>This kind of synchronization is done using a Jellyfin scheduled task that regularly synchronizes data from TubeArchivist API to Jellyfin.</p>
<p>In the text field you can specify one or more Jellyfin usernames to update data for.</p>

### Playlists synchronization
<p>Starting from v.1.4.1 this plugin offers playlists bidirectional synchronization, but you can choose to enable only a one way synchronization (Jellyfin -> TubeArchivist or TubeArchivist -> Jellyfin) too.</p>

#### Jellyfin -> TubeArchivist playlists synchronization
<p>There is a task that retrieves playlists and recreates them on TubeArchivist with the videos in the same order. Please note that playlists can also have videos not beloging from TubeArchivist, they will be simply ignored, so you won't find them on TubeArchivist playlist.</p>
<p>It is present also a setting to automatically delete playlists from TubeArchivist when they are no more available on Jellyfin.</p>

#### TubeArchivist -> Jellyfin playlists synchronization
<p>There is a task that retrieves playlists from TubeArchivist and recreates them on Jellyfin with videos in the same order.</p>
<p>It is present, also in this case, a setting to automatically delete playlists from Jellyfin when they are no more present on TubeArchivist, but beware that the will be deleted also if they contain videos not beloning to TubeArchivist.</p>

> [!CAUTION]
> Pay attention when you enable the automatic deletion options, be sure that is your wanted behavior, especially when playlists contain also other videos not belonging from TubeArchivist, playlists removed won't be available again, there's no undo!


## Playlist seasons

*This section covers the fork's addition. Everything above and below it is upstream behaviour.*

By default a channel's videos are split into seasons by the year they were published, which mixes
unrelated playlists together - a single year can contain episodes from three different Let's Play
series. With **Group seasons by TubeArchivist playlist** enabled, each playlist becomes its own
season, named as it is in TubeArchivist:

```
Before                          After
------                          -----
Season 2012                     Lets Play Skyrim (Chapter 1) : Orc Warlock
Season 2013                     Lets Play Skyrim (Chapter 2) : Orc Warlock
Season 2014                     Let's Play Falskaar (Skyrim)
...                             Let's Play Beyond Skyrim - Bruma
                                S.T.A.L.K.E.R. 2 : Heart of Chornobyl
```

Seasons are ordered chronologically by the publish date of each playlist's first video, so a channel
reads in the order it was made. Once assigned, a playlist keeps its season number permanently - new
playlists are appended at the end and never renumber existing seasons. Videos belonging to no
playlist are collected into a single season named **Unsorted**.

### Enabling it

1. **Dashboard -> Plugins -> TubeArchivist Metadata-playlist_sort**, tick
   **Group seasons by TubeArchivist playlist**, and save.
2. If your library already contains episodes, run the
   [Rebuild playlist seasons](#converting-an-existing-library) task.

A library scanned for the first time with the setting already enabled is grouped by playlist
immediately and needs no further action.

> [!IMPORTANT]
> **Episodes already imported will not move on their own.** Jellyfin stores each episode's season
> number on the item and will not overwrite it, even during a "Replace all metadata" refresh - it
> seeds the refresh with the existing value, so the plugin is never asked for a new one. Measured on
> a 116-episode library: a full replace moved **zero** episodes. Use the task below.

### Converting an existing library

**Dashboard -> Scheduled Tasks -> Rebuild playlist seasons -> run manually.**

The task clears each episode's stored season number in libraries matching the configured collection
title, refreshes them so the current setting decides the season afresh, then refreshes the affected
series to rebuild the seasons themselves. It works in both directions: enabling the setting regroups
a year-based library by playlist, and disabling it returns episodes to upload-year seasons.

Both phases matter. Reassigning an episode does not create the season it now belongs to, so without
the second phase the library shows the previous seasons standing empty. Progress reflects this - the
first 90% is the episodes, the remainder is the seasons. **Let it reach 100%.**

The task has no automatic trigger. Regrouping a library is disruptive enough that it should happen
when you choose, not as a side effect of saving a settings page.

**It deletes nothing.** Only the season number field on each episode is modified. Verified on a
116-episode library: media files untouched, watch state and resume positions preserved, no orphaned
seasons left behind - Jellyfin removes the now-empty old seasons itself. Season names may briefly
read "Season Unknown" immediately afterwards; that is a cached field on the episode and the next
library refresh restores the correct name.

If a series fails to refresh, an error naming it is logged and the remaining series still run. That
series keeps its old seasons, which appear empty; a library scan corrects it.

> [!WARNING]
> **Never delete a Series or Episode to reset state.** In Jellyfin, `DELETE /Items/{id}` on a
> file-backed item **deletes the media from disk**, and there is no trash or recycle bin. Deleting
> seasons by hand does not work either: Jellyfin derives a season's id from its series and index
> number, and deleting one does not clear its episodes' stored season number, so a rescan recreates
> exactly the same seasons. Measured: 30 seasons deleted, 0 episodes moved.

### Existing libraries: the Season metadata fetcher is enabled for you

Jellyfin stores enabled metadata fetchers per library *and per item type*, and a library created
before this fork has an **empty** Season list. That is not harmless - Jellyfin treats an empty list
as "all disabled" rather than falling back to defaults, so the Season provider would never run:
seasons keep their `Season 1` names, nothing appears in the log, and no error is raised.

On first startup after installing, the plugin checks every library that already uses TubeArchivist
as its *Series* fetcher and adds the Season fetcher where missing:

```
[INF] Enabled the TubeArchivist Season metadata fetcher on library YouTube.
```

Libraries that do not use this plugin are left untouched. **This runs once, not on every startup** -
if you remove the Season fetcher yourself afterwards it stays removed. To check it by hand:
**Libraries -> *your library* -> Manage -> Metadata downloaders (Season) -> TubeArchivist**.

### If you keep `.nfo` files next to your media, they win

An `.nfo` containing `<season>` is a local metadata source, and Jellyfin prefers it over anything a
plugin supplies, so those episodes stay where the file says regardless of the setting or the task.
Measured: an episode with `<season>1</season>` in its `.nfo` ignored the rebuild entirely, then moved
correctly the moment the file was removed. Either delete the `.nfo` files, remove their `<season>`
element, or disable the "Nfo" metadata reader for the library.

The task itself will **not** modify or create `.nfo` files - it saves below Jellyfin's "manual edit"
threshold precisely so the NFO savers stay out of the way. Verified against a hand-written `.nfo`:
byte-identical after a full run.

### Turning the setting off

Disabling it stops the plugin supplying season names, but Jellyfin keeps the names it already stored;
a refresh alone will not revert them. Run the **Rebuild playlist seasons** task to return the library
to upload-year seasons.

### Notes and limitations

- **Season names come from TubeArchivist**, with whitespace normalised and a 120-character cap.
  Punctuation is preserved so seasons read as they do in TubeArchivist.
- **A video in several playlists** is placed in the lowest-numbered matching season, chosen
  deterministically so it does not move between refreshes.
- **If TubeArchivist is unreachable**, episodes fall back to upload-year grouping and a warning is
  logged. They are not swept into *Unsorted*, because that would be sticky and unrepairable.
- **Playlist data is cached for 30 minutes.** A newly created playlist may take that long to appear;
  restart Jellyfin to pick it up immediately.
- **Season 0 is never used.** Jellyfin reserves it for Specials and force-renames it.
- The playlist-to-season map is stored in the plugin configuration and survives restarts. Season
  numbers outside the valid range are rejected and reallocated automatically.
- Season numbers are allocated across the whole library, so a second channel's first playlist
  continues from where the previous channel left off rather than restarting at 1. This is invisible
  while playlist names are shown.

### Other fixes in this fork

- **TubeArchivist playlists were fetched twice.** Paging continued from `CurrentPage + 1`, but
  TubeArchivist treats `?page=0` and `?page=1` as the same first page, so every playlist on it was
  duplicated (50 results for 29 real playlists). This also affected the playlist-sync tasks.
- **Redirects during playlist paging.** The base URL was prefixed onto an already-absolute
  `Location` header, producing an invalid URI. Redirects are now followed as-is.

## Tasks intervals
<p>Since many of the feature are implemented as background tasks periodically executing, in the `Tasks intervals` section you will find the settings to adjust this period in seconds.<br>
Keep in mind that Jellyfin lowest accepted period is of 1 minute (60 seconds) and the lower is the interval the higher will be the resources consuption on your system.</p>
<p>Here are the configurable intervals:</p>

![Tasks intervals settings](https://github.com/user-attachments/assets/19db6b83-6715-477d-8ce7-b78526e87ba9)

## Episode numbering

<p>There are different ways to number the episodes as they are configured in Jellyfin.<br>
This changes the number after E in S--E-- (for example S2024E100 for episode number 100 of season 2024).</p>

![Episode numbering scheme options](https://github.com/user-attachments/assets/6d36bc2c-ca9d-4a5c-8021-e15d399316fc)

The options correlate with:
- Default - leave the numbering to what Jellyfin does by default (this is what the plugin has always done)
- YYYYMMDD - numbers the episode by the year, month, day (e.g. 20250804 for a video published on the 4th of August 2025)
- Playlist index - numbers the episode by its position within its TubeArchivist playlist. Only meaningful with [Playlist seasons](#playlist-seasons) enabled; episodes with no playlist are left unnumbered

## Build

1. To build this plugin you will need [.Net 9.x](https://dotnet.microsoft.com/download/dotnet/9.0).

2. Build plugin with following command
  ```
  $ dotnet publish Jellyfin.Plugin.TubeArchivistMetadata --configuration Release --output bin
  ```

3. Place **only** `bin/Jellyfin.Plugin.TubeArchivistMetadata.dll` in the `plugins/TubeArchivistMetadata` folder (you might need to create the folders) of your Jellyfin installation. `dotnet publish` also emits around 30 Jellyfin dependency DLLs into `bin/`; copying those shadows the server's own assemblies and can stop the plugin loading

## License

This plugins code and packages are distributed under the GPLv3 License. See [LICENSE](./LICENSE) for more information.
