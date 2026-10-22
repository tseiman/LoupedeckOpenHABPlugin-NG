#nullable enable
namespace Loupedeck.OpenHABPluginNG
{
    using System;

    /// <summary>
    /// This class contains information on an item update event
    /// </summary>
    public class OpenHABEventArgs : EventArgs
    {
        /// <summary>
        /// Stable item name used as the NG profile parameter (not an absolute URL)
        /// </summary>
        public string Link { get; set; }
        /// <summary>
        /// Item state
        /// </summary>
        public string State { get; set; }
        /// <summary>
        /// Command (not used currently)
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Constructur
        /// </summary>
        /// <param name="item"></param>
        /// <param name="state"></param>
        /// <param name="command"></param>
        public OpenHABEventArgs(string item, string state, string command)
        {
            Link = item;
            State = state;
            Command = command;
        }
    }
}
#nullable restore

