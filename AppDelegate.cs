using System;
using System.Collections.Generic;
using System.Linq;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using TikTok.Views;
using TikTok.Views.Profile;
using TikTok.Views.Notifications;

namespace TikTok
{
    [Register("AppDelegate")]
    public partial class AppDelegate : UIApplicationDelegate
    {
        UIWindow window;
        UITabBarController tabBarController;
        public static AppDelegate instance;

        public void RestartApplicationUI()
        {
            var nav = window.RootViewController as UINavigationController;

            if (nav != null)
            {
                nav.PopToRootViewController(false);
                if (nav.PresentedViewController != null)
                {
                    nav.DismissModalViewControllerAnimated(false);
                }
            }
            else if (window.RootViewController.PresentedViewController != null)
            {
                window.RootViewController.DismissModalViewControllerAnimated(false);
            }

            var newRootViewController = new WelcomeViewController();
            window.RootViewController = newRootViewController;

            window.MakeKeyAndVisible();
        }

        public void SetOnLoad(VideoViewController video_view, NotificationsViewController notifications_view, MessagesViewController messages_view, ProfileViewController profile_view)
        {
            window = new UIWindow(UIScreen.MainScreen.Bounds);

            UINavigationBar.Appearance.TintColor = UIColor.Black;

            var tabBarController = new UITabBarController();

            var a = new UINavigationController(video_view);
            a.TabBarItem = new UITabBarItem("Home", UIImage.FromBundle("item-home.png"), 0);

            var b = new UINavigationController(notifications_view);
            b.TabBarItem = new UITabBarItem("Notifications", UIImage.FromBundle("item-notifications.png"), 0);

            var c = new UINavigationController(messages_view);
            c.TabBarItem = new UITabBarItem("Inbox", UIImage.FromBundle("item-add.png"), 0);

            var d = new UINavigationController(profile_view);
            d.TabBarItem = new UITabBarItem("Profile", UIImage.FromBundle("item-account.png"), 0);

            tabBarController.ViewControllers = new UIViewController[] {
                a,
                b,
                d
            };
            window.RootViewController = tabBarController;

            window.MakeKeyAndVisible();
        }

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            instance = this;

            window = new UIWindow(UIScreen.MainScreen.Bounds);

            var viewController = new WelcomeViewController();

            window.RootViewController = viewController;

            window.MakeKeyAndVisible();

            return true;
        }
    }
}

