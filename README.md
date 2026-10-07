# Base for Unity games

Release copies of the two Base packages (published from Base > Hub > Release, also on OpenUPM):

- `com.zen.core`: Base Core (data, UI, ads waterfall, IAP, Firebase, Base > Hub). See [its README](com.zen.core/README.md)
  and [CHANGELOG](com.zen.core/CHANGELOG.md).
- `com.zen.plugins.adzative`: AdZative, AdMob native ads drawn over the Unity view.

## Install

`Packages/manifest.json` of the game:

```json
{
  "scopedRegistries": [
    { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": [ "com.google", "com.zen" ] }
  ],
  "dependencies": {
    "com.zen.core": "1.2.5",
    "com.zen.plugins.adzative": "1.2.5"
  }
}
```

Requires DOTween (free, Asset Store) or DOTween Pro (paid, added by hand) in Assets/Plugins/Demigiant; no ASMDEF needed.

Then **Base > Hub > Setup**: Install required + selected (AdMob, mediation, AppsFlyer, Play, IAP, Firebase at their
tested versions), Fix all. Setup also updates or rolls back Base to any released version.

Without OpenUPM: `https://github.com/zendios/base.git?path=/com.zen.core#1.2.5` (and `/com.zen.plugins.adzative`).

## License

MIT, see [LICENSE](LICENSE).
