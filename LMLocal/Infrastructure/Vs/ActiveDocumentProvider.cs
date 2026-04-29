using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace LMLocal.Infrastructure.Vs
{
    internal class ActiveDocumentProvider
    {
        public async Task<string> GetContentAsync()
        {
            var (_, text) = await GetActiveTextDocumentAsync();
            return text;
        }

        private async Task<(string filePath, string content)> GetActiveTextDocumentAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!(await ServiceProvider.GetGlobalServiceAsync(typeof(SVsShellMonitorSelection))
                is IVsMonitorSelection monitor))
                return (null, string.Empty);

            monitor.GetCurrentElementValue(
                (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame,
                out object frameObj);

            if (!(frameObj is IVsWindowFrame frame))
                return (null, string.Empty);

            frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object pathObj);
            string filePath = pathObj as string;

            if (string.IsNullOrEmpty(filePath))
                return (null, string.Empty);


            if (!(await ServiceProvider.GetGlobalServiceAsync(typeof(SVsRunningDocumentTable))
                is IVsRunningDocumentTable rdt))
                return (filePath, string.Empty);

            IntPtr docData = IntPtr.Zero;

            try
            {
                int hr = rdt.FindAndLockDocument(
                    (uint)_VSRDTFLAGS.RDT_NoLock,
                    filePath,
                    out IVsHierarchy _,
                    out uint _,
                    out docData,
                    out uint _);

                if (hr != VSConstants.S_OK || docData == IntPtr.Zero)
                    return (filePath, string.Empty);

                object comObj = Marshal.GetObjectForIUnknown(docData);

                if (!(comObj is IVsTextLines buffer))
                    return (filePath, string.Empty);

                hr = buffer.GetLastLineIndex(out int lastLine, out int lastIndex);
                if (hr != VSConstants.S_OK)
                    return (filePath, string.Empty);

                hr = buffer.GetLineText(0, 0, lastLine, lastIndex, out string text);
                if (hr != VSConstants.S_OK)
                    return (filePath, string.Empty);

                return (filePath, text ?? string.Empty);
            }
            finally
            {
                if (docData != IntPtr.Zero)
                    Marshal.Release(docData);
            }
        }
    }
}
