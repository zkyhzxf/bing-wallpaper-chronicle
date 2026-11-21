# BingWallpaperChronicle

[中文](README.md) | **English**

A Bing daily wallpaper tool for Windows. Three layers — tray, script, scheduled task — with history going back to **March 2016**. Bilingual UI that follows your system language.

> Repository name is `bing-wallpaper-chronicle`; the executable is still `BingWallpaperLite.exe` for continuity. Built with AI assistance — details under Notes.

## Why build another one

Microsoft's official client shows a promotional banner that can't be turned off. It's a separate child window; normal fixes don't work, and the only option is to force-hide it via a system call — after which the main program just recreates it on next launch.

Worse, **what appears there is decided remotely**. Today it's a close button; tomorrow it could be a campaign promo or something else entirely. I can't stop that — the code isn't mine.

Rolling back seemed sensible — except official channels now ship MSIX only, and the old double-clickable EXE has been pulled. Digging up a 2021 build from the Internet Archive made things worse: 468 KB watermarked JPEGs, while the same Bing API still serves a 3.5 MB watermark-free original.

So I wrote my own: only Bing's public APIs, all behaviour in local code, nothing pushed to the UI.

## Features

**Daily changes**

* Switches at 00:10 daily, with retries at 08:30 and 12:00 (Bing is occasionally late)
* Browse back and forth through the last 8 days
* Skips re-downloading when today's image hasn't been published yet

**History**

* Calendar picker for any date from **March 2016** to today
* **This day in past years** — compare the same date across ten years
* Images fetched from history go into a separate archive, isolated from the daily cache
* Archived images carry Chinese titles and credits

**Management**

* Library split into favourites / last 30 days / history archive
* Random favourite, skipping the current image
* Favourites use move semantics — no duplicate files
* Pause auto-change (manual changes still work)
* Auto-clean cache (30 days by default, roughly 105 MB)
* Dark/light appearance follows the system, or can be forced
* Bilingual UI, switches with the system language

**Reliability**

* The scheduled task runs independently — **closing the tray doesn't stop it**
* Task health is shown right in the menu; one click fixes it if broken
* Corrupted cache files are detected and re-downloaded
* Retries every 15 minutes, up to 3 times, on failure

## Installation

Download `Setup.exe` from [Releases](../../releases), run it, pick a folder.

It will then:

1. Register itself for autostart
2. Create a scheduled task (00:10, 08:30, 12:00 daily + on logon)
3. **Change your wallpaper immediately**

> ⚠️ That third point is worth flagging — your desktop changes right away.

A portable build is also available: extract to any writable folder and run. No registry writes, no scheduled task.

## Usage

Right-click (or left-click) the tray icon:

```
Current: First Snow in the Wild (20261121)
Task: OK · next 2026/11/22 0:10:00
Where is this?
☐ Add to favorites
─────────────────────
Today's image
This day in past years ▸
Previous (older)
Next (newer)
Library ▸
─────────────────────
Pick a date…
Random favorite
─────────────────────
☐ Pause auto-change
☐ Notify on change
☐ Run at startup
─────────────────────
Cache ▸   Data sources ▸   Appearance ▸   About & uninstall ▸
─────────────────────
Exit
```

### Three things that trip people up

**"Pause auto-change" does not disable changing.** It only stops the 00:10 automatic update. Manual changes still work, and the pinned target follows the new image.

**Favouriting moves the file.** It goes from `cache\` to `favorites\`, so it disappears from "Last 30 days" — it's in "Favourites" instead. This avoids storing the same image twice.

**Images before May 2019 are smaller.** Bing has purged the originals from before then, so only a 1080p archive link is available (~350 KB). Later dates give originals, typically 3–8 MB.

## Command line

The core script can be called on its own — useful for automation or AI agents:

```powershell
.\bing-wallpaper.ps1                    # today's image (respects the pause switch)
.\bing-wallpaper.ps1 -Index 0           # force today's image
.\bing-wallpaper.ps1 -Step -1           # one step newer
.\bing-wallpaper.ps1 -Step 1            # one step older
.\bing-wallpaper.ps1 -Date 20180315     # specific date
.\bing-wallpaper.ps1 -Use "D:\x.jpg"    # specific local file
.\bing-wallpaper.ps1 -Random            # random favourite
.\bing-wallpaper.ps1 -Favorite          # favourite the current image
.\bing-wallpaper.ps1 -Unfavorite        # remove from favourites
.\bing-wallpaper.ps1 -Clean -Keep 7     # clear cache older than 7 days
.\bing-wallpaper.ps1 -CleanAll          # clear all cache (current kept)
.\bing-wallpaper.ps1 -CleanArchive      # clear the history archive
```

Every parameter is **idempotent** — running the same command ten times yields the same result as once.

Exit codes:

| Code | Meaning |
|---|---|
| 0 | Success |
| 2 | Another instance is already running |
| 3 | Cannot favourite (image not in the cache folder) |
| 4 | Reached the limit — nothing to switch to |
| 5 | No wallpaper for that date |
| 6 | "Data sources" needs to be enabled |
| 7 | Archive source unreachable |
| 8 | Success, but fell back to a compressed version |
| 9 | Bing hasn't published today's image yet (waiting, not a failure) |

## Credits

This project stands on other people's work:

* **[Zhu-junwei/bing-wallpaper-archive](https://github.com/Zhu-junwei/bing-wallpaper-archive)** — primary history source, March 2016 onward, providing image IDs, Chinese titles and credits
* **[niumoo/bing-wallpaper](https://github.com/niumoo/bing-wallpaper)** — fallback source, plus its China mirror site
* **[gh-proxy.com](https://gh-proxy.com/)** — GitHub content proxy; `raw.githubusercontent.com` is essentially unreachable from mainland China
* **Microsoft Bing** — daily images, copyright belongs to Microsoft and the respective photographers

Prior art worth a look: [pexcn/BingWallpaper](https://github.com/pexcn/BingWallpaper), [jadepeng/bing-wallpaper](https://github.com/jadepeng/bing-wallpaper), [AlfrZhou/BingWallpaperLite](https://github.com/AlfrZhou/BingWallpaperLite).

## Notes

**On how this was built**: feature decisions, problem isolation and testing are mine; code and documentation were written with AI assistance. Every number and behavioural claim in this README was measured. There were wrong turns along the way — e.g. a confidently stated `schtasks` encoding fix that produced garbage and took three iterations, and a hover-preview idea that turned out to be impossible in WinForms after writing it.

**On copyright**: all images belong to Microsoft Bing and the respective photographers. This tool only fetches and applies them — it stores and redistributes nothing. Do not use commercially.

**On maintenance**: assembled for personal daily use. Time is limited, so prompt replies and ongoing updates aren't guaranteed. Forking is welcome — the code lives under `src\`.

## License

[MIT](LICENSE)
