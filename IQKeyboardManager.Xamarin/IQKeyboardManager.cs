using System;
using System.Collections.Generic;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using UIKit;

namespace IQKeyboardManager.Xamarin
{
	public class IQKeyboardManager : UIGestureRecognizerDelegate
	{
		// Current instance (singleton).
		private static IQKeyboardManager currentInstance;
		// Default tag for toolbar with Done button   -1002.
		private static int kIQDoneButtonToolbarTag = -1002;
		// Default tag for toolbar with Previous/Next buttons -1005.
		private static int kIQPreviousNextButtonToolbarTag = -1005;
		private static UIWindow keyWindow;
		private List<Type> registeredClasses = new List<Type> { typeof(UIView) };
		private bool isEnabled;
		private bool isKeyboardShowing;
		private bool isAutoToolbarEnabled;
		// Used with textView to detect a textFieldView contentInset is changed or not.
		private bool isTextViewContentInsetChanged;
		private bool shouldResignOnTouchOutside;
		private nfloat keyboardDistanceFromTextField = 10.0f;
		private nfloat movedDistance;
		private nfloat _layoutGuideConstraintInitialConstant;
		private nfloat _animationDuration = 0.25f;
		// Used to adjust contentInset of UITextView.
		private UIEdgeInsets startingTextViewContentInsets = UIEdgeInsets.Zero;
		// Used to adjust scrollIndicatorInsets of UITextView.
		private UIEdgeInsets startingTextViewScrollIndicatorInsets = UIEdgeInsets.Zero;
		private UIEdgeInsets _startingScrollIndicatorInsets = UIEdgeInsets.Zero;
		private UIEdgeInsets _startingContentInsets = UIEdgeInsets.Zero;
		private UIView _textFieldView;
		private CGRect _topViewBeginRect = CGRect.Empty;
		private CGRect _statusBarFrame = CGRect.Empty;
		private CGPoint _startingContentOffset = CGPoint.Empty;
		private CGSize _kbSize = CGSize.Empty;
		private UIViewController _rootViewController;
		private NSLayoutConstraint _layoutGuideConstraint;
		private UIScrollView _lastScrollView;
		private NSNotification _kbShowNotification;
		private UIViewAnimationOptions _animationCurve = UIViewAnimationOptions.CurveEaseOut;
		private UITapGestureRecognizer _tapGesture;

