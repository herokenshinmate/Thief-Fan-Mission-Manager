# Using Thief FM Manager

## First-run setup

On first launch (or from the **Settings** menu any time), Settings opens
to the **FM Folders** tab. For each game you own, set:

- **FM folder** — the loader's `fms/` folder where installed missions live
  (e.g. the `fms` folder next to FMSel/NewDarkLoader).
- **Executable** — the loader or game exe that **Play** should launch.
- **Downloads folder** — where you save mission archives (`.zip`/`.7z`/`.rar`)
  before installing them.

You only need to set up the games you actually play; the other game's
section of the mission list just won't show anything.

Settings has two other tabs: **NewDark Versions** and **General** — see
[Playing a mission](#playing-a-mission) below for what they control.

## Cataloging missions

**Scan for New Missions** (toolbar) checks your configured FM and Downloads
folders and lists anything it finds that isn't cataloged yet:

- Items found in your **FM folder** are already installed — confirm them to
  add them as installed, uninstalled-status missions.
- Items found in your **Downloads folder** are archives you haven't
  installed yet — select one and choose **Install** to extract it straight
  into the right game's FM folder.

Each candidate shows a pre-filled title (from the folder/file name) that you
can edit before confirming. Don't want to see a folder or archive again?
Right-click it in the scan results and choose **Ignore** — manage ignored
items later from the Ignore List.

You can also add a mission manually without scanning, and edit any
mission's fields later from its right-click **Properties**.

## The mission list

Missions are grouped by game, with a banner showing how many you own, have
completed, and have installed. Click a banner's arrow (or double-click it)
to collapse/expand that game; **Collapse All** / **Expand All** do it for
both at once.

- **Series** (multi-part FMs) get their own collapsible header showing how
  many parts you own and completed. Parts you don't own yet show up dimmed
  — double-click one to open its Thief Guild page.
- **Campaigns** (one FM bundling several missions) are marked with a gold
  "CAMPAIGN · N" badge.
- Click a column header to sort by it (Title, Status, Rating, TG Rating,
  Author, Tags, Type); click again to reverse the order.
- Use the toolbar's filters (Game, Tag, Status, Author, Sort) to narrow the
  list down.

## Playing a mission

Click the green **Play** button on a mission (or double-click it — see
below) to launch it directly, no need to pick it again from the loader's
own list. If no executable is configured for that mission's game yet,
you'll be prompted to set one in Settings.

If the mission has a briefing or notes from Thief Guild, or its required
NewDark version looks newer than what you have configured, a window opens
first showing that information with **Play** and **Cancel** buttons. Two
Settings > General options control this:

- **Show mission briefing before playing** (on by default) — turn off to
  skip straight to launching even when a mission has briefing/notes text.
- **Warn if a mission needs a newer NewDark than I have configured**
  (on by default) — compares a mission's Thief Guild-listed required
  NewDark version against what you've set in Settings > NewDark Versions
  for that game, and shows a warning banner in the same window if yours is
  older. This is only ever a heads-up, not a hard block — missing or
  unrecognized version numbers on either side are silently skipped rather
  than guessed at.

Settings > General also has **Double-clicking a mission plays it** (on by
default); turn it off to have double-click open Properties instead, like
earlier versions of the app.

### NewDark version

Settings > NewDark Versions shows, per game, a version auto-detected from
the configured executable's file info (labeled as a guess, since NewDark
doesn't have a reliable API to query) alongside a text field to type in
your actual version if you know it's different or nothing was detected.
This is what the mismatch warning above compares against.

## Tracking your progress

Right-click a mission for quick actions:

- **Set Status** — Not Played, In Progress, Completed, Abandoned. Date
  Started/Completed are set automatically based on status.
- **Rate** — 0 to 5 stars, or Not Rated.
- **Properties** — edit every field, including your personal notes, tags,
  and a manual Thief Guild URL if the automatic lookup didn't find the
  right page. If Thief Guild has data for the mission, Properties also
  shows a read-only **Mission Briefing** section (the story synopsis).
- **Delete from Library...** — removes the catalog entry and (if the
  mission was installed) its folder from disk; asks for confirmation
  first. The original downloaded archive, if any, is kept.

## Thief Guild metadata

When a mission gets installed, the app automatically searches
[Thief Guild](https://www.thiefguild.com) for it and fills in author,
release year, tags, community rating, story briefing, and series info.
It also picks up the author's own notes (warnings, recommended settings,
required NewDark version if mentioned) — these fill your personal Notes
field only when it's still blank, so anything you've already written
there is never overwritten. Already-linked missions are re-checked once
automatically at startup.

To refresh everything at once (e.g. after ratings changed on Thief Guild),
use **Refresh Thief Guild Data** from the menu. If a lookup picked the wrong
page, or didn't find one, set the correct URL by hand in a mission's
Properties and click **Fetch Metadata**.

## Updating

See the [README](../README.md#updating) for how automatic updates work.
