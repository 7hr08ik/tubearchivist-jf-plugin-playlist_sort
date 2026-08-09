<h1 align="center">Jellyfin TubeArchivist Plugin<br><sub>playlist_sort fork</sub></h1>

<p align="center">
  Brings <a href="https://www.tubearchivist.com/">TubeArchivist</a> metadata into Jellyfin &mdash;
  and groups a channel's videos into seasons by <b>playlist</b> instead of by <b>upload year</b>.
</p>

<p align="center">
  <img alt="License GPLv3" src="https://img.shields.io/badge/license-GPLv3-blue.svg">
  <img alt="Jellyfin 10.11" src="https://img.shields.io/badge/Jellyfin-10.11-purple.svg">
  <img alt="dotnet 9" src="https://img.shields.io/badge/.NET-9.0-512BD4.svg">
  <img alt="Version 1.5.0" src="https://img.shields.io/badge/version-1.5.0.0-green.svg">
</p>

---

## What this fork changes

Upstream sorts a channel's videos into seasons **by upload year**, which mixes unrelated series
together &mdash; one year can hold episodes from three different Let's Plays. This fork adds an
option to use the **TubeArchivist playlist** as the season instead:

| Upstream | This fork |
|---|---|
| `Season 2012` | `Lets Play Skyrim (Chapter 1) : Orc Warlock` |
| `Season 2013` | `Let's Play Falskaar (Skyrim)` |
| `Season 2014` | `S.T.A.L.K.E.R. 2 : Heart of Chornobyl` |

