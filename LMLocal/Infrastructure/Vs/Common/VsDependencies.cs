using System;
using System.Threading.Tasks;
using LMLocal.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace LMLocal.Infrastructure.Vs.Common
{
    /// <summary>
    /// Provides read-only access to VS solution information.
    /// Data is cached after async initialization from UI thread.
    /// Automatically invalidates cache when solution changes.
    /// Safe to call from any thread after initialization.
    /// </summary>
    internal interface IVsDependencies
    {
        /// <summary>
        /// Gets the cached solution directory.
        /// Safe to call from any thread after InitializeAsync.
        /// </summary>
        string GetSolutionDirectory();

        /// <summary>
        /// Gets the cached IVsSolution instance.
        /// Safe to call from any thread after InitializeAsync.
        /// </summary>
        IVsSolution GetSolution();

        /// <summary>
        /// Gets the file provider for enumerating solution files.
        /// Must be called on UI thread.
        /// </summary>
        ISolutionFileProvider GetFileProvider();

        /// <summary>
        /// Initializes solution information on UI thread.
        /// Must be called before GetSolutionDirectory or GetSolution.
        /// </summary>
        Task InitializeAsync();
    }

    internal class VsDependencies : IVsDependencies, IVsSolutionEvents
    {
        private string _solutionDirectory;
        private IVsSolution _solution;
        private bool _initialized;
        private uint _solutionEventsCookie = VSConstants.VSCOOKIE_NIL;

        public string GetSolutionDirectory()
        {
            if (!_initialized)
                throw new InvalidOperationException("VsDependencies not initialized. Call InitializeAsync() first.");
            if (string.IsNullOrEmpty(_solutionDirectory))
                throw new InvalidOperationException("Solution directory is not available. Ensure a solution is loaded.");
            return _solutionDirectory;
        }

        public IVsSolution GetSolution()
        {
            if (!_initialized)
                throw new InvalidOperationException("VsDependencies not initialized. Call InitializeAsync() first.");
            return _solution;
        }

        public ISolutionFileProvider GetFileProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!_initialized)
                throw new InvalidOperationException("VsDependencies not initialized. Call InitializeAsync() first.");
            return new SolutionFileProvider(_solution);
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            // Switch to UI thread to access VS services
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                _solution = (IVsSolution)Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution));

                if (_solution != null)
                {
                    if (_solution.GetSolutionInfo(out string solutionDirectory, out _, out _) == VSConstants.S_OK)
                    {
                        _solutionDirectory = solutionDirectory?.TrimEnd('\\');
                    }

                    if (_solution is IVsSolutionEvents solutionEvents)
                    {
                        _solution.AdviseSolutionEvents(this, out _solutionEventsCookie);
                    }
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                InternalLogger.Error($"VsDependencies: Failed to initialize solution information: {ex}");
                throw new InvalidOperationException("Failed to initialize solution information.", ex);
            }
        }

        // IVsSolutionEvents implementation - invalidate cache when solution changes
        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            // Solution opened - this is OK, we already have the right data
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            // Solution is about to close - invalidate cache
            // Warning suppression: This callback is guaranteed to be on UI thread by VS
#pragma warning disable VSTHRD010
            InvalidateCache();
#pragma warning restore VSTHRD010
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            // Solution closed - cache is already invalidated in OnQueryCloseSolution
            return VSConstants.S_OK;
        }

        private void InvalidateCache()
        {
            // This can be called from solution events which are on UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            // Unsubscribe from solution events
            if (_solutionEventsCookie != VSConstants.VSCOOKIE_NIL && _solution != null)
            {
                try
                {
                    _solution.UnadviseSolutionEvents(_solutionEventsCookie);
                }
                catch (Exception ex)
                {
                    InternalLogger.Warn($"VsDependencies: Failed to unadvise solution events: {ex.Message}");
                }
                _solutionEventsCookie = VSConstants.VSCOOKIE_NIL;
            }

            // Clear cached data
            _solution = null;
            _solutionDirectory = null;
            _initialized = false;
        }
    }
}
