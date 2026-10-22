namespace Loupedeck.OpenHABPluginNG
{
    using System;

    /// <summary>
    /// This class can be used to connect the Loupedeck plugin to an application.
    /// As there is no openHAB application to connect this class is unused
    /// </summary>
    public class OpenHABApplication : ClientApplication
    {
        public OpenHABApplication()
        {
        }

        /// <summary>
        /// This method can be used to link the plugin to a Windows application.
        /// </summary>
        /// <returns></returns>
        protected override String GetProcessName() => "";

        /// <summary>
        /// This method can be used to link the plugin to a macOS application.
        /// </summary>
        /// <returns></returns>
        protected override String GetBundleName() => "";

        /// <summary>
        /// This method can be used to check whether the application is installed or not.
        /// </summary>
        /// <returns></returns>
        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
