#import <UIKit/UIKit.h>

// iPhone dokunsal geri bildirim. 0 hafif, 1 orta, 2 sert, 3 başarı.
extern "C" void _bbHaptic(int style)
{
    if (style >= 3)
    {
        UINotificationFeedbackGenerator *n = [[UINotificationFeedbackGenerator alloc] init];
        [n notificationOccurred:UINotificationFeedbackTypeSuccess];
        return;
    }
    UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight
                            : style == 1 ? UIImpactFeedbackStyleMedium
                                         : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g impactOccurred];
}
