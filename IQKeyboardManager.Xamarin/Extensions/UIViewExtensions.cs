using System.Collections.Generic;
using UIKit;

namespace IQKeyboardManager.Xamarin
{
	public static class UIViewExtensions
	{
		//public static bool IsAskingCanBecomeFirstResponder(this UIView view)
		//{

		//}

		/// <summary>
		/// Returns the UIViewController object that manages the receiver.
		/// </summary>
		public static UIViewController GetViewController(this UIView view)
		{
			var nextResponder = view.NextResponder;

			while (nextResponder != null)
			{
				var vc = nextResponder as UIViewController;

				if (vc != null)
					return vc;

				nextResponder = nextResponder.NextResponder;
			}

			return null;
		}

		/// <summary>
		/// Returns the topMost UIViewController object in hierarchy.
		/// </summary>
		public static UIViewController GetTopMostController(this UIView view)
		{
			var controllersHierarchy = new List<UIViewController>();
			var topController = view.Window?.RootViewController;

			if (topController != null)
			{
				controllersHierarchy.Add(topController);

				while (topController.PresentedViewController != null)
				{
					controllersHierarchy.Add(topController.PresentedViewController);
					topController = topController.PresentedViewController;
				}

				var matchController = view.GetViewController() as UIResponder;

				while (matchController != null && !controllersHierarchy.Contains(matchController as UIViewController))
				{
					matchController = matchController.NextResponder;

					while (matchController != null && (matchController as UIViewController) == null)
					{
						matchController = matchController.NextResponder;
					}
				}

				return matchController as UIViewController;
			}

			return view.GetViewController();
		}
	}
}
