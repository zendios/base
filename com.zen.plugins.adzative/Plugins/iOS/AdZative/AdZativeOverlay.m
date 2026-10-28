#import "AdZativeOverlay.h"

@interface AdZativeOverlayView : UIView
@property (nonatomic, copy, nullable) dispatch_block_t onResize;
/// Fullscreen roots (shared with AdZativeOverlay).
@property (nonatomic, strong) NSHashTable<UIView *> *topLayer;
@end

@implementation AdZativeOverlayView
{
    CGSize _lastSize;
}

- (UIView *)hitTest:(CGPoint)point withEvent:(UIEvent *)event
{
    UIView *hit = [super hitTest:point withEvent:event];
    if (hit == self)
        return nil;   // a touch on no ad goes on to Unity
    // The bare root of an inline layout (e.g. the transparent strip above ad_container) is not the ad: like a
    // non-clickable Android view, it lets the touch through to Unity.
    if (hit.superview == self && ![self.topLayer containsObject:hit])
        return nil;
    return hit;
}

// A touch on an ad ends here. UIKit would pass it on up the responder chain to the Unity view, so the game would also
// get it (Android: the ad views consume it; a fullscreen root is clickable).
- (void)touchesBegan:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
}

- (void)touchesMoved:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
}

- (void)touchesEnded:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
}

- (void)touchesCancelled:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event
{
}

- (void)layoutSubviews
{
    [super layoutSubviews];
    if (CGSizeEqualToSize(self.bounds.size, _lastSize))
        return;
    _lastSize = self.bounds.size;
    if (self.onResize != nil)
        self.onResize();
}

- (void)safeAreaInsetsDidChange
{
    [super safeAreaInsetsDidChange];
    if (self.onResize != nil)
        self.onResize();
}

@end

@implementation AdZativeOverlay
{
    AdZativeOverlayView *_view;
    NSHashTable<UIView *> *_topLayer;
    NSHashTable<id<AdZativeOverlayObserver>> *_observers;
}

+ (CGFloat)pixelScale
{
    UIView *unityView = UnityGetGLView();
    CGFloat scale = unityView != nil ? unityView.contentScaleFactor : UIScreen.mainScreen.scale;
    return scale > 0 ? scale : 1;
}

- (instancetype)init
{
    if ((self = [super init]))
    {
        _view = [[AdZativeOverlayView alloc] initWithFrame:CGRectZero];
        _view.backgroundColor = UIColor.clearColor;
        _view.opaque = NO;
        _view.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
        _topLayer = [NSHashTable weakObjectsHashTable];
        _view.topLayer = _topLayer;
        _observers = [NSHashTable weakObjectsHashTable];
        __weak AdZativeOverlay *weakSelf = self;
        _view.onResize = ^{
            [weakSelf notifyResize];
        };
        [self attach];
    }
    return self;
}

- (UIView *)container
{
    return _view;
}

/// On top of the Unity view; again on every add, so views other SDKs added later stay below the ads.
- (void)attach
{
    UIView *parent = AdZativeRootViewController().view;
    if (parent == nil)
        return;
    if (_view.superview != parent)
    {
        _view.frame = parent.bounds;
        [parent addSubview:_view];
    }
    [parent bringSubviewToFront:_view];
}

- (void)add:(UIView *)view
{
    [self attach];
    [view removeFromSuperview];
    NSArray<UIView *> *subviews = _view.subviews;
    NSUInteger index = subviews.count;
    for (NSUInteger i = 0; i < subviews.count; i++)
    {
        if ([_topLayer containsObject:subviews[i]])
        {
            index = i;
            break;
        }
    }
    [_view insertSubview:view atIndex:(NSInteger)index];
}

- (void)addTop:(UIView *)view
{
    [self attach];
    [view removeFromSuperview];
    [_topLayer addObject:view];
    [_view addSubview:view];
}

- (void)remove:(UIView *)view
{
    if (view == nil)
        return;
    [_topLayer removeObject:view];
    if (view.superview == _view)
        [view removeFromSuperview];
}

- (UIEdgeInsets)safeInsets
{
    return _view.safeAreaInsets;
}

- (BOOL)isLandscape
{
    CGSize size = _view.bounds.size;
    if (size.width <= 0 || size.height <= 0)
        size = UIScreen.mainScreen.bounds.size;
    return size.width > size.height;
}

- (void)addObserver:(id<AdZativeOverlayObserver>)observer
{
    [_observers addObject:observer];
}

- (void)removeObserver:(id<AdZativeOverlayObserver>)observer
{
    [_observers removeObject:observer];
}

- (void)notifyResize
{
    for (id<AdZativeOverlayObserver> observer in _observers.allObjects)
        [observer overlayDidResize];
}

@end
