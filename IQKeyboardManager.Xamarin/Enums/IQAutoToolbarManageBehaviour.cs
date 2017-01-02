namespace IQKeyboardManager.Xamarin
{
	public enum IQAutoToolbarManageBehaviour
	{
		/// <summary>
		/// Creates Toolbar according to subview's hirarchy of Textfields in view.
		/// </summary>
		BySubviews,

		/// <summary>
		/// Creates Toolbar according to tag property of TextFields.
		/// </summary>
		ByTag,

		/// <summary>
		/// Creates Toolbar according to the x,y position of textField in its superview coordinate.
		/// </summary>
		ByPosition
	}
}
