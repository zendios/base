# Base for Unity games

Release copies of the two Base packages (published from Base > Hub > Release, also on OpenUPM):

- `com.zen.core`: Base (data, UI, ads waterfall, IAP, Firebase, Base > Hub). See [its README](com.zen.core/README.md)
  and [CHANGELOG](com.zen.core/CHANGELOG.md).
- `com.zen.plugins.adzative`: Base Native Ad (AdZative), AdMob native ads drawn over the Unity view.

## Install

**One file:** download [Base-Installer.unitypackage](https://github.com/zendios/base/releases/latest/download/Base-Installer.unitypackage)
from the [latest release](https://github.com/zendios/base/releases/latest) (every version: [Releases](https://github.com/zendios/base/releases))
and drag it into the Unity project. It adds the OpenUPM registry and Base 1.2.6 to Packages/manifest.json and
removes itself. Without DOTween, Base then offers to download and import the latest free DOTween from
dotween.demigiant.com (DOTween Pro: import it from the Asset Store instead). Then **Base > Hub > Setup**.

By hand, `Packages/manifest.json` of the game:

```json
{
  "scopedRegistries": [
    { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": [ "com.google", "com.zen" ] }
  ],
  "dependencies": {
    "com.zen.core": "1.2.6",
    "com.zen.plugins.adzative": "1.2.6"
  }
}
```

Base needs DOTween (free) or DOTween Pro in Assets; without it Base stays off (no compile errors) and offers to install
the free DOTween. No ASMDEF needed.

Then **Base > Hub > Setup**: Install required + selected (AdMob, mediation, AppsFlyer, Play, IAP, Firebase at their
tested versions), Fix all. Setup also updates or rolls back Base to any released version.

Without OpenUPM: `https://github.com/zendios/base.git?path=/com.zen.core#1.2.6` (and `/com.zen.plugins.adzative`).

## License

MIT, see [LICENSE](LICENSE).
