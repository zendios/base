// Screen scale for AdmobBanner.cs (DllImport "__Internal"): points -> pixels of the adaptive banner size.
// These three functions came with the old ZativeAd framework, removed with the legacy native engine.

#import <UIKit/UIKit.h>

#include <math.h>

float GetScreenScale(void)
{
    return (float)UIScreen.mainScreen.scale;
}

float GetScreenNativeScale(void)
{
    return (float)UIScreen.mainScreen.nativeScale;
}

/// The device renders at scale and downsamples to its panel (e.g. iPhone 8 Plus: scale 3, native scale 2.608).
/// int, not bool: the default C# bool marshalling is 4 bytes.
int GetIsDownsampling(void)
{
    UIScreen *screen = UIScreen.mainScreen;
    return fabs(screen.scale - screen.nativeScale) > 0.01 ? 1 : 0;
}