		private IQKeyboardManager()
		{
			/*
			 //  Registering for keyboard notification.
        NotificationCenter.default.addObserver(self, selector: #selector(self.keyboardWillShow(_:)),                name: NSNotification.Name.UIKeyboardWillShow, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(self.keyboardDidShow(_:)),                name: NSNotification.Name.UIKeyboardDidShow, object: nil)

        NotificationCenter.default.addObserver(self, selector: #selector(self.keyboardWillHide(_:)),                name: NSNotification.Name.UIKeyboardWillHide, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(self.keyboardDidHide(_:)),                name: NSNotification.Name.UIKeyboardDidHide, object: nil)
        */
			//  Registering for UITextField notification.
			RegisterTextFieldViewClass(new UITextField(), UITextField.TextDidBeginEditingNotification, UITextField.TextDidEndEditingNotification);

			//  Registering for UITextView notification.
			RegisterTextFieldViewClass(new UITextView(), UITextView.TextDidBeginEditingNotification, UITextView.TextDidEndEditingNotification);

			/*
//  Registering for orientation changes notification
NotificationCenter.default.addObserver(self, selector: #selector(self.willChangeStatusBarOrientation(_:)),          name: NSNotification.Name.UIApplicationWillChangeStatusBarOrientation, object: UIApplication.shared)

//  Registering for status bar frame change notification
NotificationCenter.default.addObserver(self, selector: #selector(self.didChangeStatusBarFrame(_:)),          name: NSNotification.Name.UIApplicationDidChangeStatusBarFrame, object: UIApplication.shared)
			      */
			//Creating gesture for @shouldResignOnTouchOutside. (Enhancement ID: #14)
			_tapGesture = new UITapGestureRecognizer(TapRecognized); //(target: self, action: #selector(self.tapRecognized(_:)))
			_tapGesture.CancelsTouchesInView = false;
			_tapGesture.Delegate = this;
			_tapGesture.Enabled = shouldResignOnTouchOutside;


			//Loading IQToolbar, IQTitleBarButtonItem, IQBarButtonItem to fix first time keyboard apperance delay (Bug ID: #550)
			var textField = new UITextField();

			//todo
			//textField.AddDoneOnKeyboardWithTarget(nil, action: #selector(self.doneAction(_:)))
			//textField.addPreviousNextDoneOnKeyboardWithTarget(nil, previousAction: #selector(self.previousAction(_:)), nextAction: #selector(self.nextAction(_:)), doneAction: #selector(self.doneAction(_:)))

			DisabledDistanceHandlingClasses.Add(typeof(UITableViewController));
			DisabledDistanceHandlingClasses.Add(typeof(UIAlertController));
			DisabledToolbarClasses.Add(typeof(UIAlertController));
			DisabledTouchResignedClasses.Add(typeof(UIAlertController));
			ToolbarPreviousNextAllowedClasses.Add(typeof(UITableView));
			ToolbarPreviousNextAllowedClasses.Add(typeof(UICollectionView));
			//ToolbarPreviousNextAllowedClasses.Add(IQPreviousNextView.self); //todo


			//todo
			/*
//Special Controllers
struct InternalClass {

static var UIAlertControllerTextFieldViewController: UIViewController.Type?  =   NSClassFromString("_UIAlertControllerTextFieldViewController") as? UIViewController.Type //UIAlertView
}

if let aClass = InternalClass.UIAlertControllerTextFieldViewController {
disabledDistanceHandlingClasses.append(aClass.self)
disabledToolbarClasses.append(aClass.self)
disabledTouchResignedClasses.append(aClass.self)
}
*/
		}

		/// <summary>
		/// Gets the current instance.
		/// </summary>
		public static IQKeyboardManager SharedManager
		{
			get
			{
				if (currentInstance == null)
					currentInstance = new IQKeyboardManager();

				return currentInstance;
			}
		}

		/// <summary>
		/// Gets or sets the keyboard distance from textField. 
		/// Can't be less than zero. Default is 10.0.
		/// </summary>
		public nfloat KeyboardDistanceFromTextField
		{
			get
			{
				return keyboardDistanceFromTextField;
			}

			set
			{
				keyboardDistanceFromTextField = (nfloat)Math.Max(0, value);
			}
		}

		/// <summary>
		/// If the keyboard is showing
		/// </summary>
		public bool IsKeyboardShowing { get { return isKeyboardShowing; } }

		/// <summary>
		/// Moved distance to the top used to maintain distance between keyboard and textField. 
		/// Most of the time this will be a positive value.
		/// </summary>
		public nfloat MovedDistance { get { return movedDistance; } }

		/// <summary>
		/// Prevent keyboard manager to slide up the rootView to more than keyboard height. Default is true.
		/// </summary>
		public bool PreventShowingBottomBlankSpace { get; set; } = true;

		/// <summary>
		/// Automaticly add the IQToolbar functionality. Default is true.
		/// </summary>
		public bool EnableAutoToolbar
		{
			set
			{
				isAutoToolbarEnabled = value;

				if (ShouldEnableAutoToolbar())
				{
					//addToolbarIfRequired()
				}
				else
				{
					//removeToolbarIfRequired()
				}
			}
		}

		/// <summary>
		/// AutoToolbar managing behaviour. Default is IQAutoToolbarManageBehaviour.BySubviews.
		/// </summary>
		public IQAutoToolbarManageBehaviour ToolbarManageBehavior { get; set; } = IQAutoToolbarManageBehaviour.BySubviews;

