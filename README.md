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
Let's Plays, and with this plugin things weren't sorted right for me. I want seasons named as the
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
- [Development](#development) &middot; [Contributing](#contributing) &middot; [License](#license)

## How it works

Media is organized as a `Shows` library where each TubeArchivist channel is a **Show**, each video
is an **Episode**, and seasons are grouped either by upload year (default) or by TubeArchivist
playlist (this fork's feature). The plugin fetches metadata and images from the TubeArchivist API.

### Features

- Video metadata (episodes) and channel metadata (shows)
- Video and channel images (thumbnails, art, banners)
- Season grouping by upload year (default) or by TubeArchivist playlist
- Bidirectional playback progress synchronization
- Bidirectional playlist synchronization
- Episode numbering: Default, YYYYMMDD, or Playlist index

> [!WARNING]
> Enabling synchronization in both directions can cause race conditions and unexpected results.

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

   `dotnet publish` also writes ~30 Jellyfin dependency DLLs into `bin`. Do not copy those &mdash; they
   shadow the server's own assemblies and can stop the plugin loading.

2. Match the ownership and permissions Jellyfin uses for its other plugin files.
3. Restart Jellyfin.

### Upgrading from the upstream plugin

This fork keeps the upstream plugin GUID, so replacing the DLL is an **in-place upgrade**. There is
nothing to uninstall, and your existing settings are preserved. Keep the folder name
`TubeArchivistMetadata` &mdash; installing alongside the original rather than over it would leave
Jellyfin loading two plugins with the same GUID.

After restarting, confirm the dashboard shows **TubeArchivist Metadata-playlist_sort**.

See [Playlist seasons](#playlist-seasons) for how to enable playlist season grouping and how to
convert a library that is already grouped by year.

## Configuration

This plugin requires a running TubeArchivist instance. Once installed, configure the following in
the plugin settings page:

- **Collection display name** &mdash; the name of your Shows library
- **TubeArchivist instance address** &mdash; the URL of your TubeArchivist server
- **TubeArchivist API key** &mdash; your TubeArchivist API token
- **Overviews length** &mdash; max length for channel and video descriptions
- **Group seasons by TubeArchivist playlist** &mdash; enables the fork's feature (see [Playlist seasons](#playlist-seasons))
- **Playback synchronization** settings &mdash; see [Playback synchronization](#playback-synchronization)

![Plugin configuration](https://github.com/7hr08ik/tubearchivist-jf-plugin/assets/31162436/d34464ea-ddfb-44b3-9d3e-5d5974956c58)

## Use the plugin

> [!NOTE]
> If using Docker, mount the TubeArchivist media path into the Jellyfin container as **read-only**
> to avoid operations that break TubeArchivist.

1. Go to **Dashboard &rarr; Libraries** and add a media library.
2. Select **Shows** as the content type, set a display name, and add your TubeArchivist media folder.

   ![Add library](https://github.com/7hr08ik/tubearchivist-jf-plugin/assets/31162436/1eca534e-0929-4134-8587-3cff0009f618)

3. Scrolling down, uncheck all metadata and image providers except **TubeArchivist**. This fork
   **does** provide a Season metadata provider, so leave **TubeArchivist** enabled under Seasons as
   well &mdash; it is what names seasons after playlists. If the option is missing there, the
   plugin enables it for you on the next restart.
4. Save. Jellyfin will fetch metadata and images for your channels and videos.

## Synchronization

This plugin offers bidirectional synchronization, configurable one-way or both ways.

![Synchronization settings](https://github.com/user-attachments/assets/b0bb556b-fce3-4a3e-bc6c-b0a2b482cedc)

### Playback synchronization

#### Jellyfin &rarr; TubeArchivist

Listens for playback progress and watched-status changes while videos play. A startup task also
synchronizes the whole library. Specify one Jellyfin username to sync from.

#### TubeArchivist &rarr; Jellyfin

A scheduled task regularly syncs playback progress from TubeArchivist to Jellyfin. Specify one or
more Jellyfin usernames to update.

### Playlists synchronization

#### Jellyfin &rarr; TubeArchivist

A task retrieves Jellyfin playlists and recreates them on TubeArchivist with videos in the same
order. Videos not belonging to TubeArchivist are ignored. An optional setting auto-deletes
TubeArchivist playlists no longer present on Jellyfin.

#### TubeArchivist &rarr; Jellyfin

A task retrieves TubeArchivist playlists and recreates them on Jellyfin with videos in the same
order. An optional setting auto-deletes Jellyfin playlists no longer present on TubeArchivist,
including any non-TubeArchivist videos they contain.

> [!CAUTION]
> Automatic deletion is permanent &mdash; there is no undo. Be especially careful when playlists
> contain videos not belonging to TubeArchivist.

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

Many features run as background tasks on a configurable interval (in seconds). Jellyfin enforces a
minimum of 60 seconds. Lower intervals mean higher resource consumption.

![Tasks intervals settings](https://github.com/user-attachments/assets/19db6b83-6715-477d-8ce7-b78526e87ba9)

## Episode numbering

Episodes can be numbered in different ways, changing the `E` in Jellyfin's `S--E--` display (e.g.
`S2024E100` for episode 100 of season 2024).

![Episode numbering scheme options](https://github.com/user-attachments/assets/6d36bc2c-ca9d-4a5c-8021-e15d399316fc)

| Scheme | Description |
|---|---|
| **Default** | Leaves numbering to Jellyfin's fallback behavior (what the plugin has always done) |
| **YYYYMMDD** | Numbers by publish date, e.g. `20250804` for August 4th, 2025 |
| **Playlist index** | Numbers by position within the TubeArchivist playlist. Only meaningful with [Playlist seasons](#playlist-seasons) enabled; episodes with no playlist are left unnumbered |

## Development

`dotnet build` is the lint gate &mdash; the project sets `TreatWarningsAsErrors` with StyleCop and
the Jellyfin ruleset, so any warning fails the build.

To build and run locally:

```bash
git clone https://github.com/7hr08ik/tubearchivist-jf-plugin.git
cd tubearchivist-jf-plugin
dotnet build                                    # Debug build (warnings are errors)
dotnet publish Jellyfin.Plugin.TubeArchivistMetadata -c Release -o bin   # Release -> ./bin
```

Drop `bin/Jellyfin.Plugin.TubeArchivistMetadata.dll` into `plugins/TubeArchivistMetadata/` and
restart Jellyfin.

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

This plugin's code and packages are distributed under the GPLv3 License. See [LICENSE](./LICENSE)
for more information.

Forked from [tubearchivist/tubearchivist-jf-plugin](https://github.com/tubearchivist/tubearchivist-jf-plugin).
All credit for the original plugin goes to its authors.

---

*Last reviewed: 2026-08-10*
