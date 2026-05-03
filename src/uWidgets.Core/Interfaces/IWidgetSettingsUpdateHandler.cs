using uWidgets.Core.Models;

namespace uWidgets.Core.Interfaces;

/// <summary>
/// Lets a widget handle model changes without forcing the host window to recreate the control.
/// </summary>
public interface IWidgetSettingsUpdateHandler
{
    /// <summary>
    /// Applies a layout/settings update in place.
    /// </summary>
    /// <returns><c>true</c> when the widget handled the update and does not need recreation.</returns>
    bool TryHandleSettingsUpdate(WidgetLayout oldLayout, WidgetLayout newLayout);
}