		/// <summary>
		/// If true, then uses textField's tint color property for IQToolbar, otherwise tint color is black. 
		/// Default is false.
		/// </summary>
		public bool ShouldToolbarUseTextFieldTintColor { get; set; } = false;

		/// <summary>
		/// This is used for toolbar's tint color when text field's keyboard appearance is UIKeyboardAppearanceDefault. 
		/// If ShouldToolbarUseTextFieldTintColor is true then this property is ignored. 
		/// Default is null and uses black color.
		/// </summary>
		public UIColor ToolbarTintColor { get; set; }

		/// <summary>
		/// The previous/next display mode.
		/// <see cref="IQPreviousNextDisplayMode"/>
		/// </summary>
		public IQPreviousNextDisplayMode PreviousNextDisplayMode { get; set; } = IQPreviousNextDisplayMode.Default;

		/// <summary>
		/// Toolbar done button icon. 
		/// If nothing is provided then check ToolbarDoneBarButtonItemText to draw done button.
		/// </summary>
		public UIImage ToolbarDoneBarButtonItemImage { get; set; }

		/// <summary>
		/// Toolbar done button text. 
		/// If nothing is provided then system's default 'UIBarButtonSystemItemDone' will be used.
		/// </summary>
		public string ToolbarDoneBarButtonItemText { get; set; }

		/// <summary>
		/// If true, then it adds the textField's placeholder text on IQToolbar. Default is true.
		/// </summary>
		public bool ShouldShowTextFieldPlaceholder { get; set; } = true;

		/// <summary>
		/// Placeholder's Font.
		/// </summary>
		public UIFont PlaceholderFont { get; set; }

		/// <summary>
		/// Overrides the keyboardAppearance for all textField/textView. Default is false.
		/// </summary>
		public bool OverrideKeyboardAppearance { get; set; }

		/// <summary>
		/// If OverrideKeyboardAppearance is true, then all the textField keyboardAppearance is set using this property.
		/// </summary>
		public UIKeyboardAppearance KeyboardAppearance { get; set; } = UIKeyboardAppearance.Default;

		/// <summary>
		/// Resigns keyboard on touching outside of UITextField/View.
		/// </summary>
		public bool ShouldResignOnTouchOutside
		{
			set
			{
				shouldResignOnTouchOutside = value;

				//_tapGesture.isEnabled = IsShouldResignOnTouchOutsideEnabled()
			}
		}

		/// <summary>
		/// If true, then it plays inputClick sound on next/previous/done click.
		/// </summary>
		public bool ShouldPlayInputClicks { get; set; } = true;

		/// <summary>
		/// If true, then calls 'setNeedsLayout' and 'layoutIfNeeded' on any frame update of to viewController's view.
		/// </summary>
		public bool LayoutIfNeededOnUpdate { get; set; }

		/// <summary>
		/// If true, then always consider UINavigationController.view begin point as {0,0}. 
		/// This is a workaround to fix bug #464 because there are no notification mechanism exist when 
		/// UINavigationController.view.frame gets changed internally.
		/// </summary>
		public bool ShouldFixInteractivePopGestureRecognizer { get; set; } = true;

		/// <summary>
		/// Disable distance handling within the scope of disabled distance handling viewControllers classes. 
		/// Within this scope, 'enabled' property is ignored. 
		/// Class should be type of UIViewController.
		/// </summary>
		public List<Type> DisabledDistanceHandlingClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Enable distance handling within the scope of enabled distance handling viewControllers classes. 
		/// Within this scope, 'enabled' property is ignored. Class should be kind of UIViewController. 
		/// If same Class is added in DisabledDistanceHandlingClasses list, then EnabledDistanceHandlingClasses will be ignored.
		/// </summary>
		public List<Type> EnabledDistanceHandlingClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Disable automatic toolbar creation within the scope of disabled toolbar viewControllers classes. 
		/// Within this scope, 'enableAutoToolbar' property is ignored. 
		/// Class should be type of UIViewController.
		/// </summary>
		public List<Type> DisabledToolbarClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Enable automatic toolbar creation within the scope of enabled toolbar viewControllers classes. 
		/// Within this scope, 'enableAutoToolbar' property is ignored. Class should be type of UIViewController. 
		/// If same Class is added in disabledToolbarClasses list, then enabledToolbarClasses will be ignore.
		/// </summary>
		public List<Type> EnabledToolbarClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Allowed subclasses of UIView to add all inner textField, this will allow to navigate between textField contains in different superview. C
		/// lass should be type of UIView.
		/// </summary>
		public List<Type> ToolbarPreviousNextAllowedClasses = new List<Type> { typeof(UIView) };

