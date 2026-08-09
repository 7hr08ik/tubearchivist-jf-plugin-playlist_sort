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
The media organization is a `Shows` collection, where each channel is a show and its videos are the episodes, organized in seasons by year.<br>
The plugin interacts with TubeArchivist APIs to fetch videos and channels metadata.

### Features
- Add metadata for videos (episodes)
- Add metadata for channels (shows)
- Add images for videos (episodes), ie. thumb images
- Add images for channels (shows), ie. thumb, tvart and banner images
- Organize videos (episodes) by year (seasons)
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

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for how to enable playlist season grouping and how to
convert a library that is already grouped by year.

## Configuration
<p>This plugin requires that you have already an instance of TubeArchivist up and running.</p>
Once installed, you have to configure the following parameters in the plugin configuration:
<ul>
    <li>Collection display name</li>
    <li>TubeArchivist instance address</li>
    <li>TubeArchivist API key</li>
    <li>Overviews length (channels and videos descriptions)</li>
    <li>Playback synchronization settings discussed in the <a href="#playback-synchronization">Playback synchronization</a> paragraph</li>
</ul>

![Plugin configuration](https://github.com/tubearchivist/tubearchivist-jf-plugin/assets/31162436/d34464ea-ddfb-44b3-9d3e-5d5974956c58)


## Use the plugin
<p>Using the plugin is very simple. Let's start from the beginning:</p>

_NOTE: If you are using Docker containers, it is important to mount the TubeArchivist media path into Jellyfin container as **read-only**, in order to avoid possible operations on the media files that will break TubeArchivist._ <br>
1. Go to `Dashboard -> Libraries` and add a media library
2. In the form select `Shows` as Content type, set a display name for the library and set the TubeArchivist media folder in the `Folders` section
![Add library](https://github.com/tubearchivist/tubearchivist-jf-plugin/assets/31162436/1eca534e-0929-4134-8587-3cff0009f618)

4. Scrolling down, uncheck all metadata and image providers except `TubeArchivist`. (You won't find TubeArchivist in seasons providers, so just disable everything there)
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

## Build

1. To build this plugin you will need [.Net 9.x](https://dotnet.microsoft.com/download/dotnet/9.0).

2. Build plugin with following command
  ```
  $ dotnet publish Jellyfin.Plugin.TubeArchivistMetadata --configuration Release --output bin
  ```

3. Place the dll-file in the `plugins/tubearchivist-jf-plugin` folder (you might need to create the folders) of your Jellyfin installation

## License

This plugins code and packages are distributed under the GPLv3 License. See [LICENSE](./LICENSE) for more information.
