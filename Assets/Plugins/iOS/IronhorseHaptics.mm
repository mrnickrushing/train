// Taptic feedback for Ironhorse Frontier (called from Haptics.cs).
// The generators are created once and kept, so taps respond quickly and nothing is allocated per buzz.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *sLight, *sMedium, *sHeavy;
static UINotificationFeedbackGenerator *sNote;

extern "C" void IronhorseHaptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (kind >= 3)
        {
            if (sNote == nil) sNote = [[UINotificationFeedbackGenerator alloc] init];
            [sNote notificationOccurred:kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError];
            return;
        }
        if (sLight == nil)
        {
            sLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            sMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            sHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        }
        UIImpactFeedbackGenerator *g = kind == 0 ? sLight : kind == 1 ? sMedium : sHeavy;
        [g impactOccurred];
    });
}