		/// <summary>
		/// Disabled classes to ignore 'shouldResignOnTouchOutside' property. 
		/// Class should be type of UIViewController.
		/// </summary>
		public List<Type> DisabledTouchResignedClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Enabled classes to forcefully enable 'shouldResignOnTouchOutsite' property. 
		/// Class should be type of UIViewController. 
		/// If same Class is added in DisabledTouchResignedClasses list, then EnabledTouchResignedClasses will be ignored.
		/// </summary>
		public List<Type> EnabledTouchResignedClasses = new List<Type> { typeof(UIViewController) };

		/// <summary>
		/// Enable managing distance between keyboard and textField. 
		/// Default is true (Enabled when class loads in load method).
		/// </summary>
		public void Enable()
		{
			if (!isEnabled)
			{
				isEnabled = true;

				//If keyboard is currently showing. Sending a fake notification for keyboardWillShow to adjust view according to keyboard.
				//if _kbShowNotification != nil {
				//	keyboardWillShow(_kbShowNotification)

				//}
			}
		}

		/// <summary>
		/// Disable managing distance between keyboard and textField. 
		/// Default is true (Enabled when class loads in load method).
		/// </summary>
		public void Disable()
		{
			if (isEnabled)
			{
				isEnabled = false;

				//keyboardWillHide(nil)
			}
		}

		/// <summary>
		/// Resigns currently first responder field.
		/// </summary>
		public bool ResignFirstResponder()
		{
			/*
if let textFieldRetain = _textFieldView {
            
            //Resigning first responder
            let isResignFirstResponder = textFieldRetain.resignFirstResponder()
            
            //  If it refuses then becoming it as first responder again.    (Bug ID: #96)
            if isResignFirstResponder == false {
                //If it refuses to resign then becoming it first responder again for getting notifications callback.
                textFieldRetain.becomeFirstResponder()
                
                showLog("Refuses to resign first responder: \(_textFieldView?._IQDescription())")
            }
            
            return isResignFirstResponder
        }
        
        return false
			*/

			return false;
		}

		/// <summary>
		/// Returns true if it can navigate to previous responder textField/textView, otherwise false.
		/// </summary>
		public bool CanGoPrevious()
		{
			/*
//Getting all responder view's.
        if let textFields = responderViews() {
            if let  textFieldRetain = _textFieldView {
                
                //Getting index of current textField.
                if let index = textFields.index(of: textFieldRetain) {
                    
                    //If it is not first textField. then it's previous object canBecomeFirstResponder.
                    if index > 0 {
                        return true
                    }
                }
            }
        }
        return false
			*/

			return false;
		}

		/// <summary>
		/// Returns true if it can navigate to next responder textField/textView, otherwise false.
		/// </summary>
		public bool CanGoNext()
		{
			/*
//Getting all responder view's.
        if let textFields = responderViews() {
            if let  textFieldRetain = _textFieldView {
                //Getting index of current textField.
                if let index = textFields.index(of: textFieldRetain) {
                    
                    //If it is not first textField. then it's previous object canBecomeFirstResponder.
                    if index < textFields.count-1 {
                        return true
                    }
                }
            }
        }
        return false
			*/

			return false;
		}

