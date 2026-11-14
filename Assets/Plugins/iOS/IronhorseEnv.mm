// Where this copy of Ironhorse Frontier came from (AdMobAds.cs shows test adverts in TestFlight builds).
#import <Foundation/Foundation.h>

extern "C"
{
    // A TestFlight (or development) install carries a sandbox receipt; an App Store one doesn't.
    bool IHIsTestFlight()
    {
        NSURL *receipt = [[NSBundle mainBundle] appStoreReceiptURL];
        return receipt != nil && [[receipt lastPathComponent] isEqualToString:@"sandboxReceipt"];
    }
}
