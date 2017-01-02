namespace IQKeyboardManager.Xamarin
{
	public enum IQPreviousNextDisplayMode
	{
		/// <summary>
		/// Show Next/Previous when there are more than 1 text field otherwise hide.
		/// </summary>
		Default,

		/// <summary>
		/// Do not show Next/Previous buttons in any case.
		/// </summary>
		AlwaysHide,

		/// <summary>
		/// Always show Next/Previous buttons. 
		/// If there are more than 1 textField then both buttons will be visible but will be shown as disabled.
		/// </summary>
		AlwaysShow
	}
}