		/// <summary>
		/// Navigate to previous responder textField/textView.
		/// </summary>
		public bool GoPrevious()
		{
			/*
//Getting all responder view's.
        if let  textFieldRetain = _textFieldView {
            if let textFields = responderViews() {
                //Getting index of current textField.
                if let index = textFields.index(of: textFieldRetain) {
                    
                    //If it is not first textField. then it's previous object becomeFirstResponder.
                    if index > 0 {
                        
                        let nextTextField = textFields[index-1]
                        
                        let isAcceptAsFirstResponder = nextTextField.becomeFirstResponder()
                        
                        //  If it refuses then becoming previous textFieldView as first responder again.    (Bug ID: #96)
                        if isAcceptAsFirstResponder == false {
                            //If next field refuses to become first responder then restoring old textField as first responder.
                            textFieldRetain.becomeFirstResponder()
                            
                            showLog("Refuses to become first responder: \(nextTextField._IQDescription())")
                        }
                        
                        return isAcceptAsFirstResponder
                    }
                }
            }
        }
        
        return false
			*/

			return false;
		}

		/// <summary>
		/// Navigate to next responder textField/textView.
		/// </summary>
		public bool GoNext()
		{
			/*
//Getting all responder view's.
        if let  textFieldRetain = _textFieldView {
            if let textFields = responderViews() {
                //Getting index of current textField.
                if let index = textFields.index(of: textFieldRetain) {
                    //If it is not last textField. then it's next object becomeFirstResponder.
                    if index < textFields.count-1 {
                        
                        let nextTextField = textFields[index+1]
                        
                        let isAcceptAsFirstResponder = nextTextField.becomeFirstResponder()
                        
                        //  If it refuses then becoming previous textFieldView as first responder again.    (Bug ID: #96)
                        if isAcceptAsFirstResponder == false {
                            //If next field refuses to become first responder then restoring old textField as first responder.
                            textFieldRetain.becomeFirstResponder()
                            
                            showLog("Refuses to become first responder: \(nextTextField._IQDescription())")
                        }
                        
                        return isAcceptAsFirstResponder
                    }
                }
            }
        }

        return false
			*/

			return false;
		}

