using System;

#nullable enable

namespace Loupedeck.OpenHABPluginNG
{
    /// <summary>
    /// OpenHAB item for internal representation, contains information read via the openHAB API
    /// </summary>
    public class OpenHABCommandItem
    {
        /// <summary>
        /// Item type
        /// </summary>
        public String? Type { get; set; }
        /// <summary>
        /// Label
        /// </summary>
        public String? Label { get; set; }
        /// <summary>
        /// Name of the item
        /// </summary>
        public String? Name { get; set; }
        /// <summary>
        /// Full link as unique id and direct access to item information on openHAB
        /// </summary>
        public String? Link { get; set; }
        /// <summary>
        /// First group listed in item info
        /// </summary>
        public String? Group { get; set; }
        /// <summary>
        /// Item icon category used for the default switch symbol
        /// </summary>
        public String? Category { get; set; }
        /// <summary>
        /// Current item state (cached)
        /// </summary>
        public String? State { get; set; }
        /// <summary>
        /// Label pattern (format string) not used currently as not compatible to C# string formatting
        /// </summary>
        public String? Pattern { get; internal set; }
    }
}

#nullable restore