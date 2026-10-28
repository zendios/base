#import "AdZativeStarsView.h"

/// ratingBarStyleSmall: 16 dp cells, no gap between them.
static const CGFloat StarSpacing = 0;
static const CGFloat StarCell = 16;

/// The Material star of RatingBar (ic_star_black_16dp), 24 x 24 viewport, scaled to the cell.
static UIBezierPath *StarPath(CGRect cell)
{
    static const CGFloat points[] = { 12, 17.27, 18.18, 21, 16.54, 13.97, 22, 9.24, 14.81, 8.63,
                                      12, 2, 9.19, 8.63, 2, 9.24, 7.46, 13.97, 5.82, 21 };
    CGFloat k = cell.size.width / 24;
    UIBezierPath *path = [UIBezierPath bezierPath];
    for (int i = 0; i < 10; i++)
    {
        CGPoint point = CGPointMake(cell.origin.x + points[2 * i] * k, cell.origin.y + points[2 * i + 1] * k);
        if (i == 0)
            [path moveToPoint:point];
        else
            [path addLineToPoint:point];
    }
    [path closePath];
    return path;
}

@implementation AdZativeStarsView

- (instancetype)initWithFrame:(CGRect)frame
{
    if ((self = [super initWithFrame:frame]))
        [self setUp];
    return self;
}

- (nullable instancetype)initWithCoder:(NSCoder *)coder
{
    if ((self = [super initWithCoder:coder]))
        [self setUp];
    return self;
}

- (void)setUp
{
    self.opaque = NO;
    self.backgroundColor = UIColor.clearColor;
    self.contentMode = UIViewContentModeRedraw;
    _fillColor = [UIColor colorWithRed:1.0 green:0.757 blue:0.027 alpha:1];   // #FFC107
    _emptyColor = [UIColor colorWithWhite:0.8 alpha:1];
    _starSize = StarCell;
}

- (void)setRating:(double)rating
{
    _rating = round(MAX(0, MIN(5, rating)) * 10) / 10.0;   // stepSize 0.1
    [self setNeedsDisplay];
}

- (void)setStarSize:(CGFloat)starSize
{
    _starSize = starSize;
    [self invalidateIntrinsicContentSize];
}

- (CGSize)intrinsicContentSize
{
    return CGSizeMake(_starSize * 5 + StarSpacing * 4, _starSize);
}

- (void)drawRect:(CGRect)rect
{
    CGSize bounds = self.bounds.size;
    CGFloat size = MIN(bounds.height, (bounds.width - StarSpacing * 4) / 5);
    if (size <= 0)
        return;
    CGContextRef context = UIGraphicsGetCurrentContext();
    for (int i = 0; i < 5; i++)
    {
        CGRect cell = CGRectMake(i * (size + StarSpacing), (bounds.height - size) / 2, size, size);
        UIBezierPath *star = StarPath(cell);
        [_emptyColor setFill];
        [star fill];
        CGFloat fraction = MAX(0, MIN(1, _rating - i));
        if (fraction <= 0)
            continue;
        CGContextSaveGState(context);
        UIRectClip(CGRectMake(cell.origin.x, cell.origin.y, cell.size.width * fraction, cell.size.height));
        [_fillColor setFill];
        [star fill];
        CGContextRestoreGState(context);
    }
}

@end