		/// <summary>
		/// Note: returning true is guaranteed to allow simultaneous recognition. 
		/// Returning false is not guaranteed to prevent simultaneous recognition, as the other gesture's delegate may return true.
		/// </summary>
		public bool GestureRecognizer(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
		{
			return false;
		}

		/// <summary>
		/// To not detect touch events in a subclass of UIControl, these may have added their own selector for specific work
		/// </summary>
		public bool GestureRecognizer(UIGestureRecognizer gestureRecognizer, UITouch touch)
		{
			var uiControl = touch.View as UIControl;
			var navigationBar = touch.View as UINavigationBar;

			return !(uiControl != null || navigationBar != null);
		}

		/// <summary>
		/// Add customised Notification for third party customised TextField/TextView. 
		/// Please be aware that the NSNotification object must be idential to UITextField/UITextView NSNotification objects and customised TextField/TextView support must be idential to UITextField/UITextView.
		/// </summary>
		/// <param name="viewClass">View class.</param>
		/// <param name="didBeginEditingNotificationName">This should be identical to UITextViewTextDidBeginEditingNotification</param>
		/// <param name="didEndEditingNotificationName">This should be identical to UITextViewTextDidEndEditingNotification</param>
		public void RegisterTextFieldViewClass(UIView viewClass, string didBeginEditingNotificationName, string didEndEditingNotificationName)
		{
			registeredClasses.Add(viewClass.GetType());

			//todo
			NSNotificationCenter.DefaultCenter.AddObserver(this, new ObjCRuntime.Selector(""), (NSString)didBeginEditingNotificationName, null);
			NSNotificationCenter.DefaultCenter.AddObserver(this, new ObjCRuntime.Selector(""), (NSString)didEndEditingNotificationName, null);
			//NotificationCenter.default.addObserver(self, selector: #selector(self.textFieldViewDidBeginEditing(_:)),    name: NSNotification.Name(rawValue: didBeginEditingNotificationName), object: nil)
			//NotificationCenter.default.addObserver(self, selector: #selector(self.textFieldViewDidEndEditing(_:)),      name: NSNotification.Name(rawValue: didEndEditingNotificationName), object: nil)
		}

		private void PreviousAction(UIBarButtonItem barButton = null)
		{
			if (ShouldPlayInputClicks)
				UIDevice.CurrentDevice.PlayInputClick();

			if (CanGoPrevious())
			{
				/*
if let textFieldRetain = _textFieldView {
                let isAcceptAsFirstResponder = goPrevious()
                
                if isAcceptAsFirstResponder &&
                    textFieldRetain.previousInvocation.target != nil &&
                    textFieldRetain.previousInvocation.action != nil {
                    
                    UIApplication.shared.sendAction(textFieldRetain.previousInvocation.action!, to: textFieldRetain.previousInvocation.target, from: textFieldRetain, for: UIEvent())
                }
            }
				*/
			}
		}

		private void NextAction(UIBarButtonItem barButton = null)
		{
			if (ShouldPlayInputClicks)
				UIDevice.CurrentDevice.PlayInputClick();

			if (CanGoNext())
			{
				/*
if let textFieldRetain = _textFieldView {
                let isAcceptAsFirstResponder = goNext()
                
                if isAcceptAsFirstResponder &&
                    textFieldRetain.nextInvocation.target != nil &&
                    textFieldRetain.nextInvocation.action != nil {
                    
                    UIApplication.shared.sendAction(textFieldRetain.nextInvocation.action!, to: textFieldRetain.nextInvocation.target, from: textFieldRetain, for: UIEvent())
                }
            }
				*/
			}
		}

		private void DoneAction(UIBarButtonItem barButton = null)
		{
			if (ShouldPlayInputClicks)
				UIDevice.CurrentDevice.PlayInputClick();

			/*
if let textFieldRetain = _textFieldView {
            //Resign textFieldView.
            let isResignedFirstResponder = resignFirstResponder()
            
            if isResignedFirstResponder &&
                textFieldRetain.doneInvocation.target != nil &&
                textFieldRetain.doneInvocation.action != nil{
                
                UIApplication.shared.sendAction(textFieldRetain.doneInvocation.action!, to: textFieldRetain.doneInvocation.target, from: textFieldRetain, for: UIEvent())
            }
        }
			*/
		}

		private void TapRecognized(UITapGestureRecognizer gesture)
		{
			if (gesture.State == UIGestureRecognizerState.Ended)
				ResignFirstResponder();//_ = resignFirstResponder()
		}

		private bool IsEnabled()
		{
			/*

if let textFieldViewController = _textFieldView?.viewController() {
            
            if isEnabled == false {
                
                //If viewController is kind of enable viewController class, then assuming it's enabled.
                for enabledClass in enabledDistanceHandlingClasses {
                    
                    if textFieldViewController.isKind(of: enabledClass) {
                        isEnabled = true
                        break
                    }
                }
            }
            
            if isEnabled == true {
                
                //If viewController is kind of disabled viewController class, then assuming it's disabled.
                for disabledClass in disabledDistanceHandlingClasses {
                    
                    if textFieldViewController.isKind(of: disabledClass) {
                        isEnabled = false
                        break
                    }
                }
            }
        }

			*/

			return isEnabled;
		}

		private bool ShouldEnableAutoToolbar()
		{
			/*
var enableToolbar = enableAutoToolbar
        
        if let textFieldViewController = _textFieldView?.viewController() {
            
            if enableToolbar == false {
                
                //If found any toolbar enabled classes then return.
                for enabledClass in enabledToolbarClasses {
                    
                    if textFieldViewController.isKind(of: enabledClass) {
                        enableToolbar = true
                        break
                    }
                }
            }
            
            if enableToolbar == true {
                
                //If found any toolbar disabled classes then return.
                for disabledClass in disabledToolbarClasses {
                    
                    if textFieldViewController.isKind(of: disabledClass) {
                        enableToolbar = false
                        break
                    }
                }
            }
        }

        return enableToolbar
			*/

			return isAutoToolbarEnabled;
		}

		private bool IsShouldResignOnTouchOutsideEnabled()
		{
			/*

var shouldResign = shouldResignOnTouchOutside
        
        if let textFieldViewController = _textFieldView?.viewController() {
            
            if shouldResign == false {
                
                //If viewController is kind of enable viewController class, then assuming shouldResignOnTouchOutside is enabled.
                for enabledClass in enabledTouchResignedClasses {
                    
                    if textFieldViewController.isKind(of: enabledClass) {
                        shouldResign = true
                        break
                    }
                }
            }
            
            if shouldResign == true {
                
                //If viewController is kind of disable viewController class, then assuming shouldResignOnTouchOutside is disable.
                for disabledClass in disabledTouchResignedClasses {
                    
                    if textFieldViewController.isKind(of: disabledClass) {
                        shouldResign = false
                        break
                    }
                }
            }
        }
        
        return shouldResign
			*/

			return shouldResignOnTouchOutside;
		}

		private UIWindow GetKeyWindow()
		{
			var window = _textFieldView?.Window;

			if (window == null)
			{
				var originalKeyWindow = UIApplication.SharedApplication.KeyWindow;

				if (originalKeyWindow != null && (keyWindow == null || keyWindow != originalKeyWindow))
					window = keyWindow = originalKeyWindow;
			}

			return window;
		}

		/// <summary>
		/// Helper function to manipulate RootViewController's frame with animation
		/// </summary>
		private void SetRootViewFrame(CGRect frame)
		{
			var controller = _textFieldView.GetTopMostController();

			if (controller == null)
				controller = GetKeyWindow().GetTopMostController();

			if (controller != null)
			{
				var newFrame = frame;

				newFrame.Size = controller.View.Frame.Size;

				UIView.AnimateNotify(_animationDuration, 0.0, UIViewAnimationOptions.BeginFromCurrentState | _animationCurve, () =>
				{
					controller.View.Frame = newFrame;

					if (LayoutIfNeededOnUpdate)
					{
						controller.View.SetNeedsLayout();
						controller.View.LayoutIfNeeded();
					}
				}, null);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine("You must set UIWindow.rootViewController in your AppDelegate to work with IQKeyboardManager");
			}
		}

		/// <summary>
		/// Adjusting RootViewController's frame according to interface orientation.
		/// </summary>
		private void AdjustFrame()
		{
			//  We are unable to get textField object while keyboard showing on UIWebView's textField.  (Bug ID: #11)
			if (_textFieldView == null)
				return;

			var startTime = CAAnimation.CurrentMediaTime();
			System.Diagnostics.Debug.WriteLine("****** AdjustFrame started ******");

			var optionalWindow = GetKeyWindow();
			var optionalRootController = _textFieldView.GetTopMostController();

			if (optionalRootController == null)
				optionalRootController = optionalWindow?.GetTopMostController();

			var optionalTextFieldViewRect = _textFieldView.Superview?.ConvertRectToView(_textFieldView.Frame, optionalWindow);

			if (optionalRootController == null || optionalWindow == null || optionalTextFieldViewRect == null)
				return;

			var rootViewRect = optionalRootController.View.Frame;
			var specialKeyboardDistanceFromTextField = _textFieldView.dist

			/*
			 * let rootController = optionalRootController!
        let window = optionalWindow!
        let textFieldViewRect = optionalTextFieldViewRect!
        
        //  Getting RootViewRect.
        var rootViewRect = rootController.view.frame
        //Getting statusBarFrame
        //Maintain keyboardDistanceFromTextField
        var specialKeyboardDistanceFromTextField = textFieldView.keyboardDistanceFromTextField
        
        if textFieldView.isSearchBarTextField() {
            
            if  let searchBar = textFieldView.superviewOfClassType(UISearchBar.self) {
                specialKeyboardDistanceFromTextField = searchBar.keyboardDistanceFromTextField
            }
        }
			 */
		}
	}
}