Everything else is upstream's, unchanged. See **[Playlist seasons](#playlist-seasons)** for the
detail, or jump to [Quick start](#quick-start).

<details>
<summary>Why this exists</summary>

<br>

This fork was made as a personal AI Slop project.

I love the idea behind TubeArchivist, and having it all working automagically through Jellyfin. But
I want the videos sorted by Playlist, NOT by year. I watch a couple of YouTubers that make
LetPlay's, and with this plugin things weren't sorted right for me. I want seasons Named as the
playlist is named.

So I vibe slopped this with my own
[Opencode Setup](https://github.com/7hr08ik/AMPG-Opencode-and-Friends), on locally hosted AI and
some free APIs.

</details>

## Quick start

1. [Build from source](#build-from-source) &mdash; `dotnet publish ... -c Release -o bin`
2. Copy the DLL into `plugins/TubeArchivistMetadata` and **restart Jellyfin**
3. [Configure](#configuration) your TubeArchivist address and API key
4. Tick **Group seasons by TubeArchivist playlist** and save
5. Already have a library? Run the
   [Rebuild playlist seasons](#converting-an-existing-library) task

## Contents

- [Installation](#installation) &middot; [Configuration](#configuration) &middot; [Use the plugin](#use-the-plugin)
- [Playlist seasons](#playlist-seasons) &mdash; the fork's feature
- [Synchronization](#synchronization) &middot; [Tasks intervals](#tasks-intervals) &middot; [Episode numbering](#episode-numbering)
- [Build](#build) &middot; [Contributing](#contributing) &middot; [License](#license)

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

### Requirements

| | |
|---|---|
| Jellyfin | 10.11 or later |
| TubeArchivist | a running instance, reachable from Jellyfin |
| Library type | a `Shows` library pointed at your TubeArchivist media |
| To build | [.NET 9 SDK](https://dotnet.microsoft.com/download) |

### Build from source

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

*The fork's feature. Everything outside this section is upstream behaviour.*

Each TubeArchivist playlist becomes its own Jellyfin season, named as it is in TubeArchivist.
Seasons are ordered chronologically by the publish date of each playlist's first video, so a channel
reads in the order it was made. Once assigned, a playlist keeps its season number permanently &mdash;
new playlists are appended at the end and never renumber existing seasons. Videos in no playlist go
to a season named **Unsorted**.

### Enabling it

1. **Dashboard &rarr; Plugins &rarr; TubeArchivist Metadata-playlist_sort**
2. Tick **Group seasons by TubeArchivist playlist**, save
3. If the library already has episodes, run
   [Rebuild playlist seasons](#converting-an-existing-library)

A library scanned for the first time with the setting already on is grouped by playlist immediately
and needs nothing further.

> [!IMPORTANT]
> **Episodes already imported will not move on their own** &mdash; not even with "Replace all
> metadata". Jellyfin seeds each refresh with the episode's existing season number, so the plugin is
> never asked for a new one. Measured on a 116-episode library: a full replace moved **zero**
> episodes. Use the task below.

### Converting an existing library

**Dashboard &rarr; Scheduled Tasks &rarr; Rebuild playlist seasons &rarr; run manually.**

Clears each episode's stored season number, refreshes so the current setting decides afresh, then
refreshes the affected series to rebuild the seasons themselves. Works both ways &mdash; enabling
regroups by playlist, disabling returns to upload-year seasons.

- **Progress is 90% episodes, 10% seasons. Let it reach 100%.** Reassigning an episode does not
  create the season it now belongs to; without the second phase the old seasons stand empty.
- **It deletes nothing.** Only the season number field changes. Verified on 116 episodes: media
  untouched, watch state and resume positions preserved, no orphaned seasons. Jellyfin removes the
  emptied seasons itself.
- **No automatic trigger.** Regrouping a library should happen when you choose, not as a side effect
  of saving a settings page.
- Season names may briefly read "Season Unknown"; the next library refresh restores them.
- If a series fails to refresh, it is logged by name and the rest still run. A library scan fixes it.

> [!WARNING]
> **Never delete a Series or Episode to reset state.** `DELETE /Items/{id}` on a file-backed item
> **deletes the media from disk** &mdash; there is no trash or recycle bin.
>
> Deleting seasons by hand does not work either. Jellyfin derives a season's id from its series and
> index number, and deleting one does not clear its episodes' stored season number, so a rescan
> recreates exactly the same seasons. Measured: 30 deleted, 0 episodes moved.

<details>
<summary><b>If you keep <code>.nfo</code> files next to your media, they win</b></summary>

<br>

An `.nfo` containing `<season>` is a local metadata source, and Jellyfin prefers it over anything a
plugin supplies. Those episodes stay where the file says, regardless of the setting or the task.
Measured: an episode with `<season>1</season>` ignored the rebuild entirely, then moved correctly
the moment the file was removed.

Either delete the `.nfo` files, remove their `<season>` element, or disable the "Nfo" metadata
reader for the library.

The task itself will **not** modify or create `.nfo` files &mdash; it saves below Jellyfin's "manual
edit" threshold precisely so the NFO savers stay out of the way. Verified against a hand-written
`.nfo`: byte-identical after a full run.

</details>

<details>
<summary><b>The Season metadata fetcher is enabled for you on upgrade</b></summary>

<br>

Jellyfin stores enabled metadata fetchers per library *and per item type*, and a library created
before this fork has an **empty** Season list. Jellyfin treats empty as "all disabled" rather than
falling back to defaults, so the Season provider would never run: seasons keep their `Season 1`
names, nothing appears in the log, and no error is raised.

On first startup the plugin checks every library already using TubeArchivist as its *Series* fetcher
and adds the Season fetcher where missing:

```
[INF] Enabled the TubeArchivist Season metadata fetcher on library YouTube.
```

Other libraries are untouched. **This runs once, not on every startup** &mdash; remove the fetcher
yourself and it stays removed. To check by hand: **Libraries &rarr; *your library* &rarr; Manage
&rarr; Metadata downloaders (Season)**.

</details>

<details>
<summary><b>Behaviour notes and limitations</b></summary>

<br>

- **Turning the setting off** stops the plugin supplying names, but Jellyfin keeps the names it
  already stored. Run the rebuild task to return to upload-year seasons.
- **Season names** come from TubeArchivist with whitespace normalised and a 120-character cap.
  Punctuation is preserved.
- **A video in several playlists** goes to the lowest-numbered matching season, chosen
  deterministically so it does not move between refreshes.
- **If TubeArchivist is unreachable**, episodes fall back to upload-year grouping and a warning is
  logged. They are not swept into *Unsorted*, which would be sticky and unrepairable.
- **Playlist data is cached for 30 minutes.** A new playlist may take that long to appear; restart
  Jellyfin to pick it up immediately.
- **Season 0 is never used** &mdash; Jellyfin reserves it for Specials and force-renames it.
- **Season numbers are allocated library-wide**, so a second channel's first playlist continues from
  where the previous channel left off rather than restarting at 1. Invisible while names are shown.
- The playlist-to-season map lives in the plugin configuration and survives restarts. Out-of-range
  numbers are rejected and reallocated automatically.

</details>

### Other fixes in this fork

- **Playlists were fetched twice.** Paging continued from `CurrentPage + 1`, but TubeArchivist
  treats `?page=0` and `?page=1` as the same first page, duplicating every playlist on it (50
  results for 29 real playlists). Also affected the playlist-sync tasks.
- **Redirects during playlist paging.** The base URL was prefixed onto an already-absolute
  `Location` header, producing an invalid URI. Now followed as-is.

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

Build and install instructions are in [Installation](#installation) above.

For development, `dotnet build` is the lint gate &mdash; the project sets `TreatWarningsAsErrors`
with StyleCop and the Jellyfin ruleset, so warnings fail the build.

## Contributing

This is a personal fork with a narrow purpose, so it is not looking for feature contributions.

- **Bugs in playlist seasons** &mdash; open an issue here, and include your Jellyfin version, the
  plugin version from the dashboard, and the relevant log lines.
- **Anything else** &mdash; please take it
  [upstream](https://github.com/tubearchivist/tubearchivist-jf-plugin). Fixes landing there benefit
  everyone and eventually reach this fork.

Two upstream bug fixes carried here (playlist pagination and redirect handling) are independent of
the season feature and are welcome to be taken upstream by anyone.

## License

This plugins code and packages are distributed under the GPLv3 License. See [LICENSE](./LICENSE) for more information.

Forked from [tubearchivist/tubearchivist-jf-plugin](https://github.com/tubearchivist/tubearchivist-jf-plugin).
All credit for the original plugin goes to its authors.
