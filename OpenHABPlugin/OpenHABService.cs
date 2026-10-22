using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

#nullable enable
namespace Loupedeck.OpenHABPlugin
{
    using System;
    using System.Text.RegularExpressions;


    public class OpenHABService
    {
        /// <summary>
        /// Http client object for openHAB API calls
        /// </summary>
        protected HttpClient? _httpClient = null;

        /// <summary>
        /// Web socket client (not used currently)
        /// </summary>
        protected ClientWebSocket? _webSocketClient = null;

        /// <summary>
        /// Event triggered upon item update
        /// </summary>
        public event EventHandler<OpenHABEventArgs>? ItemChanged;

        /// <summary>
        /// Timer canceling, not used currently
        /// </summary>
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// List of items read from openHAB, contains <see cref="OpenHABCommandItem" elements/>
        /// </summary>
        public List<OpenHABCommandItem> Items { get; } = new List<OpenHABCommandItem>();

        /// <summary>
        /// OpenHAB base url
        /// </summary>
        protected String? _baseUrl;

        /// <summary>
        /// OpenHAB API token (not used currently)
        /// </summary>
        protected String? _apiToken;

        /// <summary>
        /// List of switches in openHAB items
        /// </summary>
        public IEnumerable<OpenHABCommandItem> Switches => Items.Where(item => item.Type == "Switch");

        /// <summary>
        /// List of dimmer in openHAB items
        /// </summary>
        public IEnumerable<OpenHABCommandItem> Dimmer => Items.Where(item => item.Type == "Dimmer");

        /// <summary>
        /// List of items considered for labels
        /// </summary>
        public IEnumerable<OpenHABCommandItem> Labels => Items.Where(item => (item.Type == "Number") || (item.Type == "String"));


