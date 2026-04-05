using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;

namespace LMLocal
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("ae3b51e3-5a57-49b6-baf2-ed10eda982cc")]
    public class MainWindow : ToolWindowPane
    {
        private readonly MainWindowControl _control;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow() : base(null)
        {
            this.Caption = "LM Local Chat";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            _control = new MainWindowControl();
            this.Content = _control;

            // Add preview key down handler to the control
            _control.PreviewKeyDown += OnControlPreviewKeyDown;
        }

        private void OnControlPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Home, End, Left, Right arrow keys
            if (e.Key == Key.Home || e.Key == Key.End || e.Key == Key.Left || e.Key == Key.Right)
            {
                int keyCode = GetKeyCode(e.Key);
                _control?.SendKeyToWebView(keyCode);
                e.Handled = true;
            }
        }

        private int GetKeyCode(Key key)
        {
            if (key == Key.Home) return 0x24;
            if (key == Key.End) return 0x23;
            if (key == Key.Left) return 0x25;
            if (key == Key.Right) return 0x27;
            return 0;
        }
    }
}
