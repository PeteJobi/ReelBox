# ReelBox
ReelBox is a lightweight media editing suite that includes features for splitting, merging, muxing, compressing media, cropping and image panning. Only supports Windows 10 and 11 (not tested on other versions of Windows). Powered by FFMPEG.

Reelbox integrates the below listed programs into one app:
1. [VideoSplitter](https://github.com/PeteJobi/VideoSplitter)
2. [ConcatMedia](https://github.com/PeteJobi/ConcatMedia)
3. [VideoCropper](https://github.com/PeteJobi/VideoCropper)
4. [CompressMedia](https://github.com/PeteJobi/CompressMedia)
5. [MediaTrackMixer](https://github.com/PeteJobi/MediaTrackMixer)
6. [ImageTour](https://github.com/PeteJobi/ImageTour)

<img width="915" height="1020" alt="image" src="https://github.com/user-attachments/assets/92f3d421-b6dd-4844-bd8a-78b5e6b0ccbf" />

## How to build
You need to have at least .NET 9 runtime installed to build the software. Download the latest runtime [here](https://dotnet.microsoft.com/en-us/download). If you're not sure which one to download, try [.NET 9.0 Version 9.0.203](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.203-windows-x64-installer)

In the project folder, run the below
```
dotnet publish -c Release -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None
```
When that completes, go to `\bin\Release\net<version>-windows\win-x64` and you'll find the **ReelBox.exe**.

## Run without building
You can also just download the release builds if you don't wish to build manually. Unfortunately packages created in WinUI 3 have to be signed with a certificate, and certificates sourced from trusted companies cost hundreds of dollars. If you wish to install the package, you'll have to install a certificate signed by myself, as described [here](https://github.com/PeteJobi/ReelBox/releases/tag/cert). You only need to do this once - future updates will not require different certificates.

## How to use
When you open the program, you will be prompted to upload one or more media files. The supported file types are **mp4**, **mkv**, **mov**, **avi**, **mp3**, **wav**, **jpg**, **jpeg**, **png**, **gif**, **srt** and **ass**. Each media file uploaded will show up in a list, along with a thumbnail (if applicable), media details like resolution and bitrate, and buttons for each media-editing programs available for that file type. Clicking on one of those buttons will navigate you to a page for the chosen program. When you're done with that program, click the **Go back** button to return to the media list, and if there were any files generated, they will be added to the list right below the media file you edited, so you can repeat the process for them if you wish to edit further.

Hovering over the thumbnail/icon will provide you with options to **open** the media file in your default player/viewer or **open the folder** containing the file.

You can select multiple media by checking the box beside their names. To select or deselect all media, click the **(De)select all** button below the list. _MediaTrackMixer_ and _ConcatMedia_ accept multiple input files. When multiple media files are selected, **Mix selected** and **Merge selected** buttons show up, and you can click on either of those buttons to edit the selected files.

Each media has a **Remove** button with which you can take the media off the list. There's also a **Remove all** button below the list to remove all media, or only those selected if there are any.

If there's too many media files in the list, you may want to check the **Compact** box. This will shrink the height of each item to only show the name and action buttons.