        /// <summary>
        /// Default constructor
        /// Opens the Http client
        /// </summary>
        public OpenHABService()
        {
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Timer to be called in regular intervals to poll item state from openHAB
        /// </summary>
        private async void Timer()
        {
            while (true && !this._cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(5000);
                var readItems = Items.Where(item => item.Registered);
                foreach (var item in readItems)
                {
                    if (item.Link != null)
                    {
                        var state = ReceiveItemState(item.Link!);
                        //Console.WriteLine($"Item {item.Link}: {state}");
                        // Send only if changed
                        if (state != item.State)
                        {
                            item.State = state;
                            ItemChanged?.Invoke(this, new OpenHABEventArgs(item.Link!, state != null ? state : "", ""));
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Initializes connection to openHAB and reads the list of items
        /// </summary>
        /// <param name="baseUrl">OpenHAB base URL</param>
        /// <param name="token">OpenHAB API token</param>
        public void Initialize(String baseUrl, String token)
        {
            _baseUrl = baseUrl;
            _apiToken = token;
            ReadOpenHABItems();
            Console.WriteLine("OpenHAB items read, triggering item update");
            TriggerRegistration();
            Timer();
            //if (WebSocketUrl != null)
            //{
            //    Task.Run(async () => await Connect());
            //}
        }

        /// <summary>
        /// Read the openHAB items to be added as a selection for the command profile actions
        /// </summary>
        protected void ReadOpenHABItems()
        {

            String ohUrl = $"{_baseUrl}/rest/items?recursive=false";
            _httpClient!.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient!.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            var response = _httpClient!.GetAsync(ohUrl).Result;


            //// Read as json
            if ((response != null) && (response.IsSuccessStatusCode))
            {
                var data = DeserializeResponseArray(response);
                if (data != null)
                {
                    int maxItems = int.MaxValue;
                    int itemNo = 0;
                    while ((itemNo < data.Count) && (itemNo < maxItems))
                    {
                        var item = data[itemNo];
                        // Get the group and select the first one as sub-group
                        string? itemGroup = item["groupNames"]?.FirstOrDefault()?.ToString();

                        if (itemGroup == null)
                        {
                            itemGroup = "No group";
                        }

                        string? itemType = item["type"]?.ToString();
                        string? itemLabel = item["label"]?.ToString();
                        string? itemName = item["name"]?.ToString();
                        string? itemLink = item["link"]?.ToString();
                        string? itemCategory = item["category"]?.ToString();
                        string? itemState = item["state"]?.ToString();
                        if ((itemLabel != null) && (itemName != null) && (itemLink != null))
                        {
                            string? pattern = item["stateDescription"]?["pattern"]?.ToString();
                            Items!.Add(new OpenHABCommandItem
                            {
                                Type = itemType,
                                Name = itemName,
                                Label = itemLabel,
                                Link = itemLink,
                                Group = itemGroup,
                                Category = itemCategory,
                                State = itemState,
                                Registered = false,
                                Pattern = pattern
                            });
                        }
                        itemNo++;
                    }
                }
            }
            else
            {
                Console.WriteLine($"openHAB connection error: {((response != null) ? response!.StatusCode.ToString() : "no response")}");
            }
        }


        /// <summary>
        /// Registers an action parameter as item to be updated
        /// </summary>
        /// <param name="actionParameter">Item link</param>
        internal void RegisterItem(String actionParameter)
        {
            Console.WriteLine("Pre-Registering for actionParameter: " + actionParameter);
            if (!String.IsNullOrEmpty(actionParameter))
            {
                var item = Items.FirstOrDefault(item => item.Link == actionParameter);
                if (item != default)
                {
                    Console.WriteLine("Item for registering found: " + item.Name);
                    item.Registered = true;
                }
                else
                {
                    Console.WriteLine($"Item {item!.Name} not found.");
                }
            }
        }

        internal Boolean ItemIsRegistered(String actionParameter)
        {
            var item = Items.FirstOrDefault(item => item.Link == actionParameter);
            
            return item != default ? item.Registered : true;
        }

        internal void TriggerRegistration()
        {
            foreach (var item in Switches)
            {
                ItemChanged?.Invoke(this, new OpenHABEventArgs(item.Link!, item.State!, ""));
            }
        }

        /// <summary>
        /// Gets the cached label of the item given by the item link
        /// </summary>
        /// <param name="actionParameter">Item link</param>
        /// <returns>Item label</returns>
        internal String? GetItemLabel(String actionParameter)
        {
            var item = Items.FirstOrDefault(item => item.Link == actionParameter);
            if (item != null)
            {
                return item.Label;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the cached state of an item
        /// </summary>
        /// <param name="actionParameter">Link to the item in OH (id)</param>
        /// <returns>Item state as string</returns>
        internal String? GetStateOfItem(String actionParameter)
        {
            var item = Items.FirstOrDefault(item => item.Link == actionParameter);
            if (item != null)
            {
                return item.State;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Apply the state description pattern to the item state
        /// </summary>
        /// <param name="actionParameter">Item Url</param>
        /// <returns>Display state or state, when pattern is not defined</returns>
        internal String? GetDisplayStateOfItem(String actionParameter)
        {
            var item = Items.FirstOrDefault(item => item.Link == actionParameter);
            //Console.WriteLine($"Item found: {item.Name}, {item.Type}, {item.State}");
            if (item != null)
            {
                String? displayState = item.State;
                if (item.Pattern != null)
                {
                    if ((item.Type == "Number") && (Double.TryParse(item.State, out double value)))
                    {
                        displayState = String.Format(item.Pattern, value);
                    }
                    else if (item.Type == "String")
                    {
                        displayState = String.Format(item.Pattern, item.State);
                    }
                    Console.WriteLine(displayState);
                }
                return displayState;
            }
            else
            {
                return null;
            }
        }

        #region OH Communication handling
        /// <summary>
        /// Deserializes an API response to JArray
        /// </summary>
        /// <param name="response">API response</param>
        /// <returns></returns>
        public static JArray? DeserializeResponseArray(HttpResponseMessage response)
        {
            if (response == null)
            {
                return null;
            }
            string jsonString = response.Content.ReadAsStringAsync().Result;
            JArray? data = JsonConvert.DeserializeObject<JArray>(jsonString);
            return data;
        }

        /// <summary>
        /// Deserializes a response message as JObject
        /// </summary>
        /// <param name="response">Http response message</param>
        /// <returns>JSON object for the response</returns>
        public static JObject? DeserializeResponseObject(HttpResponseMessage? response)
        {
            if (response == null)
            {
                return null;
            }
            string jsonString = response.Content.ReadAsStringAsync().Result;
            JObject? data = JsonConvert.DeserializeObject<JObject>(jsonString);
            return data;
        }

        /// <summary>
        /// Returns byte array of icon defined in openHAB for given item category and state
        /// </summary>
        /// <param name="state">Item state</param>
        /// <param name="itemCategory">Item category as defined in openHAB</param>
        /// <returns>Byte array of img</returns>
        public Byte[]? GetItemIconForState(String state, String? itemCategory)
        {
            string imageUrl = _baseUrl + "/icon/" + itemCategory + "?anyFormat=true&format=png&state=" + state;
            var response = _httpClient?.GetAsync(imageUrl).Result;
            byte[]? imgBytes = response?.Content.ReadAsByteArrayAsync().Result;
            return imgBytes;
        }

        /// <summary>
        /// Toggles an openHAB switch
        /// </summary>
        /// <param name="itemUrl">Url of the item</param>
        /// <returns>Item state</returns>
        public String? ToggleItem(String itemUrl)
        {
            HttpContent body = new StringContent("TOGGLE");
            body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            var response = _httpClient?.PostAsync(itemUrl, body).Result;
            String? state = ReceiveItemState(itemUrl);
            return state;
        }

        /// <summary>
        /// Gets the state of the given item
        /// </summary>
        /// <param name="itemUrl">Url of the item</param>
        /// <returns>Item state</returns>
        protected String? ReceiveItemState(String itemUrl)
        {
            var response = _httpClient?.GetAsync(itemUrl + "/state").Result;
            var state = response?.Content.ReadAsStringAsync().Result;
            //Console.WriteLine("Got state for item: " + itemUrl + ": " + state);
            return state;
        }

        /// <summary>
        /// Set an openHAB item state
        /// </summary>
        /// <param name="itemUrl">Url of the item</param>
        /// <returns>Item state</returns>
        public String? SendItemState(String itemUrl, string value)
        {
            /// Update internal value first and don't wait for an update
            var item = Items.FirstOrDefault(item => item.Link == itemUrl);
            if (item != null)
            {
                item.State = value;
            }
            HttpContent body = new StringContent(value);
            body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            var response = _httpClient?.PostAsync(itemUrl, body).Result;
            response = _httpClient?.GetAsync(itemUrl + "/state").Result;
            string? state = response?.Content.ReadAsStringAsync().Result;
            return state;
        }
        #endregion


        #region Helper methods
        /// <summary>
        /// Extracts the numerical value from a string containing units or other elements
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Value as double, returns NaN if value could not be extracted</returns>
        public static double ExtractNumericalValue(string input)
        {
            string pattern = @"([-+]?[0-9]*\.?[0-9]+)";
            Match match = Regex.Match(input, pattern);
            if (match.Success)
            {
                if (double.TryParse(match.Value, out double value))
                {
                    return value;
                }

            }
            return double.NaN;
        }

        /// <summary>
        /// Restricts a given value between the given minimum and maximum.
        /// Returns minimum if value is less than minimum and maximum if value is greater than maximum.
        /// </summary>
        /// <typeparam name="T">Value type</typeparam>
        /// <param name="value">Value</param>
        /// <param name="min">Minimum value</param>
        /// <param name="max">Maximum value</param>
        /// <returns>Min/Max restricted value</returns>
        public static T MinMax<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;
            else if (value.CompareTo(max) > 0)
                return max;
            else
                return value;
        }

        #endregion

        /// <summary>
        /// Not documented from here
        /// </summary>

        #region WebSocket (currently unused)

        protected String? WebSocketUrl
        {
            get
            {
                if (_baseUrl != null)
                {
                    if (_baseUrl.StartsWith("https://"))
                    {
                        return "wss://" + _baseUrl.Substring(8) + "/ws";
                    }
                    else if (_baseUrl.StartsWith("http://"))
                    {
                        return "ws://" + _baseUrl.Substring(7) + "/ws";
                    }
                }
                return null;
            }
        }

        private async Task Connect()
        {
            _webSocketClient = new ClientWebSocket();
            Console.WriteLine("WebSocket created, connecting to: " + WebSocketUrl);

            await _webSocketClient.ConnectAsync(new Uri(WebSocketUrl), CancellationToken.None);
            //SendEncodedMessage(_apiToken);
            Console.WriteLine("API Token sent");

            System.Timers.Timer timer = new System.Timers.Timer(5000);
            timer.Elapsed += Watchdog_Timer;

            var receiveTask = ReceiveLoop();
            while (true)
            {
                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite));
                if (completedTask == receiveTask)
                {
                    if (receiveTask.IsCompleted)
                    {
                        break;
                    }

                    var data = JsonConvert.DeserializeObject<JObject>(receiveTask.Result);
                    if (data != null)
                    {
                        /// Handle that event:
                        /// {
                        ///     "type": "ItemStateEvent",
                        ///     "topic": "openhab/items/DTR/state",
                        ///     "payload": "{\"type\":\"Quantity\",\"value\":\"5 MB/s\"}"
                        /// }
                        ///
                        if (data["topic"] != null)
                        {
                            string[] elements = data["topic"]!.ToString().Split('/');
                            string? itemName = elements.Count() >= 3 ? elements[2] : null;

                            if ((itemName != null) && (data["type"] != null) && (data["type"]!.ToString() == "ItemStateEvent"))
                            {
                                if (data["payload"] != null)
                                {
                                    var payload = JsonConvert.DeserializeObject<JObject>(data["payload"]!.ToString());
                                    string? itemValue = payload?["value"]?.ToString();
                                    if (itemValue != null)
                                    {
                                        /// Update the state
                                        /// Call an update of all registered elements
                                        var item = Items.FirstOrDefault(item => item.Name == itemName);
                                        if ((item != null) && (item.Registered))
                                        {
                                            item.State = itemValue;
                                            ItemChanged?.Invoke(this, new OpenHABEventArgs(itemName, itemValue!, ""));
                                        }
                                    }

                                }
                            }
                        }
                    }

                    Console.WriteLine("New message received: " + receiveTask.Result);
                    receiveTask = ReceiveLoop();
                }
                else
                {
                    //handle other tasks
                }
            }
        }

        private void SendEncodedMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);
            _webSocketClient?.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            Console.WriteLine("Message sent: " + message);
        }

        private void Watchdog_Timer(Object sender, System.Timers.ElapsedEventArgs e)
        {
            SendEncodedMessage("{\r\n    \"type\": \"WebSocketEvent\",\r\n    \"topic\": \"openhab/websocket/heartbeat\",\r\n    \"payload\": \"PING\",\r\n    \"source\": \"Loupedeck_OpenHABPlugin\"\r\n}");
        }

        public async Task<string> ReceiveLoop()
        {
            if (_webSocketClient != null)
            {
                while (_webSocketClient!.State == WebSocketState.Open)
                {
                    var buffer = new ArraySegment<byte>(new byte[4096]);
                    var result = await _webSocketClient.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        Console.WriteLine("New message received: " + message);

                        if (result.CloseStatus != null)
                        {
                            await _webSocketClient.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                            return "";
                        }
                        return message;
                    }
                }
            }
            return "";
        }

        public async Task CloseWebSocket()
        {
            if (_webSocketClient != null)
            {
                await _webSocketClient!.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
        }

        #endregion
    }
}
#nullable restore

